#!/usr/bin/env bash
# Compila el ffmpeg.exe mínimo de Video Lite (Windows x64, estático) con MSYS2 / MINGW64.
#
# Uso (desde la consola MINGW64 de MSYS2, o):
#   E:\msys64\usr\bin\env.exe MSYSTEM=MINGW64 /usr/bin/bash -l /e/Video-Lite/escritorio/ffmpeg-build/compilar-ffmpeg.sh [carpeta-de-trabajo]
#
# Requiere (pacman): mingw-w64-x86_64-gcc mingw-w64-x86_64-nasm mingw-w64-x86_64-pkgconf
#                    mingw-w64-x86_64-dav1d mingw-w64-x86_64-zlib make diffutils git
#
# Resultado: ./salida/ffmpeg.exe, ./salida/COMPILACION.txt y ./salida/fuentes/ (las fuentes exactas usadas).
# Licencia del binario resultante: GPL-2.0-or-later (FFmpeg con --enable-gpl + x264).
set -euo pipefail

AQUI="$(cd "$(dirname "$0")" && pwd)"
TRABAJO="${1:-/e/descargas-build/ffmpeg-trabajo}"
PREFIX="$TRABAJO/prefix"
SALIDA="$AQUI/salida"
JOBS="$(nproc)"

# ---- Versiones fijas (cambiar una exige actualizar su hash) ----
FFMPEG_VER="9.0.2"
FFMPEG_URL="https://ffmpeg.org/releases/ffmpeg-$FFMPEG_VER.tar.xz"
FFMPEG_SHA256="8c3850283eb25fa026482078a04051e0be17347b09ef81a0849bec15a96e002e"   # tarball con firma GPG verificada (llave FCF986EA15E6E293A5644F10B4322F04D67658D8)
X264_COMMIT="b35605ace3ddf7c1a5d67a2eb553f034aef41d55"   # rama stable, 2025-06-08 (el hash del commit verifica el contenido)
X264_GIT="https://code.videolan.org/videolan/x264.git"
# dav1d y zlib se enlazan desde los paquetes de MSYS2; sus fuentes se guardan para el paquete GPL.
DAV1D_VER="1.5.4"
DAV1D_URL="https://downloads.videolan.org/pub/videolan/dav1d/$DAV1D_VER/dav1d-$DAV1D_VER.tar.xz"
DAV1D_SHA256="686616b7c69eb88d44459391ab25cac13b6647a3b288835c5784e71c1514a5c5"   # = .sha256 oficial de VideoLAN
ZLIB_VER="1.3.2"
ZLIB_URL="https://zlib.net/zlib-$ZLIB_VER.tar.gz"
ZLIB_SHA256="bb329a0a2cd0274d05519d61c667c062e06990d72e125ee2dfa8de64f0119d16"    # = hash publicado en zlib.net
# LAME (codificador MP3, LGPL) desde el paquete de MSYS2; su fuente también va al paquete GPL.
LAME_VER="3.100"
LAME_URL="https://downloads.sourceforge.net/project/lame/lame/$LAME_VER/lame-$LAME_VER.tar.gz"
LAME_SHA256="ddfe36cab873794038ae2c1210557ad34857a4b6bdc515785d1da9e175b1da1e"

[ "${MSYSTEM:-}" = "MINGW64" ] || { echo "Corre esto en MSYS2 con MSYSTEM=MINGW64"; exit 1; }
mkdir -p "$TRABAJO" "$PREFIX" "$SALIDA/fuentes"
cd "$TRABAJO"

bajar() { # url archivo sha256
  [ -f "$2" ] || curl -fL --retry 3 -o "$2" "$1"
  local h; h="$(sha256sum "$2" | cut -d' ' -f1)"
  if [ "$3" != "${3#__}" ]; then echo "AVISO: $2 sin hash fijado; su SHA-256 es $h"; else
    [ "$h" = "$3" ] || { echo "ERROR: hash de $2 no coincide ($h)"; exit 1; }; fi
}

bajar "$FFMPEG_URL" "ffmpeg-$FFMPEG_VER.tar.xz" "$FFMPEG_SHA256"
bajar "$DAV1D_URL" "dav1d-$DAV1D_VER.tar.xz" "$DAV1D_SHA256"
bajar "$ZLIB_URL" "zlib-$ZLIB_VER.tar.gz" "$ZLIB_SHA256"
bajar "$LAME_URL" "lame-$LAME_VER.tar.gz" "$LAME_SHA256"
# Las fuentes guardadas deben ser las de las librerías que de verdad se enlazan.
[ "$(pkg-config --modversion dav1d)" = "$DAV1D_VER" ] || { echo "ERROR: el paquete dav1d de MSYS2 no es $DAV1D_VER"; exit 1; }
[ "$(pkg-config --modversion zlib)" = "$ZLIB_VER" ] || { echo "ERROR: el paquete zlib de MSYS2 no es $ZLIB_VER"; exit 1; }
pacman -Q mingw-w64-x86_64-lame | grep -q " $LAME_VER-" || { echo "ERROR: el paquete lame de MSYS2 no es $LAME_VER"; exit 1; }
if [ ! -d x264-git ]; then git clone --quiet "$X264_GIT" x264-git; fi
( cd x264-git && git fetch --quiet origin && git checkout --quiet "$X264_COMMIT" && [ "$(git rev-parse HEAD)" = "$X264_COMMIT" ]   && git archive --format=tar.gz --prefix="x264-$X264_COMMIT/" -o "../x264-$X264_COMMIT.tar.gz" "$X264_COMMIT" )

# ---- x264 (estático, sin CLI) ----
rm -rf "x264-$X264_COMMIT" && tar xzf "x264-$X264_COMMIT.tar.gz"
( cd "x264-$X264_COMMIT"
  ./configure --prefix="$PREFIX" --host=x86_64-w64-mingw32 --enable-static --disable-cli \
              --disable-opencl --enable-strip --extra-ldflags=-static
  make -j"$JOBS" && make install )

# ---- FFmpeg mínimo ----
# Solo lo que usa Video Lite: leer los formatos de video comunes, filtros de escala/velocidad/audio,
# y escribir MP4/MOV/MKV con H.264 (libx264) + AAC, o solo audio en M4A (AAC) o MP3 (LAME).
# Sin red, sin dispositivos, sin autodetección.
DEMUXERS="mov matroska avi mpegts mpegps flv asf ogg mp3 aac wav h264 hevc m4v mpegvideo"
DECODERS="h264 hevc mpeg4 msmpeg4v1 msmpeg4v2 msmpeg4v3 mpeg1video mpeg2video h263 h263p flv vp8 vp9 libdav1d
          mjpeg prores wmv1 wmv2 wmv3 vc1 theora dvvideo rawvideo
          aac aac_latm mp3 mp3float mp2 mp2float ac3 eac3 dca opus vorbis flac alac wmav1 wmav2 wmapro amrnb amrwb
          pcm_s16le pcm_s16be pcm_s24le pcm_s24be pcm_s32le pcm_f32le pcm_u8 pcm_alaw pcm_mulaw adpcm_ima_wav adpcm_ms adpcm_ima_qt"
PARSERS="h264 hevc aac aac_latm mpegaudio mpeg4video mpegvideo vp8 vp9 av1 ac3 dca opus vorbis flac mjpeg vc1 h263"
# (buffer, buffersink, abuffer y abuffersink vienen siempre en FFmpeg 9)
# highpass, lowpass, afftdn, dynaudnorm, loudnorm y acompressor: para «Mejorar voz» (si se aprueba)
FILTERS="scale fps setpts format null transpose hflip vflip rotate crop trim
         atempo pan astats aresample aformat anull atrim highpass lowpass afftdn dynaudnorm loudnorm acompressor"
opts=()
for x in $DEMUXERS; do opts+=(--enable-demuxer="$x"); done
for x in $DECODERS; do opts+=(--enable-decoder="$x"); done
for x in $PARSERS;  do opts+=(--enable-parser="$x"); done
for x in $FILTERS;  do opts+=(--enable-filter="$x"); done

rm -rf "ffmpeg-$FFMPEG_VER" && tar xJf "ffmpeg-$FFMPEG_VER.tar.xz"
( cd "ffmpeg-$FFMPEG_VER"
  PKG_CONFIG_PATH="$PREFIX/lib/pkgconfig:/mingw64/lib/pkgconfig" ./configure \
    --prefix="$PREFIX" --target-os=mingw32 --arch=x86_64 \
    --enable-gpl --enable-libx264 --enable-libdav1d --enable-libmp3lame --enable-zlib \
    --disable-everything --disable-autodetect --enable-w32threads \
    --disable-doc --disable-debug --disable-ffplay --disable-ffprobe --disable-network \
    --enable-protocol=file --enable-protocol=pipe \
    --enable-encoder=libx264 --enable-encoder=aac --enable-encoder=libmp3lame \
    --enable-encoder=pcm_s16le --enable-encoder=wrapped_avframe \
    --enable-muxer=mp4 --enable-muxer=mov --enable-muxer=matroska --enable-muxer=ipod --enable-muxer=mp3 --enable-muxer=null \
    "${opts[@]}" \
    --pkg-config-flags=--static \
    --extra-cflags="-I$PREFIX/include" --extra-ldflags="-L$PREFIX/lib -static" \
    --extra-version=videolite
  make -j"$JOBS" ffmpeg.exe )

cp "ffmpeg-$FFMPEG_VER/ffmpeg.exe" "$SALIDA/ffmpeg.exe"
strip "$SALIDA/ffmpeg.exe"

# ---- Registro de la compilación (para la licencia y para reproducirla) ----
rm -f "$SALIDA"/fuentes/*
cp "ffmpeg-$FFMPEG_VER.tar.xz" "x264-$X264_COMMIT.tar.gz" "dav1d-$DAV1D_VER.tar.xz" "zlib-$ZLIB_VER.tar.gz" "lame-$LAME_VER.tar.gz" "$SALIDA/fuentes/"
# Textos de licencia, sacados de las propias fuentes (el zip de la app los lleva en LICENCIAS\)
mkdir -p "$SALIDA/licencias" && rm -f "$SALIDA"/licencias/*
cp "ffmpeg-$FFMPEG_VER/COPYING.GPLv2" "$SALIDA/licencias/ffmpeg-COPYING.GPLv2.txt"
cp "ffmpeg-$FFMPEG_VER/LICENSE.md" "$SALIDA/licencias/ffmpeg-LICENSE.md"
cp "x264-$X264_COMMIT/COPYING" "$SALIDA/licencias/x264-COPYING.txt"
tar -xOf "dav1d-$DAV1D_VER.tar.xz" "dav1d-$DAV1D_VER/COPYING" > "$SALIDA/licencias/dav1d-COPYING.txt"
tar -xOzf "zlib-$ZLIB_VER.tar.gz" "zlib-$ZLIB_VER/LICENSE" > "$SALIDA/licencias/zlib-LICENSE.txt"
tar -xOzf "lame-$LAME_VER.tar.gz" "lame-$LAME_VER/COPYING" > "$SALIDA/licencias/lame-COPYING.txt"
{
  echo "ffmpeg.exe de Video Lite — compilado $(date -u +%Y-%m-%dT%H:%MZ)"
  echo "SHA-256: $(sha256sum "$SALIDA/ffmpeg.exe" | cut -d' ' -f1)"
  echo
  echo "FFmpeg $FFMPEG_VER  sha256 $(sha256sum "ffmpeg-$FFMPEG_VER.tar.xz" | cut -d' ' -f1)  $FFMPEG_URL"
  echo "x264 commit $X264_COMMIT  ($X264_GIT; tarball generado con git archive: x264-$X264_COMMIT.tar.gz)"
  echo "dav1d $(pkg-config --modversion dav1d) (paquete MSYS2 $(pacman -Q mingw-w64-x86_64-dav1d))"
  echo "zlib $(pkg-config --modversion zlib) (paquete MSYS2 $(pacman -Q mingw-w64-x86_64-zlib))"
  echo "LAME $LAME_VER (paquete MSYS2 $(pacman -Q mingw-w64-x86_64-lame)); fuente lame-$LAME_VER.tar.gz sha256 $LAME_SHA256"
  echo "Compilador: $(gcc --version | head -1)"
  echo "Runtime: $(pacman -Q mingw-w64-x86_64-crt mingw-w64-x86_64-winpthreads | tr '\n' ' ')"
  echo
  echo "Configuración (ffmpeg -buildconf):"
  "$SALIDA/ffmpeg.exe" -hide_banner -buildconf
} > "$SALIDA/COMPILACION.txt"
cat "$SALIDA/COMPILACION.txt" | head -12
ls -la "$SALIDA/ffmpeg.exe"
