# Arma los dos archivos que se publican juntos, en el mismo lugar de descarga:
#   dist\VideoLite-<ver>-win64.zip           la app + ffmpeg.exe + LICENSE.txt (GPL-3.0) + LEEME/README + LICENCIAS
#   dist\VideoLite-<ver>-ffmpeg-fuentes.zip  el código fuente completo de ffmpeg.exe (GPL) y su receta
# Uso: powershell -ExecutionPolicy Bypass -File escritorio\empaquetar.ps1
$ErrorActionPreference = "Stop"
$ver = "0.0.2.6"
$aqui = Split-Path -Parent $MyInvocation.MyCommand.Path
$dist = Join-Path $aqui "dist"
$fb = Join-Path $aqui "ffmpeg-build"
$sal = Join-Path $fb "salida"
foreach ($f in @("$dist\VideoLite.exe", "$sal\ffmpeg.exe", "$sal\COMPILACION.txt")) { if (-not (Test-Path $f)) { throw "Falta $f (compila primero)" } }
$fuentes = Get-ChildItem "$sal\fuentes" -File
foreach ($p in @("ffmpeg-*.tar.xz", "x264-*.tar.gz", "dav1d-*.tar.xz", "zlib-*.tar.gz", "lame-*.tar.gz")) { if (-not ($fuentes | Where-Object Name -like $p)) { throw "Falta la fuente $p en $sal\fuentes" } }

$tmp = Join-Path $env:TEMP ("videolite-paquete-" + [guid]::NewGuid().ToString("N"))
$app = Join-Path $tmp "VideoLite-$ver-win64"
$lic = Join-Path $app "LICENCIAS"
New-Item -ItemType Directory -Force $lic | Out-Null

Copy-Item "$dist\VideoLite.exe", "$sal\ffmpeg.exe" $app
# La licencia de la app (GPL-3.0) es la del repositorio: LICENSE en la raíz del proyecto.
Copy-Item (Join-Path (Split-Path $aqui -Parent) "LICENSE") "$app\LICENSE.txt"
Copy-Item "$sal\COMPILACION.txt" "$lic\ffmpeg-COMPILACION.txt"

# Textos de licencia: los deja compilar-ffmpeg.sh en salida\licencias, sacados de las propias fuentes.
# (No se extraen aquí: el tar.exe de Windows se cuelga con .tar.xz y el GNU tar de Git toma «E:» como servidor remoto.)
$lics = Get-ChildItem "$sal\licencias" -File
foreach ($n in @("ffmpeg-COPYING.GPLv2.txt", "ffmpeg-LICENSE.md", "x264-COPYING.txt", "dav1d-COPYING.txt", "zlib-LICENSE.txt", "lame-COPYING.txt")) {
    if (-not ($lics | Where-Object Name -eq $n)) { throw "Falta la licencia $n en $sal\licencias" }
}
$lics | Copy-Item -Destination $lic

@"
CÓDIGO FUENTE DE ffmpeg.exe (GPL-2.0-or-later)

ffmpeg.exe es FFmpeg compilado por Zedran Labs con x264, LAME, dav1d y zlib. Su código fuente
completo (las fuentes exactas de FFmpeg, x264, LAME, dav1d y zlib, más el script que las compila)
se publica junto a esta descarga, en el mismo lugar, con el nombre:

    VideoLite-$ver-ffmpeg-fuentes.zip

Detalle de versiones, hashes y configuración: ffmpeg-COMPILACION.txt (en esta carpeta).
Licencias: ffmpeg-COPYING.GPLv2.txt, ffmpeg-LICENSE.md, x264-COPYING.txt, lame-COPYING.txt,
dav1d-COPYING.txt, zlib-LICENSE.txt.

Video Lite (VideoLite.exe) es un programa aparte, de código abierto bajo la GPL-3.0 (LICENSE.txt;
código en https://github.com/KiercZedran/Video-Lite), que ejecuta ffmpeg.exe como proceso independiente.

---

SOURCE CODE OF ffmpeg.exe (GPL-2.0-or-later)

ffmpeg.exe is FFmpeg built by Zedran Labs with x264, LAME, dav1d and zlib. Its complete source
code (the exact sources of FFmpeg, x264, LAME, dav1d and zlib, plus the script that builds them)
is published next to this download, in the same place, as:

    VideoLite-$ver-ffmpeg-fuentes.zip

Versions, hashes and configuration: ffmpeg-COMPILACION.txt (in this folder).
Video Lite (VideoLite.exe) is a separate, open-source program under the GPL-3.0 (LICENSE.txt;
code at https://github.com/KiercZedran/Video-Lite) that runs ffmpeg.exe as an independent process.
"@ | Set-Content -Encoding utf8 "$lic\ffmpeg-CODIGO-FUENTE.txt"

@"
VIDEO LITE · BY ZEDRAN LABS — v$ver para Windows 10/11 (64 bits)

1. Descomprime esta carpeta donde quieras. No se instala.
2. Abre VideoLite.exe. Arrastra tus videos, elige el tamaño final y da clic en Procesar.
   Los comprimidos se guardan en una carpeta «Video Lite» junto a cada original.
3. Opcional: «Agregar a «Enviar a»», en el pie de la ventana, te deja comprimir con clic
   derecho en un video → Enviar a → Video Lite.

Si Windows muestra «Windows protegió su PC»: es porque la app todavía no tiene firma de
desarrollador. Da clic en «Más información» y luego en «Ejecutar de todas formas».

ffmpeg.exe debe quedarse en la misma carpeta que VideoLite.exe.
Tus videos no salen de tu computadora y la app no se conecta a internet.

Para quitarla: si usaste «Agregar a «Enviar a»», quítalo primero con el mismo enlace; luego borra
esta carpeta y, si quieres, %LOCALAPPDATA%\ZedranLabs\VideoLite (solo guarda el tema y el idioma).

Gratis y de código abierto (GPL-3.0, LICENSE.txt): https://github.com/KiercZedran/Video-Lite
Página: https://videolite.zedran.xyz
Reportes: https://discord.com/invite/2ZRbaYDAnR · Kierc117@hotmail.com
"@ | Set-Content -Encoding utf8 "$app\LEEME.txt"

@"
VIDEO LITE · BY ZEDRAN LABS — v$ver for Windows 10/11 (64-bit)

1. Unzip this folder wherever you like. Nothing gets installed.
2. Open VideoLite.exe (the "English" button switches the language). Drag your videos in,
   choose the final size and click Process. The compressed files are saved in a
   "Video Lite" folder next to each original.
3. Optional: "Add to Send to", in the window footer, lets you right-click a video →
   Send to → Video Lite.

If Windows shows "Windows protected your PC": the app doesn't have a developer signature yet.
Click "More info" and then "Run anyway".

ffmpeg.exe must stay in the same folder as VideoLite.exe.
Your videos never leave your computer, and the app never connects to the internet.

To remove it: if you used "Add to Send to", remove it first with the same link; then delete this
folder and, if you like, %LOCALAPPDATA%\ZedranLabs\VideoLite (it only stores the theme and language).

Free and open source (GPL-3.0, LICENSE.txt): https://github.com/KiercZedran/Video-Lite
Website: https://videolite.zedran.xyz/en/
Bug reports: https://discord.com/invite/2ZRbaYDAnR · Kierc117@hotmail.com
"@ | Set-Content -Encoding utf8 "$app\README.txt"

# Paquete de fuentes
$src = Join-Path $tmp "VideoLite-$ver-ffmpeg-fuentes"
New-Item -ItemType Directory -Force $src | Out-Null
$fuentes | Copy-Item -Destination $src
Copy-Item "$fb\compilar-ffmpeg.sh", "$fb\LEEME.md", "$sal\COMPILACION.txt" $src

$zipApp = Join-Path $dist "VideoLite-$ver-win64.zip"
$zipSrc = Join-Path $dist "VideoLite-$ver-ffmpeg-fuentes.zip"
Remove-Item $zipApp, $zipSrc -ErrorAction SilentlyContinue

# Compress-Archive de PowerShell 5.1 guarda las rutas con «\», que en Linux y Mac salen como nombres rotos.
# Se escribe el zip con .NET y rutas con «/», como manda el formato ZIP.
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
function Nuevo-Zip($carpeta, $zip) {
    $base = Split-Path $carpeta -Parent
    $z = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        Get-ChildItem $carpeta -Recurse -File | Sort-Object FullName | ForEach-Object {
            $rel = $_.FullName.Substring($base.Length + 1).Replace('\', '/')
            [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($z, $_.FullName, $rel, [System.IO.Compression.CompressionLevel]::Optimal)
        }
    } finally { $z.Dispose() }
}
Nuevo-Zip $app $zipApp
Nuevo-Zip $src $zipSrc
Remove-Item -Recurse -Force $tmp

foreach ($z in @($zipApp, $zipSrc)) {
    "{0}  {1:N1} MB  SHA-256 {2}" -f (Split-Path $z -Leaf), ((Get-Item $z).Length / 1MB), (Get-FileHash $z -Algorithm SHA256).Hash
}
