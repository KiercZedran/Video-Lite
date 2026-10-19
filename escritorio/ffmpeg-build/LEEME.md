# ffmpeg.exe de Video Lite: compilación mínima (código abierto, GPL)

Video Lite para Windows usa un `ffmpeg.exe` propio, compilado solo con lo que la app necesita: leer los formatos de video comunes, cambiar escala, velocidad, giro y audio, y escribir MP4/MOV/MKV con H.264 + AAC, o solo el audio en MP3 o M4A. Esta carpeta trae **la receta completa y reproducible** de esa compilación.

- **Licencia de `ffmpeg.exe`:** GPL-2.0-or-later. Es FFmpeg compilado con `--enable-gpl` y enlazado con x264 (GPL-2.0-or-later), LAME (LGPL-2.0-or-later), dav1d (BSD-2-Clause) y zlib (licencia zlib).
- **La app Video Lite (`VideoLite.exe`) es de código cerrado** y es un programa aparte: solo ejecuta `ffmpeg.exe` como proceso independiente, por línea de comandos, y no enlaza ningún código de FFmpeg. La licencia de Video Lite no limita ningún derecho que la GPL te da sobre `ffmpeg.exe`: puedes copiarlo, estudiarlo, modificarlo y redistribuirlo bajo la GPL.

## Qué incluye

| Componente | Versión | Origen | Verificación |
|---|---|---|---|
| FFmpeg | 9.0.2 | https://ffmpeg.org/releases/ffmpeg-9.0.2.tar.xz | Firma GPG verificada con la llave de versiones de FFmpeg `FCF9 86EA 15E6 E293 A564 4F10 B432 2F04 D676 58D8`; SHA-256 fijo en el script |
| x264 | commit `b35605ace3ddf7c1a5d67a2eb553f034aef41d55` (rama stable) | https://code.videolan.org/videolan/x264.git | El hash del commit verifica el contenido |
| dav1d | 1.5.4 | paquete MSYS2 `mingw-w64-x86_64-dav1d` | Firmado por MSYS2 |
| zlib | 1.3.2 | paquete MSYS2 `mingw-w64-x86_64-zlib` | Firmado por MSYS2 |
| LAME | 3.100 | paquete MSYS2 `mingw-w64-x86_64-lame`; fuente de https://downloads.sourceforge.net/project/lame/lame/3.100/lame-3.100.tar.gz | Firmado por MSYS2; la fuente, con SHA-256 fijo en el script, y el script comprueba que el paquete sea la misma versión |

Las versiones exactas del compilador y del runtime de cada compilación quedan en `salida/COMPILACION.txt`, junto con la salida de `ffmpeg -buildconf`.

## Cómo reproducirla

1. Instala MSYS2 (https://www.msys2.org) y, en la consola **MINGW64**:
   ```
   pacman -S --needed mingw-w64-x86_64-gcc mingw-w64-x86_64-nasm mingw-w64-x86_64-pkgconf mingw-w64-x86_64-dav1d mingw-w64-x86_64-zlib mingw-w64-x86_64-lame make diffutils git
   ```
2. Corre `compilar-ffmpeg.sh [carpeta-de-trabajo]`. Descarga y verifica las fuentes, compila x264 y luego FFmpeg, y deja `salida/ffmpeg.exe` más una copia de las fuentes usadas en `salida/fuentes/`.

## Qué tiene y qué no

- **Lee:** MP4/MOV/M4V/3GP, MKV/WEBM, AVI, MPEG-TS/MTS/M2TS, MPG/VOB, FLV, WMV/ASF, OGG.
- **Decodifica video:** H.264, HEVC/H.265, AV1 (dav1d), VP8, VP9, MPEG-4 Part 2 (Xvid/DivX), MPEG-1/2, H.263, Sorenson/FLV1, MJPEG, ProRes, WMV1/2/3, VC-1, Theora, DV.
- **Decodifica audio:** AAC, MP3, MP2, AC-3/E-AC-3, DTS, Opus, Vorbis, FLAC, ALAC, WMA, AMR-NB/WB, PCM y ADPCM comunes.
- **Codifica:** H.264 (libx264), AAC y MP3 (LAME). **Escribe:** MP4, MOV, MKV, M4A (`ipod`) y MP3. Además trae dos codificadores internos para la salida "null" (`pcm_s16le` y `wrapped_avframe`), que se usan para medir el audio y para la primera pasada; sin ellos, la medición de fase falla. Para el editor, la salida cruda `rawvideo` (las miniaturas de la línea de tiempo) y `s16le` (la forma de onda; en `configure` se llama `pcm_s16le`).
- **Filtros:** solo los que usa la app (escala, cuadros por segundo, velocidad, giro y volteo, medición y mezcla de audio), `highpass`, `lowpass`, `afftdn`, `dynaudnorm`, `loudnorm` y `acompressor` para "Mejorar voz", y `split`, `asplit`, `concat`, `asetpts`, `apad`, `adelay` y `volume` para aplicar las ediciones del editor.
- **Textos de licencia:** el script los deja en `salida/licencias/`, sacados de las propias fuentes.
- **No tiene:** red, dispositivos de captura, aceleración por hardware ni `ffprobe` o `ffplay`.
