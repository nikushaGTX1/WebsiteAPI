param(
    [string]$PostgresBin = "$env:LOCALAPPDATA\velven-postgres\pgsql\bin",
    [string]$DataDirectory = "$env:LOCALAPPDATA\velven-api-test-postgres",
    [int]$Port = 55432
)

$ErrorActionPreference = 'Stop'
$pgCtl = Join-Path $PostgresBin 'pg_ctl.exe'
if (!(Test-Path -LiteralPath $pgCtl) -or !(Test-Path -LiteralPath (Join-Path $DataDirectory 'PG_VERSION'))) {
    throw 'Local test PostgreSQL is not initialized. Supply PostgresBin and DataDirectory for an isolated test cluster.'
}

& $pgCtl -D $DataDirectory status | Out-Null
if ($LASTEXITCODE -ne 0) {
    & $pgCtl -D $DataDirectory -l (Join-Path $DataDirectory 'server.log') -o "-h 127.0.0.1 -p $Port" -w start
    if ($LASTEXITCODE -ne 0) { throw 'Could not start the local test database.' }
}

# Tests create and remove their own databases using this maintenance connection.
$previousTest = $env:MYHOME_TEST_DATABASE
$previousDefault = $env:ConnectionStrings__Default
try {
    $env:MYHOME_TEST_DATABASE = "Host=127.0.0.1;Port=$Port;Database=postgres;Username=postgres"
    $env:ConnectionStrings__Default = $env:MYHOME_TEST_DATABASE
    Push-Location $PSScriptRoot
    try {
        dotnet test 'Website API.Tests\Website API.Tests.csproj'
        if ($LASTEXITCODE -ne 0) { throw 'API tests failed.' }
        dotnet ef migrations has-pending-model-changes --no-build
        if ($LASTEXITCODE -ne 0) { throw 'Migration model validation failed.' }
    } finally { Pop-Location }
} finally {
    $env:MYHOME_TEST_DATABASE = $previousTest
    $env:ConnectionStrings__Default = $previousDefault
}
