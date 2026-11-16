<#
.SYNOPSIS  Generates fresh production secrets and writes deploy\.env.prod (git-ignored). Never prints the values.
#>
$ErrorActionPreference = 'Stop'
$target = Join-Path $PSScriptRoot '.env.prod'
if (Test-Path $target) { throw ".env.prod already exists; delete it first if you really want new keys (the SDK key in the webapp would have to change too)." }

function New-Secret([int]$bytes = 32) {
  $b = New-Object byte[] $bytes
  [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b)
  ([Convert]::ToBase64String($b)).TrimEnd('=').Replace('+', 'A').Replace('/', 'B')
}

$lines = @(
  "CLICKHOUSE_PASSWORD=$(New-Secret 24)",
  "PASTIL_WRITE_KEY=tk_$(New-Secret 18)",
  "PASTIL_READ_KEY=rk_$(New-Secret 32)",
  "PASTIL_ORIGIN_1=https://app.pastil.pet",
  "PASTIL_ORIGIN_2=https://pastil.pet",
  "PASTIL_READONLY_CONNECTION="
)
# LF line endings: the file is read by bash and Docker on Linux (CRLF leaves an invisible \r on every value).
[System.IO.File]::WriteAllText($target, (($lines -join "`n") + "`n"), (New-Object System.Text.ASCIIEncoding))
"Wrote $target"
