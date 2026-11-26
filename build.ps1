<#
  PC CLEANER — Windows derleme betiği
  Kullanım (PowerShell):   .\build.ps1
  Çıktılar:  artifacts\portable\PCCleaner.exe
             artifacts\PCCleaner_Setup_1.0.0.exe   (NSIS kuruluysa)
#>
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

Write-Host "1/4  Derleniyor..." -ForegroundColor Cyan
dotnet build PcCleaner.sln -c Release

Write-Host "2/4  Testler çalıştırılıyor..." -ForegroundColor Cyan
dotnet test PcCleaner.sln -c Release --no-build

Write-Host "3/4  Taşınabilir EXE üretiliyor..." -ForegroundColor Cyan
dotnet publish src/PcCleaner.App/PcCleaner.App.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true -p:PublishReadyToRun=true -p:DebugType=none `
  -o artifacts/portable

Write-Host "4/4  Kurulum dosyası (Setup.exe)..." -ForegroundColor Cyan
$makensis = Get-Command makensis -ErrorAction SilentlyContinue
if (-not $makensis) {
  $default = "${env:ProgramFiles(x86)}\NSIS\makensis.exe"
  if (Test-Path $default) { $makensis = $default }
}
if ($makensis) {
  & $makensis installer\PcCleaner.nsi
  Write-Host "Tamam: artifacts klasörüne bakın." -ForegroundColor Green
} else {
  Write-Warning "NSIS bulunamadı, Setup.exe atlandı. (https://nsis.sourceforge.io)"
}
