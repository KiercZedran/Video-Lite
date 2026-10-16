# Video Lite · by Zedran Labs

**Compresor y editor de video para Windows.** Elige cuántos MB debe pesar el video y sale exacto. Gratis, sin anuncios, sin instalar y sin internet.

*[English below](#english)*

## Descargar

Desde la página oficial: **[videolite.zedran.xyz](https://videolite.zedran.xyz/#descarga)**. El SHA-256 de cada zip está ahí mismo. También hay una versión que funciona en el navegador, sin descargar nada.

Requisitos: Windows de 64 bits. Está probado en Windows 10 Pro.

## Qué hace

- **Tamaño exacto:** eliges los MB y el resultado no se pasa. Si un video difícil se pasa, la segunda pasada se repite sola, y si aun así no cabe, la app lo dice.
- **Destinos con su límite:** Discord, Discord Nitro, WhatsApp (720p) y correo.
- **Arregla el audio mudo:** los videos que se oyen en audífonos pero no en bocinas ni laptops, porque tienen los canales en fase invertida.
- **«Mejorar voz»:** quita el ruido de fondo y empareja el volumen de la voz.
- **Ajustes:**
  - la resolución máxima;
  - la velocidad, de ×0.5 a ×2, sin cambiar el tono de la voz;
  - girar y voltear;
  - quitar el audio, o sacar solo el audio en MP3 o M4A.
- **Formatos de salida:** MP4, MOV o MKV, siempre con H.264 + AAC, que abre en cualquier equipo.
- **Muchos videos a la vez:** se pueden arrastrar archivos o carpetas, o usar clic derecho → Enviar a → Video Lite.
- **Interfaz:** en español e inglés, con tema claro u oscuro.

## Privacidad

La app no se conecta a internet y tus videos no salen de tu computadora. Solo escribe en estos lugares:
- los videos procesados, en una carpeta «Video Lite» junto a cada original;
- `%LOCALAPPDATA%\ZedranLabs\VideoLite\ajustes.txt`, con el tema y el idioma;
- el acceso de «Enviar a», solo si lo pides con el enlace del pie;
- `%TEMP%\VideoLite\`, con archivos temporales que se borran al terminar cada video.

Política de privacidad: [videolite.zedran.xyz/privacidad](https://videolite.zedran.xyz/privacidad/).

## Quitar la app

1. Si usaste «Agregar a «Enviar a»», quítalo con el mismo enlace del pie de la ventana.
2. Borra la carpeta de la app.
3. Si quieres, borra también `%LOCALAPPDATA%\ZedranLabs\VideoLite`.

## Compilar desde el código

- **La app:** `escritorio\compilar.ps1` la compila con el `csc` de .NET Framework 4.8, que ya viene en Windows 10 y 11, así que no hay que instalar nada. El resultado queda en `escritorio\dist\VideoLite.exe`.

  ```powershell
  powershell -ExecutionPolicy Bypass -File escritorio\compilar.ps1
  ```

- **`ffmpeg.exe`:** la app lo necesita junto a `VideoLite.exe`. El que se publica es un FFmpeg mínimo, compilado con [`escritorio/ffmpeg-build/compilar-ffmpeg.sh`](escritorio/ffmpeg-build/compilar-ffmpeg.sh) en MSYS2; los pasos están en [`LEEME.md`](escritorio/ffmpeg-build/LEEME.md).
- **El paquete:** `escritorio\empaquetar.ps1` arma el zip de la app y el zip con el código fuente de ffmpeg, que siempre se publican juntos.
- **Compilación automática:** cada cambio se compila en GitHub Actions ([`.github/workflows/compilar.yml`](.github/workflows/compilar.yml)).

## Licencia

- **Video Lite:** [GPL-3.0](LICENSE). Quien modifique el código y distribuya su versión tiene que publicarla con la misma licencia.
- **`ffmpeg.exe`:** es [FFmpeg](https://ffmpeg.org), software libre bajo la GPL versión 2 o posterior, compilado con x264, LAME, dav1d y zlib. Su código fuente completo se publica junto a cada versión (`VideoLite-<versión>-ffmpeg-fuentes.zip`).
- **Nombre y logo:** «Video Lite», «Zedran Labs» y sus logotipos no están cubiertos por la licencia. Una versión modificada debe usar otro nombre.
- FFmpeg es una marca de Fabrice Bellard; Video Lite no está afiliado al proyecto FFmpeg.

## Política de firma de código

Por ahora las versiones no llevan firma digital, así que Windows puede mostrar «Windows protegió su PC». Se está solicitando firma gratuita para proyectos de código abierto. Cuando exista, cada versión firmada saldrá de la compilación automática de este repositorio.

- **Autor y aprobador de cada versión:** [KiercZedran](https://github.com/KiercZedran) (Erick Hernández · Zedran Labs).
- **Privacidad:** el programa no envía información a ningún sistema en red.

## Contacto

[Discord](https://discord.com/invite/2ZRbaYDAnR) · Kierc117@hotmail.com · Cambios por versión: [CHANGELOG.md](CHANGELOG.md)

---

## English

**Video compressor and editor for Windows.** Choose how many MB the video should weigh and it comes out exact. Free, no ads, no install and no internet needed.

- **Download:** from the official page, **[videolite.zedran.xyz/en](https://videolite.zedran.xyz/en/#descarga)**, which lists each zip's SHA-256. There is also an in-browser version that needs no download.
- **Requirements:** 64-bit Windows, tested on Windows 10 Pro.
- **Features:**
  - exact size, with destinations for Discord, WhatsApp and email;
  - fixes silent audio (phase-inverted channels);
  - «Enhance voice»;
  - resolution, speed from ×0.5 to ×2, and rotate;
  - no audio, or MP3/M4A audio only;
  - MP4, MOV or MKV output;
  - many videos at once, and right-click → Send to;
  - Spanish and English, with light and dark themes.
- **Privacy:** the app never connects to the internet, and your videos never leave your computer. It only writes the processed files (a «Video Lite» folder next to each original), `%LOCALAPPDATA%\ZedranLabs\VideoLite\ajustes.txt`, the Send to shortcut if you ask for it, and temporary files in `%TEMP%\VideoLite\`. Privacy policy: [videolite.zedran.xyz/en/privacy](https://videolite.zedran.xyz/en/privacy/).
- **To remove it:**
  1. Remove the Send to shortcut with the same footer link, if you added it.
  2. Delete the app folder.
  3. Optionally, delete `%LOCALAPPDATA%\ZedranLabs\VideoLite`.
- **Build:**
  - `escritorio\compilar.ps1` uses the .NET Framework 4.8 `csc` that ships with Windows.
  - `ffmpeg.exe` is built with `escritorio/ffmpeg-build/compilar-ffmpeg.sh` in MSYS2.
  - `escritorio\empaquetar.ps1` builds the release zips.
- **License:**
  - Video Lite is [GPL-3.0](LICENSE).
  - `ffmpeg.exe` is FFmpeg (GPL-2.0-or-later); its full source code ships with every release.
  - «Video Lite», «Zedran Labs» and their logos are not covered by the license: a modified version must use another name.
- **Code signing policy:** releases are not digitally signed yet; free signing for open-source projects is being requested. Author and release approver: [KiercZedran](https://github.com/KiercZedran). The program sends no information to any networked system.
- **Contact:** [Discord](https://discord.com/invite/2ZRbaYDAnR) · Kierc117@hotmail.com
