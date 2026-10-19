# Roda os testes com as DLLs nativas do Revit visiveis (so vale para esta execucao)
$revit = "C:\Program Files\Autodesk\Revit 2021"

if (-not (Test-Path "$revit\RevitAPI.dll")) {
    Write-Host "RevitAPI.dll nao encontrado em $revit. Ajuste o caminho no arquivo." -ForegroundColor Red
    exit 1
}

$env:PATH = "$revit;" + $env:PATH
dotnet test