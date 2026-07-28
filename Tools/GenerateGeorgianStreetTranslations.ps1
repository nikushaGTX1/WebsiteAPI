param(
    [string]$StreetDataPath = ".\Data\StreetData.cs",
    [string]$OsmDataPath = ".\Data\georgia-osm-roads.json",
    [string]$OutputPath = ".\Data\GeorgianStreetTranslations.g.cs",
    [string]$AuditPath = ".\Data\GeorgianStreetTranslationAudit.csv"
)

$ErrorActionPreference = "Stop"

function Normalize-Name([string]$value) {
    if ([string]::IsNullOrWhiteSpace($value)) {
        return ""
    }

    $normalized = $value.ToLowerInvariant()
    $normalized = $normalized -replace "'s\b", ""
    $normalized = $normalized -replace "\b(street|st|avenue|ave|road|rd|lane|ln|square|highway|hwy)\b", " "
    $normalized = $normalized -replace "[^a-z0-9]+", " "
    return ($normalized -replace "\s+", " ").Trim()
}

$osm = Get-Content $OsmDataPath -Raw -Encoding UTF8 | ConvertFrom-Json
$pairs = @{}

foreach ($element in $osm.elements) {
    $english = [string]$element.tags.'name:en'
    $georgian = [string]$element.tags.'name:ka'

    if ([string]::IsNullOrWhiteSpace($english) -or
        [string]::IsNullOrWhiteSpace($georgian)) {
        continue
    }

    $key = Normalize-Name $english
    if ([string]::IsNullOrWhiteSpace($key)) {
        continue
    }

    if (-not $pairs.ContainsKey($key)) {
        $pairs[$key] = [System.Collections.Generic.HashSet[string]]::new()
    }

    [void]$pairs[$key].Add($georgian.Trim())
}

$uniquePairs = @{}
foreach ($entry in $pairs.GetEnumerator()) {
    if ($entry.Value.Count -eq 1) {
        $uniquePairs[$entry.Key] = [string]($entry.Value | Select-Object -First 1)
    }
}

$streetMatches = Select-String -Path $StreetDataPath -Pattern '^\s*"([^"]+)"[,]?\s*$'
$streets = $streetMatches |
    ForEach-Object { $_.Matches[0].Groups[1].Value.Trim() } |
    Sort-Object -Unique

$verified = [ordered]@{}
$audit = foreach ($street in $streets) {
    $key = Normalize-Name $street
    [string[]]$matches = if ($pairs.ContainsKey($key)) {
        $pairs[$key] | ForEach-Object { [string]$_ }
    } else {
        @()
    }

    $matchType = "exact"

    if ($matches.Count -eq 0 -and $key.Length -ge 5) {
        [string[]]$containedMatches = $uniquePairs.GetEnumerator() |
            Where-Object {
                $_.Key -eq $key -or
                $_.Key.StartsWith("$key ") -or
                $_.Key.EndsWith(" $key") -or
                $key.StartsWith("$($_.Key) ") -or
                $key.EndsWith(" $($_.Key)")
            } |
            Select-Object -ExpandProperty Value -Unique

        if ($containedMatches.Count -eq 1) {
            $matches = $containedMatches
            $matchType = "unique-contained-name"
        } elseif ($containedMatches.Count -gt 1) {
            $matches = $containedMatches
            $matchType = "ambiguous-contained-name"
        }
    }

    if ($matches.Count -eq 1) {
        $verified[$street] = [string]$matches[0]
    }

    [PSCustomObject]@{
        English = $street
        Normalized = $key
        Georgian = if ($matches.Count -eq 1) { $matches[0] } else { "" }
        Status = if ($matches.Count -eq 1) {
            $matchType
        } elseif ($matches.Count -gt 1) {
            "ambiguous"
        } else {
            "unmatched"
        }
        CandidateCount = $matches.Count
    }
}

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add("// Generated from OpenStreetMap bilingual name tags (ODbL).")
$lines.Add("// Run Tools/GenerateGeorgianStreetTranslations.ps1 to refresh.")
$lines.Add("#nullable enable")
$lines.Add("namespace Website_API.Data;")
$lines.Add("")
$lines.Add("public static partial class GeorgianStreetTranslations")
$lines.Add("{")
$lines.Add("    private static readonly Dictionary<string, string> Verified =")
$lines.Add("        new(StringComparer.OrdinalIgnoreCase)")
$lines.Add("        {")

foreach ($entry in $verified.GetEnumerator()) {
    $english = $entry.Key.Replace("\", "\\").Replace('"', '\"')
    $georgian = $entry.Value.Replace("\", "\\").Replace('"', '\"')
    $lines.Add("            [`"$english`"] = `"$georgian`",")
}

$lines.Add("        };")
$lines.Add("")
$lines.Add("    public static string? Find(string englishName) =>")
$lines.Add("        GeorgianStreetOverrides.Find(englishName) ??")
$lines.Add("        TbilisiStreetData.Find(englishName) ??")
$lines.Add("        (Verified.TryGetValue(englishName.Trim(), out var georgian)")
$lines.Add("            ? georgian")
$lines.Add("            : null);")
$lines.Add("")
$lines.Add("    public static string? FindEnglish(string georgianName) =>")
$lines.Add("        GeorgianStreetOverrides.FindEnglish(georgianName) ??")
$lines.Add("        TbilisiStreetData.FindEnglish(georgianName) ??")
$lines.Add("        Verified.FirstOrDefault(item =>")
$lines.Add("            item.Value.Equals(")
$lines.Add("                georgianName.Trim(),")
$lines.Add("                StringComparison.OrdinalIgnoreCase)).Key;")
$lines.Add("}")

[System.IO.File]::WriteAllLines(
    (Join-Path (Get-Location) $OutputPath),
    $lines,
    [System.Text.UTF8Encoding]::new($false))

$audit | Export-Csv $AuditPath -NoTypeInformation -Encoding UTF8

$verifiedCount = @(
    $audit | Where-Object {
        $_.Status -eq "exact" -or
        $_.Status -eq "unique-contained-name"
    }
).Count
$ambiguousCount = @($audit | Where-Object Status -eq "ambiguous").Count
$unmatchedCount = @($audit | Where-Object Status -eq "unmatched").Count

Write-Output "Verified: $verifiedCount"
Write-Output "Ambiguous: $ambiguousCount"
Write-Output "Unmatched: $unmatchedCount"
