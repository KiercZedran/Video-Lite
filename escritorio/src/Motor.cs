// Video Lite · Zedran Labs: motor de compresión con ffmpeg nativo.
// Misma lógica que sitio/app.js (plan, contenedor, fase invertida, dos pasadas); si cambias una, cambia la otra.
// C# 5: compila con el csc de .NET Framework 4.8 que trae Windows.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace VideoLite
{
    public class InfoVideo
    {
        public double Dur;
        public int W, H;
        public double Fps;
        public bool HasAudio;
        public int Ach;
        public string VCodec, ACodec;
    }

    public class Ajustes
    {
        public double TargetMB = 25;
        public int Res;          // 0 = original; si no, lado corto máximo
        public double Speed = 1;
        public bool Fix = true;
        public bool Voz;                 // "Mejorar voz"
        public string Formato = "mp4";   // mp4 | mov | mkv | mp3 | m4a (estos dos: solo audio)
        public string Rotacion = "";     // "" | 90 | 270 | 180 | h | v
        public bool SinAudio;
        public bool SoloAudio { get { return Formato == "mp3" || Formato == "m4a"; } }
    }

    public class Plan
    {
        public double OutDur, Fps, Bpp, VsSource, EstMB;
        public int W, H, AK, VK;
        public bool Mono, Capped, TooSmall;
        public double TargetB, OverheadB;   // tamaño pedido y peso estimado del contenedor, en bytes
        public string Preset;
        public bool SoloAudio, SinPistaAudio, HasAudio;
        public int OW, OH;                  // tamaño de salida ya girado (90/270 intercambian ancho y alto)
        public Edicion Ed;                  // edición del editor, o null (como "ed" en sitio/plan.js)
    }

    // Edición del editor: el mismo modelo que sitio/editor.js (INVARIANTES 30). Nunca toca el original:
    // describe qué se conserva, qué se silencia, el volumen y el desfase, en segundos sobre el video original.
    public class Edicion
    {
        public const double MinTramo = 0.1;   // s: un tramo más corto no tiene sentido y complica la concatenación
        public double Dur, Entrada, Salida;
        public List<double[]> Cortes = new List<double[]>(), Silencios = new List<double[]>();
        public double Volumen = 1, Desfase = 0;

        public static Edicion Vacia(double dur) { return new Edicion { Dur = dur, Entrada = 0, Salida = dur }; }
        public Edicion Copia()
        {
            return new Edicion { Dur = Dur, Entrada = Entrada, Salida = Salida, Volumen = Volumen, Desfase = Desfase,
                Cortes = Cortes.Select(x => new[] { x[0], x[1] }).ToList(), Silencios = Silencios.Select(x => new[] { x[0], x[1] }).ToList() };
        }
        // Junta rangos que se tocan o se enciman (unir en editor.js).
        public static List<double[]> Unir(IEnumerable<double[]> rangos)
        {
            var r = rangos.Where(x => x[1] - x[0] > 1e-3).Select(x => new[] { x[0], x[1] }).OrderBy(x => x[0]).ToList();
            var o = new List<double[]>();
            foreach (var x in r)
            {
                if (o.Count > 0 && x[0] <= o[o.Count - 1][1] + 1e-3) o[o.Count - 1][1] = Math.Max(o[o.Count - 1][1], x[1]);
                else o.Add(x);
            }
            return o;
        }
        // Lo que queda del video: [Entrada, Salida] menos los tramos quitados.
        public List<double[]> Tramos()
        {
            var t = new List<double[]>();
            double a = Entrada;
            foreach (var c in Unir(Cortes))
            {
                if (c[1] <= a || c[0] >= Salida) continue;
                if (c[0] - a >= MinTramo) t.Add(new[] { a, Math.Min(c[0], Salida) });
                a = Math.Max(a, c[1]);
            }
            if (Salida - a >= MinTramo) t.Add(new[] { a, Salida });
            return t;
        }
        public double Duracion() { return Tramos().Sum(x => x[1] - x[0]); }
        public bool HayCambios()
        {
            return Entrada > 0.01 || Salida < Dur - 0.01 || Cortes.Count > 0 || Silencios.Count > 0 || Math.Abs(Volumen - 1) > 0.001 || Math.Abs(Desfase) > 0.0005;
        }
    }

    // El grafo de ffmpeg de una edición: lo mismo que grafoEdicion en sitio/grafo.js, carácter por carácter
    // (herramientas/paridad-edicion.mjs lo compara).
    public class Grafo
    {
        public string[] Leer;               // -t: hasta dónde hace falta leer el original
        public Func<string, string> Video;  // vf (filtros de siempre) → grafo con salida [vout]
        public Func<string, string> Audio;  // af → grafo con salida [aout]
    }

    public static class Motor
    {
        static readonly CultureInfo CI = CultureInfo.InvariantCulture;
        public const double MaxMB = 100000;
        public static string Ffmpeg;
        static Process actual;
        static readonly object candado = new object();
        public static volatile bool Cancelado;

        /* ---------- ffmpeg ---------- */

        public static string BuscarFfmpeg()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            foreach (string c in new[] { Path.Combine(dir, "ffmpeg.exe"), Path.Combine(dir, "ffmpeg", "ffmpeg.exe"), Path.Combine(dir, "ffmpeg", "bin", "ffmpeg.exe") })
                if (File.Exists(c)) return c;
            string path = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string p in path.Split(';'))
            {
                try
                {
                    string c = Path.Combine(p.Trim().Trim('"'), "ffmpeg.exe");
                    if (p.Trim().Length > 0 && File.Exists(c)) return c;
                }
                catch (ArgumentException) { }
            }
            return null;
        }

        static string Quote(string a)
        {
            if (a.Length > 0 && a.IndexOfAny(new[] { ' ', '\t', '"' }) < 0) return a;
            var sb = new StringBuilder("\"");
            int bs = 0;
            foreach (char ch in a)
            {
                if (ch == '\\') { bs++; continue; }
                if (ch == '"') { sb.Append('\\', bs * 2 + 1); sb.Append('"'); bs = 0; continue; }
                sb.Append('\\', bs); bs = 0; sb.Append(ch);
            }
            sb.Append('\\', bs * 2); sb.Append('"');
            return sb.ToString();
        }

        // Corre ffmpeg y devuelve el código de salida. stderr y stdout llegan línea por línea.
        public static int Ejecutar(IList<string> args, Action<string> err, Action<string> outp)
        {
            var psi = new ProcessStartInfo(Ffmpeg);
            var parts = new List<string>();
            foreach (string a in args) parts.Add(Quote(a));
            psi.Arguments = string.Join(" ", parts);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardError = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardInput = true;
            psi.StandardErrorEncoding = Encoding.UTF8;
            psi.StandardOutputEncoding = Encoding.UTF8;
            using (var p = new Process())
            {
                p.StartInfo = psi;
                p.ErrorDataReceived += (s, e) => { if (e.Data != null && err != null) err(e.Data); };
                p.OutputDataReceived += (s, e) => { if (e.Data != null && outp != null) outp(e.Data); };
                lock (candado) { if (Cancelado) return -1; p.Start(); actual = p; }
                p.BeginErrorReadLine();
                p.BeginOutputReadLine();
                p.WaitForExit();
                lock (candado) { actual = null; }
                return p.ExitCode;
            }
        }

        public static void Cancelar()
        {
            lock (candado)
            {
                Cancelado = true;
                try { if (actual != null && !actual.HasExited) actual.Kill(); } catch (Exception) { }
            }
        }

        /* ---------- Análisis ---------- */

        static double Hms(string s)
        {
            string[] p = s.Split(':');
            return double.Parse(p[0], CI) * 3600 + double.Parse(p[1], CI) * 60 + double.Parse(p[2], CI);
        }

        public static InfoVideo Analizar(string path)
        {
            var lines = new List<string>();
            Ejecutar(new[] { "-hide_banner", "-i", path }, l => { lock (lines) lines.Add(l); }, null);
            string txt = string.Join("\n", lines);
            Match d = Regex.Match(txt, @"Duration: (\d+:\d+:[\d.]+)");
            Match vs = Regex.Match(txt, @"Stream #\d+:\d+.*?: Video: .*");
            if (!d.Success || !vs.Success) return null;
            var m = new InfoVideo();
            m.Dur = Hms(d.Groups[1].Value);
            Match wh = Regex.Match(vs.Value, @", (\d{2,5})x(\d{2,5})[ ,\[]");
            m.W = wh.Success ? int.Parse(wh.Groups[1].Value) : 1280;
            m.H = wh.Success ? int.Parse(wh.Groups[2].Value) : 720;
            Match fps = Regex.Match(vs.Value, @"([\d.]+) fps");
            if (!fps.Success) fps = Regex.Match(vs.Value, @"([\d.]+) tbr");
            m.Fps = fps.Success ? Math.Min(double.Parse(fps.Groups[1].Value, CI), 120) : 30;
            m.VCodec = Regex.Match(vs.Value, @"Video: (\w+)").Groups[1].Value;
            Match a = Regex.Match(txt, @"Stream #\d+:\d+.*?: Audio: .*");
            if (a.Success)
            {
                string s = a.Value;
                Match ch = Regex.Match(s, @"(\d+) channels");
                m.Ach = s.Contains("mono") ? 1 : s.Contains("stereo") ? 2 : ch.Success ? int.Parse(ch.Groups[1].Value) : 2;
                m.ACodec = Regex.Match(s, @"Audio: (\w+)").Groups[1].Value;
            }
            m.HasAudio = m.Ach > 0;
            Match rot = Regex.Match(txt, @"rotation of (-?[\d.]+) degrees");
            if (rot.Success && Math.Abs((int)Math.Round(double.Parse(rot.Groups[1].Value, CI))) % 180 == 90)
            { int t = m.W; m.W = m.H; m.H = t; }
            if (m.Dur <= 0) return null;
            return m;
        }

        // Canales en fase invertida (L = −R): al mezclarlos se cancelan y queda silencio.
        // Devuelve null si no se pudo medir; quien llama debe tratarlo como "puede estar invertido".
        public static bool? FaseInvertida(string path, double dur)
        {
            string ss = Math.Max(0, Math.Floor(dur / 3)).ToString(CI);
            string t = Math.Min(30, Math.Max(5, Math.Floor(dur))).ToString(CI);
            Func<string, double?> rms = expr =>
            {
                string last = null;
                Ejecutar(new[] { "-hide_banner", "-ss", ss, "-t", t, "-i", path, "-vn", "-af", "pan=mono|c0=" + expr + ",astats=measure_perchannel=0", "-f", "null", "NUL" },
                    l => { if (l.Contains("RMS level dB")) last = l; }, null);
                if (last == null) return null;
                string v = last.Substring(last.LastIndexOf(':') + 1).Trim();
                if (v.Contains("inf")) return -200;
                double r;
                return double.TryParse(v, NumberStyles.Float, CI, out r) ? (double?)r : null;
            };
            double? sum = rms("0.5*c0+0.5*c1");
            double? dif = rms("0.5*c0-0.5*c1");
            if (!sum.HasValue || !dif.HasValue) return null;
            return sum.Value < dif.Value - 20;
        }

        /* ---------- Plan ---------- */

        public static Plan Planear(InfoVideo m, long size, Ajustes a) { return Planear(m, size, a, m.Dur); }

        // dur: la duración que se comprime (tras el editor). El tope del 90 % sigue usando el bitrate del original completo.
        public static Plan Planear(InfoVideo m, long size, Ajustes a, double dur)
        {
            var p = new Plan();
            p.OutDur = dur / a.Speed;
            if (a.SoloAudio)
            {
                // Solo audio: calidad fija (MP3 192 kbps, M4A/AAC 160 kbps); el tamaño depende de la duración.
                p.SoloAudio = true; p.HasAudio = m.HasAudio; p.SinPistaAudio = !m.HasAudio; p.TooSmall = !m.HasAudio;
                p.AK = a.Formato == "mp3" ? 192 : 160;
                p.EstMB = p.AK * p.OutDur / 8000 + 12 * 47 * p.OutDur / 1e6;
                p.TargetB = double.MaxValue; p.Bpp = 1; p.VsSource = 1;
                return p;
            }
            bool hasAudio = m.HasAudio && !a.SinAudio;
            p.HasAudio = hasAudio;
            p.Fps = Math.Min(Math.Round(m.Fps * 100, MidpointRounding.AwayFromZero) / 100, 60);
            if (p.Fps <= 0) p.Fps = 30;
            int w = m.W, h = m.H, sh = Math.Min(w, h);
            if (a.Res > 0 && sh > a.Res)
            {
                double k = (double)a.Res / sh;
                // AwayFromZero = Math.round de JavaScript con positivos (C# redondea .5 al par por omisión): misma medida que plan.js
                w = (int)Math.Round(w * k / 2, MidpointRounding.AwayFromZero) * 2; h = (int)Math.Round(h * k / 2, MidpointRounding.AwayFromZero) * 2;
            }
            w -= w % 2; h -= h % 2;
            p.W = w; p.H = h;
            // El contenedor MP4 pesa ~12 bytes por cuadro de video y por cuadro de audio AAC (~47/s a 48 kHz).
            double overheadB = 12 * (p.Fps + (hasAudio ? 47 : 0)) * p.OutDur;
            double totalK = Math.Max(0, a.TargetMB * 1e6 - overheadB) * 8 / p.OutDur / 1000;
            // No tiene caso gastar más que el original: el total se topa al 90 % de su bitrate (ajustado por escala y velocidad).
            double srcK = size * 8.0 / m.Dur / 1000;
            double areaK = (double)w * h / ((double)m.W * m.H);
            double maxTotalK = srcK * Math.Min(1, areaK) * a.Speed * 0.9;
            p.Capped = totalK > maxTotalK;
            if (p.Capped) totalK = maxTotalK;
            if (hasAudio)
            {
                if (totalK >= 600) p.AK = 128; else if (totalK >= 250) p.AK = 96; else if (totalK >= 120) p.AK = 64; else { p.AK = 40; p.Mono = true; }
            }
            int vK = (int)Math.Floor((totalK - p.AK) * 0.96);
            p.VK = vK;
            // 1 = mismo bitrate total por píxel que el original
            p.VsSource = srcK > 0 ? (vK + p.AK) / (srcK * Math.Min(1, areaK) * a.Speed) : 0;
            p.Bpp = vK * 1000.0 / (w * h * p.Fps);
            p.EstMB = ((vK / 0.96) + p.AK) * p.OutDur / 8000 + overheadB / 1e6;
            // Nativo y multihilo: se puede gastar más tiempo por cuadro que en el navegador.
            p.Preset = (long)w * h <= 1280L * 720 ? "slow" : "medium";
            p.TooSmall = vK < 16;
            p.TargetB = a.TargetMB * 1e6;
            p.OverheadB = overheadB;
            bool gira = a.Rotacion == "90" || a.Rotacion == "270";
            p.OW = gira ? h : w; p.OH = gira ? w : h;
            return p;
        }

        public static double RecomendarMB(InfoVideo m, long size, Ajustes a) { return RecomendarMB(m, size, a, m.Dur); }

        public static double RecomendarMB(InfoVideo m, long size, Ajustes a, double dur)
        {
            double w = m.W, h = m.H, sh = Math.Min(w, h);
            if (a.Res > 0 && sh > a.Res) { double k = a.Res / sh; w *= k; h *= k; }
            double fps = Math.Min(m.Fps > 0 ? m.Fps : 30, 60);
            double vK = 0.08 * w * h * fps / 1000;
            double total = vK + (m.HasAudio && !a.SinAudio ? 128 : 0);
            double mb = total * (dur / a.Speed) / 8000;
            return Math.Max(1, Math.Min(Math.Round(mb, MidpointRounding.AwayFromZero), Math.Max(1, Math.Round(size / 1e6 * 0.8, MidpointRounding.AwayFromZero))));
        }

        public static string Calidad(Plan p, out int nivel)
        {
            // nivel: 2 buena, 1 aceptable, 0 baja
            if (p.VsSource >= 0.6) { nivel = 2; return Tx.T("qSame"); }
            if (p.Bpp >= 0.08) { nivel = 2; return Tx.T("qVeryGood"); }
            if (p.Bpp >= 0.045) { nivel = 2; return Tx.T("qGood"); }
            if (p.Bpp >= 0.025) { nivel = 1; return Tx.T("qOk"); }
            if (p.Bpp >= 0.012) { nivel = 0; return Tx.T("qLow"); }
            nivel = 0; return Tx.T("qVeryLow");
        }

        /* ---------- Compresión ---------- */

        // Menos de 1 MB se muestra en KB: "0.0 MB" o "0.03 MB → 0.03 MB" confunden. Igual que fmtMB en app.js.
        public static string Tam(double bytes)
        {
            if (bytes < 1e6) return Math.Round(bytes / 1e3).ToString(CI) + " KB";
            double m = bytes / 1e6;
            return (m >= 100 ? Math.Round(m).ToString(CI) : m.ToString("0.0", CI)) + " MB";
        }

        public static string NombreLibre(string dir, string baseName, string ext = "mp4")
        {
            string c = Path.Combine(dir, baseName + "-lite." + ext);
            for (int i = 2; File.Exists(c); i++) c = Path.Combine(dir, baseName + "-lite (" + i + ")." + ext);
            return c;
        }

        // "Mejorar voz": la misma cadena que sitio/app.js (INVARIANTES 19). Quita el zumbido grave, baja el ruido y empareja el volumen.
        public const string Voz = "highpass=f=80,afftdn=nf=-25,dynaudnorm=f=200:g=15";

        // Como n3 en grafo.js: hasta 3 decimales, sin ceros de más ("2", "2.5", "0.3").
        static string N3(double x) { return (Math.Round(x * 1000, MidpointRounding.AwayFromZero) / 1000).ToString("0.###", CI); }

        // Ambos flujos empiezan en 0, se recortan los tramos que se conservan y se unen con concat; después van los filtros
        // de siempre (vf/af). En el audio, antes de recortar: desfase (adelay o atrim), relleno hasta la duración (apad),
        // volumen y silencios. Igual que grafoEdicion en sitio/grafo.js.
        public static Grafo GrafoEdicion(List<double[]> tr, List<double[]> silencios, double volumen, double desfase, double dur)
        {
            int n = tr.Count;
            Func<string, string, string, string, string> unir = (pre, tipo, sufijo, salida) =>
            {
                bool v = tipo == "v";
                string trim = v ? "trim" : "atrim", pts = v ? "setpts=PTS-STARTPTS" : "asetpts=PTS-STARTPTS";
                string fin = string.IsNullOrEmpty(sufijo) ? "" : "," + sufijo;
                if (n == 1) return pre + "," + trim + "=start=" + N3(tr[0][0]) + ":end=" + N3(tr[0][1]) + "," + pts + fin + "[" + salida + "]";
                var partes = new List<string>();
                var sp = new StringBuilder(pre + "," + (v ? "split" : "asplit") + "=" + n);
                for (int i = 0; i < n; i++) sp.Append("[" + tipo + "s" + i + "]");
                partes.Add(sp.ToString());
                for (int i = 0; i < n; i++)
                    partes.Add("[" + tipo + "s" + i + "]" + trim + "=start=" + N3(tr[i][0]) + ":end=" + N3(tr[i][1]) + "," + pts + "[" + tipo + "t" + i + "]");
                var cc = new StringBuilder();
                for (int i = 0; i < n; i++) cc.Append("[" + tipo + "t" + i + "]");
                cc.Append("concat=n=" + n + ":v=" + (v ? 1 : 0) + ":a=" + (v ? 0 : 1) + fin + "[" + salida + "]");
                partes.Add(cc.ToString());
                return string.Join(";", partes);
            };
            // Hasta dónde leer: el final del último tramo, más lo que el audio se adelantó (atrim) y medio segundo de margen.
            double finLectura = tr.Max(x => x[1]) + Math.Max(0, -desfase) + 0.5;
            var g = new Grafo();
            g.Leer = finLectura < dur ? new[] { "-t", N3(finLectura) } : new string[0];
            g.Video = vf => unir("[0:v]setpts=PTS-STARTPTS", "v", vf, "vout");
            g.Audio = af =>
            {
                var f = new List<string> { "asetpts=PTS-STARTPTS" };
                if (desfase > 0) f.Add("adelay=delays=" + Math.Round(desfase * 1000, MidpointRounding.AwayFromZero).ToString(CI) + ":all=1");
                if (desfase < 0) { f.Add("atrim=start=" + N3(-desfase)); f.Add("asetpts=PTS-STARTPTS"); }
                f.Add("apad=whole_dur=" + N3(dur));
                if (volumen != 1) f.Add("volume=" + N3(volumen));
                foreach (var s in silencios) f.Add("volume=enable='between(t," + N3(s[0]) + "," + N3(s[1]) + ")':volume=0");
                return unir("[0:a]" + string.Join(",", f), "a", af, "aout");
            };
            return g;
        }

        // Filtro de giro/volteo (se aplica después de escalar).
        static string FiltroGiro(string r)
        {
            switch (r) { case "90": return "transpose=1"; case "270": return "transpose=2"; case "180": return "hflip,vflip"; case "h": return "hflip"; case "v": return "vflip"; default: return null; }
        }

        // progreso(fracción 0–1, texto). Devuelve la ruta del archivo creado. Lanza excepción si falla.
        public static string Comprimir(string input, InfoVideo m, Plan p, Ajustes a, string outDir, Action<double, string> progreso, out string nota)
        {
            bool noCupo;
            return Comprimir(input, m, p, a, outDir, progreso, out nota, out noCupo);
        }

        // noCupo = true si, tras los reintentos, el archivo sigue pasando del tamaño pedido (la nota lo explica).
        public static string Comprimir(string input, InfoVideo m, Plan p, Ajustes a, string outDir, Action<double, string> progreso, out string nota, out bool noCupo)
        {
            nota = null; noCupo = false;
            Directory.CreateDirectory(outDir);
            string tmp = Path.Combine(Path.GetTempPath(), "VideoLite");
            Directory.CreateDirectory(tmp);
            string passlog = Path.Combine(tmp, "pass-" + Guid.NewGuid().ToString("N"));
            string ext = string.IsNullOrEmpty(a.Formato) ? "mp4" : a.Formato;
            string salida = NombreLibre(outDir, Path.GetFileNameWithoutExtension(input), ext);
            string parcial = salida + ".part." + ext;   // la extensión le dice a ffmpeg qué contenedor escribir
            try
            {
                bool? fase = false;
                if (p.SoloAudio && !m.HasAudio) throw new Exception(Tx.T("sinPistaAudio"));
                if ((p.HasAudio || p.SoloAudio) && m.Ach >= 2 && (a.Fix || p.Mono))
                {
                    progreso(0, Tx.T("revisandoAudio"));
                    fase = FaseInvertida(input, m.Dur);
                    if (Cancelado) throw new OperationCanceledException();
                }
                bool inverted = fase == true;
                bool mono = p.Mono;
                // Sin arreglo, o si no se pudo medir, no se mezcla a mono: con canales invertidos quedaría mudo.
                if ((inverted && !a.Fix) || fase == null) mono = false;
                if (fase == null) nota = Tx.T("faseNoMedida");
                var af = new List<string>();
                if (inverted && a.Fix)
                {
                    af.Add(mono ? "pan=mono|c0=c0" : "pan=stereo|c0=c0|c1=c0");
                    nota = Tx.T("faseCorregida");
                }
                string sp = a.Speed.ToString(CI);
                if (a.Voz) af.Add(Voz);   // después del arreglo de fase y antes del cambio de velocidad
                if (a.Speed != 1) af.Add("atempo=" + sp);
                // Con edición, los mismos filtros van dentro de un grafo que primero recorta y une los tramos (como en la web).
                Grafo g = p.Ed != null && p.Ed.HayCambios() ? GrafoEdicion(p.Ed.Tramos(), Edicion.Unir(p.Ed.Silencios), p.Ed.Volumen, p.Ed.Desfase, m.Dur) : null;
                string[] leer = g != null ? g.Leer : new string[0];
                var errs0 = new List<string>();
                if (p.SoloAudio)
                {
                    // Solo audio: una pasada, calidad fija.
                    var aa = new List<string> { "-hide_banner", "-nostats", "-progress", "pipe:1" };
                    aa.AddRange(leer);
                    aa.AddRange(new[] { "-i", input, "-vn" });
                    if (g != null) aa.AddRange(new[] { "-filter_complex", g.Audio(string.Join(",", af)), "-map", "[aout]" });
                    else { aa.AddRange(new[] { "-map", "0:a:0" }); if (af.Count > 0) aa.AddRange(new[] { "-af", string.Join(",", af) }); }
                    if (ext == "mp3") aa.AddRange(new[] { "-c:a", "libmp3lame", "-b:a", "192k" });
                    else aa.AddRange(new[] { "-c:a", "aac", "-b:a", "160k", "-movflags", "+faststart" });
                    aa.AddRange(new[] { "-map_metadata", "-1", "-y", parcial });
                    int c0 = Ejecutar(aa, l => { lock (errs0) { errs0.Add(l); if (errs0.Count > 200) errs0.RemoveAt(0); } }, l =>
                    {
                        long us;
                        if (l.StartsWith("out_time_us=") && long.TryParse(l.Substring(12), out us) && us >= 0)
                            progreso(Math.Min(1, us / 1e6 / p.OutDur), Tx.T("extrayendo") + " · " + Math.Round(Math.Min(1, us / 1e6 / p.OutDur) * 100) + " %");
                    });
                    if (Cancelado) throw new OperationCanceledException();
                    if (c0 != 0) throw new Exception(UltimoError(errs0));
                    File.Move(parcial, salida);
                    return salida;
                }
                var vf = new List<string>();
                if (a.Speed != 1) vf.Add("setpts=PTS/" + sp);
                vf.Add("scale=" + p.W + ":" + p.H);
                string giro = FiltroGiro(a.Rotacion);
                if (giro != null) vf.Add(giro);
                vf.Add("fps=" + p.Fps.ToString(CI));
                vf.Add("format=yuv420p");

                // conAudio: en la pasada 2, el grafo de la edición también lleva el audio.
                Func<int, bool, List<string>> common = (vk, conAudio) =>
                {
                    var l = new List<string> { "-hide_banner", "-nostats", "-progress", "pipe:1" };
                    l.AddRange(leer);
                    l.AddRange(new[] { "-i", input });
                    if (g != null)
                    {
                        string vfs = string.Join(",", vf);
                        l.AddRange(new[] { "-filter_complex", conAudio ? g.Video(vfs) + ";" + g.Audio(string.Join(",", af)) : g.Video(vfs), "-map", "[vout]" });
                        if (conAudio) l.AddRange(new[] { "-map", "[aout]" });
                    }
                    else l.AddRange(new[] { "-map", "0:v:0", "-vf", string.Join(",", vf) });
                    l.AddRange(new[] { "-c:v", "libx264", "-preset", p.Preset, "-profile:v", "high", "-b:v", vk + "k", "-map_metadata", "-1", "-passlogfile", passlog });
                    return l;
                };

                var errs = new List<string>();
                Action<string> err = l => { lock (errs) { errs.Add(l); if (errs.Count > 200) errs.RemoveAt(0); } };
                Func<double, double, string, Action<string>> prog = (b, wgt, label) => l =>
                {
                    if (!l.StartsWith("out_time_us=")) return;
                    long us;
                    if (!long.TryParse(l.Substring(12), out us) || us < 0) return;
                    double f = Math.Min(1, us / 1e6 / p.OutDur);
                    progreso(b + wgt * f, label + " · " + Math.Round(f * 100) + " %");
                };

                var a1 = common(p.VK, false); a1.AddRange(new[] { "-pass", "1", "-an", "-f", "null", "NUL" });
                progreso(0.01, Tx.T("pasada1Ini"));
                int code = Ejecutar(a1, err, prog(0, 0.3, Tx.T("pasada1")));
                if (Cancelado) throw new OperationCanceledException();
                if (code != 0) throw new Exception(UltimoError(errs));

                // Pasada 2 verificada: x264 no garantiza el tamaño en videos cortos o muy difíciles. Si el archivo se pasa,
                // se repite solo la pasada 2 con menos bitrate (la pasada 1 sirve para cualquier bitrate). Hasta 4 intentos.
                int vK = p.VK;
                long bytes = 0;
                for (int intento = 1; ; intento++)
                {
                    var a2 = common(vK, p.HasAudio); a2.AddRange(new[] { "-pass", "2" });
                    if (p.HasAudio)
                    {
                        if (g == null) a2.AddRange(new[] { "-map", "0:a:0" });
                        a2.AddRange(new[] { "-c:a", "aac", "-b:a", p.AK + "k" });
                        if (mono) a2.AddRange(new[] { "-ac", "1" });
                        if (g == null && af.Count > 0) a2.AddRange(new[] { "-af", string.Join(",", af) });
                    }
                    else a2.Add("-an");
                    if (ext != "mkv") a2.AddRange(new[] { "-movflags", "+faststart" });   // MKV no usa movflags
                    a2.AddRange(new[] { "-y", parcial });
                    code = Ejecutar(a2, err, prog(0.3, 0.7, intento == 1 ? Tx.T("pasada2") : Tx.T("ajustando", intento)));
                    if (Cancelado) throw new OperationCanceledException();
                    if (code != 0) throw new Exception(UltimoError(errs));
                    bytes = new FileInfo(parcial).Length;
                    if (bytes <= p.TargetB || intento == 4 || vK <= 16) break;
                    double otros = p.AK * 1.03 * p.OutDur * 125 + p.OverheadB; // bytes que no son video (el AAC sale ~3 % arriba)
                    double videoReal = Math.Max(1, bytes - otros), videoMeta = Math.Max(1, p.TargetB * 0.98 - otros);
                    vK = Math.Max(16, (int)Math.Floor(vK * videoMeta / videoReal));
                }
                noCupo = bytes > p.TargetB;
                if (noCupo)
                    nota = Tx.T("noCupoNota", Tam(p.TargetB), Tam(bytes)) + (nota != null ? " " + nota : "");
                File.Move(parcial, salida);
                return salida;
            }
            finally
            {
                try { if (File.Exists(parcial)) File.Delete(parcial); } catch (Exception) { }
                foreach (string f in new[] { passlog + "-0.log", passlog + "-0.log.mbtree", passlog + "-0.log.temp", passlog + "-0.log.mbtree.temp" })
                    try { if (File.Exists(f)) File.Delete(f); } catch (Exception) { }
            }
        }

        /* ---------- Editor: miniaturas y forma de onda ---------- */

        // Corre ffmpeg y devuelve su salida estándar en binario (cuadros o muestras crudas). null si no salió nada.
        // No pasa por "actual": Cancelar() detiene la compresión, no las vistas del editor.
        public static byte[] EjecutarBytes(IList<string> args, long maxBytes)
        {
            var psi = new ProcessStartInfo(Ffmpeg);
            var parts = new List<string>();
            foreach (string a in args) parts.Add(Quote(a));
            psi.Arguments = string.Join(" ", parts);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.RedirectStandardInput = true;
            using (var p = new Process())
            {
                p.StartInfo = psi;
                p.ErrorDataReceived += (s, e) => { };
                p.Start();
                p.BeginErrorReadLine();
                var ms = new MemoryStream();
                var buf = new byte[1 << 16];
                Stream st = p.StandardOutput.BaseStream;
                int n;
                while ((n = st.Read(buf, 0, buf.Length)) > 0)
                {
                    ms.Write(buf, 0, n);
                    if (ms.Length > maxBytes) { try { p.Kill(); } catch (Exception) { } break; }
                }
                p.WaitForExit();
                return ms.Length > 0 ? ms.ToArray() : null;
            }
        }

        // Miniaturas para la pista de video: n cuadros de "alto" px, en bgr24 uno tras otro. Solo decodifica cuadros clave,
        // así que es rápido aun en videos largos. null si este ffmpeg no puede (le falta rawvideo) o el video no se lee.
        public static byte[] Miniaturas(string path, InfoVideo m, int n, int alto, out int ancho)
        {
            ancho = Math.Max(2, (int)Math.Round(m.W * (double)alto / m.H / 2, MidpointRounding.AwayFromZero) * 2);
            var args = new List<string> { "-hide_banner", "-nostats", "-skip_frame", "nokey", "-i", path, "-an", "-sn",
                "-vf", "fps=" + n.ToString(CI) + "/" + Math.Max(0.1, m.Dur).ToString("0.###", CI) + ",scale=" + ancho + ":" + alto + ",format=bgr24",
                "-frames:v", n.ToString(CI), "-f", "rawvideo", "pipe:1" };
            return EjecutarBytes(args, (long)ancho * alto * 3 * (n + 1));
        }

        // Forma de onda para la pista de audio: el pico de cada uno de n tramos iguales del tiempo, de 0 a 1
        // (normalizado al más alto, como en la web). null si no hay audio o este ffmpeg no puede (le falta s16le).
        public static float[] Onda(string path, double dur, int n)
        {
            const int Hz = 2000;
            var args = new List<string> { "-hide_banner", "-nostats", "-i", path, "-vn", "-sn", "-ac", "1", "-ar", Hz.ToString(CI), "-c:a", "pcm_s16le", "-f", "s16le", "pipe:1" };
            byte[] b = EjecutarBytes(args, (long)(Hz * 2 * (dur + 5)) + 1000000);
            if (b == null || b.Length < 2 || dur <= 0) return null;
            var picos = new float[n];
            int muestras = b.Length / 2;
            for (int i = 0; i < muestras; i++)
            {
                int k = (int)(i / (double)Hz / dur * n);
                if (k >= n) k = n - 1;
                float v = Math.Abs(BitConverter.ToInt16(b, i * 2) / 32768f);
                if (v > picos[k]) picos[k] = v;
            }
            float max = picos.Max();
            if (max > 0) for (int k = 0; k < n; k++) picos[k] /= max;
            return picos;
        }

        static string UltimoError(List<string> errs)
        {
            var sel = new List<string>();
            lock (errs)
                foreach (string l in errs)
                    if (Regex.IsMatch(l, "error|invalid|failed|cannot|not supported|no such", RegexOptions.IgnoreCase)) sel.Add(l.Trim());
            if (sel.Count > 2) sel = sel.GetRange(sel.Count - 2, 2);
            return Tx.T("ffmpegFallo") + (sel.Count > 0 ? ": " + string.Join(" · ", sel) : ".");
        }
    }
}
