$ErrorActionPreference = 'Stop'
$aimRoot = Split-Path $PSScriptRoot -Parent
$statePath = Join-Path $aimRoot '.local/session.json'
if (-not (Test-Path -LiteralPath $statePath)) { Write-Host 'Nenhuma sessão AIM registrada.'; return }
$session = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
foreach ($entry in @(@{ Id = $session.uiPid; Started = $session.uiStarted }, @{ Id = $session.apiPid; Started = $session.apiStarted })) {
    $process = Get-Process -Id $entry.Id -ErrorAction SilentlyContinue
    if ($process -and $process.StartTime.ToUniversalTime().Ticks -eq $entry.Started) {
        $command = (Get-CimInstance Win32_Process -Filter "ProcessId=$($entry.Id)").CommandLine
        if ($command -and $command.Contains($aimRoot)) { Stop-Process -Id $entry.Id }
        else { throw "Processo $($entry.Id) não pertence ao AIM; encerramento recusado." }
    }
}
Remove-Item -LiteralPath $statePath
Write-Host 'Serviços AIM encerrados. O banco local foi preservado.'
