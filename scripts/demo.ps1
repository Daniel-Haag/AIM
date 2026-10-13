param([ValidateSet('Add', 'Restore')][string]$Action = 'Add')
$ErrorActionPreference = 'Stop'
$aimRoot = Split-Path $PSScriptRoot -Parent
$fixture = Join-Path $aimRoot 'fixtures/nest/src/orders.module.ts'
$backup = Join-Path $aimRoot '.local/orders.module.ts.backup'
if ($Action -eq 'Restore') {
    if (-not (Test-Path -LiteralPath $backup)) { throw 'Não existe backup de demonstração para restaurar.' }
    Copy-Item -LiteralPath $backup -Destination $fixture
    Remove-Item -LiteralPath $backup
    Write-Host 'Fixture restaurada; acompanhe a remoção da relação no mapa.'
    return
}
if (Test-Path -LiteralPath $backup) { throw 'A demonstração já foi aplicada. Use -Action Restore antes de repetir.' }
$source = Get-Content -LiteralPath $fixture -Raw
if (-not $source.Contains('imports: [UsersModule]') -or $source.Contains('AuditModule')) { throw 'Fixture foi editada. Demonstração recusada para preservar suas mudanças.' }
New-Item -ItemType Directory -Path (Split-Path $backup -Parent) -Force | Out-Null
Copy-Item -LiteralPath $fixture -Destination $backup
$updated = $source.Replace("import { UsersModule }", "import { AuditModule } from './audit.module';`nimport { UsersModule }").Replace('imports: [UsersModule]', 'imports: [UsersModule, AuditModule]')
[System.IO.File]::WriteAllText($fixture, $updated)
Write-Host 'Dependência AuditModule adicionada à fixture. O watcher deve atualizar uma investigação que já inclua orders.module.ts.'
