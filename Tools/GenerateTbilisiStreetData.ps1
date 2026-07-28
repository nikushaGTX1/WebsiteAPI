param(
    [string]$InputPattern = ".\Data\tbilisi-osm-streets-*.json",
    [string]$OutputPath = ".\Data\TbilisiStreetData.g.cs"
)

$ErrorActionPreference = "Stop"
$streets = @{}

foreach ($file in Get-ChildItem $InputPattern) {
    $osm = Get-Content $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json

    foreach ($element in $osm.elements) {
        $english = [string]$element.tags.'name:en'
        $georgian = [string]$element.tags.'name:ka'
        $defaultName = [string]$element.tags.name

        if ([string]::IsNullOrWhiteSpace($english)) {
            if ([string]::IsNullOrWhiteSpace($defaultName)) {
                continue
            }

            $english = $defaultName.Trim()
        }

        $english = $english.Trim()
        if ([string]::IsNullOrWhiteSpace($georgian) -and
            $defaultName -match '[\u10A0-\u10FF]') {
            $georgian = $defaultName.Trim()
        }

        if (-not $streets.ContainsKey($english) -or
            ([string]::IsNullOrWhiteSpace($streets[$english]) -and
             -not [string]::IsNullOrWhiteSpace($georgian))) {
            $streets[$english] = $georgian.Trim()
        }
    }
}

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add("// Generated from OpenStreetMap named highway features in Tbilisi (ODbL).")
$lines.Add("// Run Tools/GenerateTbilisiStreetData.ps1 after refreshing the Overpass snapshots.")
$lines.Add("#nullable enable")
$lines.Add("namespace Website_API.Data;")
$lines.Add("")
$lines.Add("public static class TbilisiStreetData")
$lines.Add("{")
$lines.Add("    private static readonly Dictionary<string, string?> Values =")
$lines.Add("        new(StringComparer.OrdinalIgnoreCase)")
$lines.Add("        {")

foreach ($entry in ($streets.GetEnumerator() | Sort-Object Key)) {
    $english = $entry.Key.Replace("\", "\\").Replace('"', '\"')
    $georgian = ([string]$entry.Value).Replace("\", "\\").Replace('"', '\"')
    $value = if ([string]::IsNullOrWhiteSpace($georgian)) {
        "null"
    } else {
        "`"$georgian`""
    }
    $lines.Add("            [`"$english`"] = $value,")
}

$lines.Add("        };")
$lines.Add("")
$lines.Add("    public static IReadOnlyCollection<string> Names => Values.Keys;")
$lines.Add("")
$lines.Add("    public static string? Find(string englishName) =>")
$lines.Add("        Values.TryGetValue(englishName.Trim(), out var georgian)")
$lines.Add("            ? georgian")
$lines.Add("            : null;")
$lines.Add("")
$lines.Add("    public static string? FindEnglish(string georgianName) =>")
$lines.Add("        Values.FirstOrDefault(item =>")
$lines.Add("            item.Value?.Equals(")
$lines.Add("                georgianName.Trim(),")
$lines.Add("                StringComparison.OrdinalIgnoreCase) == true).Key;")
$lines.Add("}")

[System.IO.File]::WriteAllLines(
    (Join-Path (Get-Location) $OutputPath),
    $lines,
    [System.Text.UTF8Encoding]::new($false))

$translated = @($streets.Values | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }).Count
Write-Output "Tbilisi streets: $($streets.Count)"
Write-Output "With Georgian name: $translated"
