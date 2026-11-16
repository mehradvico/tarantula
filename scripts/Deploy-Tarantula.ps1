<#
.SYNOPSIS
    One-command deploy of the Tarantula ingest API (+ ClickHouse) to the Pastil server.

.DESCRIPTION
    Same pattern as Pastil's other deploy scripts (PuTTY pscp/plink, DPAPI-saved SSH password), so it also runs from
    Pastil Deploy. The server is small and shared, so the image is built HERE and shipped as an archive.

    Local:   1. docker build -t tarantula-api:latest .
             2. docker save + gzip -> deploy\tarantula-api.tar.gz
    Upload:  3. compose file, ClickHouse low-memory config, image archive
             4. deploy\.env.prod ONLY when the server has none (or with -ForceEnv); line endings are normalised to LF
    Remote:  5. docker load, docker compose up -d --no-build, wait for ClickHouse + API
             6. checks: no stray CR in secrets, /v1/live answers, memory left

    Connection settings: scripts\.env.deploy (gitignored). The SSH password is prompted, or saved once with -SaveCredential.
    The reverse proxy (nginx + certificate for t.pastil.pet) is NOT touched by this script.

.PARAMETER SkipBuild   Reuse the last built archive.
.PARAMETER ForceEnv    Overwrite the server's .env.prod with the local one (new keys!). Needed only after New-Keys.ps1.
.PARAMETER SaveCredential / ClearCredential   Manage the DPAPI-encrypted SSH password.
#>
param(
    [string]$ServerHost,
    [string]$ServerUser,
    [string]$RemoteDir,
    [switch]$SkipBuild,
    [switch]$ForceEnv,
    [switch]$SaveCredential,
    [switch]$ClearCredential
)

$ErrorActionPreference = 'Stop'

$Root       = Split-Path -Parent $PSScriptRoot
$DeployDir  = Join-Path $Root 'deploy'
$ConfigPath = Join-Path $PSScriptRoot '.env.deploy'
$CredPath   = Join-Path $PSScriptRoot '.deploy-credential.xml'
$Archive    = Join-Path $DeployDir 'tarantula-api.tar.gz'
$ImageTar   = Join-Path $DeployDir 'tarantula-api.tar'
$ApiPort    = 5080

$script:StepIndex = 0

function Write-Step { param([string]$Message) $script:StepIndex++; Write-Host ''; Write-Host ("[{0}] {1}" -f $script:StepIndex, $Message) -ForegroundColor Cyan }
function Write-Ok   { param([string]$Message) Write-Host ("    OK  {0}" -f $Message) -ForegroundColor Green }
function Write-Warn { param([string]$Message) Write-Host ("    !!  {0}" -f $Message) -ForegroundColor Yellow }
function Stop-WithError { param([string]$Message) Write-Host ''; Write-Host ("  FAILED: {0}" -f $Message) -ForegroundColor Red; exit 1 }

function Invoke-NativeCapture {
    param([string]$Exe, [string[]]$Arguments)
    $ErrorActionPreference = 'SilentlyContinue'
    $output = & $Exe @Arguments 2>&1
    [pscustomobject]@{ ExitCode = $LASTEXITCODE; Text = ($output | Out-String) }
}

function Invoke-NativeStreaming {
    param([string]$Exe, [string[]]$Arguments)
    $ErrorActionPreference = 'Continue'
    & $Exe @Arguments 2>&1 | ForEach-Object {
        if ($_ -is [System.Management.Automation.ErrorRecord]) { Write-Host $_.Exception.Message } else { Write-Host $_ }
    }
    return $LASTEXITCODE
}

function Resolve-Executable {
    param([string]$Name, [string[]]$Fallbacks)
    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    foreach ($candidate in $Fallbacks) { if (Test-Path -LiteralPath $candidate) { return $candidate } }
    return $null
}

if ($ClearCredential) {
    if (Test-Path -LiteralPath $CredPath) { Remove-Item -LiteralPath $CredPath -Force; Write-Host 'Stored deploy credential removed.' -ForegroundColor Green }
    else { Write-Host 'No stored credential to remove.' -ForegroundColor Yellow }
    exit 0
}

# --- configuration ----------------------------------------------------------
$config = @{}
if (Test-Path -LiteralPath $ConfigPath) {
    foreach ($line in (Get-Content -LiteralPath $ConfigPath)) {
        $t = $line.Trim()
        if (-not $t -or $t.StartsWith('#')) { continue }
        $i = $t.IndexOf('=')
        if ($i -le 0) { continue }
        $config[$t.Substring(0, $i).Trim()] = $t.Substring($i + 1).Trim().Trim('"').Trim("'")
    }
}
if (-not $ServerHost) { $ServerHost = $config['TARANTULA_DEPLOY_HOST'] }
if (-not $ServerUser) { $ServerUser = $config['TARANTULA_DEPLOY_USER'] }
if (-not $RemoteDir)  { $RemoteDir  = $config['TARANTULA_DEPLOY_REMOTE_DIR'] }
if (-not $ServerUser) { $ServerUser = 'root' }
if (-not $RemoteDir)  { $RemoteDir  = '/root/tarantula' }
if (-not $ServerHost) {
    Write-Host "  No server host. Create $ConfigPath with TARANTULA_DEPLOY_HOST=<ip> (gitignored), or pass -ServerHost." -ForegroundColor Yellow
    exit 1
}

Write-Host ''
Write-Host '  Tarantula deploy' -ForegroundColor White
Write-Host ("  target: {0}@{1}:{2}" -f $ServerUser, $ServerHost, $RemoteDir) -ForegroundColor DarkGray

# --- prerequisites ----------------------------------------------------------
Write-Step 'Checking prerequisites'
$dockerExe = Resolve-Executable -Name 'docker' -Fallbacks @('C:\Program Files\Docker\Docker\resources\bin\docker.exe')
$tarExe    = Resolve-Executable -Name 'tar'    -Fallbacks @("$env:SystemRoot\System32\tar.exe")
$pscpExe   = Resolve-Executable -Name 'pscp'   -Fallbacks @('C:\Program Files\PuTTY\pscp.exe')
$plinkExe  = Resolve-Executable -Name 'plink'  -Fallbacks @('C:\Program Files\PuTTY\plink.exe')
if (-not $dockerExe) { Stop-WithError 'docker not found. Start Docker Desktop.' }
if (-not $tarExe)    { Stop-WithError 'tar not found (expected in Windows System32).' }
if (-not $pscpExe)   { Stop-WithError 'pscp not found. Install PuTTY (https://www.putty.org).' }
if (-not $plinkExe)  { Stop-WithError 'plink not found. Install PuTTY (https://www.putty.org).' }
Write-Ok 'docker, tar, pscp, plink available'

# --- credential -------------------------------------------------------------
Write-Step 'Resolving SSH credential'
$securePassword = $null
if ($SaveCredential) {
    $securePassword = Read-Host -Prompt "  SSH password for $ServerUser@$ServerHost" -AsSecureString
    $securePassword | ConvertFrom-SecureString | Set-Content -LiteralPath $CredPath -Encoding utf8
    Write-Ok "Credential saved (DPAPI-encrypted) to $CredPath"
} elseif (Test-Path -LiteralPath $CredPath) {
    try {
        $securePassword = (Get-Content -LiteralPath $CredPath -Raw).Trim() | ConvertTo-SecureString -ErrorAction Stop
        Write-Ok 'Using stored credential'
    } catch { Write-Warn 'Stored credential could not be decrypted. Prompting instead.' }
}
if (-not $securePassword) {
    if ([Console]::IsInputRedirected) {
        Stop-WithError 'No usable stored SSH credential and no console to type it in. Click the key icon on the Tarantula card in Pastil Deploy to save the password, then run again.'
    }
    $securePassword = Read-Host -Prompt "  SSH password for $ServerUser@$ServerHost" -AsSecureString
}

$pwFile = Join-Path ([System.IO.Path]::GetTempPath()) ("tarantula-{0}.tmp" -f ([guid]::NewGuid().ToString('N')))
$bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
try {
    [System.IO.File]::WriteAllText($pwFile, [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr), (New-Object System.Text.UTF8Encoding($false)))
} finally { [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }

$tmpEnv = $null
function Remove-Temp {
    if (Test-Path -LiteralPath $pwFile) {
        try { [System.IO.File]::WriteAllText($pwFile, ('0' * 256)) } catch { }
        Remove-Item -LiteralPath $pwFile -Force -ErrorAction SilentlyContinue
    }
    if ($tmpEnv -and (Test-Path -LiteralPath $tmpEnv)) { Remove-Item -LiteralPath $tmpEnv -Force -ErrorAction SilentlyContinue }
}

try {
    # --- connection -----------------------------------------------------------
    Write-Step 'Testing SSH connection'
    $probe = Invoke-NativeCapture -Exe $plinkExe -Arguments @('-batch', '-ssh', '-l', $ServerUser, '-pwfile', $pwFile, $ServerHost, 'echo tarantula-ssh-ok')
    if ($probe.ExitCode -ne 0) {
        if ($probe.Text -match 'host key is not cached|server''s host key') {
            Write-Host "  The server host key is not cached. Run once and answer y:  plink -ssh $ServerUser@$ServerHost" -ForegroundColor Yellow
            Stop-WithError 'Host key not cached.'
        }
        Write-Host $probe.Text -ForegroundColor DarkGray
        Stop-WithError 'SSH connection failed. Check host, user and password.'
    }
    Write-Ok "Connected to $ServerUser@$ServerHost"

    # --- build ----------------------------------------------------------------
    if ($SkipBuild) {
        Write-Step 'Build skipped'
        if (-not (Test-Path -LiteralPath $Archive)) { Stop-WithError "No archive at $Archive; run without -SkipBuild." }
    } else {
        Write-Step 'Building the image locally (the server has too little free RAM to build)'
        $code = Invoke-NativeStreaming -Exe $dockerExe -Arguments @('build', '-t', 'tarantula-api:latest', $Root)
        if ($code -ne 0) { Stop-WithError "docker build failed (exit code $code)" }
        Write-Step 'Exporting the image'
        $code = Invoke-NativeStreaming -Exe $dockerExe -Arguments @('save', 'tarantula-api:latest', '-o', $ImageTar)
        if ($code -ne 0) { Stop-WithError "docker save failed (exit code $code)" }
        if (Test-Path -LiteralPath $Archive) { Remove-Item -LiteralPath $Archive -Force }
        $code = Invoke-NativeStreaming -Exe $tarExe -Arguments @('-czf', $Archive, '-C', $DeployDir, 'tarantula-api.tar')
        Remove-Item -LiteralPath $ImageTar -Force -ErrorAction SilentlyContinue
        if ($code -ne 0) { Stop-WithError "compressing the image failed (exit code $code)" }
    }
    Write-Ok ("archive {0} MB" -f [math]::Round((Get-Item -LiteralPath $Archive).Length / 1MB, 1))

    # --- upload ---------------------------------------------------------------
    Write-Step 'Uploading'
    $null = Invoke-NativeCapture -Exe $plinkExe -Arguments @('-batch', '-ssh', '-l', $ServerUser, '-pwfile', $pwFile, $ServerHost, "mkdir -p '$RemoteDir'")
    $files = @('docker-compose.prod.yml', 'clickhouse-low-memory.xml', 'users-low-memory.xml', 'tarantula-api.tar.gz') | ForEach-Object { Join-Path $DeployDir $_ }
    foreach ($f in $files) { if (-not (Test-Path -LiteralPath $f)) { Stop-WithError "Missing $f" } }
    $code = Invoke-NativeStreaming -Exe $pscpExe -Arguments (@('-batch', '-pwfile', $pwFile) + $files + @("{0}@{1}:{2}/" -f $ServerUser, $ServerHost, $RemoteDir))
    if ($code -ne 0) { Stop-WithError "Upload failed (exit code $code)" }

    $hasEnv = (Invoke-NativeCapture -Exe $plinkExe -Arguments @('-batch', '-ssh', '-l', $ServerUser, '-pwfile', $pwFile, $ServerHost, "test -f '$RemoteDir/.env.prod' && echo yes || echo no")).Text.Trim() -eq 'yes'
    $localEnv = Join-Path $DeployDir '.env.prod'
    if ($ForceEnv -or -not $hasEnv) {
        if (-not (Test-Path -LiteralPath $localEnv)) { Stop-WithError 'deploy\.env.prod is missing. Run deploy\New-Keys.ps1 first.' }
        # Always LF: a CR at the end of a value silently breaks keys and passwords on Linux.
        $tmpEnv = Join-Path ([System.IO.Path]::GetTempPath()) ("tarantula-env-{0}.tmp" -f ([guid]::NewGuid().ToString('N')))
        $text = [System.IO.File]::ReadAllText($localEnv).Replace("`r`n", "`n").Replace("`r", "`n")
        [System.IO.File]::WriteAllText($tmpEnv, $text, (New-Object System.Text.UTF8Encoding($false)))
        $code = Invoke-NativeStreaming -Exe $pscpExe -Arguments @('-batch', '-pwfile', $pwFile, $tmpEnv, ("{0}@{1}:{2}/.env.prod" -f $ServerUser, $ServerHost, $RemoteDir))
        if ($code -ne 0) { Stop-WithError "Uploading .env.prod failed (exit code $code)" }
        Write-Ok 'uploaded .env.prod (LF line endings)'
    } else {
        Write-Ok 'server already has .env.prod; left unchanged (use -ForceEnv to replace it)'
    }
    Write-Ok 'Upload complete'

    # --- remote ---------------------------------------------------------------
    Write-Step 'Loading the image and (re)starting the stack'
    $remote = @"
set -e
cd '$RemoteDir'
chmod 600 .env.prod
sed -i 's/\r$//' .env.prod
echo '--> docker load'
gunzip -c tarantula-api.tar.gz | docker load
echo '--> compose up'
docker compose -f docker-compose.prod.yml --env-file .env.prod up -d --no-build
echo '--> waiting for the API'
READ_KEY=`$(grep '^PASTIL_READ_KEY=' .env.prod | cut -d= -f2)
ok=0
for i in `$(seq 1 40); do
  if curl -fsS -m 3 -H "X-Tarantula-Read-Key: `$READ_KEY" http://127.0.0.1:$ApiPort/v1/live > /tmp/tk-live.json 2>/dev/null; then ok=1; break; fi
  sleep 2
done
docker compose -f docker-compose.prod.yml --env-file .env.prod ps
if [ `$ok -ne 1 ]; then echo 'API did not answer'; docker logs --tail 30 tarantula-api-1 2>&1 | cut -c1-300; exit 1; fi
echo "live: `$(cat /tmp/tk-live.json)"
echo "CR in container read key (must be 0): `$(docker exec tarantula-api-1 printenv Tarantula__Projects__Items__0__ReadKey | grep -c `$'\r' || true)"
rm -f tarantula-api.tar.gz
docker stats --no-stream --format '{{.Name}} {{.MemUsage}}' tarantula-api-1 tarantula-clickhouse-1
free -m | head -2
"@
    $remote = $remote.Replace("`r`n", "`n")
    $code = Invoke-NativeStreaming -Exe $plinkExe -Arguments @('-batch', '-ssh', '-l', $ServerUser, '-pwfile', $pwFile, $ServerHost, $remote)
    if ($code -ne 0) { Stop-WithError "Remote step failed (exit code $code). See the log above." }

    Write-Host ''
    Write-Host '  Tarantula is running on the server.' -ForegroundColor Green
    Write-Host '  Public access still needs the nginx server block + certificate for t.pastil.pet (see docs\deploy\PRODUCTION.md).' -ForegroundColor DarkGray
}
finally {
    Remove-Temp
}
