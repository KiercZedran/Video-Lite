// Video Lite · Zedran Labs — motor de compresión con ffmpeg nativo.
// Misma lógica que sitio/app.js (plan, contenedor, fase invertida, dos pasadas); si cambias una, cambia la otra.
// C# 5: compila con el csc de .NET Framework 4.8 que trae Windows.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
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
        public bool Voz;                 // «Mejorar voz»
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
        // Devuelve null si no se pudo medir; quien llama debe tratarlo como «puede estar invertido».
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

        public static Plan Planear(InfoVideo m, long size, Ajustes a)
        {
            var p = new Plan();
            p.OutDur = m.Dur / a.Speed;
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

        public static double RecomendarMB(InfoVideo m, long size, Ajustes a)
        {
            double w = m.W, h = m.H, sh = Math.Min(w, h);
            if (a.Res > 0 && sh > a.Res) { double k = a.Res / sh; w *= k; h *= k; }
            double fps = Math.Min(m.Fps > 0 ? m.Fps : 30, 60);
            double vK = 0.08 * w * h * fps / 1000;
            double total = vK + (m.HasAudio && !a.SinAudio ? 128 : 0);
            double mb = total * (m.Dur / a.Speed) / 8000;
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

        // Menos de 1 MB se muestra en KB: «0.0 MB» o «0.03 MB → 0.03 MB» confunden. Igual que fmtMB en app.js.
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

        // «Mejorar voz»: la misma cadena que sitio/app.js (INVARIANTES 19). Quita el zumbido grave, baja el ruido y empareja el volumen.
        public const string Voz = "highpass=f=80,afftdn=nf=-25,dynaudnorm=f=200:g=15";

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
                var errs0 = new List<string>();
                if (p.SoloAudio)
                {
                    // Solo audio: una pasada, calidad fija.
                    var aa = new List<string> { "-hide_banner", "-nostats", "-progress", "pipe:1", "-i", input, "-vn", "-map", "0:a:0" };
                    if (af.Count > 0) aa.AddRange(new[] { "-af", string.Join(",", af) });
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

                Func<int, List<string>> common = vk => new List<string> { "-hide_banner", "-nostats", "-progress", "pipe:1", "-i", input, "-map", "0:v:0", "-vf", string.Join(",", vf),
                    "-c:v", "libx264", "-preset", p.Preset, "-profile:v", "high", "-b:v", vk + "k", "-map_metadata", "-1", "-passlogfile", passlog };

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

                var a1 = common(p.VK); a1.AddRange(new[] { "-pass", "1", "-an", "-f", "null", "NUL" });
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
                    var a2 = common(vK); a2.AddRange(new[] { "-pass", "2" });
                    if (p.HasAudio)
                    {
                        a2.AddRange(new[] { "-map", "0:a:0", "-c:a", "aac", "-b:a", p.AK + "k" });
                        if (mono) a2.AddRange(new[] { "-ac", "1" });
                        if (af.Count > 0) a2.AddRange(new[] { "-af", string.Join(",", af) });
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
