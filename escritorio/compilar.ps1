# Compila Video Lite para Windows con el csc de .NET Framework 4.8 (viene con Windows 10/11; no hay que instalar nada).
# Uso: powershell -ExecutionPolicy Bypass -File escritorio\compilar.ps1
$ErrorActionPreference = "Stop"
$aqui = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$dist = Join-Path $aqui "dist"
New-Item -ItemType Directory -Force $dist | Out-Null
$exe = Join-Path $dist "VideoLite.exe"

& $csc /nologo /target:winexe /optimize+ /platform:x64 /codepage:65001 `
    "/out:$exe" "/win32icon:$aqui\videolite.ico" "/win32manifest:$aqui\src\app.manifest" `
    /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll `
    "$aqui\src\Textos.cs" "$aqui\src\Motor.cs" "$aqui\src\Ventana.cs"
if ($LASTEXITCODE -ne 0) { throw "csc falló ($LASTEXITCODE)" }

$h = (Get-FileHash $exe -Algorithm SHA256).Hash
"OK $exe  $([math]::Round((Get-Item $exe).Length / 1KB)) KB  SHA-256 $h"
