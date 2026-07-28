param(
    [string]$StreetDataPath = ".\Data\StreetData.cs",
    [string]$OsmDataPath = ".\Data\georgia-osm-locations.json",
    [string]$OutputPath = ".\Data\VerifiedGeorgianLocations.g.cs",
    [string]$AuditPath = ".\Data\GeorgianLocationTranslationAudit.csv"
)

$ErrorActionPreference = "Stop"

function Normalize-Name([string]$value) {
    $normalized = $value.ToLowerInvariant()
    $normalized = $normalized -replace "\b(municipality|district|settlement)\b", " "
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
    if (-not $pairs.ContainsKey($key)) {
        $pairs[$key] = [System.Collections.Generic.HashSet[string]]::new()
    }
    [void]$pairs[$key].Add($georgian.Trim())
}

$values = Select-String -Path $StreetDataPath `
        -Pattern '(City|District)\s*=\s*"([^"]+)"' -AllMatches |
    ForEach-Object { $_.Matches } |
    ForEach-Object { $_.Groups[2].Value.Trim() } |
    Sort-Object -Unique

$verified = [ordered]@{}
$audit = foreach ($value in $values) {
    $key = Normalize-Name $value
    [string[]]$matches = if ($pairs.ContainsKey($key)) {
        $pairs[$key] | ForEach-Object { [string]$_ }
    } else {
        @()
    }

    if ($matches.Count -gt 1) {
        if ($value -match '(?i)\b(Municipality|District|Settlement)$') {
            $longestLength = ($matches | Measure-Object Length -Maximum).Maximum
            [string[]]$preferred = $matches |
                Where-Object { $_.Length -eq $longestLength }
            if ($preferred.Count -eq 1) {
                $matches = $preferred
            }
        }
    }

    if ($matches.Count -eq 1) {
        $verified[$value] = [string]$matches[0]
    }

    [PSCustomObject]@{
        English = $value
        Georgian = if ($matches.Count -eq 1) { $matches[0] } else { "" }
        Status = if ($matches.Count -eq 1) {
            "verified"
        } elseif ($matches.Count -gt 1) {
            "ambiguous"
        } else {
            "unmatched"
        }
        CandidateCount = $matches.Count
    }
}

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add("// Generated from OpenStreetMap bilingual place tags (ODbL).")
$lines.Add("#nullable enable")
$lines.Add("namespace Website_API.Data;")
$lines.Add("")
$lines.Add("public static class VerifiedGeorgianLocations")
$lines.Add("{")
$lines.Add("    private static readonly Dictionary<string, string> Values =")
$lines.Add("        new(StringComparer.OrdinalIgnoreCase)")
$lines.Add("        {")

foreach ($entry in $verified.GetEnumerator()) {
    $english = $entry.Key.Replace("\", "\\").Replace('"', '\"')
    $georgian = $entry.Value.Replace("\", "\\").Replace('"', '\"')
    $lines.Add("            [`"$english`"] = `"$georgian`",")
}

$lines.Add("        };")
$lines.Add("")
$lines.Add("    public static string? Find(string english) =>")
$lines.Add("        Values.TryGetValue(english.Trim(), out var georgian)")
$lines.Add("            ? georgian")
$lines.Add("            : null;")
$lines.Add("")
$lines.Add("    public static string? FindEnglish(string georgian) =>")
$lines.Add("        Values.FirstOrDefault(item =>")
$lines.Add("            item.Value.Equals(")
$lines.Add("                georgian.Trim(),")
$lines.Add("                StringComparison.OrdinalIgnoreCase)).Key;")
$lines.Add("}")

[System.IO.File]::WriteAllLines(
    (Join-Path (Get-Location) $OutputPath),
    $lines,
    [System.Text.UTF8Encoding]::new($false))

$audit | Export-Csv $AuditPath -NoTypeInformation -Encoding UTF8
Write-Output "Verified: $(@($audit | Where-Object Status -eq 'verified').Count)"
Write-Output "Ambiguous: $(@($audit | Where-Object Status -eq 'ambiguous').Count)"
Write-Output "Unmatched: $(@($audit | Where-Object Status -eq 'unmatched').Count)"
