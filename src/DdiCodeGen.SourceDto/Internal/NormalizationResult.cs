namespace DdiCodeGen.SourceDto.Internal
{
    using System.Collections.Generic;

    public sealed class NormalizationResult<T>
    {
        public bool IsSuccess { get; }
        public T? Value { get; }
        public IReadOnlyList<NormalizationError>? Errors { get; }

        private NormalizationResult(T value)
        {
            IsSuccess = true;
            Value = value;
            Errors = null;
        }

        private NormalizationResult(IReadOnlyList<NormalizationError> errors)
        {
            IsSuccess = false;
            Value = default;
            Errors = errors;
        }

        public static NormalizationResult<T> Ok(T value) => new NormalizationResult<T>(value);

        public static NormalizationResult<T> Fail(params NormalizationError[] errors) =>
            new NormalizationResult<T>(errors);
    }
}
