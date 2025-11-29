#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Audit PackageReference usage against Directory.Packages.props.

.DESCRIPTION
  Scans all .csproj files for PackageReference Include/Update names and compares them
  to PackageVersion entries in Directory.Packages.props. Reports missing central
  versions and exits with code 1 if any are missing.

.EXIT CODES
  0 - OK (no missing central versions)
  1 - Missing central versions found
  2 - Central props file not found
  3 - XML parse error
#>

[CmdletBinding()]
param(
  [string]$SolutionRoot = (Get-Location).Path,
  [string]$CentralProps = "Directory.Packages.props"
)

function Load-XmlFile([string]$path) {
  if (-not (Test-Path $path)) {
    Write-Error "File not found: $path"
    exit 2
  }
  try {
    [xml]$xml = Get-Content $path -Raw
    return $xml
  } catch {
    Write-Error "Failed to parse XML: $path"
    exit 3
  }
}

Push-Location $SolutionRoot
try {
  Write-Host "Audit packages in: $SolutionRoot"
  $centralPath = Join-Path $SolutionRoot $CentralProps
  $centralXml = Load-XmlFile $centralPath

  # Prepare namespace manager for central props
  $nsmgr = New-Object System.Xml.XmlNamespaceManager($centralXml.NameTable)
  $centralNs = $centralXml.DocumentElement.NamespaceURI
  if ($centralNs) { $nsmgr.AddNamespace("msb", $centralNs) }

  # Collect central package names from PackageVersion elements
  $centralPackages = @()
  if ($centralNs) {
    $nodes = $centralXml.SelectNodes("//msb:PackageVersion", $nsmgr)
  } else {
    $nodes = $centralXml.SelectNodes("//PackageVersion")
  }

  if ($nodes) {
    foreach ($n in $nodes) {
      $name = $null
      if ($n -and $n.Attributes -and $n.Attributes["Include"]) {
        $name = $n.Attributes["Include"].Value
      } elseif ($n -and $n.InnerText) {
        $name = $n.InnerText.Trim()
      }
      if ($name) { $centralPackages += $name }
    }
  }
  $centralPackages = $centralPackages | Where-Object { $_ } | Sort-Object -Unique

  # Find all csproj files and extract PackageReference Include/Update names
  $projFiles = Get-ChildItem -Path $SolutionRoot -Recurse -Filter *.csproj -File
  if (-not $projFiles) {
    Write-Host "No .csproj files found; nothing to audit."
    exit 0
  }

  $usedPackages = [System.Collections.Generic.HashSet[string]]::new()
  foreach ($proj in $projFiles) {
    try {
      [xml]$px = Get-Content $proj.FullName -Raw
    } catch {
      Write-Warning "Skipping unreadable project: $($proj.FullName)"
      continue
    }

    $projNs = $px.DocumentElement.NamespaceURI
    $pNsmgr = New-Object System.Xml.XmlNamespaceManager($px.NameTable)
    if ($projNs) { $pNsmgr.AddNamespace("msb", $projNs) }

    if ($projNs) {
      $refs = $px.SelectNodes("//msb:PackageReference", $pNsmgr)
    } else {
      $refs = $px.SelectNodes("//PackageReference")
    }

    if ($refs) {
      foreach ($r in $refs) {
        # Prefer Include, fall back to Update attribute if present
        $inc = $null
        if ($r -and $r.Attributes) {
          if ($r.Attributes["Include"]) { $inc = $r.Attributes["Include"].Value }
          elseif ($r.Attributes["Update"]) { $inc = $r.Attributes["Update"].Value }
        }
        if (-not $inc) {
          # Some projects may use child <PackageReference><Name>...</Name></PackageReference> patterns; skip those
          continue
        }
        $usedPackages.Add($inc) | Out-Null
      }
    }
  }

  $usedList = $usedPackages | Sort-Object
  $missing = @()
  foreach ($pkg in $usedList) {
    if (-not ($centralPackages -contains $pkg)) {
      $missing += $pkg
    }
  }

  if ($missing.Count -eq 0) {
    Write-Host "OK: All PackageReference entries have central versions in $CentralProps"
    exit 0
  }

  Write-Host "ERROR: The following packages are referenced by projects but missing from $($CentralProps):" -ForegroundColor Red
  foreach ($m in $missing) {
    Write-Host "  - $m"
  }

  Write-Host ""

  Write-Host "Suggested action: add entries like the following to $($CentralProps):" -ForegroundColor Yellow
  foreach ($m in $missing) {
    Write-Host "<PackageVersion Include=`"$m`" Version=`"x.y.z`" />"
  }

  exit 1
}
finally {
  Pop-Location
}
