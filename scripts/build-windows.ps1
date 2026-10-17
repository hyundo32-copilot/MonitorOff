#!/usr/bin/env pwsh
# scripts/build-windows.ps1 — 맥용 scripts/make-app.sh 에 대응한다.
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")
dotnet publish Windows/MonitorOff.Windows -c Release -r win-x64 --self-contained true -o build/windows
Write-Host "Built build/windows/MonitorOff.exe"
