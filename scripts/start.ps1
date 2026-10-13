param(
    [string]$Repository,
    [int]$ApiPort = 5187,
    [int]$UiPort = 5173,
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
$aimRoot = Split-Path $PSScriptRoot -Parent
if (-not $Repository) { $Repository = Join-Path $aimRoot 'fixtures/nest' }
$Repository = (Resolve-Path -LiteralPath $Repository).Path
$aimLocal = Join-Path $aimRoot '.local'
New-Item -ItemType Directory -Path $aimLocal -Force | Out-Null
$statePath = Join-Path $aimLocal 'session.json'
if (Test-Path -LiteralPath $statePath) { throw 'Execute scripts/stop.ps1 antes de iniciar outra sessão.' }
foreach ($port in @($ApiPort, $UiPort)) {
    if ($port -lt 1024 -or $port -gt 65535) { throw 'Porta inválida.' }
    if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) { throw "Porta $port ocupada." }
}
if ($ApiPort -eq $UiPort) { throw 'As portas da API e UI devem ser diferentes.' }
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
Push-Location $aimRoot
try {
    if (-not $NoBuild) {
        if (-not (Test-Path -LiteralPath (Join-Path $aimRoot 'node_modules/typescript'))) {
            & npm.cmd ci --no-fund --no-audit
            if ($LASTEXITCODE -ne 0) { throw 'Falha ao instalar dependências.' }
        }
        & dotnet build src/Aim.Api/Aim.Api.csproj --nologo
        if ($LASTEXITCODE -ne 0) { throw 'Falha no build da API.' }
    }
    if (-not $env:AIM_TOKEN) { $env:AIM_TOKEN = [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32)) }
    if ($env:AIM_TOKEN.Length -lt 32) { throw 'AIM_TOKEN deve conter pelo menos 32 caracteres aleatórios.' }
    $env:AIM_REPOSITORY = $Repository
    $env:AIM_DATABASE = Join-Path $aimLocal 'aim.db'
    $env:AIM_ANALYZER = Join-Path $aimRoot 'analyzer/extract.mjs'
    $env:AIM_NODE = (Get-Command node.exe).Source
    $env:AIM_PORT = "$ApiPort"
    $env:AIM_UI_ORIGIN = "http://127.0.0.1:$UiPort"
    $env:VITE_API_URL = "http://127.0.0.1:$ApiPort"
    $apiDll = Join-Path $aimRoot 'src/Aim.Api/bin/Debug/net10.0/Aim.Api.dll'
    $vite = Join-Path $aimRoot 'node_modules/vite/bin/vite.js'
    $frontend = Join-Path $aimRoot 'frontend'
    $apiProcess = $null
    $uiProcess = $null
    try {
        $apiProcess = Start-Process -FilePath (Get-Command dotnet.exe).Source -ArgumentList @('"' + $apiDll + '"') -WorkingDirectory (Join-Path $aimRoot 'src/Aim.Api') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $aimLocal 'api.log') -RedirectStandardError (Join-Path $aimLocal 'api.error.log')
        $uiProcess = Start-Process -FilePath $env:AIM_NODE -ArgumentList @('"' + $vite + '"', '"' + $frontend + '"', '--host', '127.0.0.1', '--port', "$UiPort", '--strictPort') -WorkingDirectory $aimRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $aimLocal 'ui.log') -RedirectStandardError (Join-Path $aimLocal 'ui.error.log')
        $ready = $false
        for ($attempt = 0; $attempt -lt 60; $attempt++) {
            if ($apiProcess.HasExited -or $uiProcess.HasExited) { throw 'Um serviço encerrou. Consulte .local/*.error.log.' }
            try {
                $null = Invoke-RestMethod "$env:VITE_API_URL/health" -TimeoutSec 1
                $null = Invoke-WebRequest $env:AIM_UI_ORIGIN -TimeoutSec 1
                $ready = $true
                break
            } catch { Start-Sleep -Milliseconds 250 }
        }
        if (-not $ready) { throw 'Os serviços não ficaram disponíveis no prazo esperado.' }
        @{
            apiPid = $apiProcess.Id; apiStarted = $apiProcess.StartTime.ToUniversalTime().Ticks
            uiPid = $uiProcess.Id; uiStarted = $uiProcess.StartTime.ToUniversalTime().Ticks
            apiUrl = $env:VITE_API_URL; uiUrl = $env:AIM_UI_ORIGIN; repository = $Repository
        } | ConvertTo-Json | Set-Content -LiteralPath $statePath -Encoding utf8
        Write-Host "AIM disponível em $env:AIM_UI_ORIGIN"
        Write-Host 'Token local (somente neste terminal; não gravado nos logs dos serviços):'
        Write-Host $env:AIM_TOKEN
        Write-Host 'Para encerrar: .\scripts\stop.ps1'
    } catch {
        if ($uiProcess -and -not $uiProcess.HasExited) { Stop-Process -Id $uiProcess.Id }
        if ($apiProcess -and -not $apiProcess.HasExited) { Stop-Process -Id $apiProcess.Id }
        throw
    }
} finally { Pop-Location }
