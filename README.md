# Video Lite - by Zedran Labs

Compresor y editor de video para Windows. Le dices cuántos MB debe pesar el video y sale de ese tamaño, sin pasarse. Es gratis, no tiene anuncios, no se instala y funciona sin internet.

*[English below](#english)*

## Descargar

Bájalo de la página oficial, [videolite.zedran.xyz](https://videolite.zedran.xyz/#descarga), o directo de la [última versión en GitHub](https://github.com/KiercZedran/Video-Lite/releases/latest) (`VideoLite-win64.zip`). En las notas de cada versión están el SHA-256 y el análisis de VirusTotal.

Si no quieres instalar nada, la página también tiene una versión que corre en el navegador.

Necesitas Windows de 64 bits. Lo probé en Windows 10 Pro.

## Qué hace

- Un editor para un video: recorta el inicio y el fin, quita tramos, silencia el audio por tramos, sube o baja el volumen y mueve el audio si no coincide con los labios. Se activa con la casilla "Activar editor" y tiene vista previa con sonido.
- Eliges los MB y el resultado no se pasa. Si un video difícil se pasa, la app repite sola la segunda pasada, y si aun así no cabe, te lo dice.
- Trae destinos con su límite: Discord, Discord Nitro, WhatsApp (720p) y correo.
- Arregla el audio mudo de los videos que se oyen en audífonos pero no en bocinas ni laptops. Pasa cuando los canales tienen la fase invertida.
- "Mejorar voz" quita el ruido de fondo y empareja el volumen de la voz.
- Puedes bajar la resolución, cambiar la velocidad de ×0.5 a ×2 sin que la voz cambie de tono, girar o voltear el video, quitarle el audio o sacar solo el audio en MP3 o M4A.
- Guarda en MP4, MOV o MKV, siempre con video H.264 y audio AAC, que abre casi cualquier equipo.
- Procesa muchos videos a la vez: arrastra archivos o carpetas, o usa clic derecho → Enviar a → Video Lite.
- Está en español e inglés, con tema claro u oscuro.

## Privacidad

La app no se conecta a internet y tus videos no salen de tu computadora. Solo escribe en estos lugares:

- los videos procesados, en una carpeta "Video Lite" junto a cada original;
- `%LOCALAPPDATA%\ZedranLabs\VideoLite\ajustes.txt`, con el tema y el idioma;
- el acceso de "Enviar a", solo si lo pides con el enlace del pie de la ventana;
- `%TEMP%\VideoLite\`, con archivos temporales que borra al terminar cada video.

Política de privacidad: [videolite.zedran.xyz/privacidad](https://videolite.zedran.xyz/privacidad/).

## Quitar la app

1. Si usaste "Agregar a Enviar a", quítalo con el mismo enlace del pie de la ventana.
2. Borra la carpeta de la app.
3. Si quieres, borra también `%LOCALAPPDATA%\ZedranLabs\VideoLite`.

## Compilar desde el código

`escritorio\compilar.ps1` compila la app con el `csc` de .NET Framework 4.8, que ya viene en Windows 10 y 11, así que no hay que instalar nada. El resultado queda en `escritorio\dist\VideoLite.exe`.

```powershell
powershell -ExecutionPolicy Bypass -File escritorio\compilar.ps1
```

La app necesita `ffmpeg.exe` junto a `VideoLite.exe`. El que publico es un FFmpeg mínimo que compilo con [`escritorio/ffmpeg-build/compilar-ffmpeg.sh`](escritorio/ffmpeg-build/compilar-ffmpeg.sh) en MSYS2; los pasos están en [`LEEME.md`](escritorio/ffmpeg-build/LEEME.md).

`escritorio\empaquetar.ps1` arma los dos zips de cada versión: el de la app y el del código fuente de ffmpeg, que siempre van juntos. Cada cambio en el repositorio se compila en GitHub Actions ([`.github/workflows/compilar.yml`](.github/workflows/compilar.yml)).

## Licencia

Video Lite usa la licencia [GPL-3.0](LICENSE): si modificas el código y distribuyes tu versión, tienes que publicarla con la misma licencia.

`ffmpeg.exe` es [FFmpeg](https://ffmpeg.org), software libre bajo la GPL versión 2 o posterior, compilado con x264, LAME, dav1d y zlib. Su código fuente completo va en cada versión, junto a la app (`VideoLite-ffmpeg-fuentes.zip`).

La licencia no cubre los nombres Video Lite y Zedran Labs ni sus logotipos: una versión modificada debe usar otro nombre. FFmpeg es una marca de Fabrice Bellard, y Video Lite no está afiliado al proyecto FFmpeg.

## Política de firma de código

Por ahora las versiones no llevan firma digital, así que Windows puede mostrar el aviso "Windows protegió su PC". Estoy pidiendo la firma gratuita para proyectos de código abierto. Cuando la tenga, cada versión firmada saldrá de la compilación automática de este repositorio.

- Autor y aprobador de cada versión: [KiercZedran](https://github.com/KiercZedran) (Zedran Labs).
- Privacidad: el programa no envía información a ningún sistema en red.

## Contacto

[Discord](https://discord.com/invite/2ZRbaYDAnR) - Kierc117@hotmail.com - Cambios por versión: [CHANGELOG.md](CHANGELOG.md)

---

## English

A video compressor and editor for Windows. Tell it how many MB the video should weigh and that's what you get, not a byte over. Free, no ads, nothing to install, and it works offline.

### Download

Grab it from the official page, [videolite.zedran.xyz/en](https://videolite.zedran.xyz/en/#descarga), or straight from the [latest release](https://github.com/KiercZedran/Video-Lite/releases/latest) (`VideoLite-win64.zip`). The SHA-256 and the VirusTotal analysis are in each release's notes.

Don't want to install anything? The page has an in-browser version too.

It needs 64-bit Windows. I've tested it on Windows 10 Pro.

### What it does

- An editor for one video at a time: trim the start and end, cut out parts, mute sections, change the volume and shift the audio when it doesn't match the lips. Check "Turn on the editor"; the preview plays with sound.
- Pick the MB and it won't go over. When a hard video does, the app redoes the second pass by itself, and if it still won't fit, it says so.
- Destinations with their limits built in: Discord, Discord Nitro, WhatsApp (720p) and email.
- Fixes silent audio, the kind you hear on headphones but not on speakers or laptops because the channels are phase-inverted.
- "Enhance voice" cuts background noise and evens out voices.
- Lower the resolution, change the speed from ×0.5 to ×2 (voices keep their pitch), rotate or flip, drop the audio, or keep only the audio as MP3 or M4A.
- Saves MP4, MOV or MKV, always H.264 video with AAC audio, which plays on almost anything.
- Many videos at once: drag in files or whole folders, or right-click → Send to → Video Lite.
- Spanish and English, light and dark themes.

### Privacy

Nothing goes online. The app never connects to the internet and your videos stay on your computer. The only things it writes are the processed files (in a "Video Lite" folder next to each original), `%LOCALAPPDATA%\ZedranLabs\VideoLitejustes.txt` with your theme and language, the "Send to" shortcut if you ask for it, and temporary files in `%TEMP%\VideoLite\`, which it deletes after each video.

Privacy policy: [videolite.zedran.xyz/en/privacy](https://videolite.zedran.xyz/en/privacy/).

### Removing it

1. Added the "Send to" shortcut? Remove it first with the same footer link.
2. Delete the app folder.
3. For a clean slate, delete `%LOCALAPPDATA%\ZedranLabs\VideoLite` as well.

### Building it

`escritorio\compilar.ps1` builds the app with the .NET Framework 4.8 `csc` that already ships with Windows, so there's nothing to install. `ffmpeg.exe` is built with `escritorio/ffmpeg-build/compilar-ffmpeg.sh` in MSYS2, and `escritorio\empaquetar.ps1` puts the release zips together.

### License

GPL-3.0, see [LICENSE](LICENSE). The bundled `ffmpeg.exe` is FFmpeg under GPL-2.0-or-later, and every release ships its full source code. The license doesn't cover the Video Lite and Zedran Labs names or logos, so a modified version needs a different name.

### Code signing policy

Releases aren't signed yet, which is why Windows may warn you. I'm applying for free code signing for open-source projects; once it's approved, every signed release will come from this repository's automatic build.

- Author and release approver: [KiercZedran](https://github.com/KiercZedran) (Zedran Labs).
- Privacy: the program sends no information to any networked system.

### Contact

[Discord](https://discord.com/invite/2ZRbaYDAnR) - Kierc117@hotmail.com - Changes per release: [CHANGELOG.md](CHANGELOG.md)
