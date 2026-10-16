# Changelog — Video Lite · by Zedran Labs

Todos los cambios de la web ([videolite.zedran.xyz](https://videolite.zedran.xyz)) y de la app para Windows, de la versión más nueva a la más vieja.

- **Qué se anota:** cada versión publicada dice qué cambió **desde la versión publicada anterior**, en cuatro apartados: novedades, cambios, correcciones y lo que se eliminó. Lo que se arregló antes de publicarse no aparece como corrección, porque nadie lo llegó a usar.
- **Compilaciones internas:** las que nunca se publicaron están en la tabla del final.
- **Numeración:** la **0.0.2.5** es posterior a la **0.1**, aunque el número sea menor. Erick decidió numerar así la versión de arreglos (2026-10-14).

---

## Sin publicar

Será la **0.0.2.6**: lo que cambió desde la 0.0.2.5. Al publicar, esta sección recibe su fecha y los datos de los zips.

### Cambios

- **La app de Windows pasa a ser de código abierto,** con licencia GPL-3.0. El código está en [github.com/KiercZedran/Video-Lite](https://github.com/KiercZedran/Video-Lite). Quien lo modifique y lo distribuya tiene que publicar su código con la misma licencia.
- **La página** dice «Gratis y de código abierto» y enlaza el código.
- **La descarga** apunta siempre a la última versión publicada en GitHub (`VideoLite-win64.zip`), así que el enlace no cambia con cada versión. El número, el SHA-256 y el análisis de VirusTotal de cada versión están en sus notas en GitHub.
- **El texto de «Licencias»** de la app dice la licencia y dónde está el código.
- **El `LEEME.txt` y el `README.txt` del zip** explican cómo quitar la app y aclaran que no se conecta a internet.

### Eliminado

- **El EULA de la app** (`EULA.txt` y `EULA-en.txt`), que la declaraba de código cerrado. Lo reemplaza `LICENSE.txt` (GPL-3.0) dentro del zip.
- **De la página:** el número de versión, el SHA-256 y el tamaño del zip, que ahora están en GitHub. La página tampoco aloja ya los zips.

---

## 0.0.2.5 — 2026-10-14 · publicada

Primera actualización desde la 0.1. Junta las compilaciones internas 0.2, 0.3, 0.4 y 0.0.2.4 con los arreglos posteriores.

| Descarga | Tamaño | SHA-256 |
|---|---|---|
| `VideoLite-0.0.2.5-win64.zip` | 7 184 673 B | `c76857c0a7c4a744f45fb2abefea50039aef6ad1d20579f19a6be5f8104b2e77` |
| `VideoLite-0.0.2.5-ffmpeg-fuentes.zip` | 17 155 871 B | `70d14659653f23b1fd84ed2a28f0620547e0766ec6d52d5cd30f1b819e9c6eab` |

### Novedades

**Web**
- **Motor rápido.** Usa el codificador de video del propio equipo (WebCodecs) cuando el video lo permite, y si no, el motor completo de siempre (ffmpeg.wasm). La página elige sola, video por video.
  - En una PC de prueba, un 1080p de 20 s a 5 MB tarda unos 4 s; antes tardaba 58 s.
  - Si el motor rápido no logra el tamaño o falla, el video pasa solo al completo.
  - El motor completo (32 MB) solo se descarga si algún video lo necesita.
- **Editor de un video.** Se activa con la casilla «Activar editor», que lo despliega con una animación. Tiene un visor grande y una línea de tiempo con el video y el audio en pistas separadas (miniaturas y forma de onda). Permite:
  - recortar el inicio y el fin;
  - quitar tramos del medio;
  - silenciar el audio por tramos;
  - el volumen del audio, de 0 a 300 %;
  - mover el audio ±5 s para sincronizarlo.
  - La vista previa ya aplica todo, cada cambio se puede deshacer y todo funciona con teclado.
- **Avance:** una barra de avance junto al botón «Procesar», con el tiempo que falta. Al terminar, las descargas aparecen ahí mismo, y con un solo archivo la descarga empieza sola.
- **Inglés:**
  - un botón ES/EN, y la página recuerda la elección;
  - páginas propias en `/en/`;
  - un aviso discreto cuando el navegador está en el otro idioma.
- **Página de privacidad** (`/privacidad/` y `/en/privacy/`):
  - qué datos recibe cada servicio y que los videos nunca se suben;
  - contacto por Discord y por correo;
  - la edad mínima de 13 años para escribir;
  - el borrado de los mensajes 90 días después de resolver.
- **Avisos nuevos:**
  - una página 404 propia;
  - un aviso si el navegador no tiene JavaScript o WebAssembly;
  - un aviso para los archivos que no son video;
  - avisos para lector de pantalla.
- **Confirmaciones** antes de quitar un resultado sin descargar o de perder las ediciones (al quitar el video, al reemplazarlo o al cerrar la pestaña).

**App de Windows**
- **Inglés:** un botón cambia toda la ventana. La primera vez, la app abre en el idioma de Windows.
- **Tema oscuro:** un botón «Tema oscuro / Tema claro», que también oscurece la barra de título. La app recuerda la elección.
- **«Agregar a «Enviar a»»:** clic derecho en un video → Enviar a → Video Lite. También acepta archivos y carpetas como argumentos.
- **Al terminar:** un sonido, un parpadeo en la barra de tareas y una notificación de Windows.
- **Pie:** enlaces a Privacidad y al correo de contacto.
- **El zip** trae `README.txt` y `EULA-en.txt`.

**Web y app**
- **Menú «Para enviar por»:**
  - Recomendado (automático);
  - Discord gratis, 20 MB;
  - Discord Nitro Basic, 50 MB;
  - WhatsApp, 64 MB y como máximo 720p;
  - Correo, 18 MB (el adjunto crece al enviarse);
  - Personalizado, que se elige solo al mover el tamaño a mano.
- **«Más opciones»:**
  - el formato: MP4, MOV o MKV, o solo el audio en MP3 o M4A;
  - girar y voltear;
  - sin audio;
  - «Avisarme al terminar». En la web, con un sonido, una notificación del navegador y el avance en el título de la pestaña.
- **«Mejorar voz»:** quita el zumbido grave y el ruido de fondo y empareja el volumen de la voz. Está apagada por defecto, porque con música puede sonar peor.

### Cambios

- **«Procesar» en vez de «Comprimir»** (web y app), porque el botón aplica todo lo elegido, ediciones incluidas. También cambian «Procesando…», «por procesar» y la notificación: «Videos procesados: n de total».
- **Cambiar un ajuste después de procesar** vuelve a dejar el video listo para procesarse. El archivo anterior se puede descargar hasta que se reemplaza.
- **Los límites de los destinos:**
  - Discord gratis pasa de 10 a 20 MB, porque Discord subió su límite.
  - El correo pasa de 25 a 18 MB, porque el adjunto crece cerca de un 37 % al enviarse y uno de 25 MB podía rechazarse.
- **La descarga de la app** pasa a un bloque plegable al final de la página. Arriba solo queda una burbuja: «¿Varios videos a la vez? Descarga para Windows».
- **Las fuentes** se sirven desde el propio sitio, así que Google ya no recibe la IP de quien visita.
- **Botones más fáciles de tocar:** 44 px los redondos y al menos 32 px el resto.
- **El texto del formato:** antes decía «El resultado es un MP4»; ahora dice «Por defecto, un MP4».
- **El nombre del archivo:** un video que ya terminaba en «-lite» ya no sale como «-lite-lite».
- **Seguridad:** la web envía HSTS. Además, el motor completo ya no se guarda una semana en la caché, así que una actualización no puede mezclar versiones.
- **La app de Windows:**
  - Ahora calcula las medidas igual que la web. Con algunas medidas poco comunes, el ancho de salida podía quedar 2 px distinto; por ejemplo, 1914×720 a 360p.
  - Su `ffmpeg.exe` se recompiló con LAME, para el MP3, y con los formatos MOV y MKV. El zip de fuentes de ffmpeg incluye LAME.
- **El EULA** pasa a su versión 0.2, con el correo de contacto y la cláusula de idioma.

### Correcciones

- **El foco del teclado** no se veía sobre la opción marcada; ahora tiene contorno.
- **En tema oscuro**, el borde de la opción marcada y la barra de progreso no tenían suficiente contraste.
- **Si un video falla**, su barra de progreso se oculta.

### Eliminado

- **Los botones de tamaño rápido** de la web y la app («Discord 10 MB», «correo 25 MB»…). Los reemplaza el menú «Para enviar por».
- **El aviso amarillo de arriba** de la página, con su botón de descarga. Lo reemplazan la burbuja y el bloque plegable.
- **La carga de Google Fonts.**
- **La descarga de la 0.1** ya no aparece en la página, que solo ofrece la 0.0.2.5.

---

## 0.1 — 2026-10-13 · publicada

Primera versión.

| Descarga | Tamaño | SHA-256 |
|---|---|---|
| `VideoLite-0.1-win64.zip` | 6 932 273 B | `70d558a4…2962703f` |
| `VideoLite-0.1-ffmpeg-fuentes.zip` | 15 632 559 B | — |

### Novedades

**Web**
- **Privada:** comprime videos dentro del navegador y nunca sube nada. El motor (ffmpeg.wasm, de un hilo) se descarga de jsDelivr y solo se usa si su SHA-256 coincide.
- **Ajustes:**
  - el tamaño final en MB, con botones de acceso rápido («Discord 10 MB», «correo 25 MB»…);
  - la calidad, como resolución máxima;
  - la velocidad, de ×0.5 a ×2, sin cambiar el tono de la voz.
- **Estimado antes de empezar:** el tamaño y la calidad que va a tener cada video.
- **Diseño:** tema claro por defecto, con un botón para el oscuro; ancho de 980 px y el pie común de las webapps de Zedran Labs.
- **Descarga de la app:** con su SHA-256, el comando para verificarlo, los pasos para el aviso «Windows protegió su PC» y el código fuente de FFmpeg.

**App de Windows**
- La misma herramienta como programa, en español:
  - no se instala;
  - procesa muchos videos a la vez;
  - es unas 10 veces más rápida que la web;
  - usa un `ffmpeg.exe` mínimo propio, cuyo código y receta se publican bajo la GPL.

**Web y app**
- **La salida:** un MP4 con H.264 y AAC, que abre en cualquier computadora, celular, navegador y WhatsApp.
- **El tamaño exacto:**
  - el cálculo descuenta lo que pesa el contenedor;
  - nunca se infla un video que ya es ligero (tope del 90 % del original);
  - si el resultado se pasa, la segunda pasada se repite hasta 4 veces;
  - si aun así no cabe, lo dice: «No cupo en X: quedó en Y».
- **«Arreglar audio mudo»,** activada por defecto, corrige los videos que se oyen en audífonos pero no en bocinas ni laptops (canales con la fase invertida). Si no puede medir la fase, deja el audio en estéreo y lo avisa, para nunca entregar un video mudo.

---

## Compilaciones internas (no publicadas)

Se hicieron entre la 0.1 y la 0.0.2.5. Todo lo suyo llegó a la gente con la **0.0.2.5**.

| Compilación | Fecha | Qué trajo |
|---|---|---|
| 0.2 (= 0.0.2.1) | 2026-10-13 | Fuentes desde el sitio, botón ES/EN en la web, tema oscuro en la app y Discord a 20 MB |
| 0.3 (= 0.0.2.2) | 2026-10-13 | App en inglés; destinos, «Más opciones», formatos, girar, sin audio y aviso al terminar; «Enviar a»; páginas `/en/` y de privacidad; correo a 18 MB |
| 0.4 (= 0.0.2.3) | 2026-10-14 | Motor rápido, editor, «Mejorar voz», descarga plegable y privacidad con edad mínima y plazo de borrado. Se reempaquetó con el cálculo de la app igual al de la web |
| 0.0.2.4 | 2026-10-14 | Arreglos del editor («Desde aquí» y «Hasta aquí», y el acomodo con el editor activo) |
| 0.0.2.5 | 2026-10-14 | «Procesar», barra de avance y descargas junto al botón: **publicada** |
