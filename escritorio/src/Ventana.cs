// Video Lite · Zedran Labs: ventana principal (WinForms, .NET Framework 4.8, C# 5).
// Estilo Zedran Aero, el mismo de la web: ZedranAero.cs, copiado tal cual del paquete del estándar de webapps
// gratuitas (zedran-aero\winforms\). Full Aero en tema claro y Dark Aero en oscuro; los colores están en Paleta.
// Textos en Textos.cs (español e inglés, se cambian en vivo). El editor de un video está en Editor.cs.
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
using ZedranAero;

[assembly: AssemblyTitle("Video Lite")]
[assembly: AssemblyProduct("Video Lite · by Zedran Labs")]
[assembly: AssemblyCompany("Zedran Labs")]
[assembly: AssemblyCopyright("Zedran Labs")]
[assembly: AssemblyVersion("0.6")]
[assembly: AssemblyFileVersion("0.6")]

namespace VideoLite
{
    // La versión de la app (la del pie de la página y la del Release). Los colores están en Paleta (ZedranAero.cs).
    static class C
    {
        public const string Version = "0.6";
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
        [DllImport("user32.dll")] static extern bool FlashWindowEx(ref FLASHWINFO fi);
        [StructLayout(LayoutKind.Sequential)]
        struct FLASHWINFO { public uint cbSize; public IntPtr hwnd; public uint dwFlags; public uint uCount; public uint dwTimeout; }

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
        public Edicion Ed;                     // edición del editor (cuenta solo con el editor activo)
    }

    public partial class Ventana : VentanaAero
    {
        static readonly CultureInfo CI = CultureInfo.InvariantCulture;
        readonly Ajustes aj = new Ajustes();
        readonly List<Item> items = new List<Item>();
        readonly string[] argsIniciales;
        bool auto = true, trabajando, actualizando;
        string ultimaCarpeta, estadoFinal;

        ListView lista;
        Panel marcoLista;
        NumericUpDown tam;
        Deslizador barraTam;
        ComboBox cbDestino, cbFormato, cbRotar;
        Label estGrande, estLinea1, estLinea2, estado, avisoFfmpeg, ayudaSoloAudio, lblPie;
        Button btnIr, btnCancelar, btnAbrir, btnTema, btnIdioma, btnMas, btnCarpeta, btnKofi;
        LinkLabel lnkEnviarA;
        FlowLayoutPanel panelMas;
        ProgressBar progreso;
        CasillaAero chkFix, chkVoz, chkJunto, chkSinAudio, chkAvisar;
        TextBox txtCarpeta;
        Panel panelAjustes;
        TableLayoutPanel cuerpo, marcoAjustes;
        FlowLayoutPanel flujoAjustes, fijoAjustes;
        Label sepAjustes;
        Label lblSecAjustes;
        readonly List<FlowLayoutPanel> seccionesAjustes = new List<FlowLayoutPanel>();
        readonly List<Tuple<Label, Control[]>> ayudasAjustes = new List<Tuple<Label, Control[]>>();
        readonly ToolTip globos = new ToolTip { AutoPopDelay = 15000, InitialDelay = 400 };
        readonly List<OpcionAero> radiosRes = new List<OpcionAero>(), radiosVel = new List<OpcionAero>();
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
            Paleta.Aplicar(Preferencias.Get("tema", "claro") == "oscuro");
            Tx.Idioma = Preferencias.Get("idioma", CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es" ? "es" : "en");

            // Segoe UI en todo, como Zedran Aero (los títulos en negrita)
            fBase = new Font("Segoe UI", 10f);
            fBold = new Font("Segoe UI Semibold", 10f);
            fChico = new Font("Segoe UI", 9f);
            fSec = new Font("Segoe UI", 12f, FontStyle.Bold);
            fTitulo = new Font("Segoe UI", 18f, FontStyle.Bold);
            fGrande = new Font("Segoe UI Semibold", 26f);

            Text = "Video Lite · by Zedran Labs";
            Font = fBase;
            BackColor = Paleta.Fondo;   // debajo de la foto que pinta VentanaAero (con el tema de las preferencias)
            ForeColor = Paleta.Ink;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96f, 96f);
            MinimumSize = new Size(800, 600);
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1200, 820);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }
            AllowDrop = true;
            DragEnter += AlArrastrar;
            DragDrop += AlSoltar;

            // Todo es transparente hasta la ventana, que pinta la foto; encima van los paneles de vidrio.
            var raiz = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(20, 10, 20, 10), BackColor = Color.Transparent };
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(raiz);

            // Encabezado (vidrio): marca a la izquierda; idioma y tema a la derecha, en orbes [HERRAMIENTA]
            var cab = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 0, 0, 10), Padding = new Padding(16, 4, 10, 4) };
            Aero.Vidrio(cab, true);
            cab.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            cab.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var marca = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0), Anchor = AnchorStyles.Left };
            marca.Controls.Add(new Label { Text = "Video Lite", Font = fTitulo, AutoSize = true, Margin = new Padding(0, 0, 8, 0) });
            marca.Controls.Add(L(new Label { ForeColor = Paleta.Ink2, AutoSize = true, Margin = new Padding(0, 12, 0, 0) }, "appBy"));
            var acciones = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0), Anchor = AnchorStyles.Right };
            btnIdioma = L(Boton("", TipoBoton.Herramienta, (s, e) => CambiarIdioma()), "otroIdioma");
            ((BotonAero)btnIdioma).Icono = IconoBoton.Sigla;   // EN o ES: el otro idioma
            btnTema = Boton(Paleta.Oscuro ? Tx.T("temaClaro") : Tx.T("temaOscuro"), TipoBoton.Herramienta, (s, e) => CambiarTema());
            ((BotonAero)btnTema).Icono = IconoBoton.Tema;
            foreach (var b in new[] { btnIdioma, btnTema }) { b.Margin = new Padding(6, 0, 0, 0); acciones.Controls.Add(b); }
            ActualizarGlobosCabecera();
            cab.Controls.Add(marca, 0, 0); cab.Controls.Add(acciones, 1, 0);
            raiz.Controls.Add(cab, 0, 0);

            avisoFfmpeg = new Label { AutoSize = true, ForeColor = Paleta.Ink, BackColor = Paleta.AmberSoft, Padding = new Padding(10, 6, 10, 6), Margin = new Padding(0, 0, 0, 10), Visible = false, MaximumSize = new Size(2000, 0) };
            raiz.Controls.Add(avisoFfmpeg, 0, 1);

            // "Activar editor" (vidrio, del ancho de su texto): antes de los controles, como en la web; despliega el editor
            var filaEditor = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0, 0, 0, 10), Padding = new Padding(12, 5, 16, 5) };
            Aero.Vidrio(filaEditor, false);
            chkEditor = L(new CasillaAero { Font = fBold, AutoSize = true, Margin = new Padding(0, 0, 8, 0) }, "edActivar");
            chkEditor.CheckedChanged += (s, e) => AlternarEditor();
            filaEditor.Controls.Add(chkEditor);
            var ayudaEditor = Ayuda("edActivarAyuda"); ayudaEditor.Margin = new Padding(0, 3, 0, 0);
            filaEditor.Controls.Add(ayudaEditor);
            raiz.Controls.Add(filaEditor, 0, 2);

            cuerpo = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
            cuerpo.Resize += (s, e) => AjustarAltoAjustes();
            cuerpo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57));
            cuerpo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
            raiz.Controls.Add(cuerpo, 0, 3);
            cuerpo.Controls.Add(ConstruirVideos(), 0, 0);
            cuerpo.Controls.Add(ConstruirAjustes(), 1, 0);

            // Pie (vidrio): el café, [SECUNDARIO], y luego los datos y los enlaces, todos a media altura del botón
            var pie = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0), Padding = new Padding(6, 3, 6, 3) };
            Aero.Vidrio(pie, true);
            btnKofi = L(Boton("", TipoBoton.Secundario, (s, e) => Abrir("https://ko-fi.com/kierczedran")), "kofi");
            btnKofi.Font = fChico; btnKofi.Margin = new Padding(0, 0, 8, 0); btnKofi.Padding = new Padding(4, 1, 4, 1);
            pie.Controls.Add(btnKofi);
            lblPie = new Label { ForeColor = Paleta.Ink2, AutoSize = true, Font = fChico, Margin = new Padding(0, 0, 4, 0) };
            L(lblPie, "pie", C.Version);
            pie.Controls.Add(lblPie);
            pie.Controls.Add(Enlace(null, (s, e) => Abrir(Tx.T("urlSitio")), "videolite.zedran.xyz"));   // la página en el idioma de la app
            pie.Controls.Add(L(Enlace("https://discord.com/invite/2ZRbaYDAnR"), "reportar"));
            pie.Controls.Add(Enlace("mailto:Kierc117@hotmail.com?subject=Video%20Lite", null, "Kierc117@hotmail.com"));
            pie.Controls.Add(L(Enlace(null, (s, e) => Abrir(Tx.T("urlPrivacidad"))), "privacidad"));
            pie.Controls.Add(L(Enlace(null, (s, e) => MostrarLicencias()), "licencias"));
            lnkEnviarA = Enlace(null, (s, e) => AlternarEnviarA());
            pie.Controls.Add(lnkEnviarA);
            foreach (Control c in pie.Controls) c.Anchor = AnchorStyles.Left;   // sin arriba ni abajo: centrados en su línea
            raiz.Controls.Add(pie, 0, 4);

            actualizando = true; tam.Value = 25; actualizando = false;
            ActualizarEnlaceEnviarA();
            AplicarAuto();
            Shown += (s, e) => { RevisarFfmpeg(); if (argsIniciales.Length > 0) Agregar(argsIniciales); };
            FormClosing += AlCerrar;
            Refrescar();
        }

        // Los orbes no llevan texto: su nombre va en el globo (y en el nombre accesible)
        void ActualizarGlobosCabecera()
        {
            ((BotonAero)btnIdioma).Sigla = Tx.Idioma == "es" ? "EN" : "ES";
            foreach (var b in new[] { btnIdioma, btnTema }) { b.AccessibleName = b.Text; globos.SetToolTip(b, b.Text); b.Invalidate(); }
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
            var cb = Aero.Campo(new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330, Margin = new Padding(0, 2, 0, 10) });   // con Aero.ConBorde, el margen pasa a su borde
            foreach (string k in claves) cb.Items.Add(Tx.T(k));
            cb.SelectedIndex = sel;
            combos.Add(Tuple.Create(cb, claves));
            return cb;
        }

        Control ConstruirVideos()
        {
            // Panel de vidrio con su barra de título; adentro, los botones y la lista (sólida, con esquinas redondas)
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Margin = new Padding(0, 0, 14, 0), Padding = new Padding(2) };
            Aero.Vidrio(t, true);
            colVideos = t;
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));   // el editor, plegado
            lblSecVideos = L(new TituloAero { Font = fSec }, "secVideos");
            t.Controls.Add(lblSecVideos, 0, 0);

            var botones = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(14, 10, 14, 6) };
            btnAgregarVideos = L(Boton("", TipoBoton.Accion, (s, e) => ElegirVideos()), "agregarVideos");
            btnAgregarCarpeta = L(Boton("", TipoBoton.Accion, (s, e) => ElegirCarpeta()), "agregarCarpeta");
            botones.Controls.Add(btnAgregarVideos);
            botones.Controls.Add(btnAgregarCarpeta);
            botones.Controls.Add(L(Boton("", TipoBoton.Terciario, (s, e) => QuitarSeleccionados()), "quitarSel"));
            botones.Controls.Add(L(Boton("", TipoBoton.Terciario, (s, e) => Limpiar()), "limpiar"));
            t.Controls.Add(botones, 0, 1);

            // La lista es de Windows (sólida): va en su tarjeta, con el encabezado de la paleta y las esquinas redondas
            lista = new ListView { View = View.Details, FullRowSelect = true, ShowItemToolTips = true, HideSelection = false, AllowDrop = true };
            marcoLista = Aero.TarjetaLista(lista);
            foreach (string k in ColKeys) lista.Columns.Add(Tx.T(k), 120);
            lista.DragEnter += AlArrastrar;
            lista.DragDrop += AlSoltar;
            lista.KeyDown += (s, e) => { if (e.KeyCode == Keys.Delete) QuitarSeleccionados(); };
            lista.DoubleClick += (s, e) => AbrirSeleccion();
            lista.Resize += (s, e) => AjustarColumnas();
            t.Controls.Add(marcoLista, 0, 2);

            var vacio = L(new Label { ForeColor = Paleta.Ink3, AutoSize = true, BackColor = Paleta.Surface, Tag = "vacio" }, "vacio");
            lista.Controls.Add(vacio);
            vacio.Location = new Point(16, 40);
            var editor = ConstruirEditor();
            editor.Visible = false;
            t.Controls.Add(editor, 0, 3);
            return t;
        }

        Control ConstruirAjustes()
        {
            // Panel de vidrio: la barra de título (fija), los ajustes (se desplazan) y la parte fija (estimado y Procesar)
            var marco = marcoAjustes = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = new Padding(0), Padding = new Padding(2) };
            Aero.Vidrio(marco, true);
            marco.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            marco.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            marco.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            lblSecAjustes = L(new TituloAero { Font = fSec }, "secAjustes");
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(16, 12, 16, 4), AutoScroll = true, Margin = new Padding(0) };
            panelAjustes = panel;
            var f = flujoAjustes = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Dock = DockStyle.Top, BackColor = Color.Transparent };
            panel.Controls.Add(f);
            var fijo = fijoAjustes = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(16, 4, 16, 12), Margin = new Padding(0) };
            marco.Controls.Add(lblSecAjustes, 0, 0);
            marco.Controls.Add(panel, 0, 1);
            marco.Controls.Add(fijo, 0, 2);
            // Solo a las etiquetas y a las secciones: con MaximumSize, el ComboBox pierde su alto en el FlowLayoutPanel.
            panel.Resize += (s, e) => AjustarAjustes();

            FlowLayoutPanel sec;   // cada grupo va en su sección: debajo del editor se acomodan en columnas

            // Tamaño: destino + deslizador + número
            sec = Seccion();
            sec.Controls.Add(Leyenda("legTam"));
            sec.Controls.Add(L(new Label { AutoSize = true, ForeColor = Paleta.Ink2, Font = fChico, Margin = new Padding(0, 0, 0, 0) }, "paraEnviar"));
            cbDestino = Combo(DestKeys, DEST_AUTO);
            cbDestino.SelectedIndexChanged += (s, e) => { if (!actualizando) CambioDestino(); };
            sec.Controls.Add(Aero.ConBorde(cbDestino));
            var filaTam = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 10) };
            barraTam = new Deslizador { Minimum = 0, Maximum = 1000, SmallChange = 10, LargeChange = 100, Width = 220, Margin = new Padding(0, 2, 8, 0) };
            barraTam.Scroll += (s, e) => { if (!actualizando) PonerTam(DesdeBarra(barraTam.Value), true); };
            tam = new NumericUpDown { DecimalPlaces = 1, Minimum = 0.5m, Maximum = (decimal)Motor.MaxMB, Increment = 1, Width = 90, TextAlign = HorizontalAlignment.Right, Font = fBold, Margin = new Padding(0, 4, 4, 0), BackColor = Paleta.Bg, ForeColor = Paleta.Ink };
            tam.ValueChanged += (s, e) => { if (!actualizando) PonerTam((double)tam.Value, true); };
            filaTam.Controls.Add(barraTam); filaTam.Controls.Add(tam);
            filaTam.Controls.Add(new Label { Text = "MB", AutoSize = true, Font = fBold, Margin = new Padding(0, 8, 0, 0) });
            sec.Controls.Add(filaTam);

            // Calidad
            sec = Seccion();
            sec.Controls.Add(Leyenda("legCalidad"));
            var filaRes = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0) };
            foreach (var r in new[] { Tuple.Create("1080p", 1080), Tuple.Create("720p", 720), Tuple.Create("480p", 480), Tuple.Create("360p", 360) }.Reverse().Concat(new[] { Tuple.Create("", 0) }).Reverse())
            {
                var rb = new OpcionAero { Text = r.Item1, Tag = r.Item2, AutoSize = true, Checked = r.Item2 == 0, Margin = new Padding(0, 0, 4, 2) };
                if (r.Item2 == 0) L(rb, "resOriginal");
                rb.CheckedChanged += (s, e) => { if (((RadioButton)s).Checked) { aj.Res = (int)((RadioButton)s).Tag; Cambio(); } };
                radiosRes.Add(rb); filaRes.Controls.Add(rb);
            }
            sec.Controls.Add(filaRes);
            sec.Controls.Add(AyudaAjuste("helpRes", radiosRes.Cast<Control>().ToArray()));

            // Velocidad (en la misma sección que la calidad)
            sec.Controls.Add(Leyenda("legVel"));
            var filaVel = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0) };
            foreach (var v in new[] { 0.5, 1, 1.5, 2 })
            {
                var rb = new OpcionAero { Text = "×" + v.ToString(CI), Tag = v, AutoSize = true, Checked = v == 1, Margin = new Padding(0, 0, 4, 2) };
                rb.CheckedChanged += (s, e) => { if (((RadioButton)s).Checked) { aj.Speed = (double)((RadioButton)s).Tag; Cambio(); } };
                radiosVel.Add(rb); filaVel.Controls.Add(rb);
            }
            sec.Controls.Add(filaVel);
            sec.Controls.Add(AyudaAjuste("helpVel", radiosVel.Cast<Control>().ToArray()));

            // Audio
            sec = Seccion();
            sec.Controls.Add(Leyenda("legAudio"));
            chkFix = L(new CasillaAero { Font = fBold, Checked = true, AutoSize = true, Margin = new Padding(0) }, "fixAudio");
            chkFix.CheckedChanged += (s, e) => { aj.Fix = chkFix.Checked; };
            sec.Controls.Add(chkFix);
            sec.Controls.Add(AyudaAjuste("helpFix", chkFix));
            chkVoz = L(new CasillaAero { Font = fBold, AutoSize = true, Margin = new Padding(0) }, "mejorarVoz");
            chkVoz.CheckedChanged += (s, e) => { aj.Voz = chkVoz.Checked; };
            sec.Controls.Add(chkVoz);
            sec.Controls.Add(AyudaAjuste("helpVoz", chkVoz));

            // Más opciones (plegable): formato, giro, sin audio, aviso al terminar (en la sección del audio)
            btnMas = L(Boton("", TipoBoton.Terciario, (s, e) => { panelMas.Visible = !panelMas.Visible; ActualizarBotonMas(); AjustarAltoAjustes(); }), "masOpciones");
            btnMas.Font = fChico;
            sec.Controls.Add(btnMas);
            panelMas = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Visible = false, Margin = new Padding(0, 4, 0, 8), BackColor = Color.Transparent };
            panelMas.Controls.Add(Leyenda("legFormato"));
            cbFormato = Combo(FmtKeys, 0);
            cbFormato.SelectedIndexChanged += (s, e) => { aj.Formato = FmtVals[cbFormato.SelectedIndex]; Cambio(); };
            panelMas.Controls.Add(Aero.ConBorde(cbFormato));
            ayudaSoloAudio = Ayuda("helpSoloAudio"); ayudaSoloAudio.Visible = false;
            panelMas.Controls.Add(ayudaSoloAudio);
            panelMas.Controls.Add(Leyenda("legGirar"));
            cbRotar = Combo(RotKeys, 0);
            cbRotar.SelectedIndexChanged += (s, e) => { aj.Rotacion = RotVals[cbRotar.SelectedIndex]; Refrescar(); };
            panelMas.Controls.Add(Aero.ConBorde(cbRotar));
            chkSinAudio = L(new CasillaAero { AutoSize = true, Margin = new Padding(0, 0, 0, 4) }, "sinAudio");
            chkSinAudio.CheckedChanged += (s, e) => { aj.SinAudio = chkSinAudio.Checked; Cambio(); };
            panelMas.Controls.Add(chkSinAudio);
            chkAvisar = L(new CasillaAero { AutoSize = true, Margin = new Padding(0, 0, 0, 4) }, "avisar");
            panelMas.Controls.Add(chkAvisar);
            sec.Controls.Add(panelMas);

            // Guardar en
            sec = Seccion();
            sec.Controls.Add(Leyenda("legGuardar"));
            chkJunto = L(new CasillaAero { Checked = true, AutoSize = true, Margin = new Padding(0) }, "junto");
            var filaCarp = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 4, 0, 10) };
            txtCarpeta = new TextBox { Width = 260, Enabled = false, BackColor = Paleta.Bg, ForeColor = Paleta.Ink, Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Video Lite") };
            btnCarpeta = L(Boton("", TipoBoton.Terciario, (s, e) => ElegirSalida()), "cambiar");
            btnCarpeta.Enabled = false;
            chkJunto.CheckedChanged += (s, e) => { txtCarpeta.Enabled = btnCarpeta.Enabled = !chkJunto.Checked; };
            filaCarp.Controls.Add(txtCarpeta); filaCarp.Controls.Add(btnCarpeta);
            sec.Controls.Add(chkJunto); sec.Controls.Add(filaCarp);

            // Parte fija: estimado, botón, progreso
            var sep = sepAjustes = new Label { AutoSize = false, Height = 1, Width = 400, BackColor = Paleta.Line, Margin = new Padding(0, 0, 0, 8) };
            fijo.Controls.Add(sep);
            estGrande = new Label { Text = "", Font = fGrande, AutoSize = true, Margin = new Padding(0) };
            estLinea1 = new Label { Text = "", ForeColor = Paleta.Ink2, AutoSize = true, Margin = new Padding(0, 2, 0, 0) };
            estLinea2 = new Label { Text = "", ForeColor = Paleta.Ink2, AutoSize = true, Margin = new Padding(0, 2, 0, 8) };
            fijo.Controls.Add(estGrande); fijo.Controls.Add(estLinea1); fijo.Controls.Add(estLinea2);
            var acciones = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0, 0, 0, 6) };
            btnIr = Boton("", TipoBoton.Principal, (s, e) => Iniciar());
            btnIr.Font = fSec; btnIr.Padding = new Padding(18, 6, 18, 6);
            btnCancelar = L(Boton("", TipoBoton.Accion, (s, e) => Cancelar()), "cancelar");
            btnCancelar.Visible = false;
            btnAbrir = L(Boton("", TipoBoton.Secundario, (s, e) => AbrirCarpeta()), "abrirCarpeta");
            btnAbrir.Visible = false;
            foreach (var b in new[] { btnCancelar, btnAbrir }) b.Anchor = AnchorStyles.Left;   // a media altura del grande
            acciones.Controls.Add(btnIr); acciones.Controls.Add(btnCancelar); acciones.Controls.Add(btnAbrir);
            fijo.Controls.Add(acciones);
            progreso = new BarraAero { Width = 400, Height = 12, Maximum = 1000, Visible = false, Margin = new Padding(0, 0, 0, 6) };
            estado = new Label { Text = "", ForeColor = Paleta.Ink2, AutoSize = true, Margin = new Padding(0) };
            fijo.Controls.Add(progreso); fijo.Controls.Add(estado);
            estado.Visible = false;   // sin texto no ocupa alto; aparece con el primer mensaje
            estado.TextChanged += (s, e) => estado.Visible = estado.Text != "";
            // Al procesar crece (barra y mensaje): la franja también. Después del acomodo en curso, porque un cambio de alto
            // pedido mientras "cuerpo" se acomoda no se aplica hasta el siguiente acomodo.
            fijo.Layout += (s, e) => { if (IsHandleCreated) BeginInvoke((Action)AjustarAltoAjustes); };
            fijo.Resize += (s, e) =>
            {
                int an = Math.Max(200, fijo.ClientSize.Width - 32);
                sep.Width = progreso.Width = an;
                foreach (Control c in new Control[] { estLinea1, estLinea2, estado }) c.MaximumSize = new Size(an, 0);
            };
            return marco;
        }

        // Ayuda de un ajuste: debajo del control en la columna de la derecha; en la franja de abajo del editor se oculta
        // y su texto queda en el globo de esos controles (para que la franja sea baja).
        Label AyudaAjuste(string clave, params Control[] de)
        {
            var l = Ayuda(clave);
            ayudasAjustes.Add(Tuple.Create(l, de));
            return l;
        }

        void ActualizarGlobos()
        {
            foreach (var a in ayudasAjustes)
                foreach (var c in a.Item2) globos.SetToolTip(c, editorOn ? a.Item1.Text : null);
        }

        FlowLayoutPanel Seccion()
        {
            var sec = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, BackColor = Color.Transparent, Margin = new Padding(0, 0, 12, 0) };
            flujoAjustes.Controls.Add(sec);
            seccionesAjustes.Add(sec);
            return sec;
        }

        static IEnumerable<Control> Descendientes(Control c)
        {
            foreach (Control h in c.Controls) { yield return h; foreach (var x in Descendientes(h)) yield return x; }
        }

        // Con el editor activo, los ajustes pasan debajo del editor, a todo el ancho y en columnas (como en la web), con el
        // estimado y "Procesar" a la derecha: el editor se queda con casi toda la ventana. Sin el editor, vuelven a la derecha.
        void AcomodoAjustes()
        {
            bool abajo = editorOn;
            cuerpo.SuspendLayout(); marcoAjustes.SuspendLayout();
            cuerpo.ColumnCount = 2; cuerpo.RowCount = 2;
            cuerpo.SetCellPosition(marcoAjustes, abajo ? new TableLayoutPanelCellPosition(0, 1) : new TableLayoutPanelCellPosition(1, 0));
            cuerpo.ColumnStyles.Clear(); cuerpo.RowStyles.Clear();
            if (abajo)
            {
                cuerpo.ColumnCount = 1;
                cuerpo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                cuerpo.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                cuerpo.RowStyles.Add(new RowStyle(SizeType.Absolute, fBase.Height * 14));
                colVideos.Margin = new Padding(0, 0, 0, 10);
            }
            else
            {
                cuerpo.RowCount = 1;
                cuerpo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57));
                cuerpo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
                cuerpo.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                colVideos.Margin = new Padding(0, 0, 12, 0);
            }
            // Fila 0: la barra de título (oculta abajo del editor); fila 1: los ajustes y, abajo del editor, a su derecha lo fijo
            marcoAjustes.ColumnCount = 2; marcoAjustes.RowCount = 3;
            marcoAjustes.SetColumnSpan(lblSecAjustes, abajo ? 2 : 1);
            marcoAjustes.SetCellPosition(fijoAjustes, abajo ? new TableLayoutPanelCellPosition(1, 1) : new TableLayoutPanelCellPosition(0, 2));
            marcoAjustes.ColumnStyles.Clear(); marcoAjustes.RowStyles.Clear();
            marcoAjustes.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            marcoAjustes.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            if (abajo)
            {
                marcoAjustes.RowCount = 2;
                marcoAjustes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                marcoAjustes.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, AnchoFijo));
            }
            else
            {
                marcoAjustes.ColumnCount = 1;
                marcoAjustes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                marcoAjustes.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }
            flujoAjustes.FlowDirection = abajo ? FlowDirection.LeftToRight : FlowDirection.TopDown;
            flujoAjustes.WrapContents = abajo;
            lblSecAjustes.Visible = !abajo;    // abajo del editor, la franja se explica sola
            sepAjustes.Visible = !abajo;       // la raya separa el estimado de los ajustes de arriba; abajo van lado a lado
            barraTam.Width = abajo ? 160 : 220;   // en columnas: que quepan el número y "MB"
            for (int i = 0; i < radiosRes.Count; i++)     // y las cinco calidades en una línea
                radiosRes[i].Margin = new Padding(0, 0, i == radiosRes.Count - 1 ? 0 : abajo ? 1 : 4, 2);
            txtCarpeta.Width = abajo ? 200 : 260;
            foreach (var a in ayudasAjustes) a.Item1.Visible = !abajo;
            ActualizarGlobos();
            marcoAjustes.ResumeLayout(true); cuerpo.ResumeLayout(true);
            AjustarAjustes();
            AjustarAltoAjustes();
            marcoAjustes.Invalidate();
        }

        // Debajo del editor, ancho de la columna del estimado y "Procesar" (a la derecha de los ajustes).
        int AnchoFijo { get { return fBase.Height * 15; } }

        // Ancho de las etiquetas y de las secciones: una sola columna a la derecha, o columnas de ~20 líneas de ancho abajo.
        void AjustarAjustes()
        {
            if (panelAjustes == null || flujoAjustes == null) return;
            int an = Math.Max(200, panelAjustes.ClientSize.Width - panelAjustes.Padding.Horizontal - 4);
            int col = an;
            if (editorOn) { int n = Math.Max(1, Math.Min(seccionesAjustes.Count, an / (fBase.Height * 19))); col = (an - 4) / n - 12; }   // 4 px de holgura: con el ancho exacto, la última sección baja de línea
            flujoAjustes.MaximumSize = editorOn ? new Size(an, 0) : Size.Empty;
            foreach (var sec in seccionesAjustes)
            {
                sec.MinimumSize = editorOn ? new Size(col, 0) : Size.Empty;
                sec.MaximumSize = editorOn ? new Size(col, 0) : Size.Empty;
            }
            int etiqueta = editorOn ? col - 4 : Math.Max(200, panelAjustes.ClientSize.Width - 36);
            foreach (Control c in Descendientes(flujoAjustes)) if (c is Label || c is CasillaAero) c.MaximumSize = new Size(etiqueta, 0);
            // Las filas de calidades y velocidades se parten a este ancho; sin tope, se quedan partidas al ancho de un acomodo anterior.
            foreach (var fila in new[] { radiosRes[0].Parent, radiosVel[0].Parent }) fila.MaximumSize = new Size(editorOn ? col : etiqueta, 0);
        }

        // Debajo del editor, la franja de ajustes mide lo que su contenido, pero a lo más el 30 % del alto (y se desplaza adentro);
        // el estimado y "Procesar" siempre se ven completos.
        void AjustarAltoAjustes()
        {
            if (!editorOn || cuerpo == null || cuerpo.RowStyles.Count < 2) return;
            int an = Math.Max(200, marcoAjustes.Width - AnchoFijo - panelAjustes.Padding.Horizontal - marcoAjustes.Padding.Horizontal - 8);
            int contenido = flujoAjustes.GetPreferredSize(new Size(an, 0)).Height + panelAjustes.Padding.Vertical + marcoAjustes.Padding.Vertical + 8;
            int boton = fijoAjustes.GetPreferredSize(new Size(AnchoFijo, 0)).Height + marcoAjustes.Padding.Vertical + 4;
            int alto = Math.Max(fBase.Height * 6, Math.Max(Math.Min(contenido, (int)(cuerpo.Height * 0.30)), boton));
            if (Math.Abs(cuerpo.RowStyles[1].Height - alto) > 2) cuerpo.RowStyles[1].Height = alto;
        }

        Label Leyenda(string clave) { return L(new Label { Font = fBold, AutoSize = true, Margin = new Padding(0, 0, 0, 4) }, clave); }
        Label Ayuda(string clave) { return L(new Label { ForeColor = Paleta.Ink3, Font = fChico, AutoSize = true, Margin = new Padding(0, 2, 0, 10) }, clave); }

        // Uno de los cinco tipos de botón de Zedran Aero (Aero.cs)
        Button Boton(string t, TipoBoton tipo, EventHandler click)
        {
            var b = new BotonAero(tipo) { Text = t, AutoSize = true, Margin = new Padding(0, 0, 4, 4), Padding = new Padding(8, 3, 8, 3) };
            b.Click += click;
            return b;
        }

        // Enlace: el texto del color normal y el subrayado verde (EnlaceAero); con url, la abre al hacer clic
        LinkLabel Enlace(string url, EventHandler click = null, string texto = null)
        {
            var l = new EnlaceAero { Text = texto ?? "", Font = fChico, Url = url };
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
            btnTema.Text = Paleta.Oscuro ? Tx.T("temaClaro") : Tx.T("temaOscuro");
            ActualizarGlobosCabecera();
            ActualizarBotonMas();
            ActualizarEnlaceEnviarA();
            if (avisoFfmpeg.Visible) avisoFfmpeg.Text = Tx.T("ffmpegFalta");
            foreach (var it in items) EscribirEstado(it);
            if (edLinea != null) edLinea.AccessibleName = Tx.T("edLinea");
            ActualizarGlobos();
            ResumeLayout(true);
            Refrescar();
            EdPintar();
            lista.Invalidate();
        }

        // Aero.CambiarTema cambia cada color de la paleta por el del otro tema (también en la lista), la barra de título y
        // las barras de desplazamiento.
        void CambiarTema()
        {
            SuspendLayout();
            Aero.CambiarTema(this, !Paleta.Oscuro);
            btnTema.Text = Paleta.Oscuro ? Tx.T("temaClaro") : Tx.T("temaOscuro");
            ActualizarGlobosCabecera();
            ResumeLayout(true);
            Preferencias.Set("tema", Paleta.Oscuro ? "oscuro" : "claro");
            Refrescar();
            EdPintar();
            Invalidate(true);
        }

        /* ---------- "Enviar a" de Windows ---------- */

        static string AtajoEnviarA() { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.SendTo), "Video Lite.lnk"); }

        void ActualizarEnlaceEnviarA() { lnkEnviarA.Text = Tx.T(File.Exists(AtajoEnviarA()) ? "quitarEnviarA" : "enviarA"); }

        // Crea o quita el acceso "Video Lite" del menú Enviar a (clic derecho en un archivo). Solo en la cuenta del usuario.
        // Antes de crearlo pregunta (sí / no). "lnk": otra ruta para el acceso, solo en las pruebas.
        void AlternarEnviarA(string lnk = null)
        {
            lnk = lnk ?? AtajoEnviarA();
            try
            {
                if (File.Exists(lnk))
                {
                    File.Delete(lnk);
                    MessageBox.Show(this, Tx.T("enviarAquitado"), "Video Lite", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    if (MessageBox.Show(this, Tx.T("enviarAConfirm"), "Video Lite", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
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
            using (var d = new OpenFileDialog { Multiselect = !editorOn, Title = Tx.T("elegirVideos"), Filter = Tx.T("filtroVideos") + "|*" + string.Join(";*", Ext) + "|" + Tx.T("filtroTodos") + "|*.*" })
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
            if (editorOn)
            {
                // El editor trabaja con un video a la vez: el primero reemplaza al que estaba (con aviso si tenía cambios).
                var validos = rutas.Where(a => File.Exists(a) && Ext.Contains(Path.GetExtension(a).ToLowerInvariant())).ToList();
                if (validos.Count == 0) return;
                if (items.Count > 0 && string.Equals(items[0].Ruta, validos[0], StringComparison.OrdinalIgnoreCase)) return;
                if (items.Count > 0 && EdicionPendiente(items[0]) &&
                    MessageBox.Show(this, Tx.T("edPerder", Path.GetFileName(items[0].Ruta)), "Video Lite", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                items.Clear(); lista.Items.Clear(); EdCargar(null);
                if (validos.Count > 1 || rutas.Count() > 1) { estadoFinal = Tx.T("edUnArchivo"); estado.Text = estadoFinal; }
                rutas = new[] { validos[0] };
            }
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
                        if (editorOn && items.Count > 0 && items[0] == cap && info != null) EdCargar(cap);
                        if (auto) AplicarAuto(); else Refrescar();
                    }));
                }
            });
        }

        void QuitarSeleccionados()
        {
            if (trabajando) return;
            var sel = lista.SelectedItems.Cast<ListViewItem>().ToList();
            var conCambios = sel.Select(lv => (Item)lv.Tag).FirstOrDefault(EdicionPendiente);
            if (conCambios != null && MessageBox.Show(this, Tx.T("edPerder", Path.GetFileName(conCambios.Ruta)), "Video Lite", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            foreach (ListViewItem lv in sel) { items.Remove((Item)lv.Tag); lista.Items.Remove(lv); if (lv.Tag == edItem) EdCargar(null); }
            if (auto) AplicarAuto(); else Refrescar();
        }

        void Limpiar()
        {
            if (trabajando) return;
            var conCambios = items.FirstOrDefault(EdicionPendiente);
            if (conCambios != null && MessageBox.Show(this, Tx.T("edPerder", Path.GetFileName(conCambios.Ruta)), "Video Lite", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            items.Clear(); lista.Items.Clear(); btnAbrir.Visible = false; estado.Text = ""; estadoFinal = null;
            EdCargar(null);
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
            PonerTam(listos.Count > 0 ? listos.Max(i => Motor.RecomendarMB(i.Info, i.Size, aj, EdicionDe(i) != null ? i.Ed.Duracion() : i.Info.Dur)) : 25, false);
        }

        void Cambio() { if (auto) AplicarAuto(); else Refrescar(); }

        /* ---------- Pintado ---------- */

        static string Mb(double bytes) { return Motor.Tam(bytes); }
        static string Dur(double s)
        {
            int t = (int)Math.Round(s), h = t / 3600, m = (t % 3600) / 60, x = t % 60;
            return h > 0 ? string.Format("{0} h {1:00} min", h, m) : m > 0 ? string.Format("{0} min {1:00} s", m, x) : x + " s";
        }

        // Texto de la columna "Estado" de un video ya procesado (se reescribe al cambiar de idioma).
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
                rb.Tachada = solo || muyGrande;
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
                if (it.Estado == "error") { sub[4].Text = it.Error; it.Lv.ForeColor = Paleta.Danger; continue; }
                if (it.Estado == "hecho" || it.Estado == "trabajando" || it.Info == null) continue;
                var ed = EdicionDe(it);
                it.Plan = Motor.Planear(it.Info, it.Size, aj, ed != null ? ed.Duracion() : it.Info.Dur);
                it.Plan.Ed = ed != null ? ed.Copia() : null;    // una copia: el hilo de compresión no ve los cambios a medias
                bool dura = aj.Speed != 1 || ed != null;
                if (it.Plan.SoloAudio)
                {
                    if (it.Plan.SinPistaAudio) { sub[2].Text = "-"; sub[3].Text = Tx.T("sinPista"); sub[4].Text = ""; alguno = true; continue; }
                    sub[2].Text = "≈ " + Mb(it.Plan.EstMB * 1e6);
                    sub[3].Text = Tx.T("soloAudio", aj.Formato.ToUpperInvariant());
                    sub[4].Text = Tx.T("porComprimir", aj.Formato.ToUpperInvariant()) + (dura ? Tx.T("dura", Dur(it.Plan.OutDur)) : "");
                    it.Lv.ForeColor = Paleta.Ink;
                    antes += it.Size; despues += it.Plan.EstMB * 1e6; n++;
                    continue;
                }
                if (it.Plan.TooSmall) { sub[2].Text = Tx.T("noCabe"); sub[3].Text = Tx.T("subeTam"); sub[4].Text = ""; alguno = true; continue; }
                int nivel;
                sub[2].Text = "≈ " + Mb(it.Plan.EstMB * 1e6);
                sub[3].Text = Motor.Calidad(it.Plan, out nivel);
                sub[4].Text = Tx.T("porComprimir", it.Plan.OW + "×" + it.Plan.OH + " " + aj.Formato.ToUpperInvariant()) + (dura ? Tx.T("dura", Dur(it.Plan.OutDur)) : "");
                it.Lv.ForeColor = Paleta.Ink;
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
                estLinea2.ForeColor = Paleta.Ink2;
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
                    estLinea2.ForeColor = nivel == 2 ? Paleta.Ink : nivel == 1 ? Paleta.Amber : Paleta.Danger;
                }
                else { estLinea2.Text = ""; }
            }
            if (!trabajando && estadoFinal != null) estado.Text = estadoFinal;
            int pendientes = items.Count(i => i.Estado == "listo" && i.Plan != null && !i.Plan.TooSmall);
            btnIr.Enabled = !trabajando && pendientes > 0 && Motor.Ffmpeg != null;   // desactivado, BotonAero lo pinta casi gris
            btnIr.Text = trabajando ? Tx.T("comprimiendo") : pendientes > 1 ? Tx.T("comprimirN", pendientes) : Tx.T("comprimir");
            chkEditor.Enabled = !trabajando;
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
            if (rep != null) rep.Pausa();
            Refrescar();
            EdPintar();
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
                            cap.Lv.ForeColor = noCupo ? Paleta.Danger : Paleta.Ink;
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
                            if (!Motor.Cancelado) cap.Lv.ForeColor = Paleta.Danger;
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
                    EdPintar();
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
            if (!trabajando)
            {
                var conCambios = items.FirstOrDefault(EdicionPendiente);
                if (conCambios != null && MessageBox.Show(this, Tx.T("edPerder", Path.GetFileName(conCambios.Ruta)), "Video Lite", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) e.Cancel = true;
                if (!e.Cancel && rep != null) rep.Cerrar();
                return;
            }
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
        // Los argumentos son archivos o carpetas (los manda el menú "Enviar a" o se arrastran sobre el .exe).
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Ventana(args));
        }
    }
}
