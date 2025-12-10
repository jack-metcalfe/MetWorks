using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using DdiCodeGen.Dtos.Canonical;
using DdiCodeGen.Validation;
using System.Diagnostics;

namespace DdiCodeGen.Generation
{
    public static class TemplateValidator
    {
        private static readonly Regex PlaceholderRegex = new Regex(@"\{\{\s*([^}\s]+(?:\.[^}\s]+)*)\s*\}\}", RegexOptions.Compiled);

        /// <summary>
        /// Validate placeholders in a template against the canonical model.
        /// Emits diagnostics for unresolved placeholders.
        /// </summary>
        public static void ValidateTemplatePlaceholders(string? templateText, CanonicalModelDto model,
            List<Diagnostic> diagnostics, string templateName = "template")
        {
            if (string.IsNullOrWhiteSpace(templateText)) return;
            if (model is null) throw new ArgumentNullException(nameof(model));
            if (diagnostics is null) throw new ArgumentNullException(nameof(diagnostics));

            var matches = PlaceholderRegex.Matches(templateText);
            foreach (Match m in matches)
            {
                var token = m.Groups[1].Value?.Trim();
                if (string.IsNullOrWhiteSpace(token)) continue;

                if (!TryResolvePathType(model.GetType(), token, out var resolvedType))
                {
                    var index = m.Index;
                    var location = $"{templateName}#pos{index}";

                    diagnostics.Add(new Diagnostic(
                        DiagnosticCode.TemplateUnresolvedPlaceholder,
                        $"Template contains unresolved placeholder '{{{{ {token} }}}}'.",
                        
                        location
                    ));
                }
            }
        }

        /// <summary>
        /// Attempts to resolve a dot-separated path against a starting Type.
        /// Returns true if each segment corresponds to a property (or element property for collections).
        /// This method uses Type information only and never dereferences instance values, avoiding null dereferences.
        /// </summary>
        // Replace or augment the existing TryResolvePathType in TemplateValidator with this version
        private static bool TryResolvePathType(Type startType, string path, out Type? resolvedType)
        {
            resolvedType = null;
            if (startType == null) return false;
            if (string.IsNullOrWhiteSpace(path)) return false;

            var segments = path.Split('.');
            Type? cursorType = startType;

            // Remember a collection type from the previous property when we normalized to element type.
            // This allows the next segment to be a collection helper (First/Last/Count/Length).
            Type? pendingCollectionType = null;

            foreach (var rawSeg in segments)
            {
                var seg = rawSeg?.Trim();
                if (string.IsNullOrEmpty(seg)) return false;

                // Detect indexer pattern like Name[0] or Name[  12 ]
                string namePart = seg;
                int? indexer = null;
                var idxStart = seg.IndexOf('[');
                if (idxStart >= 0 && seg.EndsWith("]"))
                {
                    namePart = seg.Substring(0, idxStart).Trim();
                    var inside = seg.Substring(idxStart + 1, seg.Length - idxStart - 2).Trim();
                    if (int.TryParse(inside, out var parsed))
                        indexer = parsed;
                    else
                        return false; // non-numeric indexer not supported here
                }

                // If cursorType itself is a collection (e.g., previous segment resolved to IEnumerable<T>),
                // then namePart should be resolved against the element type or be a collection helper.
                if (IsEnumerableButNotString(cursorType))
                {
                    var elementType = GetEnumerableElementType(cursorType);
                    if (elementType == null) return false;

                    // If the segment is a collection helper (First/Last/Count/Length), handle it
                    if (IsCollectionHelper(namePart))
                    {
                        cursorType = namePart.Equals("Count", StringComparison.OrdinalIgnoreCase) ||
                                     namePart.Equals("Length", StringComparison.OrdinalIgnoreCase)
                            ? typeof(int)
                            : elementType;

                        // Consumed any pending collection context
                        pendingCollectionType = null;
                        continue;
                    }

                    // Otherwise, treat the segment as a property on the element type
                    var propOnElement = elementType.GetProperty(namePart, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                                        ?? elementType.GetProperty(TrySingularize(namePart), BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                    if (propOnElement == null) return false;

                    // If there was an indexer on the segment (e.g., Namespaces[0].Prop), indexing applies to the *collection*,
                    // so after indexing we should be at the element type and then navigate propOnElement on that element.
                    // Here cursorType is the collection; since we already know elementType, set cursorType to the element's property type.
                    cursorType = NormalizePropertyType(propOnElement.PropertyType);

                    // Clear any pending collection because we've consumed it by navigating into the element
                    pendingCollectionType = null;
                    continue;
                }

                // Normal object property lookup on cursorType
                var property = cursorType.GetProperty(namePart, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                               ?? cursorType.GetProperty(TryPluralize(namePart), BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                if (property == null)
                {
                    // If property not found, but we have a pending collection (previous property was a collection)
                    // and the current segment is a collection helper, apply the helper to the pending collection.
                    if (pendingCollectionType != null && IsCollectionHelper(namePart))
                    {
                        var elem = GetEnumerableElementType(pendingCollectionType);
                        if (elem == null) return false;

                        cursorType = namePart.Equals("Count", StringComparison.OrdinalIgnoreCase) ||
                                     namePart.Equals("Length", StringComparison.OrdinalIgnoreCase)
                            ? typeof(int)
                            : elem;

                        // Consumed the pending collection
                        pendingCollectionType = null;
                        continue;
                    }

                    // Otherwise the property truly doesn't exist on the current type
                    return false;
                }

                // We have a property; examine its raw type before normalization
                var propertyType = property.PropertyType;

                // If an indexer was present on this segment, the property must be a collection
                if (indexer.HasValue)
                {
                    if (!IsEnumerableButNotString(propertyType))
                        return false;

                    var elem = GetEnumerableElementType(propertyType);
                    if (elem == null) return false;

                    // After indexing, cursor becomes the element type
                    cursorType = elem;

                    // Clear pending collection because indexing consumes the collection
                    pendingCollectionType = null;
                    continue;
                }

                // If the property itself is a collection and the segment name is a collection helper (e.g., Namespaces.Count)
                if (IsEnumerableButNotString(propertyType) && IsCollectionHelper(namePart))
                {
                    cursorType = namePart.Equals("Count", StringComparison.OrdinalIgnoreCase) ||
                                 namePart.Equals("Length", StringComparison.OrdinalIgnoreCase)
                        ? typeof(int)
                        : GetEnumerableElementType(propertyType);
                    if (cursorType == null) return false;

                    // Clear pending collection because helper consumed it
                    pendingCollectionType = null;
                    continue;
                }

                // No indexer and no helper: if the property is a collection, remember it as pendingCollectionType
                // but set cursorType to the element type so normal navigation works for the next segment.
                if (IsEnumerableButNotString(propertyType))
                {
                    pendingCollectionType = propertyType;
                    var elem = GetEnumerableElementType(propertyType);
                    if (elem == null) return false;
                    cursorType = elem;
                    continue;
                }

                // No indexer and no helper and not a collection: normalize the property type for further navigation
                cursorType = NormalizePropertyType(propertyType);

                // Clear any pending collection because we've moved to a non-collection property
                pendingCollectionType = null;
            }

            resolvedType = cursorType;
            return true;
        }

        private static bool IsCollectionHelper(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return false;
            return token.Equals("First", StringComparison.OrdinalIgnoreCase)
                || token.Equals("Last", StringComparison.OrdinalIgnoreCase)
                || token.Equals("Count", StringComparison.OrdinalIgnoreCase)
                || token.Equals("Length", StringComparison.OrdinalIgnoreCase);
        }

        private static Type NormalizePropertyType(Type t)
        {
            if (t == null) return typeof(object);
            // Unwrap Nullable<T>
            var underlying = Nullable.GetUnderlyingType(t);
            if (underlying != null) return underlying;

            // If property is IEnumerable<T>, prefer T for further navigation
            var elem = GetEnumerableElementType(t);
            if (elem != null) return elem;

            return t;
        }

        private static bool IsEnumerableButNotString(Type? t)
        {
            if (t == null) return false;
            if (t == typeof(string)) return false;
            if (t.IsArray) return true;
            return t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        }

        private static Type? GetEnumerableElementType(Type enumerableType)
        {
            if (enumerableType == null) return null;
            if (enumerableType.IsArray) return enumerableType.GetElementType();

            var ifaces = enumerableType.GetInterfaces().Concat(new[] { enumerableType });
            foreach (var iface in ifaces)
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    return iface.GetGenericArguments()[0];
            }
            return null;
        }

        // Small heuristics for pluralization/singularization
        private static string TryPluralize(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (s.EndsWith("y", StringComparison.OrdinalIgnoreCase)) return s.Substring(0, s.Length - 1) + "ies";
            if (s.EndsWith("s", StringComparison.OrdinalIgnoreCase)) return s;
            return s + "s";
        }

        private static string TrySingularize(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (s.EndsWith("ies", StringComparison.OrdinalIgnoreCase)) return s.Substring(0, s.Length - 3) + "y";
            if (s.EndsWith("s", StringComparison.OrdinalIgnoreCase)) return s.Substring(0, s.Length - 1);
            return s;
        }
    }
}
