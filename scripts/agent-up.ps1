<#
  Local stack for agents: uses ONLY the containers already on this machine.
  Never creates containers, images, volumes or networks (no `docker run`,
  no `docker compose up`, no image pulls). If something is missing, it stops
  and tells the human instead of improvising.

    powershell -ExecutionPolicy Bypass -File scripts/agent-up.ps1            # postgres, redis, files, mailpit, backend :5225, vite :5173
    powershell -ExecutionPolicy Bypass -File scripts/agent-up.ps1 -Calls     # also livekit (+ speech with -Speech)
    powershell -ExecutionPolicy Bypass -File scripts/agent-up.ps1 -Stop      # stop backend and vite (containers stay)

  Logs: .dev-logs/backend.log, .dev-logs/vite.log (gitignored).
  Needs: .NET SDK (global.json), Node.js, Docker Desktop running.

  Learned the hard way (PR #477, 24/11/2026): after a Windows reboot WinNAT
  re-reserves ephemeral port ranges and the postgres container (host port
  55620) cannot bind. Restarting WinNAT needs admin; recreating the container
  on another host port is a human decision (data lives in the volume, but the
  port is part of the team's local setup). This script detects that case and
  stops with the exact fix instead of building a parallel stack.
#>
param([switch]$Calls, [switch]$Speech, [switch]$Stop)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
$logs = Join-Path $root '.dev-logs'; New-Item -ItemType Directory -Force $logs | Out-Null

# Knows only these containers. Anything else on the machine is left alone.
$need = @('trustcabinet-postgres', 'trustcabinet-redis', 'trustcabinet-files', 'e2e-mailpit')
if ($Calls) { $need += 'trustcabinet-livekit' }
if ($Speech) { $need += 'trustcabinet-speech' }

function Stop-Port([int]$port) {
  Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue |
    ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }
}

function Wait-Http([string]$url, [int]$seconds, [string]$name) {
  $end = (Get-Date).AddSeconds($seconds)
  while ((Get-Date) -lt $end) {
    try { if ((Invoke-WebRequest $url -UseBasicParsing -TimeoutSec 3).StatusCode -lt 500) { Write-Host "ok   $name"; return } } catch {}
    Start-Sleep 2
  }
  throw "$name did not answer at $url within ${seconds}s - see .dev-logs/"
}

if ($Stop) { Stop-Port 5225; Stop-Port 5173; Write-Host 'backend and vite stopped'; return }

# 1. The containers must already exist. Never create them here.
$existing = docker ps -a --format '{{.Names}}' 2>$null
foreach ($c in $need) {
  if ($existing -notcontains $c) {
    throw "Container '$c' is not on this machine. Ask the human to create the team stack first (docker compose up -d); this script never creates containers."
  }
}

# 2. Start them (no-op when already running) and report what actually came up.
docker start $need 2>&1 | Out-Null
Start-Sleep 5
$running = docker ps --format '{{.Names}}' 2>$null
$missing = @($need | Where-Object { $running -notcontains $_ })
if ($missing.Count -gt 0) {
  $msg = docker start $missing 2>&1 | Out-String
  if ($msg -match 'ports are not available|forbidden|access permissions') {
    $port = if ($msg -match '127\.0\.0\.1:(\d+)') { $Matches[1] } else { '?' }
    throw @"
Container(s) $($missing -join ', ') cannot bind host port ${port}: Windows is holding that range (see: netsh int ipv4 show excludedportrange protocol=tcp).
This happens after a reboot. I will NOT recreate containers or move ports on my own. Human fix, pick one:
  (a) admin PowerShell:  net stop winnat ; net start winnat   — then re-run this script; or
  (b) recreate only the postgres container on a free host port, keeping the SAME volume (data is preserved):
      docker rm trustcabinet-postgres
      docker create --name trustcabinet-postgres -e POSTGRES_DB=trustcabinet -e POSTGRES_USER=trustcabinet -e POSTGRES_PASSWORD=<from dotnet user-secrets> -p 127.0.0.1:5433:5432 -v trustcabinet_trustcabinet-pgdata:/var/lib/postgresql/data postgres:17-alpine
      dotnet user-secrets set "ConnectionStrings:TrustCabinet" "Host=localhost;Port=5433;Database=trustcabinet;Username=trustcabinet;Password=<same>" --project backend/TrustCabinet.Web
  (c) permanent: admin PowerShell:  netsh int ipv4 add excludedportrange protocol=tcp startport=55620 numberofports=1
"@
  }
  throw "Containers did not stay up: $($missing -join ', '). Last docker message: $msg"
}
Write-Host "ok   containers: $($need -join ', ')"

# 3. Files bucket must exist explicitly, or reads answer 503.
$bucket = (dotnet user-secrets list --project backend/TrustCabinet.Web | Select-String '^FileStorage:BucketName = (.+)$').Matches.Groups[1].Value
if ($bucket) { curl.exe -s -o NUL -X "PUT" "http://localhost:8333/$bucket" }

# 4. Backend + frontend, same as the dev flow (fresh processes, current code).
Stop-Port 5225
$env:Logging__EventLog__LogLevel__Default = 'None'   # no admin rights: the EventLog logger would kill startup
$env:ASPNETCORE_ENVIRONMENT = 'Development'
Start-Process dotnet -ArgumentList 'run', '--project', 'backend/TrustCabinet.Web', '--no-launch-profile', '--urls', 'http://localhost:5225' `
  -WorkingDirectory $root -WindowStyle Hidden -RedirectStandardOutput "$logs\backend.log" -RedirectStandardError "$logs\backend.err.log"
Wait-Http 'http://localhost:5225/api/health' 480 'backend :5225'   # first run compiles: allow extra minutes

if (-not (Test-Path 'frontend/node_modules')) { Push-Location frontend; npm ci; Pop-Location }
Stop-Port 5173
Start-Process node -ArgumentList 'node_modules/vite/bin/vite.js', '--port', '5173', '--strictPort' `
  -WorkingDirectory "$root\frontend" -WindowStyle Hidden -RedirectStandardOutput "$logs\vite.log" -RedirectStandardError "$logs\vite.err.log"
Wait-Http 'http://localhost:5173/api/health' 60 'vite :5173 (proxy to backend)'

Write-Host ''
Write-Host 'Ready: http://localhost:5173   mail: http://localhost:8025   QA: cd tests/qa; node run.mjs plans/smoke.mjs'
Write-Host 'xUnit needs: $env:TRUSTCABINET_TEST_CONNECTION_STRING="Host=localhost;Port=<pg host port>;Database=postgres;Username=trustcabinet;Password=<POSTGRES_PASSWORD>" and $env:TRUSTCABINET_TEST_REDIS="localhost:6379"'
