// Video Lite · Zedran Labs — ventana principal (WinForms, .NET Framework 4.8, C# 5).
// Colores = tokens de sitio/styles.css (paleta del Comparador antes/después), en tema claro y oscuro.
// Textos en Textos.cs (español e inglés, se cambian en vivo).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Video Lite")]
[assembly: AssemblyProduct("Video Lite · by Zedran Labs")]
[assembly: AssemblyCompany("Zedran Labs")]
[assembly: AssemblyCopyright("Erick Hernández · Zedran Labs")]
[assembly: AssemblyVersion("0.0.2.6")]
[assembly: AssemblyFileVersion("0.0.2.6")]

namespace VideoLite
{
    // Mismos valores que :root y :root[data-theme="dark"] de sitio/styles.css (contraste verificado con herramientas/contraste.py).
    static class C
    {
        public const string Version = "0.0.2.6";   // v0.0.2.x hasta publicar la versión de arreglos (Erick, 2026-10-14)
        public static Color Bg, Surface, Surface2, Line, LineStrong, Ink, Ink2, Ink3, Accent, AccentText, AccentSoft, Amber, AmberSoft, Danger;
        public static bool Oscuro;
        static C() { Aplicar(false); }

        public static void Aplicar(bool oscuro)
        {
            Oscuro = oscuro;
            if (!oscuro)
            {
                Bg = Hex("#ffffff"); Surface = Hex("#f6f9fd"); Surface2 = Hex("#e9f0f8"); Line = Hex("#d3dde9"); LineStrong = Hex("#76869b");
                Ink = Hex("#0f1b2d"); Ink2 = Hex("#435166"); Ink3 = Hex("#56657b"); Accent = Hex("#1a73c9"); AccentText = Hex("#1767b8");
                AccentSoft = Hex("#eaf4fe"); Amber = Hex("#8a5800"); AmberSoft = Hex("#fbefd6"); Danger = Hex("#c0392b");
            }
            else
            {
                Bg = Hex("#1b2330"); Surface = Hex("#232e3e"); Surface2 = Hex("#2b384b"); Line = Hex("#344257"); LineStrong = Hex("#71849e");
                Ink = Hex("#e6edf6"); Ink2 = Hex("#b4c2d4"); Ink3 = Hex("#9aaabf"); Accent = Hex("#2f6fe0"); AccentText = Hex("#8fb8ff");
                AccentSoft = Hex("#1f3556"); Amber = Hex("#f2a51a"); AmberSoft = Hex("#33301f"); Danger = Hex("#f08a7a");
            }
        }

        public static Color[] Tokens() { return new[] { Bg, Surface, Surface2, Line, LineStrong, Ink, Ink2, Ink3, Accent, AccentText, AccentSoft, Amber, AmberSoft, Danger }; }
        static Color Hex(string h) { return ColorTranslator.FromHtml(h); }
    }

    // Preferencias en %LOCALAPPDATA%\ZedranLabs\VideoLite\ajustes.txt (líneas clave=valor). Solo tema e idioma.
    static class Preferencias
    {
        static string Archivo() { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZedranLabs", "VideoLite", "ajustes.txt"); }

        static Dictionary<string, string> Leer()
        {
            var d = new Dictionary<string, string>();
            try
            {
                if (File.Exists(Archivo()))
                    foreach (string l in File.ReadAllLines(Archivo()))
                    {
                        int i = l.IndexOf('=');
                        if (i > 0) d[l.Substring(0, i).Trim()] = l.Substring(i + 1).Trim();
                    }
            }
            catch (Exception) { }
            return d;
        }

        public static string Get(string k, string def) { string v; return Leer().TryGetValue(k, out v) ? v : def; }

        public static void Set(string k, string v)
        {
            try
            {
                var d = Leer(); d[k] = v;
                Directory.CreateDirectory(Path.GetDirectoryName(Archivo()));
                File.WriteAllLines(Archivo(), d.Select(kv => kv.Key + "=" + kv.Value).ToArray());
            }
            catch (Exception) { }
        }
    }

    static class Win
    {
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr hwnd, string app, string idList);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] static extern bool FlashWindowEx(ref FLASHWINFO fi);
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        [StructLayout(LayoutKind.Sequential)]
        struct FLASHWINFO { public uint cbSize; public IntPtr hwnd; public uint dwFlags; public uint uCount; public uint dwTimeout; }

        // Barra de título oscura (Windows 10 20H1+ usa el atributo 20; versiones anteriores, 19).
        public static void TituloOscuro(Form f, bool oscuro)
        {
            try
            {
                int v = oscuro ? 1 : 0;
                if (DwmSetWindowAttribute(f.Handle, 20, ref v, 4) != 0) DwmSetWindowAttribute(f.Handle, 19, ref v, 4);
                SetWindowPos(f.Handle, IntPtr.Zero, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0004 | 0x0020); // NOSIZE|NOMOVE|NOZORDER|FRAMECHANGED
                // Al cambiar de tema con la ventana abierta, Windows 10 no repinta la barra hasta que cambia de activa a inactiva:
                // se le manda WM_NCACTIVATE dos veces (el estado contrario y luego el real).
                bool activa = Form.ActiveForm == f;
                SendMessage(f.Handle, 0x0086, activa ? IntPtr.Zero : (IntPtr)1, IntPtr.Zero);
                SendMessage(f.Handle, 0x0086, activa ? (IntPtr)1 : IntPtr.Zero, IntPtr.Zero);
            }
            catch (Exception) { }
        }

        // Barras de desplazamiento oscuras (Windows 10 1809+). En versiones viejas no hace nada.
        public static void Barras(Control c, bool oscuro) { try { SetWindowTheme(c.Handle, oscuro ? "DarkMode_Explorer" : "Explorer", null); } catch (Exception) { } }

        // Hace parpadear el botón de la barra de tareas hasta que la ventana recibe el foco.
        public static void Parpadear(Form f)
        {
            try
            {
                var fi = new FLASHWINFO { hwnd = f.Handle, dwFlags = 0x3 | 0xC, uCount = 0, dwTimeout = 0 }; // FLASHW_ALL | FLASHW_TIMERNOFG
                fi.cbSize = (uint)Marshal.SizeOf(fi);
                FlashWindowEx(ref fi);
            }
            catch (Exception) { }
        }
    }

    // Radio y casilla dibujados con la paleta: los de Windows pintan el texto desactivado casi negro sobre el fondo oscuro.
    static class Marca
    {
        const TextFormatFlags Flags = TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding;
        static int Lado(Control c) { return (int)Math.Round(c.Font.Height * 0.8); }

        // Tamaño propio: círculo/cuadro + texto, que se parte en líneas si hay MaximumSize.
        public static Size Medir(ButtonBase c)
        {
            int d = Lado(c);
            int max = c.MaximumSize.Width > 0 ? c.MaximumSize.Width - d - 10 : int.MaxValue;
            Size t = TextRenderer.MeasureText(c.Text, c.Font, new Size(Math.Max(20, max), int.MaxValue), Flags);
            return new Size(d + 10 + t.Width, Math.Max(t.Height, d) + 6);
        }

        public static void Pintar(ButtonBase c, Graphics g, bool marcado, bool redondo, bool foco)
        {
            g.Clear(c.BackColor);
            int d = Lado(c), ancho = Math.Max(20, c.Width - d - 10);
            Size t = TextRenderer.MeasureText(g, c.Text, c.Font, new Size(ancho, int.MaxValue), Flags);
            int arriba = Math.Max(0, (c.Height - t.Height) / 2);
            float x = 1.5f, y = arriba + (Math.Min(t.Height, c.Font.Height) - d) / 2f;   // a la altura de la primera línea
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Color borde = !c.Enabled ? C.Line : marcado ? C.Accent : C.LineStrong;
            Color relleno = marcado && c.Enabled && !redondo ? C.Accent : C.Bg;
            using (var b = new SolidBrush(relleno))
            using (var p = new Pen(borde, 1.5f))
            {
                if (redondo) { g.FillEllipse(b, x, y, d, d); g.DrawEllipse(p, x, y, d, d); }
                else { g.FillRectangle(b, x, y, d, d); g.DrawRectangle(p, x, y, d, d); }
            }
            if (marcado)
            {
                Color punto = c.Enabled ? (redondo ? C.Accent : Color.White) : C.LineStrong;
                if (redondo) using (var b = new SolidBrush(punto)) g.FillEllipse(b, x + d * 0.25f, y + d * 0.25f, d * 0.5f, d * 0.5f);
                else using (var p = new Pen(punto, 2f)) g.DrawLines(p, new[] { new PointF(x + d * 0.2f, y + d * 0.52f), new PointF(x + d * 0.42f, y + d * 0.74f), new PointF(x + d * 0.8f, y + d * 0.28f) });
            }
            TextRenderer.DrawText(g, c.Text, c.Font, new Rectangle(d + 8, arriba, ancho, t.Height), c.Enabled ? c.ForeColor : C.Ink3, Flags);
            if (foco) ControlPaint.DrawFocusRectangle(g, new Rectangle(d + 5, Math.Max(0, arriba - 2), Math.Min(t.Width + 6, c.Width - d - 5), Math.Min(c.Height, t.Height + 4)), C.Ink, c.BackColor);
        }
    }

    class Opcion : RadioButton
    {
        public Opcion() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); }
        public override Size GetPreferredSize(Size propuesto) { return Marca.Medir(this); }
        protected override void OnPaint(PaintEventArgs e) { Marca.Pintar(this, e.Graphics, Checked, true, Focused && ShowFocusCues); }
    }

    class Casilla : CheckBox
    {
        public Casilla() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); }
        public override Size GetPreferredSize(Size propuesto) { return Marca.Medir(this); }
        protected override void OnPaint(PaintEventArgs e) { Marca.Pintar(this, e.Graphics, Checked, false, Focused && ShowFocusCues); }
    }

    class Item
    {
        public string Ruta;
        public long Size;
        public InfoVideo Info;
        public Plan Plan;
        public ListViewItem Lv;
        public string Estado = "analizando";   // analizando | listo | trabajando | hecho | error
        public string Salida, Error, Nota;
        public bool NoCupo;
        public double Segundos;
    }

    public class Ventana : Form
    {
        static readonly CultureInfo CI = CultureInfo.InvariantCulture;
        readonly Ajustes aj = new Ajustes();
        readonly List<Item> items = new List<Item>();
        readonly string[] argsIniciales;
        bool auto = true, trabajando, actualizando;
        string ultimaCarpeta, estadoFinal;

        ListView lista;
        NumericUpDown tam;
        TrackBar barraTam;
        ComboBox cbDestino, cbFormato, cbRotar;
        Label estGrande, estLinea1, estLinea2, estado, avisoFfmpeg, ayudaSoloAudio, lblPie;
        Button btnIr, btnCancelar, btnAbrir, btnTema, btnIdioma, btnMas, btnCarpeta;
        LinkLabel lnkEnviarA;
        FlowLayoutPanel panelMas;
        ProgressBar progreso;
        Casilla chkFix, chkVoz, chkJunto, chkSinAudio, chkAvisar;
        TextBox txtCarpeta;
        Panel panelAjustes;
        readonly List<Opcion> radiosRes = new List<Opcion>(), radiosVel = new List<Opcion>();
        Font fBase, fBold, fTitulo, fGrande, fSec, fChico;

        // Controles cuyo texto sale del diccionario: se vuelven a escribir al cambiar de idioma.
        readonly List<Tuple<Control, string, object[]>> textos = new List<Tuple<Control, string, object[]>>();
        readonly List<Tuple<ComboBox, string[]>> combos = new List<Tuple<ComboBox, string[]>>();
        static readonly string[] ColKeys = { "colVideo", "colOriginal", "colQuedara", "colCalidad", "colEstado" };

        static readonly string[] DestKeys = { "destAuto", "destDiscord", "destNitro", "destWhatsapp", "destCorreo", "destCustom" };
        static readonly double[] DestMB = { 0, 20, 50, 64, 18, 0 };     // límites con su fuente en ALCANCE.md §3
        static readonly int[] DestRes = { 0, 0, 0, 720, 0, 0 };
        const int DEST_AUTO = 0, DEST_CUSTOM = 5;
        static readonly string[] FmtKeys = { "fmtMp4", "fmtMov", "fmtMkv", "fmtMp3", "fmtM4a" };
        static readonly string[] FmtVals = { "mp4", "mov", "mkv", "mp3", "m4a" };
        static readonly string[] RotKeys = { "rotNone", "rot90", "rot270", "rot180", "flipH", "flipV" };
        static readonly string[] RotVals = { "", "90", "270", "180", "h", "v" };

        public Ventana() : this(new string[0]) { }

        public Ventana(string[] args)
        {
            argsIniciales = args ?? new string[0];
            C.Aplicar(Preferencias.Get("tema", "claro") == "oscuro");
            Tx.Idioma = Preferencias.Get("idioma", CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es" ? "es" : "en");

            string disp = FontFamily.Families.Any(f => f.Name == "Bahnschrift") ? "Bahnschrift SemiBold" : "Segoe UI Semibold";
            fBase = new Font("Segoe UI", 10f);
            fBold = new Font("Segoe UI Semibold", 10f);
            fChico = new Font("Segoe UI", 9f);
            fSec = new Font(disp, 12f);
            fTitulo = new Font(disp, 18f);
            fGrande = new Font(disp, 26f);

            Text = "Video Lite · by Zedran Labs";
            Font = fBase;
            BackColor = C.Bg;
            ForeColor = C.Ink;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96f, 96f);
            MinimumSize = new Size(800, 600);
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1200, 820);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }
            AllowDrop = true;
            DragEnter += AlArrastrar;
            DragDrop += AlSoltar;

            var raiz = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(20, 12, 20, 8), BackColor = C.Bg };
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(raiz);

            // Encabezado: marca a la izquierda; idioma y tema a la derecha
            var cab = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 0, 0, 6) };
            cab.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            cab.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var marca = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            marca.Controls.Add(new Label { Text = "Video Lite", Font = fTitulo, AutoSize = true, Margin = new Padding(0, 0, 8, 0) });
            marca.Controls.Add(L(new Label { ForeColor = C.Ink3, AutoSize = true, Margin = new Padding(0, 12, 0, 0) }, "appBy"));
            var acciones = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0), Anchor = AnchorStyles.Right | AnchorStyles.Top };
            btnIdioma = L(Boton("", false, (s, e) => CambiarIdioma()), "otroIdioma");
            btnTema = Boton(C.Oscuro ? Tx.T("temaClaro") : Tx.T("temaOscuro"), false, (s, e) => CambiarTema());
            foreach (var b in new[] { btnIdioma, btnTema }) { b.Font = fChico; b.Margin = new Padding(8, 6, 0, 0); acciones.Controls.Add(b); }
            cab.Controls.Add(marca, 0, 0); cab.Controls.Add(acciones, 1, 0);
            raiz.Controls.Add(cab, 0, 0);

            avisoFfmpeg = new Label { AutoSize = true, ForeColor = C.Ink, BackColor = C.AmberSoft, Padding = new Padding(10, 6, 10, 6), Margin = new Padding(0, 0, 0, 8), Visible = false, MaximumSize = new Size(2000, 0) };
            raiz.Controls.Add(avisoFfmpeg, 0, 1);

            var cuerpo = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
            cuerpo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57));
            cuerpo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
            raiz.Controls.Add(cuerpo, 0, 2);
            cuerpo.Controls.Add(ConstruirVideos(), 0, 0);
            cuerpo.Controls.Add(ConstruirAjustes(), 1, 0);

            var pie = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0, 8, 0, 0) };
            lblPie = new Label { ForeColor = C.Ink3, AutoSize = true, Font = fChico, Margin = new Padding(0, 2, 4, 0) };
            L(lblPie, "pie", C.Version);
            pie.Controls.Add(lblPie);
            pie.Controls.Add(L(Enlace("https://ko-fi.com/kierczedran"), "kofi"));
            pie.Controls.Add(Enlace(null, (s, e) => Abrir(Tx.T("urlSitio")), "videolite.zedran.xyz"));   // la página en el idioma de la app
            pie.Controls.Add(L(Enlace("https://discord.com/invite/2ZRbaYDAnR"), "reportar"));
            pie.Controls.Add(Enlace("mailto:Kierc117@hotmail.com?subject=Video%20Lite", null, "Kierc117@hotmail.com"));
            pie.Controls.Add(L(Enlace(null, (s, e) => Abrir(Tx.T("urlPrivacidad"))), "privacidad"));
            pie.Controls.Add(L(Enlace(null, (s, e) => MostrarLicencias()), "licencias"));
            lnkEnviarA = Enlace(null, (s, e) => AlternarEnviarA());
            pie.Controls.Add(lnkEnviarA);
            raiz.Controls.Add(pie, 0, 3);

            actualizando = true; tam.Value = 25; actualizando = false;
            ActualizarEnlaceEnviarA();
            AplicarAuto();
            HandleCreated += (s, e) => AplicarDetallesWindows();
            Shown += (s, e) => { RevisarFfmpeg(); if (argsIniciales.Length > 0) Agregar(argsIniciales); };
            FormClosing += AlCerrar;
            Refrescar();
        }

        /* ---------- Construcción ---------- */

        // Registra un control cuyo texto viene del diccionario.
        T L<T>(T c, string clave, params object[] args) where T : Control
        {
            c.Text = Tx.T(clave, args);
            textos.Add(Tuple.Create((Control)c, clave, args));
            return c;
        }

        ComboBox Combo(string[] claves, int sel)
        {
            var cb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330, FlatStyle = FlatStyle.Flat, BackColor = C.Bg, ForeColor = C.Ink, Margin = new Padding(0) };
            foreach (string k in claves) cb.Items.Add(Tx.T(k));
            cb.SelectedIndex = sel;
            combos.Add(Tuple.Create(cb, claves));
            return cb;
        }

        // El ComboBox plano no dibuja borde: va dentro de un panel de 1 px del color de los bordes.
        static Control ConBorde(ComboBox cb)
        {
            var p = new Panel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(1), BackColor = C.LineStrong, Margin = new Padding(0, 2, 0, 10) };
            cb.Location = new Point(1, 1);
            p.Controls.Add(cb);
            return p;
        }

        Control ConstruirVideos()
        {
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = new Padding(0, 0, 12, 0) };
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            t.Controls.Add(L(new Label { Font = fSec, AutoSize = true, Margin = new Padding(0, 0, 0, 6) }, "secVideos"), 0, 0);

            var botones = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0, 0, 0, 6) };
            botones.Controls.Add(L(Boton("", false, (s, e) => ElegirVideos()), "agregarVideos"));
            botones.Controls.Add(L(Boton("", false, (s, e) => ElegirCarpeta()), "agregarCarpeta"));
            botones.Controls.Add(L(Boton("", false, (s, e) => QuitarSeleccionados()), "quitarSel"));
            botones.Controls.Add(L(Boton("", false, (s, e) => Limpiar()), "limpiar"));
            t.Controls.Add(botones, 0, 1);

            lista = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, ShowItemToolTips = true, HideSelection = false, BorderStyle = BorderStyle.FixedSingle, BackColor = C.Surface, ForeColor = C.Ink, AllowDrop = true };
            lista.OwnerDraw = true;   // el encabezado se dibuja con la paleta (en oscuro, el de Windows quedaría blanco)
            lista.DrawColumnHeader += (s, e) =>
            {
                using (var fondo = new SolidBrush(C.Surface2)) e.Graphics.FillRectangle(fondo, e.Bounds);
                using (var linea = new Pen(C.Line)) e.Graphics.DrawLine(linea, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
                TextRenderer.DrawText(e.Graphics, e.Header.Text, fBold, new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height), C.Ink,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            };
            lista.DrawItem += (s, e) => { e.DrawDefault = true; };
            lista.DrawSubItem += (s, e) => { e.DrawDefault = true; };
            foreach (string k in ColKeys) lista.Columns.Add(Tx.T(k), 120);
            lista.DragEnter += AlArrastrar;
            lista.DragDrop += AlSoltar;
            lista.KeyDown += (s, e) => { if (e.KeyCode == Keys.Delete) QuitarSeleccionados(); };
            lista.DoubleClick += (s, e) => AbrirSeleccion();
            lista.Resize += (s, e) => AjustarColumnas();
            t.Controls.Add(lista, 0, 2);

            var vacio = L(new Label { ForeColor = C.Ink3, AutoSize = true, BackColor = C.Surface, Tag = "vacio" }, "vacio");
            lista.Controls.Add(vacio);
            vacio.Location = new Point(16, 40);
            return t;
        }

        Control ConstruirAjustes()
        {
            var marco = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = C.Surface, Margin = new Padding(0), Padding = new Padding(1) };
            marco.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            marco.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            marco.Paint += (s, e) => { using (var p = new Pen(C.Line)) e.Graphics.DrawRectangle(p, 0, 0, marco.Width - 1, marco.Height - 1); };
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = C.Surface, Padding = new Padding(16, 12, 16, 4), AutoScroll = true, Margin = new Padding(0) };
            panelAjustes = panel;
            var f = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Dock = DockStyle.Top, BackColor = C.Surface };
            panel.Controls.Add(f);
            var fijo = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Dock = DockStyle.Fill, BackColor = C.Surface, Padding = new Padding(16, 4, 16, 12), Margin = new Padding(0) };
            marco.Controls.Add(panel, 0, 0);
            marco.Controls.Add(fijo, 0, 1);
            // Solo a las etiquetas: con MaximumSize, el ComboBox pierde su alto en el FlowLayoutPanel y se encima con la fila de abajo.
            panel.Resize += (s, e) => { foreach (Control c in f.Controls.Cast<Control>().Concat(panelMas != null ? panelMas.Controls.Cast<Control>() : Enumerable.Empty<Control>())) if (c is Label || c is Casilla) c.MaximumSize = new Size(Math.Max(200, panel.ClientSize.Width - 36), 0); };

            f.Controls.Add(L(new Label { Font = fSec, AutoSize = true, Margin = new Padding(0, 0, 0, 8) }, "secAjustes"));

            // Tamaño: destino + deslizador + número
            f.Controls.Add(Leyenda("legTam"));
            f.Controls.Add(L(new Label { AutoSize = true, ForeColor = C.Ink2, Font = fChico, Margin = new Padding(0, 0, 0, 0) }, "paraEnviar"));
            cbDestino = Combo(DestKeys, DEST_AUTO);
            cbDestino.SelectedIndexChanged += (s, e) => { if (!actualizando) CambioDestino(); };
            f.Controls.Add(ConBorde(cbDestino));
            var filaTam = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 10) };
            barraTam = new TrackBar { Minimum = 0, Maximum = 1000, TickStyle = TickStyle.None, Width = 220, BackColor = C.Surface, Margin = new Padding(0, 4, 8, 0) };
            barraTam.Scroll += (s, e) => { if (!actualizando) PonerTam(DesdeBarra(barraTam.Value), true); };
            tam = new NumericUpDown { DecimalPlaces = 1, Minimum = 0.5m, Maximum = (decimal)Motor.MaxMB, Increment = 1, Width = 90, TextAlign = HorizontalAlignment.Right, Font = fBold, Margin = new Padding(0, 4, 4, 0), BackColor = C.Bg, ForeColor = C.Ink };
            tam.ValueChanged += (s, e) => { if (!actualizando) PonerTam((double)tam.Value, true); };
            filaTam.Controls.Add(barraTam); filaTam.Controls.Add(tam);
            filaTam.Controls.Add(new Label { Text = "MB", AutoSize = true, Font = fBold, Margin = new Padding(0, 8, 0, 0) });
            f.Controls.Add(filaTam);

            // Calidad
            f.Controls.Add(Leyenda("legCalidad"));
            var filaRes = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0) };
            foreach (var r in new[] { Tuple.Create("1080p", 1080), Tuple.Create("720p", 720), Tuple.Create("480p", 480), Tuple.Create("360p", 360) }.Reverse().Concat(new[] { Tuple.Create("", 0) }).Reverse())
            {
                var rb = new Opcion { Text = r.Item1, Tag = r.Item2, AutoSize = true, Checked = r.Item2 == 0, Margin = new Padding(0, 0, 10, 0) };
                if (r.Item2 == 0) L(rb, "resOriginal");
                rb.CheckedChanged += (s, e) => { if (((RadioButton)s).Checked) { aj.Res = (int)((RadioButton)s).Tag; Cambio(); } };
                radiosRes.Add(rb); filaRes.Controls.Add(rb);
            }
            f.Controls.Add(filaRes);
            f.Controls.Add(Ayuda("helpRes"));

            // Velocidad
            f.Controls.Add(Leyenda("legVel"));
            var filaVel = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0) };
            foreach (var v in new[] { 0.5, 1, 1.5, 2 })
            {
                var rb = new Opcion { Text = "×" + v.ToString(CI), Tag = v, AutoSize = true, Checked = v == 1, Margin = new Padding(0, 0, 10, 0) };
                rb.CheckedChanged += (s, e) => { if (((RadioButton)s).Checked) { aj.Speed = (double)((RadioButton)s).Tag; Cambio(); } };
                radiosVel.Add(rb); filaVel.Controls.Add(rb);
            }
            f.Controls.Add(filaVel);
            f.Controls.Add(Ayuda("helpVel"));

            // Audio
            f.Controls.Add(Leyenda("legAudio"));
            chkFix = L(new Casilla { Font = fBold, Checked = true, AutoSize = true, Margin = new Padding(0) }, "fixAudio");
            chkFix.CheckedChanged += (s, e) => { aj.Fix = chkFix.Checked; };
            f.Controls.Add(chkFix);
            f.Controls.Add(Ayuda("helpFix"));
            chkVoz = L(new Casilla { Font = fBold, AutoSize = true, Margin = new Padding(0) }, "mejorarVoz");
            chkVoz.CheckedChanged += (s, e) => { aj.Voz = chkVoz.Checked; };
            f.Controls.Add(chkVoz);
            f.Controls.Add(Ayuda("helpVoz"));

            // Más opciones (plegable): formato, giro, sin audio, aviso al terminar
            btnMas = L(Boton("", false, (s, e) => { panelMas.Visible = !panelMas.Visible; ActualizarBotonMas(); }), "masOpciones");
            btnMas.Font = fChico;
            f.Controls.Add(btnMas);
            panelMas = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Visible = false, Margin = new Padding(0, 4, 0, 8), BackColor = C.Surface };
            panelMas.Controls.Add(Leyenda("legFormato"));
            cbFormato = Combo(FmtKeys, 0);
            cbFormato.SelectedIndexChanged += (s, e) => { aj.Formato = FmtVals[cbFormato.SelectedIndex]; Cambio(); };
            panelMas.Controls.Add(ConBorde(cbFormato));
            ayudaSoloAudio = Ayuda("helpSoloAudio"); ayudaSoloAudio.Visible = false;
            panelMas.Controls.Add(ayudaSoloAudio);
            panelMas.Controls.Add(Leyenda("legGirar"));
            cbRotar = Combo(RotKeys, 0);
            cbRotar.SelectedIndexChanged += (s, e) => { aj.Rotacion = RotVals[cbRotar.SelectedIndex]; Refrescar(); };
            panelMas.Controls.Add(ConBorde(cbRotar));
            chkSinAudio = L(new Casilla { AutoSize = true, Margin = new Padding(0, 0, 0, 4) }, "sinAudio");
            chkSinAudio.CheckedChanged += (s, e) => { aj.SinAudio = chkSinAudio.Checked; Cambio(); };
            panelMas.Controls.Add(chkSinAudio);
            chkAvisar = L(new Casilla { AutoSize = true, Margin = new Padding(0, 0, 0, 4) }, "avisar");
            panelMas.Controls.Add(chkAvisar);
            f.Controls.Add(panelMas);

            // Guardar en
            f.Controls.Add(Leyenda("legGuardar"));
            chkJunto = L(new Casilla { Checked = true, AutoSize = true, Margin = new Padding(0) }, "junto");
            var filaCarp = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 4, 0, 10) };
            txtCarpeta = new TextBox { Width = 260, Enabled = false, BackColor = C.Bg, ForeColor = C.Ink, Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Video Lite") };
            btnCarpeta = L(Boton("", false, (s, e) => ElegirSalida()), "cambiar");
            btnCarpeta.Enabled = false;
            chkJunto.CheckedChanged += (s, e) => { txtCarpeta.Enabled = btnCarpeta.Enabled = !chkJunto.Checked; };
            filaCarp.Controls.Add(txtCarpeta); filaCarp.Controls.Add(btnCarpeta);
            f.Controls.Add(chkJunto); f.Controls.Add(filaCarp);

            // Parte fija: estimado, botón, progreso
            var sep = new Label { AutoSize = false, Height = 1, Width = 400, BackColor = C.Line, Margin = new Padding(0, 0, 0, 8) };
            fijo.Controls.Add(sep);
            estGrande = new Label { Text = "", Font = fGrande, AutoSize = true, Margin = new Padding(0) };
            estLinea1 = new Label { Text = "", ForeColor = C.Ink2, AutoSize = true, Margin = new Padding(0, 2, 0, 0) };
            estLinea2 = new Label { Text = "", ForeColor = C.Ink2, AutoSize = true, Margin = new Padding(0, 2, 0, 8) };
            fijo.Controls.Add(estGrande); fijo.Controls.Add(estLinea1); fijo.Controls.Add(estLinea2);
            var acciones = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0, 0, 0, 6) };
            btnIr = Boton("", true, (s, e) => Iniciar());
            btnIr.Font = fSec; btnIr.Padding = new Padding(18, 6, 18, 6);
            btnCancelar = L(Boton("", false, (s, e) => Cancelar()), "cancelar");
            btnCancelar.Visible = false;
            btnAbrir = L(Boton("", false, (s, e) => AbrirCarpeta()), "abrirCarpeta");
            btnAbrir.Visible = false;
            acciones.Controls.Add(btnIr); acciones.Controls.Add(btnCancelar); acciones.Controls.Add(btnAbrir);
            fijo.Controls.Add(acciones);
            progreso = new ProgressBar { Width = 400, Height = 10, Maximum = 1000, Visible = false, Margin = new Padding(0, 0, 0, 4) };
            estado = new Label { Text = "", ForeColor = C.Ink2, AutoSize = true, Margin = new Padding(0) };
            fijo.Controls.Add(progreso); fijo.Controls.Add(estado);
            fijo.Resize += (s, e) =>
            {
                int an = Math.Max(200, fijo.ClientSize.Width - 32);
                sep.Width = progreso.Width = an;
                foreach (Control c in new Control[] { estLinea1, estLinea2, estado }) c.MaximumSize = new Size(an, 0);
            };
            return marco;
        }

        Label Leyenda(string clave) { return L(new Label { Font = fBold, AutoSize = true, Margin = new Padding(0, 0, 0, 4) }, clave); }
        Label Ayuda(string clave) { return L(new Label { ForeColor = C.Ink3, Font = fChico, AutoSize = true, Margin = new Padding(0, 2, 0, 10) }, clave); }

        Button Boton(string t, bool primario, EventHandler click)
        {
            var b = new Button { Text = t, AutoSize = true, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Margin = new Padding(0, 0, 8, 4), Padding = new Padding(8, 3, 8, 3) };
            b.FlatAppearance.BorderColor = primario ? C.Accent : C.LineStrong;
            b.BackColor = primario ? C.Accent : C.Bg;
            b.ForeColor = primario ? Color.White : C.Ink;
            b.Click += click;
            return b;
        }

        LinkLabel Enlace(string url, EventHandler click = null, string texto = null)
        {
            var l = new LinkLabel { Text = texto ?? "", AutoSize = true, Font = fChico, LinkColor = C.AccentText, ActiveLinkColor = C.Accent, Margin = new Padding(0, 2, 8, 0) };
            if (url != null) l.LinkClicked += (s, e) => Abrir(url);
            if (click != null) l.LinkClicked += (s, e) => click(s, e);
            return l;
        }

        void ActualizarBotonMas() { btnMas.Text = Tx.T(panelMas.Visible ? "menosOpciones" : "masOpciones"); }

        /* ---------- Idioma y tema ---------- */

        void CambiarIdioma()
        {
            Tx.Idioma = Tx.Idioma == "es" ? "en" : "es";
            Preferencias.Set("idioma", Tx.Idioma);
            SuspendLayout();
            foreach (var r in textos) r.Item1.Text = Tx.T(r.Item2, r.Item3);
            actualizando = true;
            foreach (var cb in combos)
            {
                int sel = cb.Item1.SelectedIndex;
                cb.Item1.Items.Clear();
                foreach (string k in cb.Item2) cb.Item1.Items.Add(Tx.T(k));
                cb.Item1.SelectedIndex = sel;
            }
            actualizando = false;
            for (int i = 0; i < ColKeys.Length; i++) lista.Columns[i].Text = Tx.T(ColKeys[i]);
            btnTema.Text = C.Oscuro ? Tx.T("temaClaro") : Tx.T("temaOscuro");
            ActualizarBotonMas();
            ActualizarEnlaceEnviarA();
            if (avisoFfmpeg.Visible) avisoFfmpeg.Text = Tx.T("ffmpegFalta");
            foreach (var it in items) EscribirEstado(it);
            ResumeLayout(true);
            Refrescar();
            lista.Invalidate();
        }

        void CambiarTema()
        {
            Color[] antes = C.Tokens();
            C.Aplicar(!C.Oscuro);
            Color[] despues = C.Tokens();
            var mapa = new Dictionary<int, Color>();
            for (int i = 0; i < antes.Length; i++) if (!mapa.ContainsKey(antes[i].ToArgb())) mapa[antes[i].ToArgb()] = despues[i];
            SuspendLayout();
            Recolorear(this, mapa);
            foreach (ListViewItem lv in lista.Items) { Color c; if (mapa.TryGetValue(lv.ForeColor.ToArgb(), out c)) lv.ForeColor = c; }
            btnTema.Text = C.Oscuro ? Tx.T("temaClaro") : Tx.T("temaOscuro");
            ResumeLayout(true);
            Preferencias.Set("tema", C.Oscuro ? "oscuro" : "claro");
            AplicarDetallesWindows();
            Refrescar();
            Invalidate(true);
        }

        // Cambia cada color de la paleta vieja por el mismo token de la nueva, en todo el árbol de controles.
        static void Recolorear(Control c, Dictionary<int, Color> mapa)
        {
            Color n;
            if (mapa.TryGetValue(c.BackColor.ToArgb(), out n)) c.BackColor = n;
            if (mapa.TryGetValue(c.ForeColor.ToArgb(), out n)) c.ForeColor = n;
            var b = c as Button;
            if (b != null && mapa.TryGetValue(b.FlatAppearance.BorderColor.ToArgb(), out n)) b.FlatAppearance.BorderColor = n;
            var l = c as LinkLabel;
            if (l != null)
            {
                if (mapa.TryGetValue(l.LinkColor.ToArgb(), out n)) l.LinkColor = n;
                if (mapa.TryGetValue(l.ActiveLinkColor.ToArgb(), out n)) l.ActiveLinkColor = n;
            }
            foreach (Control h in c.Controls) Recolorear(h, mapa);
        }

        void AplicarDetallesWindows()
        {
            Win.TituloOscuro(this, C.Oscuro);
            if (lista != null) Win.Barras(lista, C.Oscuro);
            if (panelAjustes != null) Win.Barras(panelAjustes, C.Oscuro);
        }

        /* ---------- «Enviar a» de Windows ---------- */

        static string AtajoEnviarA() { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.SendTo), "Video Lite.lnk"); }

        void ActualizarEnlaceEnviarA() { lnkEnviarA.Text = Tx.T(File.Exists(AtajoEnviarA()) ? "quitarEnviarA" : "enviarA"); }

        // Crea o quita el acceso «Video Lite» del menú Enviar a (clic derecho en un archivo). Solo en la cuenta del usuario.
        void AlternarEnviarA()
        {
            string lnk = AtajoEnviarA();
            try
            {
                if (File.Exists(lnk))
                {
                    File.Delete(lnk);
                    MessageBox.Show(this, Tx.T("enviarAquitado"), "Video Lite", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    Type tipo = Type.GetTypeFromProgID("WScript.Shell");
                    object shell = Activator.CreateInstance(tipo);
                    object atajo = tipo.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
                    Type ta = atajo.GetType();
                    ta.InvokeMember("TargetPath", BindingFlags.SetProperty, null, atajo, new object[] { Application.ExecutablePath });
                    ta.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, atajo, new object[] { Path.GetDirectoryName(Application.ExecutablePath) });
                    ta.InvokeMember("IconLocation", BindingFlags.SetProperty, null, atajo, new object[] { Application.ExecutablePath + ",0" });
                    ta.InvokeMember("Save", BindingFlags.InvokeMethod, null, atajo, null);
                    Marshal.FinalReleaseComObject(atajo); Marshal.FinalReleaseComObject(shell);
                    MessageBox.Show(this, Tx.T("enviarAok"), "Video Lite", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Video Lite", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            ActualizarEnlaceEnviarA();
        }

        /* ---------- ffmpeg ---------- */

        void RevisarFfmpeg()
        {
            Motor.Ffmpeg = Motor.BuscarFfmpeg();
            if (Motor.Ffmpeg == null)
            {
                avisoFfmpeg.Text = Tx.T("ffmpegFalta");
                avisoFfmpeg.Visible = true;
                btnIr.Enabled = false;
            }
        }

        /* ---------- Archivos ---------- */

        static readonly string[] Ext = { ".mp4", ".mov", ".mkv", ".avi", ".m4v", ".webm", ".3gp", ".wmv", ".flv", ".ts", ".mts", ".m2ts", ".mpg", ".mpeg" };

        void AlArrastrar(object s, DragEventArgs e) { e.Effect = !trabajando && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; }
        void AlSoltar(object s, DragEventArgs e)
        {
            var rutas = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (rutas != null) Agregar(rutas);
        }

        void ElegirVideos()
        {
            using (var d = new OpenFileDialog { Multiselect = true, Title = Tx.T("elegirVideos"), Filter = Tx.T("filtroVideos") + "|*" + string.Join(";*", Ext) + "|" + Tx.T("filtroTodos") + "|*.*" })
                if (d.ShowDialog(this) == DialogResult.OK) Agregar(d.FileNames);
        }

        void ElegirCarpeta()
        {
            using (var d = new FolderBrowserDialog { Description = Tx.T("elegirCarpeta") })
                if (d.ShowDialog(this) == DialogResult.OK) Agregar(new[] { d.SelectedPath });
        }

        void ElegirSalida()
        {
            using (var d = new FolderBrowserDialog { Description = Tx.T("dondeGuardar"), SelectedPath = txtCarpeta.Text })
                if (d.ShowDialog(this) == DialogResult.OK) txtCarpeta.Text = d.SelectedPath;
        }

        void Agregar(IEnumerable<string> rutas)
        {
            if (trabajando) return;
            var nuevos = new List<Item>();
            foreach (string r in rutas)
            {
                IEnumerable<string> archivos;
                if (Directory.Exists(r))
                {
                    try { archivos = Directory.EnumerateFiles(r, "*", SearchOption.AllDirectories).Where(x => !x.Contains("\\Video Lite\\")).ToList(); }
                    catch (Exception) { continue; }
                }
                else archivos = new[] { r };
                foreach (string a in archivos)
                {
                    if (!Ext.Contains(Path.GetExtension(a).ToLowerInvariant()) || !File.Exists(a)) continue;
                    if (items.Any(i => string.Equals(i.Ruta, a, StringComparison.OrdinalIgnoreCase))) continue;
                    var it = new Item { Ruta = a, Size = new FileInfo(a).Length };
                    it.Lv = new ListViewItem(new[] { Path.GetFileName(a), Mb(it.Size), "", "", Tx.T("analizando") }) { Tag = it, ToolTipText = a };
                    items.Add(it); nuevos.Add(it); lista.Items.Add(it.Lv);
                }
            }
            Refrescar();
            if (nuevos.Count == 0 || Motor.Ffmpeg == null) return;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                foreach (var it in nuevos)
                {
                    InfoVideo info = null;
                    try { info = Motor.Analizar(it.Ruta); } catch (Exception) { }
                    var cap = it;
                    BeginInvoke(new Action(() =>
                    {
                        cap.Info = info;
                        cap.Estado = info == null ? "error" : "listo";
                        if (info == null) cap.Error = Tx.T("noLegible");
                        if (auto) AplicarAuto(); else Refrescar();
                    }));
                }
            });
        }

        void QuitarSeleccionados()
        {
            if (trabajando) return;
            foreach (ListViewItem lv in lista.SelectedItems.Cast<ListViewItem>().ToList()) { items.Remove((Item)lv.Tag); lista.Items.Remove(lv); }
            if (auto) AplicarAuto(); else Refrescar();
        }

        void Limpiar()
        {
            if (trabajando) return;
            items.Clear(); lista.Items.Clear(); btnAbrir.Visible = false; estado.Text = ""; estadoFinal = null;
            if (auto) AplicarAuto(); else Refrescar();
        }

        /* ---------- Tamaño y destino ---------- */

        double MaxBarra() { return Math.Max(10, items.Count > 0 ? Math.Ceiling(items.Max(i => i.Size) / 1e6) : 10); }
        int HaciaBarra(double mb) { return (int)Math.Round(Math.Log(Math.Max(1, mb)) / Math.Log(MaxBarra()) * 1000); }
        double DesdeBarra(int v) { return Math.Max(1, Math.Round(Math.Exp(v / 1000.0 * Math.Log(MaxBarra())))); }

        void PonerTam(double mb, bool delUsuario)
        {
            mb = mb < 10 ? Math.Round(mb * 10) / 10 : Math.Round(mb);
            mb = Math.Max(0.5, Math.Min(Motor.MaxMB, mb));
            aj.TargetMB = mb;
            actualizando = true;
            if (delUsuario) { auto = false; cbDestino.SelectedIndex = DEST_CUSTOM; }
            tam.Value = (decimal)mb;
            barraTam.Value = Math.Max(0, Math.Min(1000, HaciaBarra(Math.Min(mb, MaxBarra()))));
            actualizando = false;
            Refrescar();
        }

        void CambioDestino()
        {
            int d = cbDestino.SelectedIndex;
            auto = d == DEST_AUTO;
            if (auto) { AplicarAuto(); return; }
            if (d == DEST_CUSTOM) { Refrescar(); return; }
            if (DestRes[d] > 0 && (aj.Res == 0 || aj.Res > DestRes[d]))
            {
                var rb = radiosRes.FirstOrDefault(r => (int)r.Tag == DestRes[d]);
                if (rb != null && rb.Enabled) rb.Checked = true;
            }
            PonerTam(DestMB[d], false);
        }

        void AplicarAuto()
        {
            var listos = items.Where(i => i.Info != null).ToList();
            actualizando = true; cbDestino.SelectedIndex = DEST_AUTO; actualizando = false;
            PonerTam(listos.Count > 0 ? listos.Max(i => Motor.RecomendarMB(i.Info, i.Size, aj)) : 25, false);
        }

        void Cambio() { if (auto) AplicarAuto(); else Refrescar(); }

        /* ---------- Pintado ---------- */

        static string Mb(double bytes) { return Motor.Tam(bytes); }
        static string Dur(double s)
        {
            int t = (int)Math.Round(s), h = t / 3600, m = (t % 3600) / 60, x = t % 60;
            return h > 0 ? string.Format("{0} h {1:00} min", h, m) : m > 0 ? string.Format("{0} min {1:00} s", m, x) : x + " s";
        }

        // Texto de la columna «Estado» de un video ya procesado (se reescribe al cambiar de idioma).
        void EscribirEstado(Item it)
        {
            if (it.Estado == "error") { it.Lv.SubItems[4].Text = it.Error; return; }
            if (it.Estado != "hecho") return;
            it.Lv.SubItems[4].Text = it.NoCupo ? Tx.T("noCupoEstado", Mb(new FileInfo(it.Salida).Length)) : Tx.T("listoEn", Dur(it.Segundos)) + (it.Nota != null && !it.NoCupo ? Tx.T("audioCorr") : "");
        }

        void Refrescar()
        {
            bool solo = aj.SoloAudio;
            var cortos = items.Where(i => i.Info != null).Select(i => Math.Min(i.Info.W, i.Info.H)).ToList();
            int maxCorto = cortos.Count > 0 ? cortos.Max() : int.MaxValue;
            foreach (var rb in radiosRes)
            {
                int v = (int)rb.Tag;
                bool muyGrande = v != 0 && v >= maxCorto;
                rb.Enabled = !trabajando && !solo && !muyGrande;
                if (muyGrande && rb.Checked) radiosRes[0].Checked = true;
            }
            foreach (var rb in radiosVel) rb.Enabled = !trabajando;
            foreach (Control c in new Control[] { tam, barraTam, cbDestino, cbRotar, chkSinAudio }) c.Enabled = !trabajando && !solo;
            ayudaSoloAudio.Visible = solo;

            double antes = 0, despues = 0; int n = 0; Plan peor = null; bool alguno = false;
            lista.BeginUpdate();
            foreach (var it in items)
            {
                var sub = it.Lv.SubItems;
                if (it.Info != null)
                    sub[1].Text = Mb(it.Size) + " · " + Dur(it.Info.Dur) + " · " + it.Info.W + "×" + it.Info.H + (it.Info.HasAudio ? "" : Tx.T("sinAudioMeta"));
                if (it.Estado == "error") { sub[4].Text = it.Error; it.Lv.ForeColor = C.Danger; continue; }
                if (it.Estado == "hecho" || it.Estado == "trabajando" || it.Info == null) continue;
                it.Plan = Motor.Planear(it.Info, it.Size, aj);
                if (it.Plan.SoloAudio)
                {
                    if (it.Plan.SinPistaAudio) { sub[2].Text = "—"; sub[3].Text = Tx.T("sinPista"); sub[4].Text = ""; alguno = true; continue; }
                    sub[2].Text = "≈ " + Mb(it.Plan.EstMB * 1e6);
                    sub[3].Text = Tx.T("soloAudio", aj.Formato.ToUpperInvariant());
                    sub[4].Text = Tx.T("porComprimir", aj.Formato.ToUpperInvariant()) + (aj.Speed != 1 ? Tx.T("dura", Dur(it.Plan.OutDur)) : "");
                    it.Lv.ForeColor = C.Ink;
                    antes += it.Size; despues += it.Plan.EstMB * 1e6; n++;
                    continue;
                }
                if (it.Plan.TooSmall) { sub[2].Text = Tx.T("noCabe"); sub[3].Text = Tx.T("subeTam"); sub[4].Text = ""; alguno = true; continue; }
                int nivel;
                sub[2].Text = "≈ " + Mb(it.Plan.EstMB * 1e6);
                sub[3].Text = Motor.Calidad(it.Plan, out nivel);
                sub[4].Text = Tx.T("porComprimir", it.Plan.OW + "×" + it.Plan.OH + " " + aj.Formato.ToUpperInvariant()) + (aj.Speed != 1 ? Tx.T("dura", Dur(it.Plan.OutDur)) : "");
                it.Lv.ForeColor = C.Ink;
                antes += it.Size; despues += it.Plan.EstMB * 1e6; n++;
                if (peor == null || it.Plan.Bpp < peor.Bpp) peor = it.Plan;
            }
            lista.EndUpdate();
            foreach (Control c in lista.Controls) if ((string)c.Tag == "vacio") c.Visible = items.Count == 0;

            var hechos = items.Where(i => i.Estado == "hecho" && i.Salida != null && File.Exists(i.Salida)).ToList();
            if (n == 0 && hechos.Count > 0 && hechos.Count == items.Count(i => i.Estado != "error"))
            {
                double a0 = hechos.Sum(i => (double)i.Size), a1 = hechos.Sum(i => (double)new FileInfo(i.Salida).Length);
                estGrande.Text = Tx.T(hechos.Count > 1 ? "estTotal" : "estUno", Mb(a1)).Replace("≈ ", "");
                estLinea1.Text = Tx.T("antes", Mb(a0), Tx.T("menos", Math.Round((1 - a1 / a0) * 100)));
                estLinea2.Text = Tx.T("doble");
                estLinea2.ForeColor = C.Ink2;
            }
            else if (n == 0)
            {
                estGrande.Text = "";
                estLinea1.Text = items.Count == 0 ? Tx.T("estVacio") : alguno ? Tx.T("estChico") : Tx.T("estAnalizando");
                estLinea2.Text = "";
            }
            else
            {
                int pct = (int)Math.Round((1 - despues / antes) * 100);
                estGrande.Text = Tx.T(n > 1 ? "estTotal" : "estUno", Mb(despues));
                estLinea1.Text = Tx.T("antes", Mb(antes), pct > 0 ? Tx.T("menos", pct) : Tx.T("igual"));
                if (peor != null)
                {
                    int nivel;
                    string q = Motor.Calidad(peor, out nivel);
                    estLinea2.Text = Tx.T("calEsperada", q) + (peor.Bpp < 0.025 && peor.VsSource < 0.6 && aj.Res != 360 ? Tx.T("bajaRes") : "");
                    estLinea2.ForeColor = nivel == 2 ? C.AccentText : nivel == 1 ? C.Amber : C.Danger;
                }
                else { estLinea2.Text = ""; }
            }
            if (!trabajando && estadoFinal != null) estado.Text = estadoFinal;
            int pendientes = items.Count(i => i.Estado == "listo" && i.Plan != null && !i.Plan.TooSmall);
            btnIr.Enabled = !trabajando && pendientes > 0 && Motor.Ffmpeg != null;
            btnIr.BackColor = btnIr.Enabled ? C.Accent : C.Surface2;
            btnIr.ForeColor = btnIr.Enabled ? Color.White : C.Ink3;
            btnIr.Text = trabajando ? Tx.T("comprimiendo") : pendientes > 1 ? Tx.T("comprimirN", pendientes) : Tx.T("comprimir");
            AjustarColumnas();
        }

        static readonly double[] PesoCol = { 2.5, 2.5, 1.5, 1.7, 2.2 };

        void AjustarColumnas()
        {
            int ancho = lista.ClientSize.Width;   // todo el ancho: lo que sobra a la derecha lo pinta Windows en blanco
            if (ancho <= 0) return;
            double suma = PesoCol.Sum();
            for (int i = 1; i < PesoCol.Length; i++) lista.Columns[i].Width = (int)(ancho * PesoCol[i] / suma);
            lista.Columns[0].Width = ancho - lista.Columns.Cast<ColumnHeader>().Skip(1).Sum(c => c.Width);
        }

        /* ---------- Compresión ---------- */

        void Iniciar()
        {
            if (trabajando) return;
            var cola = items.Where(i => i.Estado == "listo" && i.Plan != null && !i.Plan.TooSmall).ToList();
            if (cola.Count == 0) return;
            trabajando = true; Motor.Cancelado = false; estadoFinal = null;
            btnCancelar.Visible = true; btnAbrir.Visible = false; progreso.Visible = true; progreso.Value = 0;
            Refrescar();
            var ajCopia = new Ajustes { TargetMB = aj.TargetMB, Res = aj.Res, Speed = aj.Speed, Fix = aj.Fix, Voz = aj.Voz, Formato = aj.Formato, Rotacion = aj.Rotacion, SinAudio = aj.SinAudio };
            bool junto = chkJunto.Checked; string carpeta = txtCarpeta.Text;
            var reloj = Stopwatch.StartNew();
            var hilo = new Thread(() =>
            {
                int hechos = 0, k = 0;
                foreach (var it in cola)
                {
                    if (Motor.Cancelado) break;
                    var cap = it; int idx = k++;
                    var t0 = reloj.Elapsed.TotalSeconds;
                    BeginInvoke(new Action(() => { cap.Estado = "trabajando"; cap.Lv.SubItems[4].Text = Tx.T("empezando"); cap.Lv.EnsureVisible(); }));
                    string outDir = junto ? Path.Combine(Path.GetDirectoryName(cap.Ruta), "Video Lite") : carpeta;
                    try
                    {
                        string nota; bool noCupo;
                        string salida = Motor.Comprimir(cap.Ruta, cap.Info, cap.Plan, ajCopia, outDir, (f, txt) =>
                        {
                            double total = (idx + f) / cola.Count;
                            double el = reloj.Elapsed.TotalSeconds;
                            string eta = total > 0.02 ? Tx.T("faltan", Dur(el / total - el)) : "";
                            BeginInvoke(new Action(() =>
                            {
                                cap.Lv.SubItems[4].Text = txt;
                                progreso.Value = Math.Max(0, Math.Min(1000, (int)(total * 1000)));
                                estado.Text = Tx.T("videoNdeM", idx + 1, cola.Count, eta);
                            }));
                        }, out nota, out noCupo);
                        long tamSal = new FileInfo(salida).Length;
                        double seg = reloj.Elapsed.TotalSeconds - t0;
                        hechos++;
                        ultimaCarpeta = outDir;
                        BeginInvoke(new Action(() =>
                        {
                            cap.Estado = "hecho"; cap.Salida = salida; cap.Nota = nota; cap.NoCupo = noCupo; cap.Segundos = seg;
                            cap.Lv.SubItems[2].Text = Mb(tamSal);
                            EscribirEstado(cap);
                            cap.Lv.ForeColor = noCupo ? C.Danger : C.AccentText;
                            cap.Lv.ToolTipText = salida + (nota != null ? "\n" + nota : "");
                        }));
                    }
                    catch (OperationCanceledException)
                    {
                        BeginInvoke(new Action(() => { cap.Estado = "listo"; cap.Lv.SubItems[4].Text = Tx.T("canceladoMin"); }));
                        break;
                    }
                    catch (Exception ex)
                    {
                        string msg = Motor.Cancelado ? Tx.T("canceladoMin") : ex.Message + Tx.T("reporteHint");
                        BeginInvoke(new Action(() =>
                        {
                            cap.Estado = Motor.Cancelado ? "listo" : "error";
                            cap.Error = msg; cap.Lv.SubItems[4].Text = msg;
                            if (!Motor.Cancelado) cap.Lv.ForeColor = C.Danger;
                        }));
                        if (Motor.Cancelado) break;
                    }
                }
                double totalSeg = reloj.Elapsed.TotalSeconds;
                BeginInvoke(new Action(() =>
                {
                    trabajando = false;
                    btnCancelar.Visible = false; progreso.Visible = false;
                    btnAbrir.Visible = hechos > 0;
                    estadoFinal = Motor.Cancelado ? Tx.T("cancelado") : Tx.T("listoFin", hechos, cola.Count, Dur(totalSeg));
                    Refrescar();
                    if (!Motor.Cancelado) AvisarFin(hechos, cola.Count);
                }));
            });
            hilo.IsBackground = true;
            hilo.Start();
        }

        // Aviso al terminar (si se marcó): sonido, parpadeo en la barra de tareas y notificación de Windows.
        void AvisarFin(int hechos, int total)
        {
            if (!chkAvisar.Checked) return;
            try { SystemSounds.Asterisk.Play(); } catch (Exception) { }
            if (!ContainsFocus) Win.Parpadear(this);
            try
            {
                var ni = new NotifyIcon { Icon = Icon ?? SystemIcons.Application, Visible = true, Text = "Video Lite" };
                ni.ShowBalloonTip(6000, Tx.T("notifTitulo"), Tx.T("notifTexto", hechos, total), ToolTipIcon.Info);
                var tm = new System.Windows.Forms.Timer { Interval = 9000 };
                tm.Tick += (s, e) => { tm.Stop(); tm.Dispose(); ni.Visible = false; ni.Dispose(); };
                tm.Start();
            }
            catch (Exception) { }
        }

        void Cancelar() { if (trabajando) { estado.Text = Tx.T("cancelando"); Motor.Cancelar(); } }

        void AlCerrar(object s, FormClosingEventArgs e)
        {
            if (!trabajando) return;
            if (MessageBox.Show(this, Tx.T("cerrarConfirm"), "Video Lite", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No) e.Cancel = true;
            else Motor.Cancelar();
        }

        void AbrirCarpeta() { if (ultimaCarpeta != null && Directory.Exists(ultimaCarpeta)) Abrir(ultimaCarpeta); }

        void AbrirSeleccion()
        {
            if (lista.SelectedItems.Count == 0) return;
            var it = (Item)lista.SelectedItems[0].Tag;
            if (it.Salida != null && File.Exists(it.Salida)) Process.Start("explorer.exe", "/select,\"" + it.Salida + "\"");
        }

        static void Abrir(string destino) { try { Process.Start(destino); } catch (Exception) { } }

        void MostrarLicencias()
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LICENCIAS");
            var r = MessageBox.Show(this, Tx.T("licTexto", C.Version), Tx.T("licencias"),
                Directory.Exists(dir) ? MessageBoxButtons.YesNo : MessageBoxButtons.OK, MessageBoxIcon.Information);
            if (r == DialogResult.Yes) Abrir(dir);
        }
    }

    static class Programa
    {
        // Los argumentos son archivos o carpetas (los manda el menú «Enviar a» o se arrastran sobre el .exe).
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Ventana(args));
        }
    }
}
