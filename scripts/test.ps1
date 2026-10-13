$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    & npm.cmd test
    if ($LASTEXITCODE -ne 0) { throw 'Testes do extrator falharam.' }
    & npm.cmd run check --workspace fixtures/nest
    if ($LASTEXITCODE -ne 0) { throw 'Fixture NestJS inválida.' }
    & dotnet test Aim.slnx --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Testes .NET falharam.' }
    & dotnet build src/Aim.Api/Aim.Api.csproj --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Build da API falhou.' }
    & npm.cmd run build
    if ($LASTEXITCODE -ne 0) { throw 'Build da UI falhou.' }
    & npm.cmd run test:e2e
    if ($LASTEXITCODE -ne 0) { throw 'Testes de navegador falharam.' }
} finally { Pop-Location }
