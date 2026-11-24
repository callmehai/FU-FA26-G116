<#
  Bring the whole local dev environment up, idempotently, and wait until it answers.
    powershell -ExecutionPolicy Bypass -File scripts/dev-up.ps1            # postgres, redis, files, livekit, mailpit, backend :5225, vite :5173
    powershell -ExecutionPolicy Bypass -File scripts/dev-up.ps1 -Speech    # also the speech worker (:8090, ~1 GB image)
    powershell -ExecutionPolicy Bypass -File scripts/dev-up.ps1 -Stop      # stop backend and vite (containers stay up)
  Logs: .dev-logs/backend.log, .dev-logs/vite.log (gitignored via *.log).
  One-time setup lives in docs/02-design/03-co-so-du-lieu.md and docs/DEV-ENV.md.
#>
param([switch]$Speech, [switch]$Stop)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
$logs = Join-Path $root '.dev-logs'; New-Item -ItemType Directory -Force $logs | Out-Null

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

# Containers. Postgres sits on POSTGRES_PORT from .env (55432 here: the Windows PostgreSQL service owns 5432).
# After a Windows reboot WinNAT may hold the host port (bind "forbidden by access permissions"): run admin
# `net stop winnat` + `net start winnat` and re-run this script instead of moving ports around.
$services = @('postgres', 'redis', 'files')
$profileArgs = @('--profile', 'calls')
if ($Speech) { $profileArgs += @('--profile', 'speech'); $services += 'speech' }
docker compose @profileArgs up -d @services livekit | Out-Null
docker start e2e-mailpit 2>$null | Out-Null   # SMTP :1025 for the mail the app sends; UI on :8025
# SeaweedFS must have the bucket created explicitly, or it is writable but reads answer 503.
$bucket = (dotnet user-secrets list --project backend/TrustCabinet.Web | Select-String '^FileStorage:BucketName = (.+)$').Matches.Groups[1].Value
if ($bucket) { curl.exe -s -o NUL -X PUT "http://localhost:8333/$bucket" }

# Wait for postgres to be healthy before the backend migrates against it.
$end = (Get-Date).AddSeconds(60)
while ((docker inspect -f '{{.State.Health.Status}}' trustcabinet-postgres) -ne 'healthy') {
  if ((Get-Date) -gt $end) { throw 'trustcabinet-postgres is not healthy' }
  Start-Sleep 2
}
Write-Host 'ok   postgres'

# Backend: restart from a clean process so it always has the current secrets and code.
Stop-Port 5225
# Without admin rights the .NET EventLog logger cannot open its source and the host dies during startup.
$env:Logging__EventLog__LogLevel__Default = 'None'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
Start-Process dotnet -ArgumentList 'run', '--project', 'backend/TrustCabinet.Web', '--no-launch-profile', '--urls', 'http://localhost:5225' `
  -WorkingDirectory $root -WindowStyle Hidden -RedirectStandardOutput "$logs\backend.log" -RedirectStandardError "$logs\backend.err.log"
Wait-Http 'http://localhost:5225/api/health' 180 'backend :5225'

# Frontend: call vite directly. `npm run dev -- --port` swallows flags, and a hidden npm wrapper was what kept dying.
if (-not (Test-Path 'frontend/node_modules')) { Push-Location frontend; npm ci; Pop-Location }
Stop-Port 5173
Start-Process node -ArgumentList 'node_modules/vite/bin/vite.js', '--port', '5173', '--strictPort' `
  -WorkingDirectory "$root\frontend" -WindowStyle Hidden -RedirectStandardOutput "$logs\vite.log" -RedirectStandardError "$logs\vite.err.log"
Wait-Http 'http://localhost:5173/api/health' 60 'vite :5173 (proxy to backend)'

Write-Host "`nReady: http://localhost:5173   mail: http://localhost:8025   QA: cd tests/qa; node run.mjs plans/smoke.mjs"
