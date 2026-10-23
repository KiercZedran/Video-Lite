// Zedran Aero para las apps de escritorio gratuitas de Zedran Labs (WinForms, .NET Framework 4.8, C# 5). Versión 1.0 (2026-10-22).
// El mismo estilo de zedran-aero.css (versión 1.1): la foto de fondo difuminada, paneles de vidrio con barra de título
// azul, los cinco tipos de botón, opciones en píldora, casilla, deslizador verde con bolita blanca, barra de avance verde
// y enlaces con subrayado verde. Full Aero en tema claro y Dark Aero en oscuro.
// En la app no van las burbujas, las partículas ni el engrane: la ventana no se anima.
//
// Es genérico: no depende de ningún proyecto. Se copia tal cual a la app y se compila con ella, junto con los fondos
// incrustados (ver el Anexo 6 del estándar y ejemplo-escritorio/):
//   csc ... ZedranAero.cs "/resource:fondos\aero_2.jpg,ZedranAero.aero_2.jpg" "/resource:fondos\vista_gris.jpg,ZedranAero.vista_gris.jpg"
// En el código de la app: using ZedranAero; y la ventana hereda de VentanaAero.
// Si cambian los colores de zedran-aero.css, se cambian también aquí (Paleta y los colores del dibujo) y se sube la versión.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ZedranAero
{
    // [PRINCIPAL] la acción principal (procesar, reproducir) · [ACCIÓN] lo que se hace con el contenido (editar, cancelar)
    // y la opción elegida · [SECUNDARIO] especiales (descargar, abrir el resultado, el café) · [TERCIARIO] poco importantes
    // (quitar, copiar, deshacer, +/-) · [HERRAMIENTA] cambian la ventana, no el contenido (idioma, tema): orbe azul.
    public enum TipoBoton { Principal, Accion, Secundario, Terciario, Herramienta }
    // Lo que lleva un orbe [HERRAMIENTA]: una sigla (EN/ES, la de la propiedad Sigla) o la luna/el sol del tema
    public enum IconoBoton { Ninguno, Sigla, Tema }

    // Tokens de zedran-aero.css (los de §2 del estándar). Los controles de Windows que no pueden ser de vidrio (listas,
    // campos, el visor) usan los sólidos: Bg y Surface son el papel, Line y LineStrong imitan --line sobre ese papel.
    // Fondo es --ae-bgc (se ve solo mientras se prepara la foto) y Visor, --visor. El texto "de acento" es Ink, como en la web.
    public static class Paleta
    {
        public static Color Fondo, Bg, Surface, Surface2, Line, LineStrong, Ink, Ink2, Ink3, Accent, AccentSoft, Amber, AmberSoft, Danger, Visor;
        public static bool Oscuro { get; private set; }
        static Paleta() { Aplicar(false); }

        public static void Aplicar(bool oscuro)
        {
            Oscuro = oscuro;
            if (!oscuro)
            {
                Fondo = H("#56c1f2"); Bg = H("#fbfdff"); Surface = H("#eef6fc"); Surface2 = H("#dcebf6"); Line = H("#b5cee1"); LineStrong = H("#81aac8");
                Ink = H("#06304f"); Ink2 = H("#2a587a"); Ink3 = H("#36617f"); Accent = H("#22b4e8"); AccentSoft = H("#d3eef9");
                Amber = H("#8a5800"); AmberSoft = H("#fbefd6"); Danger = H("#c0392b"); Visor = H("#021a2e");
            }
            else
            {
                Fondo = H("#05080c"); Bg = H("#061a2c"); Surface = H("#0a2238"); Surface2 = H("#12324f"); Line = H("#264b64"); LineStrong = H("#437490");
                Ink = H("#eaf6ff"); Ink2 = H("#bcd8ec"); Ink3 = H("#9fc2da"); Accent = H("#3ec3f2"); AccentSoft = H("#0f3a55");
                Amber = H("#f2a51a"); AmberSoft = H("#33301f"); Danger = H("#f08a7a"); Visor = H("#010b16");
            }
        }

        // Todos los tokens, en orden: para cambiar de tema se cambia cada color viejo por el mismo token del tema nuevo.
        public static Color[] Tokens() { return new[] { Fondo, Bg, Surface, Surface2, Line, LineStrong, Ink, Ink2, Ink3, Accent, AccentSoft, Amber, AmberSoft, Danger, Visor }; }

        // Colores del dibujo, con transparencia (sobre el vidrio)
        public static Color Foco { get { return H("#ffb21f"); } }                                              // --focus
        public static Color Subrayado { get { return H("#6fd23f"); } }                                         // --enlace-subrayado
        public static Color Papel { get { return Oscuro ? A(31, 110, 175, 235) : A(89, 250, 253, 255); } }        // --ae-paper
        public static Color LineaVidrio { get { return Oscuro ? A(56, 140, 220, 255) : A(61, 0, 80, 140); } }     // --line
        public static Color LineaFuerteVidrio { get { return Oscuro ? A(112, 140, 220, 255) : A(117, 0, 80, 140); } }   // --line-strong

        internal static Color H(string h) { return ColorTranslator.FromHtml(h); }
        static Color A(int a, int r, int g, int b) { return Color.FromArgb(a, r, g, b); }
    }

    // La ventana: pinta la foto (fija, difuminada) y las sombras de los paneles; todo lo demás va transparente encima.
    // La ventana de la app hereda de aquí. Para cambiar de tema: CambiarTema(true/false).
    public class VentanaAero : Form
    {
        public VentanaAero()
        {
            Font = new Font("Segoe UI", 10f);
            BackColor = Paleta.Fondo;
            ForeColor = Paleta.Ink;
            DoubleBuffered = true;
            ResizeRedraw = true;
            SizeChanged += (s, e) => Invalidate(true);   // la foto llena la ventana: con otro tamaño cambia debajo de todo
            HandleCreated += (s, e) => Aero.DetallesWindows(this);
        }

        protected override void OnLoad(EventArgs e) { Aero.Preparar(this); base.OnLoad(e); }
        protected override void OnPaintBackground(PaintEventArgs e) { Aero.PintarFondo(e.Graphics, this, e.ClipRectangle); }
        public void CambiarTema(bool oscuro) { Aero.CambiarTema(this, oscuro); }
    }

    public static class Aero
    {
        public static float Esc = 1f;   // escala de la pantalla (96 ppp = 1)
        static Aero() { try { using (var g = Graphics.FromHwnd(IntPtr.Zero)) Esc = g.DpiX / 96f; } catch (Exception) { } }
        public static float E(float px) { return px * Esc; }
        public static int Ei(float px) { return (int)Math.Round(px * Esc); }
        static Color H(string h) { return Paleta.H(h); }
        static Color A(int a, int r, int g, int b) { return Color.FromArgb(a, r, g, b); }
        static bool O { get { return Paleta.Oscuro; } }
        public static Color Mezcla(Color a, Color b, float t)
        {
            return Color.FromArgb((int)Math.Round(a.R + (b.R - a.R) * t), (int)Math.Round(a.G + (b.G - a.G) * t), (int)Math.Round(a.B + (b.B - a.B) * t));
        }
        static Color Por(Color c, float k) { return Color.FromArgb(c.A, Math.Min(255, (int)(c.R * k)), Math.Min(255, (int)(c.G * k)), Math.Min(255, (int)(c.B * k))); }

        static Color Canto { get { return O ? A(56, 140, 220, 255) : A(89, 255, 255, 255); } }         // --ae-edge
        static Color Anillo { get { return O ? A(56, 120, 200, 255) : A(128, 255, 255, 255); } }       // el anillo de 1 px de --ae-shadow
        static Color Vidrio1 { get { return O ? A(158, 24, 66, 104) : A(140, 255, 255, 255); } }       // --ae-glass
        static Color Vidrio2 { get { return O ? A(107, 6, 28, 52) : A(77, 255, 255, 255); } }
        static Color SombraPanel { get { return O ? A(40, 0, 0, 0) : A(20, 0, 40, 90); } }             // una capa de --ae-shadow
        static Color FondoTexto { get { return O ? H("#10304a") : H("#cfe6f4"); } }   // detrás de lo desactivado, aproximado

        /* ---------- Fondo: la foto, difuminada, fija a la ventana ---------- */

        // Las fotos: si la app no pone otras, se toman de los recursos incrustados (ZedranAero.aero_2.jpg y
        // ZedranAero.vista_gris.jpg); sin ellos, va el degradado de respaldo de --ae-bg.
        public static Image FotoClaro, FotoOscuro;
        public static string RecursoClaro = "ZedranAero.aero_2.jpg", RecursoOscuro = "ZedranAero.vista_gris.jpg";
        public static float BlurClaro = 6, BlurOscuro = 5, BlurVidrio = 10, SaturacionVidrio = 1.6f;   // --ae-fondo-blur, --ae-blur, saturate()

        static readonly Dictionary<bool, Bitmap> fuentes = new Dictionary<bool, Bitmap>();
        static Bitmap fondo;
        static TextureBrush pincelVidrio;   // el vidrio: la foto más difuminada y saturada, como backdrop-filter
        static Size tam;
        static bool tamOscuro;
        static readonly Dictionary<Control, bool> vidrios = new Dictionary<Control, bool>();   // panel → lleva franjas
        static readonly List<Control> conSombra = new List<Control>();

        static Bitmap Fuente(bool oscuro)
        {
            Bitmap b;
            if (fuentes.TryGetValue(oscuro, out b)) return b;
            b = null;
            try
            {
                Image propia = oscuro ? FotoOscuro : FotoClaro;
                if (propia != null) b = new Bitmap(propia);
                else
                {
                    Assembly asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
                    using (Stream s = asm.GetManifestResourceStream(oscuro ? RecursoOscuro : RecursoClaro))
                        if (s != null) using (var img = Image.FromStream(s)) b = new Bitmap(img);
                }
            }
            catch (Exception) { b = null; }
            fuentes[oscuro] = b;
            return b;
        }

        static void Respaldo(Graphics g, int w, int h)
        {
            using (var lg = new LinearGradientBrush(new Rectangle(0, -1, w, h + 2), Color.Black, Color.Black, 90f))
            {
                lg.InterpolationColors = O ? Mezcla(new[] { H("#010307"), H("#020a14"), H("#04162a") }, new[] { 0f, .55f, 1f })
                                           : Mezcla(new[] { H("#0b7fd6"), H("#56c1f2"), H("#c8f0ff"), H("#c8f0ff") }, new[] { 0f, .4f, .7f, 1f });
                g.FillRectangle(lg, 0, 0, w, h);
            }
        }

        // El fondo y el vidrio al tamaño de la ventana. Se trabaja a un tercio del tamaño: lo borroso no pierde nada y así no
        // se nota al cambiar el tamaño de la ventana.
        static void Asegurar(Size cliente)
        {
            cliente = new Size(Math.Max(1, cliente.Width), Math.Max(1, cliente.Height));
            if (fondo != null && tam == cliente && tamOscuro == O) return;
            Liberar();
            tam = cliente; tamOscuro = O;
            const int div = 3;
            int w = Math.Max(1, (cliente.Width + div - 1) / div), h = Math.Max(1, (cliente.Height + div - 1) / div);
            int[] px = new int[w * h];
            using (var chico = new Bitmap(w, h, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(chico))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    Bitmap src = Fuente(O);
                    if (src != null)
                    {
                        float k = Math.Max(w / (float)src.Width, h / (float)src.Height);   // background-size: cover, centrada
                        float sw = w / k, sh = h / k;
                        using (var ia = new ImageAttributes())
                        {
                            ia.SetWrapMode(WrapMode.TileFlipXY);
                            g.DrawImage(src, new Rectangle(0, 0, w, h), (src.Width - sw) / 2, (src.Height - sh) / 2, sw, sh, GraphicsUnit.Pixel, ia);
                        }
                    }
                    else Respaldo(g, w, h);
                }
                Leer(chico, px);
            }
            float s = Esc / div;
            int[] f = Desenfocar(px, w, h, (O ? BlurOscuro : BlurClaro) * s);
            int[] v = Saturar(Desenfocar(f, w, h, BlurVidrio * s), SaturacionVidrio);
            fondo = Agrandar(f, w, h, cliente);
            using (Bitmap vb = Agrandar(v, w, h, cliente)) pincelVidrio = new TextureBrush(vb, WrapMode.Clamp);   // el pincel guarda su copia
        }

        static void Liberar()
        {
            if (pincelVidrio != null) pincelVidrio.Dispose();
            if (fondo != null) fondo.Dispose();
            pincelVidrio = null; fondo = null;
        }

        static void Leer(Bitmap b, int[] px)
        {
            BitmapData d = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            for (int y = 0; y < b.Height; y++) Marshal.Copy(IntPtr.Add(d.Scan0, y * d.Stride), px, y * b.Width, b.Width);
            b.UnlockBits(d);
        }

        static Bitmap Agrandar(int[] px, int w, int h, Size cliente)
        {
            using (var chico = new Bitmap(w, h, PixelFormat.Format32bppArgb))
            {
                BitmapData d = chico.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                for (int y = 0; y < h; y++) Marshal.Copy(px, y * w, IntPtr.Add(d.Scan0, y * d.Stride), w);
                chico.UnlockBits(d);
                var b = new Bitmap(cliente.Width, cliente.Height, PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(b))
                using (var ia = new ImageAttributes())
                {
                    ia.SetWrapMode(WrapMode.TileFlipXY);   // sin orilla oscura
                    g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(chico, new Rectangle(0, 0, cliente.Width, cliente.Height), 0, 0, w, h, GraphicsUnit.Pixel, ia);
                }
                return b;
            }
        }

        // Desenfoque gaussiano aproximado con tres pasadas de caja (horizontal y vertical), con la orilla repetida.
        static int[] Desenfocar(int[] px, int w, int h, float sigma)
        {
            if (sigma < 0.3f) return (int[])px.Clone();
            int[] a = (int[])px.Clone(), b = new int[px.Length];
            foreach (int r in Cajas(sigma, 3))
            {
                if (r < 1) continue;
                Caja(a, b, w, h, r, true);
                Caja(b, a, w, h, r, false);
            }
            return a;
        }

        static int[] Cajas(double sigma, int n)
        {
            double ideal = Math.Sqrt(12 * sigma * sigma / n + 1);
            int wl = (int)Math.Floor(ideal); if (wl % 2 == 0) wl--;
            int wu = wl + 2;
            int m = (int)Math.Round((12 * sigma * sigma - n * wl * wl - 4 * n * wl - 3 * n) / (-4.0 * wl - 4));
            var r = new int[n];
            for (int i = 0; i < n; i++) r[i] = ((i < m ? wl : wu) - 1) / 2;
            return r;
        }

        static void Caja(int[] src, int[] dst, int w, int h, int r, bool horizontal)
        {
            int largo = horizontal ? w : h, lineas = horizontal ? h : w;
            int paso = horizontal ? 1 : w;
            int div = r + r + 1;
            for (int l = 0; l < lineas; l++)
            {
                int ini = horizontal ? l * w : l;
                int sr = 0, sg = 0, sb = 0;
                for (int k = -r; k <= r; k++)
                {
                    int p = src[ini + Math.Max(0, Math.Min(largo - 1, k)) * paso];
                    sr += (p >> 16) & 255; sg += (p >> 8) & 255; sb += p & 255;
                }
                for (int i = 0; i < largo; i++)
                {
                    dst[ini + i * paso] = unchecked((int)0xFF000000) | ((sr / div) << 16) | ((sg / div) << 8) | (sb / div);
                    int pa = src[ini + Math.Min(largo - 1, i + r + 1) * paso], pq = src[ini + Math.Max(0, i - r) * paso];
                    sr += ((pa >> 16) & 255) - ((pq >> 16) & 255);
                    sg += ((pa >> 8) & 255) - ((pq >> 8) & 255);
                    sb += (pa & 255) - (pq & 255);
                }
            }
        }

        // saturate() de CSS
        static int[] Saturar(int[] px, float s)
        {
            float rr = .213f + .787f * s, rg = .715f - .715f * s, rb = .072f - .072f * s;
            float gr = .213f - .213f * s, gg = .715f + .285f * s, gb = .072f - .072f * s;
            float br = .213f - .213f * s, bg = .715f - .715f * s, bb = .072f + .928f * s;
            for (int i = 0; i < px.Length; i++)
            {
                int p = px[i], r = (p >> 16) & 255, g = (p >> 8) & 255, b = p & 255;
                int nr = Math.Max(0, Math.Min(255, (int)(rr * r + rg * g + rb * b)));
                int ng = Math.Max(0, Math.Min(255, (int)(gr * r + gg * g + gb * b)));
                int nb = Math.Max(0, Math.Min(255, (int)(br * r + bg * g + bb * b)));
                px[i] = unchecked((int)0xFF000000) | (nr << 16) | (ng << 8) | nb;
            }
            return px;
        }

        // Dónde está el área de un control dentro de la ventana (o dentro de "hasta").
        static Point Desplazamiento(Control c, Control hasta)
        {
            int x = 0, y = 0;
            for (Control k = c; k != null && k != hasta && !(k is Form); k = k.Parent) { x += k.Left; y += k.Top; }
            return new Point(x, y);
        }

        // La ventana pinta la foto y, debajo de cada panel de vidrio, su sombra (solo afuera del panel, como box-shadow).
        public static void PintarFondo(Graphics g, Form f, Rectangle clip)
        {
            Asegurar(f.ClientSize);
            Rectangle r = Rectangle.Intersect(clip, new Rectangle(Point.Empty, tam));
            if (r.Width > 0 && r.Height > 0)
            {
                var st = g.Save();
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.CompositingMode = CompositingMode.SourceCopy;
                g.DrawImage(fondo, r, r, GraphicsUnit.Pixel);
                g.Restore(st);
            }
            foreach (var c in conSombra) if (c.Visible && c.FindForm() == f) Sombra(g, c, f, clip);
        }

        // 0 30px 60px -22px: una sombra suave que asoma abajo del panel (capas que se suman, como un desenfoque)
        static void Sombra(Graphics g, Control c, Form f, Rectangle clip)
        {
            Point o = Desplazamiento(c, f);
            var cuerpo = new RectangleF(o.X + 1.5f, o.Y + 1.5f, c.Width - 3f, c.Height - 3f);
            float dy = E(30), menos = E(22), borroso = E(30), radio = E(18);
            var s = new RectangleF(cuerpo.X + menos, cuerpo.Y + menos + dy, cuerpo.Width - 2 * menos, cuerpo.Height - 2 * menos);
            if (s.Width <= 0 || s.Height <= 0 || !RectangleF.Inflate(s, borroso, borroso).IntersectsWith(clip)) return;
            var st = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var p = Redondo(cuerpo, radio)) g.SetClip(p, CombineMode.Exclude);
            const int capas = 10;
            using (var b = new SolidBrush(SombraPanel))
                for (int i = 0; i <= capas; i++)
                {
                    float d = -borroso + i * 2 * borroso / capas;
                    var rr = RectangleF.Inflate(s, d, d);
                    if (rr.Width <= 1 || rr.Height <= 1) continue;
                    using (var p = Redondo(rr, Math.Max(0, radio - menos + d))) g.FillPath(b, p);
                }
            g.Restore(st);
        }

        /* ---------- Paneles de vidrio ([R-PANEL]) ---------- */

        // Vuelve vidrio a un contenedor (Panel, TableLayoutPanel, FlowLayoutPanel): transparente, con su pintado y sombra.
        // Para que la barra de título llegue a las orillas, el panel lleva Padding(2) y el TituloAero va arriba, sin margen.
        public static void Vidrio(Control p, bool franjas, bool sombra = true)
        {
            Estilos(p, ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor);
            p.BackColor = Color.Transparent;
            vidrios[p] = franjas;
            if (sombra) conSombra.Add(p);
            p.Paint += (s, e) => PintarVidrio(e.Graphics, p);
            p.Disposed += (s, e) => { vidrios.Remove(p); conSombra.Remove(p); };
        }

        public static void PintarVidrio(Graphics g, Control p)
        {
            Form f = p.FindForm();
            if (f == null || p.Width < 4 || p.Height < 4) return;
            Asegurar(f.ClientSize);
            Point o = Desplazamiento(p, f);
            var st = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var cuerpo = new RectangleF(1.5f, 1.5f, p.Width - 3f, p.Height - 3f);
            float radio = E(18);
            var caja = new Rectangle(0, 0, p.Width, p.Height);
            using (var camino = Redondo(cuerpo, radio))
            {
                pincelVidrio.ResetTransform();
                pincelVidrio.TranslateTransform(-o.X, -o.Y);
                g.FillPath(pincelVidrio, camino);
                using (var lg = new LinearGradientBrush(caja, Vidrio1, Vidrio2, 70f)) g.FillPath(lg, camino);   // 160deg
                bool franjas;
                if (vidrios.TryGetValue(p, out franjas) && franjas) using (var fr = Franjas(caja)) g.FillPath(fr, camino);
                using (var pen = new Pen(Paleta.LineaVidrio)) g.DrawPath(pen, camino);
            }
            using (var adentro = Redondo(RectangleF.Inflate(cuerpo, -1, -1), radio - 1)) using (var pen = new Pen(Canto)) g.DrawPath(pen, adentro);
            using (var afuera = Redondo(new RectangleF(.5f, .5f, p.Width - 1f, p.Height - 1f), radio + 1)) using (var pen = new Pen(Anillo)) g.DrawPath(pen, afuera);
            g.Restore(st);
        }

        // --ae-franjas: las franjas de luz en diagonal (118deg)
        static LinearGradientBrush Franjas(Rectangle caja)
        {
            var lg = new LinearGradientBrush(caja, Color.Transparent, Color.Transparent, 28f);
            if (O)
            {
                int[] al = { 0, 0, 26, 8, 0, 0, 15, 0, 0, 20, 5, 0, 0 };
                float[] pos = { 0, .06f, .11f, .17f, .21f, .25f, .28f, .31f, .60f, .66f, .71f, .75f, 1 };
                lg.InterpolationColors = Alfas(al, pos, 160, 220, 255);
            }
            else
            {
                int[] al = { 0, 0, 66, 15, 0, 0, 36, 0, 0, 46, 13, 0, 0, 26, 0, 0 };
                float[] pos = { 0, .06f, .11f, .17f, .21f, .25f, .28f, .31f, .60f, .66f, .71f, .75f, .82f, .85f, .88f, 1 };
                lg.InterpolationColors = Alfas(al, pos, 255, 255, 255);
            }
            return lg;
        }

        static ColorBlend Alfas(int[] al, float[] pos, int r, int g, int b)
        {
            var cs = new Color[al.Length];
            for (int i = 0; i < al.Length; i++) cs[i] = Color.FromArgb(al[i], r, g, b);
            return new ColorBlend { Colors = cs, Positions = pos };
        }

        static ColorBlend Mezcla(Color[] cs, float[] pos) { return new ColorBlend { Colors = cs, Positions = pos }; }

        // Pinta lo que se vería detrás de "c" si fuera transparente hasta su panel de vidrio (para lo que vive dentro de
        // algo opaco, como una zona para soltar dentro de un visor).
        public static void PintarDetras(Graphics g, Control c)
        {
            Control v = c.Parent;
            while (v != null && !vidrios.ContainsKey(v)) v = v.Parent;
            if (v == null) return;
            Point o = Desplazamiento(c, v);
            var st = g.Save();
            g.TranslateTransform(-o.X, -o.Y);
            PintarVidrio(g, v);
            g.Restore(st);
        }

        // Zona para soltar (.drop): tarjeta de papel con orilla punteada del color de acento y un brillo arriba.
        // Uso: panel.Paint += (s, e) => Aero.PintarZonaSoltar(e.Graphics, panel);
        public static void PintarZonaSoltar(Graphics g, Control c)
        {
            PintarDetras(g, c);
            var st = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(E(6), E(6), c.Width - E(12) - 1, c.Height - E(12) - 1);
            using (var p = Redondo(r, E(10)))
            {
                using (var b = new SolidBrush(Paleta.Papel)) g.FillPath(b, p);
                using (var e = new GraphicsPath())
                {
                    e.AddEllipse(r.X - r.Width * .2f, r.Y - r.Height * .7f, r.Width * 1.4f, r.Height * 1.4f);
                    using (var pg = new PathGradientBrush(e))
                    {
                        pg.CenterPoint = new PointF(r.X + r.Width / 2, r.Y);
                        pg.InterpolationColors = Mezcla(new[] { Color.FromArgb(0, Paleta.Accent), Color.FromArgb(51, Paleta.Accent) }, new[] { 0f, 1f });
                        var st2 = g.Save(); g.SetClip(p, CombineMode.Intersect); g.FillPath(pg, e); g.Restore(st2);
                    }
                }
                using (var pen = new Pen(Color.FromArgb(153, Paleta.Accent), E(2)) { DashStyle = DashStyle.Dash }) g.DrawPath(pen, p);
            }
            g.Restore(st);
        }

        // Tarjeta de adentro (.ae-tarjeta), solo la orilla: para una lista de Windows (sólida) dentro del vidrio
        public static void Marco(Graphics g, Rectangle r, float radio)
        {
            var st = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var p = Redondo(new RectangleF(r.X + .5f, r.Y + .5f, r.Width - 1f, r.Height - 1f), radio)) using (var pen = new Pen(Paleta.LineaFuerteVidrio)) g.DrawPath(pen, p);
            g.Restore(st);
        }

        // Una lista de Windows lista para ir en un panel de vidrio: sólida, encabezado con la paleta y esquinas redondas,
        // dentro de una tarjeta con su orilla. Devuelve la tarjeta (es la que se agrega al panel).
        public static Panel TarjetaLista(ListView lista)
        {
            var tarjeta = new Panel { Dock = DockStyle.Fill, Padding = new Padding(1), Margin = new Padding(16, 0, 16, 16) };
            tarjeta.Paint += (s, e) => Marco(e.Graphics, tarjeta.ClientRectangle, E(8));
            lista.Dock = DockStyle.Fill;
            lista.BorderStyle = BorderStyle.None;
            lista.BackColor = Paleta.Surface;
            lista.ForeColor = Paleta.Ink;
            lista.Resize += (s, e) => Redondear(lista, Ei(7));
            lista.OwnerDraw = true;   // el encabezado con la paleta (en oscuro, el de Windows quedaría blanco)
            lista.DrawColumnHeader += (s, e) =>
            {
                using (var fondo = new SolidBrush(Paleta.Surface2)) e.Graphics.FillRectangle(fondo, e.Bounds);
                using (var linea = new Pen(Paleta.Line)) e.Graphics.DrawLine(linea, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
                using (var f = new Font("Segoe UI Semibold", lista.Font.Size))
                    TextRenderer.DrawText(e.Graphics, e.Header.Text, f, new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height), Paleta.Ink,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            };
            lista.DrawItem += (s, e) => { e.DrawDefault = true; };
            lista.DrawSubItem += (s, e) => { e.DrawDefault = true; };
            // La última columna llena lo que sobra: si no, Windows pinta ese pedazo del encabezado en blanco
            EventHandler llenar = (s, e) =>
            {
                if (lista.Columns.Count == 0) return;
                int otras = 0;
                for (int i = 0; i < lista.Columns.Count - 1; i++) otras += lista.Columns[i].Width;
                int w = lista.ClientSize.Width - otras;
                ColumnHeader ultima = lista.Columns[lista.Columns.Count - 1];
                if (w > Ei(40) && ultima.Width != w) ultima.Width = w;
            };
            lista.Resize += llenar;
            lista.ClientSizeChanged += llenar;
            lista.HandleCreated += llenar;
            tarjeta.Controls.Add(lista);
            return tarjeta;
        }

        // Un campo de Windows (TextBox, NumericUpDown, ComboBox) con el papel sólido de la paleta
        public static T Campo<T>(T c) where T : Control
        {
            c.BackColor = Paleta.Bg;
            c.ForeColor = Paleta.Ink;
            var cb = c as ComboBox;
            if (cb != null) cb.FlatStyle = FlatStyle.Flat;
            return c;
        }

        // El ComboBox plano no dibuja borde: va dentro de un panel de 1 px del color de los bordes fuertes.
        public static Control ConBorde(Control c)
        {
            var p = new Panel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(1), BackColor = Paleta.LineStrong, Margin = c.Margin };
            c.Margin = new Padding(0);
            c.Location = new Point(1, 1);
            p.Controls.Add(c);
            return p;
        }

        [DllImport("gdi32.dll")] static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int w, int h);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr o);

        // Esquinas redondas para un control de Windows: recorta su ventana.
        public static void Redondear(Control c, int radio)
        {
            if (c.Width < 2 || c.Height < 2) return;
            IntPtr h = CreateRoundRectRgn(0, 0, c.Width + 1, c.Height + 1, radio * 2, radio * 2);
            var viejo = c.Region;
            c.Region = Region.FromHrgn(h);
            DeleteObject(h);
            if (viejo != null) viejo.Dispose();
        }

        /* ---------- Formas ---------- */

        public static GraphicsPath Redondo(RectangleF r, float radio)
        {
            var p = new GraphicsPath();
            float d = Math.Max(0, Math.Min(radio * 2, Math.Min(r.Width, r.Height)));
            if (d < 1) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static GraphicsPath RedondoArriba(RectangleF r, float radio)
        {
            var p = new GraphicsPath();
            float d = Math.Max(0, Math.Min(radio * 2, Math.Min(r.Width, r.Height * 2)));
            if (d < 1) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddLine(r.Right, r.Bottom, r.X, r.Bottom);
            p.CloseFigure();
            return p;
        }

        static LinearGradientBrush Vertical(RectangleF r, Color[] cs, float[] pos)
        {
            var lg = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, Math.Max(1, r.Width), r.Height + 2), Color.Black, Color.Black, 90f);
            lg.InterpolationColors = Mezcla(cs, pos);
            return lg;
        }

        /* ---------- Ventana, tema y repintado ---------- */

        static readonly MethodInfo setStyle = typeof(Control).GetMethod("SetStyle", BindingFlags.NonPublic | BindingFlags.Instance);
        public static void Estilos(Control c, ControlStyles s) { setStyle.Invoke(c, new object[] { s, true }); }

        // Todo lo transparente toma su fondo de la ventana (la foto fija). Al moverse un control (acomodo, desplazamiento),
        // Windows copia sus píxeles viejos a la posición nueva: se repinta completo para que la foto no quede corrida.
        // VentanaAero lo llama al cargar; lo que se agrega después queda preparado solo.
        public static void Preparar(Control c)
        {
            if (c is Panel || c is Label) Estilos(c, ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint);
            c.LocationChanged -= AlMoverse; c.LocationChanged += AlMoverse;
            c.ControlAdded -= AlAgregar; c.ControlAdded += AlAgregar;
            foreach (Control h in c.Controls) Preparar(h);
        }
        static void AlAgregar(object s, ControlEventArgs e) { Preparar(e.Control); }
        static void AlMoverse(object s, EventArgs e)
        {
            var c = (Control)s;
            c.Invalidate(true);
            var sc = c.Parent as ScrollableControl;
            if (sc != null && sc.AutoScroll) sc.Invalidate(true);
        }

        // Los botones, radios y casillas de Windows no pintan su fondo transparente cuando se dibujan a mano (quedaría lo
        // que había en el búfer): lo piden aquí a su contenedor, que a su vez lo pide hasta la ventana. "fondo" y "pintura"
        // son InvokePaintBackground e InvokePaint del control (protegidos en Control).
        public static void PintarPadre(Control c, PaintEventArgs e, Action<Control, PaintEventArgs> fondo, Action<Control, PaintEventArgs> pintura)
        {
            Control p = c.Parent;
            if (p == null) return;
            Graphics g = e.Graphics;
            var st = g.Save();
            g.TranslateTransform(-c.Left, -c.Top);
            Rectangle clip = e.ClipRectangle;
            clip.Offset(c.Left, c.Top);
            var pe = new PaintEventArgs(g, clip);
            fondo(p, pe);
            pintura(p, pe);
            g.Restore(st);
        }

        // Cambia de tema: cada color viejo de la paleta por el mismo token del nuevo, en todo el árbol de controles; la
        // barra de título y las barras de desplazamiento de Windows también.
        public static void CambiarTema(Form f, bool oscuro)
        {
            if (oscuro == Paleta.Oscuro) return;
            Color[] antes = Paleta.Tokens();
            Paleta.Aplicar(oscuro);
            Color[] despues = Paleta.Tokens();
            var mapa = new Dictionary<int, Color>();
            for (int i = 0; i < antes.Length; i++) if (!mapa.ContainsKey(antes[i].ToArgb())) mapa[antes[i].ToArgb()] = despues[i];
            f.SuspendLayout();
            Recolorear(f, mapa);
            f.ResumeLayout(true);
            DetallesWindows(f);
            f.Invalidate(true);
        }

        static void Recolorear(Control c, Dictionary<int, Color> mapa)
        {
            Color n;
            if (c.BackColor.A == 255 && mapa.TryGetValue(c.BackColor.ToArgb(), out n)) c.BackColor = n;
            if (mapa.TryGetValue(c.ForeColor.ToArgb(), out n)) c.ForeColor = n;
            var l = c as LinkLabel;
            if (l != null)
            {
                if (mapa.TryGetValue(l.LinkColor.ToArgb(), out n)) l.LinkColor = n;
                if (mapa.TryGetValue(l.ActiveLinkColor.ToArgb(), out n)) l.ActiveLinkColor = n;
                if (mapa.TryGetValue(l.VisitedLinkColor.ToArgb(), out n)) l.VisitedLinkColor = n;
                if (mapa.TryGetValue(l.DisabledLinkColor.ToArgb(), out n)) l.DisabledLinkColor = n;
            }
            var lv = c as ListView;
            if (lv != null)
                foreach (ListViewItem it in lv.Items)
                {
                    if (mapa.TryGetValue(it.ForeColor.ToArgb(), out n)) it.ForeColor = n;
                    if (mapa.TryGetValue(it.BackColor.ToArgb(), out n)) it.BackColor = n;
                }
            foreach (Control h in c.Controls) Recolorear(h, mapa);
        }

        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr hwnd, string app, string idList);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);

        // Barra de título oscura en Dark (Windows 10 20H1+ usa el atributo 20; antes, 19) y barras de desplazamiento oscuras
        // (Windows 10 1809+). En versiones viejas no hace nada.
        public static void DetallesWindows(Form f)
        {
            if (!f.IsHandleCreated) return;
            try
            {
                int v = Paleta.Oscuro ? 1 : 0;
                if (DwmSetWindowAttribute(f.Handle, 20, ref v, 4) != 0) DwmSetWindowAttribute(f.Handle, 19, ref v, 4);
                SetWindowPos(f.Handle, IntPtr.Zero, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0004 | 0x0020);   // NOSIZE|NOMOVE|NOZORDER|FRAMECHANGED
                // Con la ventana abierta, Windows 10 no repinta la barra hasta que cambia de activa a inactiva: se le manda
                // WM_NCACTIVATE dos veces (el estado contrario y luego el real).
                bool activa = Form.ActiveForm == f;
                SendMessage(f.Handle, 0x0086, activa ? IntPtr.Zero : (IntPtr)1, IntPtr.Zero);
                SendMessage(f.Handle, 0x0086, activa ? (IntPtr)1 : IntPtr.Zero, IntPtr.Zero);
            }
            catch (Exception) { }
            BarrasOscuras(f);
        }

        static void BarrasOscuras(Control c)
        {
            var sc = c as ScrollableControl;
            if (c.IsHandleCreated && (c is ListView || c is TreeView || c is TextBoxBase || (sc != null && sc.AutoScroll && !(c is Form))))
                try { SetWindowTheme(c.Handle, Paleta.Oscuro ? "DarkMode_Explorer" : "Explorer", null); } catch (Exception) { }
            foreach (Control h in c.Controls) BarrasOscuras(h);
        }

        public static void Abrir(string destino) { try { Process.Start(destino); } catch (Exception) { } }

        /* ---------- Botones ---------- */

        static Color[] Colores(TipoBoton t, out float[] pos, out Color borde)
        {
            switch (t)
            {
                case TipoBoton.Principal:
                    pos = new[] { 0f, .495f, .505f, .75f, 1f };
                    borde = O ? H("#1d5a15") : H("#2f7f22");
                    return O ? new[] { H("#6cc248"), H("#5cbb31"), H("#23801a"), H("#2c8a1f"), H("#50a83a") }
                             : new[] { H("#86e45e"), H("#76e043"), H("#32a41d"), H("#3fae27"), H("#6ccb4c") };
                case TipoBoton.Terciario:
                    pos = new[] { 0f, .49f, .51f, 1f };
                    borde = O ? H("#3e464d") : H("#a3acb4");
                    return O ? new[] { H("#8e979f"), H("#6e777f"), H("#5a636b"), H("#757e86") }
                             : new[] { H("#ffffff"), H("#f0f2f4"), H("#dadee2"), H("#eef0f2") };
                default:   // Acción y Secundario: --ae-a1 / --ae-a2 partido, con color-mix hacia blanco arriba y abajo
                    pos = new[] { 0f, .49f, .51f, 1f };
                    borde = O ? H("#044169") : H("#055484");
                    return O ? new[] { H("#8ecee7"), H("#1c9ccf"), H("#05578c"), H("#55b5db") }
                             : new[] { H("#91daf4"), H("#22b4e8"), H("#0670b0"), H("#59c7ee") };
            }
        }

        // El cuerpo de un botón (o de una opción). "apretado": presionado; "hundido": la opción elegida.
        public static void PintarBoton(Graphics g, RectangleF r, TipoBoton t, bool circulo, bool apretado, bool hundido)
        {
            float radio = circulo ? r.Width / 2 : r.Height / 2;
            if (!apretado && !hundido)   // sombra de afuera (la de abajo; lo demás no cabe en el control)
                using (var sp = Redondo(new RectangleF(r.X, r.Y + E(1.3f), r.Width, r.Height), radio)) using (var b = new SolidBrush(Color.FromArgb(O ? 90 : 50, 0, 30, 60))) g.FillPath(b, sp);
            using (var p = Redondo(r, radio))
            {
                if (t == TipoBoton.Herramienta) { PintarOrbe(g, r, p); return; }
                float[] pos; Color borde;
                Color[] cs = Colores(t, out pos, out borde);
                using (var lg = Vertical(r, cs, pos)) g.FillPath(lg, p);
                var st = g.Save();
                g.SetClip(p, CombineMode.Intersect);
                if (t == TipoBoton.Principal)   // brillo leve de abajo: radial (55% 60% at 50% 100%)
                    Resplandor(g, r, .55f, .6f, O ? Color.FromArgb(102, 180, 245, 150) : Color.FromArgb(140, 205, 255, 175), .3f);
                if (t == TipoBoton.Principal || t == TipoBoton.Secundario)   // --btn-reflejo: la mitad de arriba
                {
                    var rr = new RectangleF(r.X + .5f, r.Y + .5f, r.Width - 1, r.Height * .4f);
                    using (var pr = Redondo(rr, rr.Height / 2))
                    using (var lg = Vertical(rr, new[] { Color.FromArgb(apretado ? 70 : 128, 255, 255, 255), Color.FromArgb(apretado ? 8 : 15, 255, 255, 255) }, new[] { 0f, 1f }))
                        g.FillPath(lg, pr);
                }
                if (apretado || hundido)   // --btn-presionado / --btn-hundido: sombra de adentro arriba
                {
                    float alto = E(hundido ? 6 : 4);
                    var rs = new RectangleF(r.X, r.Y, r.Width, alto);
                    using (var lg = Vertical(rs, new[] { Color.FromArgb(hundido ? 64 : 50, 0, 0, 0), Color.FromArgb(0, 0, 0, 0) }, new[] { 0f, 1f })) g.FillRectangle(lg, rs);
                }
                g.Restore(st);
                Color arriba, abajo;   // cantos de adentro: la luz de arriba (y en algunos, abajo)
                if (apretado || hundido) { arriba = Color.FromArgb(hundido ? 115 : 77, 255, 255, 255); abajo = Color.FromArgb(hundido ? 89 : 102, 0, 0, 0); }
                else if (t == TipoBoton.Principal) { arriba = abajo = Color.FromArgb(64, 255, 255, 255); }
                else if (t == TipoBoton.Accion) { arriba = Color.FromArgb(153, 255, 255, 255); abajo = Color.FromArgb(64, 255, 255, 255); }
                else if (t == TipoBoton.Secundario) { arriba = Color.FromArgb(255, 255, 255, 255); abajo = Color.FromArgb(102, 255, 255, 255); }
                else { arriba = O ? Color.FromArgb(77, 255, 255, 255) : Color.White; abajo = Color.FromArgb(0, 255, 255, 255); }
                CantoAdentro(g, r, radio, arriba, abajo);
                using (var pen = new Pen(borde)) g.DrawPath(pen, p);
            }
        }

        static void CantoAdentro(Graphics g, RectangleF r, float radio, Color arriba, Color abajo)
        {
            var ri = RectangleF.Inflate(r, -1, -1);
            if (ri.Width < 2 || ri.Height < 2) return;
            using (var p = Redondo(ri, Math.Max(0, radio - 1)))
            using (var lg = Vertical(ri, new[] { arriba, Color.FromArgb(0, arriba), Color.FromArgb(0, abajo), abajo }, new[] { 0f, .35f, .65f, 1f }))
            using (var pen = new Pen(lg, 1f))
                g.DrawPath(pen, p);
        }

        // radial-gradient(rx% ry% at 50% 100%, color, transparente en "corte")
        static void Resplandor(Graphics g, RectangleF r, float rx, float ry, Color c, float corte)
        {
            float ax = r.Width * rx, ay = r.Height * ry;
            using (var e = new GraphicsPath())
            {
                e.AddEllipse(r.X + r.Width / 2 - ax, r.Bottom - ay, 2 * ax, 2 * ay);
                using (var pg = new PathGradientBrush(e))
                {
                    pg.CenterPoint = new PointF(r.X + r.Width / 2, r.Bottom);
                    pg.InterpolationColors = Mezcla(new[] { Color.FromArgb(0, c), Color.FromArgb(0, c), c }, new[] { 0f, corte, 1f });
                    g.FillPath(pg, e);
                }
            }
        }

        // [HERRAMIENTA] el orbe de los botones de atrás y adelante de Windows 7 (--btn-herramienta)
        static void PintarOrbe(Graphics g, RectangleF r, GraphicsPath circulo)
        {
            using (var lg = Vertical(r, new[] { H("#13517a"), H("#13517a"), H("#1771ae"), H("#1d93c3"), H("#2eabd3"), H("#37b2dd") }, new[] { 0f, .5f, .62f, .74f, .86f, 1f }))
                g.FillPath(lg, circulo);
            var st = g.Save();
            g.SetClip(circulo, CombineMode.Intersect);
            Resplandor(g, r, .6f, .3f, Color.FromArgb(230, 119, 220, 255), .2f);
            // Mitad de arriba: radial desde el centro del orbe, casi azul marino al centro y azul pálido en la orilla
            g.SetClip(new RectangleF(r.X - 1, r.Y - 1, r.Width + 2, r.Height / 2 + 1), CombineMode.Intersect);
            using (var b = new SolidBrush(H("#c4d6f5"))) g.FillPath(b, circulo);
            using (var pg = new PathGradientBrush(circulo))
            {
                pg.CenterPoint = new PointF(r.X + r.Width / 2, r.Y + r.Height / 2);
                pg.InterpolationColors = Mezcla(
                    new[] { H("#c4d6f5"), H("#9ab7dc"), H("#87aad2"), H("#7398c0"), H("#6290b8"), H("#4f77a3"), H("#3a6390"), H("#1e476b"), H("#1e476b") },
                    new[] { 0f, .06f, .12f, .24f, .38f, .58f, .70f, .82f, 1f });
                g.FillPath(pg, circulo);
            }
            g.Restore(st);
            using (var adentro = new GraphicsPath())
            {
                adentro.AddEllipse(RectangleF.Inflate(r, -1, -1));
                using (var pen = new Pen(Color.FromArgb(102, 255, 255, 255))) g.DrawPath(pen, adentro);
            }
            using (var pen = new Pen(H("#1d3a66"))) g.DrawPath(pen, circulo);
        }

        // Icono blanco del orbe, con su sombra (rgba(0,25,60,.85)): una sigla, o la luna (claro) / el sol (oscuro)
        public static void PintarIcono(Graphics g, RectangleF r, IconoBoton icono, string sigla)
        {
            float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
            for (int capa = 0; capa < 2; capa++)
            {
                float dy = capa == 0 ? E(1) : 0;
                Color c = capa == 0 ? Color.FromArgb(217, 0, 25, 60) : Color.White;
                if (icono == IconoBoton.Sigla && !string.IsNullOrEmpty(sigla))
                {
                    using (var f = new Font("Segoe UI", E(sigla.Length > 2 ? 10.5f : 12.5f), FontStyle.Bold, GraphicsUnit.Pixel))
                    using (var b = new SolidBrush(c))
                    using (var sf = new StringFormat(StringFormat.GenericTypographic) { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                        g.DrawString(sigla, f, b, new RectangleF(r.X, r.Y + dy + E(.5f), r.Width, r.Height), sf);
                    }
                }
                else if (icono == IconoBoton.Tema && !O)
                    using (var p = Luna(cx, cy + dy, E(8f))) using (var b = new SolidBrush(c)) g.FillPath(b, p);
                else if (icono == IconoBoton.Tema)
                    using (var b = new SolidBrush(c)) using (var pen = new Pen(c, E(1.8f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    {
                        float rc = E(4.2f);
                        g.FillEllipse(b, cx - rc, cy + dy - rc, 2 * rc, 2 * rc);
                        for (int i = 0; i < 8; i++)
                        {
                            double a = i * Math.PI / 4;
                            float c1 = (float)Math.Cos(a), s1 = (float)Math.Sin(a);
                            g.DrawLine(pen, cx + c1 * E(6.8f), cy + dy + s1 * E(6.8f), cx + c1 * E(9.4f), cy + dy + s1 * E(9.4f));
                        }
                    }
            }
        }

        // Luna creciente: el círculo grande menos uno corrido arriba a la derecha
        static GraphicsPath Luna(float cx, float cy, float r1)
        {
            float r2 = r1 * .82f, d = r1 * .62f;
            double fi = -Math.PI / 4;
            float x2 = cx + d * (float)Math.Cos(fi), y2 = cy + d * (float)Math.Sin(fi);
            double a = (r1 * r1 - r2 * r2 + d * d) / (2 * d);
            double alfa = Math.Acos(Math.Max(-1, Math.Min(1, a / r1))) * 180 / Math.PI;
            double fiG = fi * 180 / Math.PI;
            var p = new GraphicsPath();
            // arco de afuera (el del círculo grande lejos del otro) y arco de adentro (el del chico, dentro del grande)
            p.AddArc(cx - r1, cy - r1, 2 * r1, 2 * r1, (float)(fiG + alfa), (float)(360 - 2 * alfa));
            double pmx = cx + r1 * Math.Cos((fiG - alfa) * Math.PI / 180), pmy = cy + r1 * Math.Sin((fiG - alfa) * Math.PI / 180);
            double ppx = cx + r1 * Math.Cos((fiG + alfa) * Math.PI / 180), ppy = cy + r1 * Math.Sin((fiG + alfa) * Math.PI / 180);
            double b1 = Math.Atan2(pmy - y2, pmx - x2) * 180 / Math.PI, b2 = Math.Atan2(ppy - y2, ppx - x2) * 180 / Math.PI;
            double barrido = b2 - b1;
            while (barrido > 0) barrido -= 360;     // en contra del reloj: el lado del círculo chico que mira al centro
            while (barrido <= -360) barrido += 360;
            p.AddArc(x2 - r2, y2 - r2, 2 * r2, 2 * r2, (float)b1, (float)barrido);
            p.CloseFigure();
            return p;
        }

        // Pinta con filtro, como filter: brightness() / grayscale() opacity() de CSS: primero en un mapa de bits y luego encima.
        public static void ConFiltro(Graphics g, Size s, float brillo, bool gris, float alfa, Action<Graphics> pintar)
        {
            if (s.Width < 1 || s.Height < 1) return;
            using (var bmp = new Bitmap(s.Width, s.Height, PixelFormat.Format32bppPArgb))
            {
                using (var gb = Graphics.FromImage(bmp))
                {
                    gb.SmoothingMode = SmoothingMode.AntiAlias;
                    pintar(gb);
                }
                var dest = new Rectangle(0, 0, s.Width, s.Height);
                if (brillo == 1f && !gris && alfa >= 1f) { g.DrawImage(bmp, dest, 0, 0, s.Width, s.Height, GraphicsUnit.Pixel); return; }
                float[][] m;
                if (gris)
                {
                    const float k = .1f;   // grayscale(.9): 10 % del color
                    m = new[]
                    {
                        new[] { .2126f + .7874f * k, .2126f - .2126f * k, .2126f - .2126f * k, 0, 0 },
                        new[] { .7152f - .7152f * k, .7152f + .2848f * k, .7152f - .7152f * k, 0, 0 },
                        new[] { .0722f - .0722f * k, .0722f - .0722f * k, .0722f + .9278f * k, 0, 0 },
                        new[] { 0f, 0, 0, alfa, 0 },
                        new[] { 0f, 0, 0, 0, 1 },
                    };
                }
                else
                    m = new[]
                    {
                        new[] { brillo, 0, 0, 0, 0 },
                        new[] { 0, brillo, 0, 0, 0 },
                        new[] { 0, 0, brillo, 0, 0 },
                        new[] { 0f, 0, 0, alfa, 0 },
                        new[] { 0f, 0, 0, 0, 1 },
                    };
                using (var ia = new ImageAttributes())
                {
                    ia.SetColorMatrix(new ColorMatrix(m));
                    g.DrawImage(bmp, dest, 0, 0, s.Width, s.Height, GraphicsUnit.Pixel, ia);
                }
            }
        }

        // Texto de un botón: blanco con sombra (o el gris del terciario), con el mismo filtro que el cuerpo. GDI (TextRenderer)
        // no pinta con transparencia: la sombra y lo desactivado se mezclan a mano con el color de abajo.
        public static void Texto(Graphics g, string t, Font f, Rectangle r, TipoBoton tipo, float brillo, bool activo, float alfaInactivo, bool gris, bool tachado)
        {
            Color tinta = tipo == TipoBoton.Terciario ? (O ? H("#f2f5f7") : H("#3b454e")) : Color.White;
            Color debajo, sombra; float a; int dy = 1;
            switch (tipo)
            {
                case TipoBoton.Principal: debajo = O ? H("#2c8a1f") : H("#3fae27"); sombra = Color.FromArgb(0, 60, 0); a = .55f; break;
                case TipoBoton.Terciario:
                    debajo = O ? H("#5a636b") : H("#dadee2");
                    if (O) { sombra = Color.Black; a = .35f; dy = -1; } else { sombra = Color.White; a = .8f; }
                    break;
                default: debajo = O ? H("#05578c") : H("#0670b0"); sombra = Color.Black; a = .35f; break;
            }
            Color cs = Mezcla(debajo, sombra, a);
            tinta = Por(tinta, brillo); cs = Por(cs, brillo);
            if (!activo)
            {
                if (gris) { tinta = Gris(tinta); cs = Gris(cs); }
                tinta = Mezcla(FondoTexto, tinta, alfaInactivo); cs = Mezcla(FondoTexto, cs, alfaInactivo);
            }
            const TextFormatFlags fl = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;
            var rs = r; rs.Offset(0, dy);
            TextRenderer.DrawText(g, t, f, rs, cs, fl);
            TextRenderer.DrawText(g, t, f, r, tinta, fl);
            if (tachado)
            {
                Size m = TextRenderer.MeasureText(g, t, f, r.Size, fl);
                int y = r.Y + r.Height / 2 + 1, x = r.X + (r.Width - m.Width) / 2;
                using (var pen = new Pen(tinta, Math.Max(1f, E(1.2f)))) g.DrawLine(pen, x, y, x + m.Width, y);
            }
        }

        static Color Gris(Color c)
        {
            int l = (int)(.2126 * c.R + .7152 * c.G + .0722 * c.B);
            return Mezcla(Color.FromArgb(l, l, l), c, .1f);
        }

        public static void AnilloFoco(Graphics g, RectangleF r, bool circulo)
        {
            var st = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rf = RectangleF.Inflate(r, E(1.5f) - .5f, E(1.5f) - .5f);
            using (var p = Redondo(rf, circulo ? rf.Width / 2 : rf.Height / 2)) using (var pen = new Pen(Paleta.Foco, E(2))) g.DrawPath(pen, p);
            g.Restore(st);
        }

        static readonly Dictionary<string, Font> fuentesTexto = new Dictionary<string, Font>();
        // Letra de los botones: Segoe UI seminegrita (600); la del principal, negrita (700). Los símbolos van como vienen.
        public static Font Letra(Font f, bool negrita)
        {
            if (f.Name.IndexOf("Symbol", StringComparison.OrdinalIgnoreCase) >= 0) return f;
            string k = f.Size.ToString(System.Globalization.CultureInfo.InvariantCulture) + (negrita ? "b" : "s");
            Font r;
            if (!fuentesTexto.TryGetValue(k, out r))
                fuentesTexto[k] = r = negrita ? new Font("Segoe UI", f.Size, FontStyle.Bold, f.Unit) : new Font("Segoe UI Semibold", f.Size, FontStyle.Regular, f.Unit);
            return r;
        }

        // Enlaces: texto del color normal y subrayado verde brillante, grueso (2 px, a 3 px de la línea base)
        public static void Subrayar(Graphics g, LinkLabel l)
        {
            if (string.IsNullOrEmpty(l.Text)) return;
            Font f = l.Font;
            Size t = TextRenderer.MeasureText(g, l.Text, f, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            float em = f.SizeInPoints * g.DpiY / 72f;
            float asc = em * f.FontFamily.GetCellAscent(f.Style) / f.FontFamily.GetEmHeight(f.Style);
            float x = l.Padding.Left + (l.ClientSize.Width - l.Padding.Horizontal - t.Width) / 2f;
            float y = l.Padding.Top + asc + E(3);
            using (var b = new SolidBrush(l.Enabled ? Paleta.Subrayado : Color.FromArgb(110, Paleta.Subrayado)))
                g.FillRectangle(b, x, y, t.Width, Math.Max(1f, E(2)));
        }
    }

    // Barra de título de un panel (.ae-titulo): azul con brillo partido, de orilla a orilla del panel
    public class TituloAero : Label
    {
        public TituloAero()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            AutoSize = false;
            Dock = DockStyle.Fill;
            Margin = new Padding(0);
        }

        int Alto { get { return Font.Height + Aero.Ei(20); } }
        public override Size GetPreferredSize(Size propuesto) { return new Size(Math.Max(1, propuesto.Width), Alto); }
        protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified) { base.SetBoundsCore(x, y, width, Alto, specified); }
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); Height = Alto; }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            var st = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0, 0, Width, Height - 1);
            bool o = Paleta.Oscuro;
            using (var p = Aero.RedondoArriba(r, Aero.E(17)))
            using (var lg = new LinearGradientBrush(new RectangleF(0, -1, Width, Height + 1), Color.Black, Color.Black, 90f))
            {
                lg.InterpolationColors = new ColorBlend
                {
                    Colors = o ? new[] { Paleta.H("#3b84b6"), Paleta.H("#1c679b"), Paleta.H("#06426f"), Paleta.H("#135786") }      // --ae-head
                               : new[] { Paleta.H("#6cc0ee"), Paleta.H("#2f9ad8"), Paleta.H("#0b6fb4"), Paleta.H("#2c8fd0") },
                    Positions = new[] { 0f, .48f, .52f, 1f }
                };
                g.FillPath(lg, p);
            }
            using (var pen = new Pen(Color.FromArgb(89, 0, 50, 100))) g.DrawLine(pen, 0, Height - .5f, Width, Height - .5f);   // --ae-head-linea
            g.Restore(st);
            const TextFormatFlags fl = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            var rt = new Rectangle(Aero.Ei(18), 0, Width - Aero.Ei(36), Height - 1);
            var rs = rt; rs.Offset(0, 1);
            TextRenderer.DrawText(g, Text, Font, rs, o ? Paleta.H("#073a65") : Paleta.H("#0c4d7f"), fl);   // --ae-head-texto
            TextRenderer.DrawText(g, Text, Font, rt, ForeColor, fl);
        }
    }

    // Botón con los cinco tipos. Al pasar el ratón, un poco más de luz; presionado, más oscuro y hundido; desactivado, casi
    // gris y medio transparente; con el teclado, el anillo naranja de foco. Los de herramienta no muestran su texto: el
    // texto es su nombre accesible (y conviene ponerlo también en un ToolTip).
    public class BotonAero : Button
    {
        public TipoBoton Tipo;
        public IconoBoton Icono;
        string sigla;
        public string Sigla { get { return sigla; } set { sigla = value; Invalidate(); } }   // la del orbe con IconoBoton.Sigla
        public bool Redondo;      // círculo (por ejemplo, reproducir); los de herramienta siempre lo son
        bool encima, apretado;

        public BotonAero() : this(TipoBoton.Accion) { }
        public BotonAero(TipoBoton tipo)
        {
            Tipo = tipo;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.Opaque, false);   // ButtonBase es opaco: sin esto no se pinta el fondo (OnPaintBackground)
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(8, 3, 8, 3);
            Margin = new Padding(0, 0, 4, 4);
        }

        bool Circulo { get { return Redondo || Tipo == TipoBoton.Herramienta; } }
        Font Letra { get { return Aero.Letra(Font, Tipo == TipoBoton.Principal); } }

        public override Size GetPreferredSize(Size propuesto)
        {
            int m = Aero.Ei(2) * 2;   // lugar para el anillo de foco
            if (Circulo) { int d = Math.Max(Aero.Ei(36), Math.Max(MinimumSize.Width, MinimumSize.Height) - m); return new Size(d + m, d + m); }
            Size t = TextRenderer.MeasureText(Text, Letra, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            int h = t.Height + Padding.Vertical + Aero.Ei(6);
            int w = t.Width + Padding.Horizontal + h / 3;   // las puntas redondas
            return new Size(Math.Max(MinimumSize.Width, w + m), Math.Max(MinimumSize.Height, h + m));
        }

        protected override void OnMouseEnter(EventArgs e) { encima = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { encima = apretado = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { apretado = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { apretado = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { encima = apretado = false; Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); if (Tipo == TipoBoton.Herramienta) AccessibleName = Text; Invalidate(); }
        protected override void OnPaintBackground(PaintEventArgs e) { Aero.PintarPadre(this, e, InvokePaintBackground, InvokePaint); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            float m = Aero.Ei(2);
            var r = new RectangleF(m + .5f, m + .5f, Width - 2 * m - 1f, Height - 2 * m - 1f);
            if (Circulo) { float d = Math.Min(r.Width, r.Height); r = new RectangleF(r.X + (r.Width - d) / 2, r.Y + (r.Height - d) / 2, d, d); }
            bool activo = Enabled;
            float brillo = !activo ? 1f : apretado ? .88f : encima ? 1.07f : 1f;
            bool ap = apretado && activo;
            Aero.ConFiltro(g, Size, brillo, !activo, activo ? 1f : .6f, gb =>
            {
                Aero.PintarBoton(gb, r, Tipo, Circulo, ap, false);
                if (Tipo == TipoBoton.Herramienta) Aero.PintarIcono(gb, r, Icono, sigla);
            });
            if (Tipo != TipoBoton.Herramienta)
                Aero.Texto(g, Text, Letra, Rectangle.Round(r), Tipo, brillo, activo, .6f, true, false);
            if (Focused && ShowFocusCues) Aero.AnilloFoco(g, r, Circulo);
        }
    }

    // Opción suelta (radio) como píldora (.ae-opciones): las no elegidas son [TERCIARIO]; la elegida, [ACCIÓN] hundida.
    // Desactivada: medio transparente; con Tachada, además tachada (para "no se puede elegir", no para "espera").
    public class OpcionAero : RadioButton
    {
        bool apretado, tachada;
        public bool Tachada { get { return tachada; } set { if (tachada != value) { tachada = value; Invalidate(); } } }

        public OpcionAero()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.Opaque, false);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            AutoSize = true;
            Margin = new Padding(0, 0, 4, 2);
        }
        // Se mide con la negrita (la de la elegida) para que nada se mueva al elegir
        public override Size GetPreferredSize(Size propuesto)
        {
            Size t = TextRenderer.MeasureText(Text, Aero.Letra(Font, true), new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            int m = Aero.Ei(2) * 2;
            return new Size(t.Width + Aero.Ei(26) + m, t.Height + Aero.Ei(10) + m);
        }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { apretado = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { apretado = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnMouseLeave(EventArgs e) { apretado = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaintBackground(PaintEventArgs e) { Aero.PintarPadre(this, e, InvokePaintBackground, InvokePaint); }
        protected override void OnPaint(PaintEventArgs e)
        {
            float m = Aero.Ei(2);
            var r = new RectangleF(m + .5f, m + .5f, Width - 2 * m - 1f, Height - 2 * m - 1f);
            var tipo = Checked ? TipoBoton.Accion : TipoBoton.Terciario;
            bool ap = apretado && Enabled;
            float brillo = ap ? .88f : 1f;
            Aero.ConFiltro(e.Graphics, Size, brillo, false, Enabled ? 1f : .5f, g => Aero.PintarBoton(g, r, tipo, false, ap, Checked));
            Aero.Texto(e.Graphics, Text, Aero.Letra(Font, Checked), Rectangle.Round(r), tipo, brillo, Enabled, .5f, false, !Enabled && tachada);
            if (Focused && ShowFocusCues) Aero.AnilloFoco(e.Graphics, r, false);
        }
    }

    // Casilla con la paleta (accent-color de la web); el texto se parte en líneas si tiene MaximumSize
    public class CasillaAero : CheckBox
    {
        const TextFormatFlags Flags = TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding;
        public CasillaAero()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.Opaque, false);
            BackColor = Color.Transparent;
            AutoSize = true;
        }
        int Lado { get { return (int)Math.Round(Font.Height * 0.8); } }
        public override Size GetPreferredSize(Size propuesto)
        {
            int d = Lado;
            int max = MaximumSize.Width > 0 ? MaximumSize.Width - d - 10 : int.MaxValue;
            Size t = TextRenderer.MeasureText(Text, Font, new Size(Math.Max(20, max), int.MaxValue), Flags);
            return new Size(d + 10 + t.Width, Math.Max(t.Height, d) + 6);
        }
        protected override void OnPaintBackground(PaintEventArgs e) { Aero.PintarPadre(this, e, InvokePaintBackground, InvokePaint); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            int d = Lado, ancho = Math.Max(20, Width - d - 10);
            Size t = TextRenderer.MeasureText(g, Text, Font, new Size(ancho, int.MaxValue), Flags);
            int arriba = Math.Max(0, (Height - t.Height) / 2);
            float x = 1.5f, y = arriba + (Math.Min(t.Height, Font.Height) - d) / 2f;   // a la altura de la primera línea
            var st = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color borde = !Enabled ? Paleta.Line : Checked ? Paleta.Accent : Paleta.LineStrong;
            using (var b = new SolidBrush(Checked && Enabled ? Paleta.Accent : Paleta.Bg))
            using (var p = new Pen(borde, 1.5f))
            using (var caja = Aero.Redondo(new RectangleF(x, y, d, d), Aero.E(3)))
            { g.FillPath(b, caja); g.DrawPath(p, caja); }
            if (Checked)
                using (var p = new Pen(Enabled ? Color.White : Paleta.LineStrong, 2f))
                    g.DrawLines(p, new[] { new PointF(x + d * 0.2f, y + d * 0.52f), new PointF(x + d * 0.42f, y + d * 0.74f), new PointF(x + d * 0.8f, y + d * 0.28f) });
            g.Restore(st);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(d + 8, arriba, ancho, t.Height), Enabled ? ForeColor : Paleta.Ink3, Flags);
            if (Focused && ShowFocusCues)
                using (var p = new Pen(Paleta.Foco, 2)) g.DrawRectangle(p, new Rectangle(d + 5, Math.Max(1, arriba - 2), Math.Min(t.Width + 6, Width - d - 7), Math.Min(Height - 2, t.Height + 4)));
        }
    }

    // Enlace: el texto del color normal y el subrayado verde. Con Url, la abre al hacer clic.
    public class EnlaceAero : LinkLabel
    {
        public string Url;
        public EnlaceAero()
        {
            AutoSize = true;
            LinkBehavior = LinkBehavior.NeverUnderline;
            LinkColor = ActiveLinkColor = VisitedLinkColor = Paleta.Ink;
            DisabledLinkColor = Paleta.Ink3;
            Padding = new Padding(0, 0, 0, 3);
            Margin = new Padding(0, 2, 7, 0);
        }
        protected override void OnLinkClicked(LinkLabelLinkClickedEventArgs e) { base.OnLinkClicked(e); if (!string.IsNullOrEmpty(Url)) Aero.Abrir(Url); }
        protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); Aero.Subrayar(e.Graphics, this); }
    }

    // Deslizador (input range): ranura, parte llena verde con brillo y bolita blanca. Como TrackBar: Minimum, Maximum,
    // Value, SmallChange, LargeChange; Scroll avisa solo lo que mueve la persona y ValueChanged, todo cambio.
    public class Deslizador : Control
    {
        int min, max = 100, val;
        bool arrastrando;
        public int SmallChange = 1, LargeChange = 10;
        public event EventHandler Scroll, ValueChanged;

        public Deslizador()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
            BackColor = Color.Transparent;
            TabStop = true;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.Slider;
            Width = 200;
        }

        public int Minimum { get { return min; } set { min = value; if (max < min) max = min; Value = val; Invalidate(); } }
        public int Maximum { get { return max; } set { max = value; if (min > max) min = max; Value = val; Invalidate(); } }
        public int Value
        {
            get { return val; }
            set
            {
                int v = Math.Max(min, Math.Min(max, value));
                if (v == val) return;
                val = v;
                Invalidate();
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
                AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
            }
        }

        int Alto { get { return Aero.Ei(26); } }
        protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified) { base.SetBoundsCore(x, y, width, Alto, specified); }

        float Bolita { get { return Aero.E(20); } }
        float X0 { get { return Bolita / 2 + Aero.E(3); } }
        float X1 { get { return Width - Bolita / 2 - Aero.E(3); } }
        float XDe(int v) { return max > min ? X0 + (X1 - X0) * (v - min) / (max - min) : X0; }
        int VDe(int x) { return max > min ? min + (int)Math.Round((x - X0) / Math.Max(1f, X1 - X0) * (max - min)) : min; }

        void PorUsuario(int v) { int antes = val; Value = v; if (val != antes && Scroll != null) Scroll(this, EventArgs.Empty); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Focus();
            arrastrando = true; Capture = true;
            PorUsuario(VDe(e.X));
            Invalidate();
        }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (arrastrando) PorUsuario(VDe(e.X)); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); arrastrando = false; Capture = false; Invalidate(); }
        protected override bool IsInputKey(Keys k)
        {
            Keys b = k & Keys.KeyCode;
            return b == Keys.Left || b == Keys.Right || b == Keys.Up || b == Keys.Down || b == Keys.Home || b == Keys.End || b == Keys.PageUp || b == Keys.PageDown || base.IsInputKey(k);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.KeyCode)
            {
                case Keys.Left: case Keys.Down: PorUsuario(val - SmallChange); break;
                case Keys.Right: case Keys.Up: PorUsuario(val + SmallChange); break;
                case Keys.PageDown: PorUsuario(val - LargeChange); break;
                case Keys.PageUp: PorUsuario(val + LargeChange); break;
                case Keys.Home: PorUsuario(min); break;
                case Keys.End: PorUsuario(max); break;
                default: return;
            }
            e.Handled = true;
        }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override AccessibleObject CreateAccessibilityInstance() { return new Accesible(this); }
        class Accesible : ControlAccessibleObject
        {
            readonly Deslizador d;
            public Accesible(Deslizador d) : base(d) { this.d = d; }
            public override AccessibleRole Role { get { return AccessibleRole.Slider; } }
            public override string Value { get { return d.Value.ToString(System.Globalization.CultureInfo.InvariantCulture); } set { int v; if (int.TryParse(value, out v)) d.PorUsuario(v); } }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            float cy = Height / 2f, b = Bolita, xb = XDe(val);
            bool o = Paleta.Oscuro;
            Aero.ConFiltro(g, Size, 1f, false, Enabled ? 1f : .5f, gb =>
            {
                var rr = new RectangleF(Aero.E(3), cy - Aero.E(4), Width - Aero.E(6) - 1, Aero.E(8));   // la ranura
                using (var p = Aero.Redondo(rr, rr.Height / 2))
                {
                    using (var lg = new LinearGradientBrush(new RectangleF(rr.X, rr.Y - 1, rr.Width, rr.Height + 2), o ? Paleta.H("#2b343c") : Paleta.H("#cfd6dc"), o ? Paleta.H("#48525b") : Paleta.H("#eef1f4"), 90f))
                        gb.FillPath(lg, p);
                    var st = gb.Save();
                    gb.SetClip(p, CombineMode.Intersect);
                    var rl = new RectangleF(rr.X, rr.Y, Math.Max(0, xb - rr.X), rr.Height);   // --slider-lleno hasta la bolita
                    if (rl.Width > 0) using (var lg = new LinearGradientBrush(new RectangleF(rl.X, rl.Y - 1, Math.Max(1, rl.Width), rl.Height + 2), Paleta.H("#86e45e"), Paleta.H("#32a41d"), 90f)) gb.FillRectangle(lg, rl);
                    var rb = new RectangleF(rr.X, rr.Y, rr.Width, rr.Height * .6f);   // --slider-brillo
                    using (var lg = new LinearGradientBrush(new RectangleF(rb.X, rb.Y - 1, rb.Width, rb.Height + 2), Color.FromArgb(o ? 102 : 140, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f)) gb.FillRectangle(lg, rb);
                    using (var pen = new Pen(Color.FromArgb(64, 0, 0, 0))) gb.DrawLine(pen, rr.X + rr.Height / 2, rr.Y + 1.5f, rr.Right - rr.Height / 2, rr.Y + 1.5f);   // sombra de adentro
                    gb.Restore(st);
                    using (var pen = new Pen(o ? Color.FromArgb(128, 0, 0, 0) : Color.FromArgb(89, 20, 60, 30))) gb.DrawPath(pen, p);
                }
                var rc = new RectangleF(xb - b / 2, cy - b / 2, b, b);   // la bolita
                using (var s = new SolidBrush(Color.FromArgb(60, 0, 0, 0))) gb.FillEllipse(s, rc.X, rc.Y + Aero.E(1.5f), rc.Width, rc.Height);
                using (var c = new GraphicsPath())
                {
                    c.AddEllipse(rc);
                    using (var pg = new PathGradientBrush(c))
                    {
                        pg.CenterPoint = new PointF(rc.X + rc.Width / 2, rc.Y + rc.Height * .32f);
                        pg.InterpolationColors = new ColorBlend { Colors = new[] { Paleta.H("#d3d9de"), Paleta.H("#eef1f3"), Color.White, Color.White }, Positions = new[] { 0f, .35f, .62f, 1f } };
                        gb.FillPath(pg, c);
                    }
                    if (arrastrando) using (var os = new SolidBrush(Color.FromArgb(20, 0, 0, 0))) gb.FillPath(os, c);
                    using (var pen = new Pen(Paleta.H("#8f9aa4"))) gb.DrawPath(pen, c);
                }
            });
            if (Focused && ShowFocusCues) Aero.AnilloFoco(g, new RectangleF(xb - b / 2, cy - b / 2, b, b), true);
        }
    }

    // Barra de avance: la verde con brillo de Windows 7 (--ae-progress) en una ranura de papel
    public class BarraAero : ProgressBar
    {
        public BarraAero()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = 12;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            var st = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(.5f, .5f, Width - 1f, Height - 1f);
            using (var p = Aero.Redondo(r, r.Height / 2))
            {
                using (var b = new SolidBrush(Paleta.Papel)) g.FillPath(b, p);
                float k = Maximum > Minimum ? (Value - Minimum) / (float)(Maximum - Minimum) : 0;
                if (k > 0)
                {
                    var rl = new RectangleF(r.X, r.Y, Math.Max(r.Height, r.Width * k), r.Height);
                    using (var pl = Aero.Redondo(rl, rl.Height / 2))
                    using (var lg = new LinearGradientBrush(new RectangleF(rl.X, rl.Y - 1, rl.Width, rl.Height + 2), Color.Black, Color.Black, 90f))
                    {
                        lg.InterpolationColors = new ColorBlend { Colors = new[] { Paleta.H("#d4f7b8"), Paleta.H("#6fd23f"), Paleta.H("#3f9e1a"), Paleta.H("#86e05a") }, Positions = new[] { 0f, .48f, .52f, 1f } };
                        g.FillPath(lg, pl);
                        using (var pen = new Pen(Color.FromArgb(153, 255, 255, 255))) g.DrawLine(pen, rl.X + rl.Height / 2, rl.Y + 1, rl.Right - rl.Height / 2, rl.Y + 1);
                    }
                }
                using (var pen = new Pen(Color.FromArgb(51, 0, 0, 0))) g.DrawLine(pen, r.X + r.Height / 2, r.Y + 1, r.Right - r.Height / 2, r.Y + 1);   // sombra de adentro
                using (var pen = new Pen(Paleta.LineaFuerteVidrio)) g.DrawPath(pen, p);
            }
            g.Restore(st);
        }
    }
}
