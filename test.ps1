# Script para rodar testes localmente com facilidade
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "   Executando Testes Unitarios e Regras   " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

$env:DOTNET_CLI_HOME = "$PSScriptRoot\.dotnet_temp"

dotnet test tests/Tournaments.UnitTests/Tournaments.UnitTests.csproj --verbosity normal -nodereuse:false /p:UseSharedCompilation=false

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n[SUCESSO] Todos os testes passaram com exito!" -ForegroundColor Green
} else {
    Write-Host "`n[FALHA] Alguns testes falharam. Verifique os logs acima." -ForegroundColor Red
}
