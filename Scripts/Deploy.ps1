$ErrorActionPreference = "Stop"

& (Join-Path $PSScriptRoot "..\deploy.ps1") @args
