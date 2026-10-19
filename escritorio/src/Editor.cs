// Video Lite · Zedran Labs: editor de un video en la app (P-026), igual que el de la web (sitio/editor.js):
// una casilla "Activar editor" lo despliega en lugar de la lista, con el visor (Reproductor de Windows Media),
// la línea de tiempo con el video y el audio en pistas separadas y cinco operaciones: recortar el inicio y el fin,
// quitar tramos, silenciar el audio por tramos, el volumen y mover el audio.
// No toca el archivo: solo describe la edición (Item.Ed) y Motor.Comprimir la aplica con el mismo grafo que la web.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace VideoLite
{
    // Reproductor de Windows Media embebido sin aximp: el control ActiveX por su CLSID.
    class Wmp : AxHost
    {
        public Wmp() : base("6bf52a52-394a-11d3-b153-00c04f79faa6") { }
        public object Ocx { get { return GetOcx(); } }
    }

    // El reproductor por IDispatch, sin ensamblados de interop. Cualquier falla se trata como "sin vista previa".
    class Reproductor
    {
        readonly object ocx;
        bool? mudo;
        int vol = -1;
        static object G(object o, string n) { return o.GetType().InvokeMember(n, BindingFlags.GetProperty, null, o, null); }
        static void S(object o, string n, object v) { o.GetType().InvokeMember(n, BindingFlags.SetProperty, null, o, new[] { v }); }
        static void M(object o, string n) { o.GetType().InvokeMember(n, BindingFlags.InvokeMethod, null, o, null); }

        public Reproductor(object ocx)
        {
            this.ocx = ocx;
            S(ocx, "uiMode", "none");
            S(ocx, "stretchToFit", true);
            S(ocx, "enableContextMenu", false);
            S(G(ocx, "settings"), "autoStart", false);
        }
        public void Abrir(string ruta) { mudo = null; vol = -1; try { S(ocx, "URL", ruta); } catch (Exception) { } }
        public void Cerrar() { try { M(G(ocx, "controls"), "stop"); M(ocx, "close"); } catch (Exception) { } }
        public void Play() { try { M(G(ocx, "controls"), "play"); } catch (Exception) { } }
        public void Pausa() { try { M(G(ocx, "controls"), "pause"); } catch (Exception) { } }
        public double Pos
        {
            get { try { return Convert.ToDouble(G(G(ocx, "controls"), "currentPosition"), CultureInfo.InvariantCulture); } catch (Exception) { return 0; } }
            set { try { S(G(ocx, "controls"), "currentPosition", value); } catch (Exception) { } }
        }
        public int Estado { get { try { return Convert.ToInt32(G(ocx, "playState"), CultureInfo.InvariantCulture); } catch (Exception) { return 0; } } }   // 3 = reproduciendo
        public bool Tocando { get { return Estado == 3; } }
        public void Sonido(bool silencio, int volumen)
        {
            try
            {
                object cfg = G(ocx, "settings");
                if (mudo != silencio) { S(cfg, "mute", silencio); mudo = silencio; }
                if (vol != volumen) { S(cfg, "volume", volumen); vol = volumen; }
            }
            catch (Exception) { }
        }
    }

    // Línea de tiempo: pista de video (miniaturas) y pista de audio (forma de onda), con lo recortado, los tramos quitados,
    // los silencios, el tramo marcado, el cabezal y las asas de inicio y fin. Se dibuja con la paleta (C).
    class LineaTiempo : Control
    {
        public Edicion Ed;
        public double Cabezal { get; set; }
        public double? Desde { get; set; }
        public double? Hasta { get; set; }
        public List<Bitmap> Cuadros;
        public float[] Picos;
        public event Action<double> Ir;             // un clic: mover el cabezal (no borra el tramo marcado)
        public event Action<double, double> Marcar; // un arrastre: marca un tramo
        public event Action<string, double> Asa;    // "in" | "out" y el tiempo nuevo
        public event Action<Keys, bool> Tecla;

        const int Mx = 8;
        string arrastre;          // null | "in" | "out" | "sel"
        double t0;
        int x0;
        bool movido;

        public LineaTiempo()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.Slider;
        }

        double Dur { get { return Ed != null && Ed.Dur > 0 ? Ed.Dur : 1; } }
        Rectangle PistaV { get { return new Rectangle(Mx, 6, Math.Max(10, Width - 2 * Mx), (int)((Height - 18) * 0.57)); } }
        Rectangle PistaA { get { var v = PistaV; return new Rectangle(Mx, v.Bottom + 6, v.Width, Math.Max(8, Height - v.Bottom - 12)); } }
        public float X(double t) { var v = PistaV; return (float)(v.Left + Math.Max(0, Math.Min(1, t / Dur)) * v.Width); }
        public double T(int x) { var v = PistaV; return Math.Max(0, Math.Min(Dur, (x - v.Left) / (double)Math.Max(1, v.Width) * Dur)); }

        protected override bool IsInputKey(Keys k)
        {
            Keys b = k & Keys.KeyCode;
            return b == Keys.Left || b == Keys.Right || b == Keys.Home || b == Keys.End || b == Keys.Space || b == Keys.Escape || base.IsInputKey(k);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (Tecla != null) Tecla(e.KeyCode, e.Shift);
            e.Handled = true;
            base.OnKeyDown(e);
        }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            if (Ed == null || e.Button != MouseButtons.Left) return;
            float xi = X(Ed.Entrada), xo = X(Ed.Salida);
            if (Math.Abs(e.X - xi) <= 7 && Math.Abs(e.X - xi) <= Math.Abs(e.X - xo)) arrastre = "in";
            else if (Math.Abs(e.X - xo) <= 7) arrastre = "out";
            else { arrastre = "sel"; t0 = T(e.X); x0 = e.X; movido = false; }
            Capture = true;
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (Ed == null) return;
            if (arrastre == null)
            {
                float xi = X(Ed.Entrada), xo = X(Ed.Salida);
                Cursor = Math.Abs(e.X - xi) <= 7 || Math.Abs(e.X - xo) <= 7 ? Cursors.SizeWE : Cursors.Hand;
                return;
            }
            if (arrastre == "in" || arrastre == "out") { if (Asa != null) Asa(arrastre, T(e.X)); return; }
            if (!movido && Math.Abs(e.X - x0) > 4) movido = true;
            if (movido && Marcar != null) Marcar(t0, T(e.X));
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (arrastre == "sel" && !movido && Ir != null) Ir(T(e.X));
            arrastre = null;
            Capture = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            var v = PistaV; var a = PistaA;
            using (var fondo = new SolidBrush(C.Surface2)) { g.FillRectangle(fondo, v); g.FillRectangle(fondo, a); }
            // Miniaturas: cada una con su proporción, repetidas a lo ancho; la de cada lugar es la de ese momento.
            if (Cuadros != null && Cuadros.Count > 0)
            {
                g.SetClip(v);
                float ancho = Math.Max(8f, v.Height * Cuadros[0].Width / (float)Math.Max(1, Cuadros[0].Height));
                for (float x = v.Left; x < v.Right; x += ancho)
                {
                    double t = T((int)(x + ancho / 2));
                    int k = Math.Max(0, Math.Min(Cuadros.Count - 1, (int)(t / Dur * Cuadros.Count)));
                    g.DrawImage(Cuadros[k], x, v.Top, ancho, v.Height);
                }
                g.ResetClip();
            }
            // Forma de onda
            if (Picos != null && Picos.Length > 0)
            {
                float medio = a.Top + a.Height / 2f;
                using (var p = new Pen(C.Accent))
                    for (int x = a.Left; x < a.Right; x++)
                    {
                        int k = Math.Max(0, Math.Min(Picos.Length - 1, (int)((x - a.Left) / (double)a.Width * Picos.Length)));
                        float h = Math.Max(1f, Picos[k] * (a.Height - 4) / 2f);
                        g.DrawLine(p, x, medio - h, x, medio + h);
                    }
            }
            if (Ed != null)
            {
                // Fuera del recorte: atenuado en las dos pistas
                using (var b = new SolidBrush(Color.FromArgb(170, C.Bg)))
                {
                    if (Ed.Entrada > 0) { g.FillRectangle(b, v.Left, v.Top, X(Ed.Entrada) - v.Left, v.Height); g.FillRectangle(b, a.Left, a.Top, X(Ed.Entrada) - a.Left, a.Height); }
                    if (Ed.Salida < Ed.Dur) { g.FillRectangle(b, X(Ed.Salida), v.Top, v.Right - X(Ed.Salida), v.Height); g.FillRectangle(b, X(Ed.Salida), a.Top, a.Right - X(Ed.Salida), a.Height); }
                }
                // Tramos quitados: rayados en rojo, en las dos pistas
                using (var b = new HatchBrush(HatchStyle.BackwardDiagonal, Color.FromArgb(200, C.Danger), Color.FromArgb(110, C.Bg)))
                    foreach (var c in Edicion.Unir(Ed.Cortes))
                    {
                        float x1 = X(c[0]), x2 = Math.Max(X(c[1]), x1 + 2);
                        g.FillRectangle(b, x1, v.Top, x2 - x1, v.Height); g.FillRectangle(b, x1, a.Top, x2 - x1, a.Height);
                    }
                // Silencios: solo en la pista de audio
                using (var b = new HatchBrush(HatchStyle.DarkUpwardDiagonal, Color.FromArgb(200, C.Ink3), Color.FromArgb(140, C.Surface2)))
                    foreach (var s in Edicion.Unir(Ed.Silencios))
                    {
                        float x1 = X(s[0]), x2 = Math.Max(X(s[1]), x1 + 2);
                        g.FillRectangle(b, x1, a.Top, x2 - x1, a.Height);
                    }
                // Tramo marcado (los dos extremos) o la marca de un solo extremo
                if (Desde.HasValue && Hasta.HasValue)
                {
                    float x1 = X(Math.Min(Desde.Value, Hasta.Value)), x2 = X(Math.Max(Desde.Value, Hasta.Value));
                    using (var b = new SolidBrush(Color.FromArgb(70, C.Accent))) g.FillRectangle(b, x1, v.Top, Math.Max(2, x2 - x1), a.Bottom - v.Top);
                    using (var p = new Pen(C.Accent, 2)) g.DrawRectangle(p, x1, v.Top, Math.Max(2, x2 - x1), a.Bottom - v.Top);
                }
                else if (Desde.HasValue || Hasta.HasValue)
                {
                    float x1 = X(Desde.HasValue ? Desde.Value : Hasta.Value);
                    using (var p = new Pen(C.AccentText, 2) { DashStyle = DashStyle.Dash }) g.DrawLine(p, x1, v.Top, x1, a.Bottom);
                }
                // Asas del recorte
                using (var b = new SolidBrush(C.Accent))
                    foreach (double t in new[] { Ed.Entrada, Ed.Salida })
                    {
                        float x = X(t);
                        g.FillRectangle(b, x - 3, v.Top - 4, 6, a.Bottom - v.Top + 8);
                    }
            }
            // Cabezal
            float xc = X(Cabezal);
            using (var p = new Pen(C.Ink, 2)) g.DrawLine(p, xc, 0, xc, Height);
            using (var b = new SolidBrush(C.Ink)) g.FillPolygon(b, new[] { new PointF(xc - 6, 0), new PointF(xc + 6, 0), new PointF(xc, 7) });
            // Foco de dos anillos (se ve sobre cualquier fondo)
            if (Focused)
            {
                using (var p1 = new Pen(C.Ink, 2)) g.DrawRectangle(p1, 1, 1, Width - 3, Height - 3);
                using (var p2 = new Pen(C.Bg, 1)) g.DrawRectangle(p2, 3, 3, Width - 7, Height - 7);
            }
        }
    }

    public partial class Ventana
    {
        bool editorOn;
        Casilla chkEditor;
        TableLayoutPanel colVideos, edRaiz;
        Panel edCaja;              // se desplaza a lo alto; adentro, edRaiz mide lo que mide su contenido
        Label lblSecVideos, edVacioTxt, edVacioSub, edSinVista, edTiempo, edQueda, edSel, edVolTxt, edNota, edAyuda;
        Button btnAgregarVideos, btnAgregarCarpeta, edElegir, edCambiarVideo, edQuitarVideo, edPlay, edEmpieza, edTermina, edDesde, edHasta, edQuitar, edSilenciar, edReset, edMenos, edMas;
        Panel edVisor, edVacio;
        TrackBar edVol;
        NumericUpDown edDes;
        FlowLayoutPanel edLista;
        LineaTiempo edLinea;
        Wmp edWmp;
        Reproductor rep;
        Item edItem;
        int edCarga;
        DateTime edCargaT0;
        bool edCargando, edActualizando;
        System.Windows.Forms.Timer edReloj, edAnim;

        // La edición cuenta solo con el editor activo (igual que edicionDe en sitio/app.js).
        Edicion EdicionDe(Item it) { return editorOn && it.Ed != null && it.Ed.HayCambios() ? it.Ed : null; }
        bool EdicionPendiente(Item it) { return EdicionDe(it) != null && it.Estado != "hecho"; }

        // m:ss.s, como fmtT en sitio/editor.js
        static string FmtT(double s)
        {
            s = Math.Max(0, s);
            int m = (int)Math.Floor(s / 60);
            double r = s - m * 60;
            return m + ":" + r.ToString("00.0", CultureInfo.InvariantCulture);
        }

        void CambiarClave(Control c, string clave)
        {
            for (int i = 0; i < textos.Count; i++)
                if (textos[i].Item1 == c) { textos[i] = Tuple.Create(c, clave, new object[0]); break; }
            c.Text = Tx.T(clave);
        }

        Control ConstruirEditor()
        {
            edCaja = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = C.Bg, Margin = new Padding(0) };
            edRaiz = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, RowCount = 5, Margin = new Padding(0), BackColor = C.Bg };
            edCaja.Controls.Add(edRaiz);
            edRaiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));   // el ancho visible: nunca se desplaza a lo ancho
            edRaiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 200));  // visor: 16:9 del ancho (se ajusta al cambiar de tamaño)
            for (int i = 1; i < 5; i++) edRaiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Visor: el reproductor, o el aviso sin vista previa, o la zona para soltar un video
            edVisor = new Panel { Dock = DockStyle.Fill, BackColor = Color.Black, Margin = new Padding(0, 0, 0, 6), AllowDrop = true };
            edVisor.DragEnter += AlArrastrar; edVisor.DragDrop += AlSoltar;
            edSinVista = new Label { Dock = DockStyle.Bottom, AutoSize = false, Height = 44, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White, BackColor = Color.FromArgb(40, 40, 40), Visible = false, Font = fChico };
            edVisor.Controls.Add(edSinVista);
            edVacio = new Panel { Dock = DockStyle.Fill, BackColor = C.Surface, AllowDrop = true };
            edVacio.DragEnter += AlArrastrar; edVacio.DragDrop += AlSoltar;
            edVacio.Paint += (s, e) => { using (var p = new Pen(C.LineStrong) { DashStyle = DashStyle.Dash }) e.Graphics.DrawRectangle(p, 6, 6, edVacio.Width - 13, edVacio.Height - 13); };
            var vacioCaja = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, BackColor = C.Surface };
            edVacioTxt = L(new Label { Font = fBold, AutoSize = true, Margin = new Padding(0, 0, 0, 2) }, "edVacio");
            edVacioSub = L(new Label { Font = fChico, ForeColor = C.Ink3, AutoSize = true, Margin = new Padding(0, 0, 0, 8) }, "edVacioSub");
            edElegir = L(Boton("", true, (s, e) => ElegirVideos()), "edElegir");
            vacioCaja.Controls.Add(edVacioTxt); vacioCaja.Controls.Add(edVacioSub); vacioCaja.Controls.Add(edElegir);
            edVacio.Controls.Add(vacioCaja);
            edVacio.Resize += (s, e) => vacioCaja.Location = new Point(Math.Max(8, (edVacio.Width - vacioCaja.Width) / 2), Math.Max(8, (edVacio.Height - vacioCaja.Height) / 2));
            edVisor.Controls.Add(edVacio);
            edRaiz.Controls.Add(edVisor, 0, 0);

            // Reproducir, tiempo y lo que queda
            var transporte = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 5, Margin = new Padding(0, 0, 0, 4) };
            transporte.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            transporte.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            transporte.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            transporte.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            transporte.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            edPlay = Boton("▶", false, (s, e) => EdPlayPausa());
            edPlay.Font = new Font("Segoe UI Symbol", 10f); edPlay.AccessibleName = Tx.T("edReproducir"); edPlay.MinimumSize = new Size(44, 30);
            edTiempo = new Label { AutoSize = true, Font = fChico, ForeColor = C.Ink2, Margin = new Padding(6, 8, 0, 0) };
            edQueda = new Label { AutoSize = true, Font = fBold, Margin = new Padding(0, 8, 0, 0), Anchor = AnchorStyles.Right };
            edQueda.Margin = new Padding(0, 8, 14, 0);
            edCambiarVideo = L(Boton("", false, (s, e) => ElegirVideos()), "edCambiarVideo");
            edQuitarVideo = L(Boton("", false, (s, e) => { if (edItem != null) { lista.SelectedItems.Clear(); edItem.Lv.Selected = true; QuitarSeleccionados(); } }), "edQuitarVideo");
            edCambiarVideo.Font = edQuitarVideo.Font = fChico;
            transporte.Controls.Add(edPlay, 0, 0); transporte.Controls.Add(edTiempo, 1, 0); transporte.Controls.Add(edQueda, 2, 0);
            transporte.Controls.Add(edCambiarVideo, 3, 0); transporte.Controls.Add(edQuitarVideo, 4, 0);
            edRaiz.Controls.Add(transporte, 0, 1);

            // Línea de tiempo
            edLinea = new LineaTiempo { Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, Height = 116, BackColor = C.Bg, Margin = new Padding(0, 0, 0, 2) };
            edLinea.AccessibleName = Tx.T("edLinea");
            edLinea.Ir += t => EdIr(t);
            edLinea.Marcar += (a, b) => { edLinea.Desde = a; edLinea.Hasta = b; EdPintar(); };
            edLinea.Asa += (cual, t) => EdPonerRecorte(cual, t);
            edLinea.Tecla += EdTecla;
            edRaiz.Controls.Add(edLinea, 0, 2);
            edAyuda = Ayuda("edAyuda"); edAyuda.Margin = new Padding(0, 0, 0, 4);
            edRaiz.Controls.Add(edAyuda, 0, 3);

            // Recortar y tramo marcado
            // Cada grupo en su fila (con su nombre al inicio); si no cabe, la fila se parte, pero el nombre sigue con sus botones.
            var ops = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 2) };
            var grupoRecorte = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0) };
            var grupoTramo = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0) };
            grupoRecorte.Controls.Add(L(new Label { Font = fBold, AutoSize = true, Margin = new Padding(0, 8, 6, 0) }, "edRecortar"));
            edEmpieza = L(Boton("", false, (s, e) => EdPonerRecorte("in", edLinea.Cabezal)), "edEmpieza");
            edTermina = L(Boton("", false, (s, e) => EdPonerRecorte("out", edLinea.Cabezal)), "edTermina");
            grupoRecorte.Controls.Add(edEmpieza); grupoRecorte.Controls.Add(edTermina);
            grupoTramo.Controls.Add(L(new Label { Font = fBold, AutoSize = true, Margin = new Padding(0, 8, 6, 0) }, "edTramoMarcado"));
            edDesde = L(Boton("", false, (s, e) => EdExtremo(true)), "edDesde");
            edHasta = L(Boton("", false, (s, e) => EdExtremo(false)), "edHasta");
            edQuitar = L(Boton("", false, (s, e) => EdQuitarTramo()), "edQuitar");
            edSilenciar = L(Boton("", false, (s, e) => EdSilenciar()), "edSilenciar");
            foreach (var b in new[] { edDesde, edHasta, edQuitar, edSilenciar }) grupoTramo.Controls.Add(b);
            ops.Controls.Add(grupoRecorte); ops.Controls.Add(grupoTramo);
            edSel = new Label { AutoSize = true, Font = fChico, ForeColor = C.Ink2, Margin = new Padding(0, 0, 0, 6) };

            // Volumen y mover el audio
            var audio = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 2) };
            var grupoVol = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0) };
            var grupoMover = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0) };
            grupoVol.Controls.Add(L(new Label { Font = fBold, AutoSize = true, Margin = new Padding(0, 8, 6, 0) }, "edVolumen"));
            edVol = new TrackBar { Minimum = 0, Maximum = 300, TickFrequency = 50, TickStyle = TickStyle.None, Width = 150, Value = 100, BackColor = C.Bg, Margin = new Padding(0, 4, 2, 0) };
            edVol.ValueChanged += (s, e) => { if (edActualizando || edItem == null) return; edItem.Ed.Volumen = edVol.Value / 100.0; EdCambio(); };
            edVolTxt = new Label { AutoSize = true, Font = fBold, Margin = new Padding(0, 8, 14, 0) };
            grupoVol.Controls.Add(edVol); grupoVol.Controls.Add(edVolTxt);
            grupoMover.Controls.Add(L(new Label { Font = fBold, AutoSize = true, Margin = new Padding(0, 8, 6, 0) }, "edMover"));
            edMenos = L(Boton("", false, (s, e) => EdMoverAudio(-0.02)), "edAdelantar");
            edDes = new NumericUpDown { Minimum = -5, Maximum = 5, DecimalPlaces = 2, Increment = 0.02m, Width = 70, TextAlign = HorizontalAlignment.Right, BackColor = C.Bg, ForeColor = C.Ink, Margin = new Padding(0, 4, 4, 0) };
            edDes.ValueChanged += (s, e) => { if (edActualizando || edItem == null) return; edItem.Ed.Desfase = (double)edDes.Value; EdCambio(); };
            edMas = L(Boton("", false, (s, e) => EdMoverAudio(0.02)), "edAtrasar");
            grupoMover.Controls.Add(edMenos); grupoMover.Controls.Add(edDes);
            grupoMover.Controls.Add(new Label { Text = "s", AutoSize = true, Margin = new Padding(0, 8, 6, 0) });
            grupoMover.Controls.Add(edMas);
            audio.Controls.Add(grupoVol); audio.Controls.Add(grupoMover);

            // Cambios, con su "Deshacer"; y la nota de lo que la vista previa no puede mostrar
            var cambios = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            var cab = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            cab.Controls.Add(L(new Label { Font = fBold, AutoSize = true, Margin = new Padding(0, 6, 8, 0) }, "edCambios"));
            edReset = L(Boton("", false, (s, e) => EdDeshacerTodo()), "edDeshacerTodo");
            edReset.Font = fChico;
            cab.Controls.Add(edReset);
            cambios.Controls.Add(cab);
            edLista = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(0, 2, 0, 4) };
            cambios.Controls.Add(edLista);
            edNota = Ayuda("edNotaVista");
            cambios.Controls.Add(edNota);
            // Debajo de la línea de tiempo, tres columnas cuando hay ancho (recortar y tramo · audio · cambios), o una debajo de otra.
            var ayudaMover = Ayuda("edMoverAyuda"); ayudaMover.Margin = new Padding(0, 0, 0, 6);
            var colOps = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 14, 0) };
            var colAudio = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 14, 0) };
            var colCambios = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            colOps.Controls.Add(ops); colOps.Controls.Add(edSel);
            colAudio.Controls.Add(audio); colAudio.Controls.Add(ayudaMover);
            colCambios.Controls.Add(cambios);
            var controles = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = true, Margin = new Padding(0) };
            controles.Controls.Add(colOps); controles.Controls.Add(colAudio); controles.Controls.Add(colCambios);
            edRaiz.Controls.Add(controles, 0, 4);
            // El ancho manda: el visor mide 16:9 (entre 150 y 320 px de alto), las filas de botones se parten en dos
            // y, si todo no cabe a lo alto, el editor se desplaza (nunca a lo ancho).
            EventHandler acomodar = (s, e) =>
            {
                int an = Math.Max(200, edCaja.ClientSize.Width - 4);
                int cols = an >= fBase.Height * 66 ? 3 : 1;          // ~3 columnas de 22 líneas de ancho
                int col = cols == 1 ? an : an / 3 - 14;
                controles.MaximumSize = new Size(an, 0);
                foreach (Control c in new Control[] { colOps, colAudio, colCambios }) { c.MinimumSize = new Size(cols == 1 ? 0 : col, 0); c.MaximumSize = new Size(col, 0); }
                foreach (Control c in new Control[] { ops, audio, cambios, grupoRecorte, grupoTramo, grupoVol, grupoMover }) c.MaximumSize = new Size(col, 0);
                foreach (Control c in new Control[] { edSel, edNota, ayudaMover }) c.MaximumSize = new Size(col, 0);
                edAyuda.MaximumSize = new Size(an, 0);
                edLinea.Width = an;
                EdAjustarVisor();
            };
            edCaja.Resize += acomodar;
            edCaja.ClientSizeChanged += acomodar;   // también cuando aparece o se va la barra de desplazamiento

            edReloj = new System.Windows.Forms.Timer { Interval = 50 };
            edReloj.Tick += (s, e) => EdTick();
            EdPintar();
            return edCaja;
        }

        // El visor mide 16:9 del ancho, pero nunca más de lo que deja libre el resto del editor (así no hay que desplazarse
        // para ver la línea de tiempo cuando hay espacio); como mínimo, 150 px.
        void EdAjustarVisor()
        {
            if (edRaiz == null || edCaja == null) return;
            int an = Math.Max(200, edCaja.ClientSize.Width - 4);
            int actual = (int)edRaiz.RowStyles[0].Height;
            int resto = edRaiz.Height - actual;
            int alto = Math.Max(150, Math.Min(an * 9 / 16, edCaja.ClientSize.Height - resto - 10));   // 10 px de holgura: sin ella, el redondeo saca la barra de desplazamiento
            if (Math.Abs(alto - actual) <= 2) return;
            edRaiz.RowStyles[0].Height = alto;
            // Si el visor se achicó dentro del Resize de la caja, la caja no vuelve a medir y deja la barra de
            // desplazamiento aunque ya todo quepa: que vuelva a medir en cuanto termine este acomodo.
            if (edCaja.IsHandleCreated) edCaja.BeginInvoke((Action)(() => edCaja.PerformLayout()));
        }

        /* ---------- Activar y desactivar ---------- */

        void AlternarEditor()
        {
            bool on = chkEditor.Checked;
            if (on == editorOn) return;
            if (on)
            {
                if (trabajando) { chkEditor.Checked = false; return; }
                if (items.Count > 1)
                {
                    if (MessageBox.Show(this, Tx.T("edSoloUno", Path.GetFileName(items[0].Ruta)), "Video Lite", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    { chkEditor.Checked = false; return; }
                    foreach (var it in items.Skip(1).ToList()) { items.Remove(it); lista.Items.Remove(it.Lv); }
                }
            }
            editorOn = on;
            btnAgregarCarpeta.Visible = !on;
            CambiarClave(lblSecVideos, on ? "edTitulo" : "secVideos");
            lblSecVideos.Visible = !on;                    // como en la web: el editor reemplaza "1 · Tus videos"
            btnAgregarVideos.Parent.Visible = !on;         // cambiar o quitar el video: en la fila de reproducir
            AcomodoAjustes();          // los ajustes pasan debajo del editor (o vuelven a la derecha)
            colVideos.PerformLayout();
            AnimarEditor(on);
            if (on) { var it = items.FirstOrDefault(i => i.Info != null); EdCargar(it); edReloj.Start(); }
            else { if (rep != null) rep.Pausa(); edReloj.Stop(); }
            Cambio();
        }

        // El editor se despliega con una animación corta: la lista se encoge a una fila (la "tarjeta" del video) y el editor crece.
        void AnimarEditor(bool on)
        {
            var filas = colVideos.RowStyles;
            int alto = colVideos.ClientSize.Height - (lblSecVideos.Visible ? lblSecVideos.Height : 0) - (btnAgregarVideos.Parent.Visible ? btnAgregarVideos.Parent.Height : 0) - 16;
            int fila = lista.Font.Height * 2 + 22;    // encabezado y una fila
            int desde = on ? Math.Max(fila, lista.Height) : fila, hasta = on ? fila : Math.Max(fila, alto);
            if (edAnim != null) edAnim.Stop();
            int paso = 0, pasos = 12;
            filas[2] = new RowStyle(SizeType.Absolute, desde);
            filas[3] = new RowStyle(SizeType.Absolute, Math.Max(0, alto - desde));
            edAnim = new System.Windows.Forms.Timer { Interval = 15 };
            edAnim.Tick += (s, e) =>
            {
                paso++;
                double k = 1 - Math.Pow(1 - paso / (double)pasos, 3);   // sale rápido y frena
                int h = (int)Math.Round(desde + (hasta - desde) * k);
                colVideos.SuspendLayout();
                filas[2] = new RowStyle(SizeType.Absolute, h);
                filas[3] = new RowStyle(SizeType.Absolute, Math.Max(0, alto - h));
                colVideos.ResumeLayout(true);
                if (paso >= pasos)
                {
                    edAnim.Stop();
                    filas[2] = on ? new RowStyle(SizeType.Absolute, fila) : new RowStyle(SizeType.Percent, 100);
                    filas[3] = on ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.Absolute, 0);
                    edCaja.Visible = on;
                    colVideos.PerformLayout();
                }
            };
            edCaja.Visible = true;
            edAnim.Start();
        }

        /* ---------- Cargar un video ---------- */

        void EdCargar(Item it)
        {
            edCarga++;
            edItem = it != null && it.Info != null ? it : null;
            edLinea.Desde = edLinea.Hasta = null;
            edLinea.Cuadros = null; edLinea.Picos = null; edLinea.Cabezal = 0;
            edVacio.Visible = edItem == null;
            edSinVista.Visible = false;
            if (edItem == null)
            {
                if (rep != null) rep.Cerrar();
                edLinea.Ed = null;
                EdPintar();
                return;
            }
            if (edItem.Ed == null || Math.Abs(edItem.Ed.Dur - edItem.Info.Dur) > 1e-6) edItem.Ed = Edicion.Vacia(edItem.Info.Dur);
            edLinea.Ed = edItem.Ed;
            EdAsegurarReproductor();
            if (rep != null)
            {
                rep.Abrir(edItem.Ruta);
                rep.Sonido(true, 0);
                rep.Play();          // se pausa en cuanto arranca: así el visor muestra el primer cuadro
                edCargando = true; edCargaT0 = DateTime.Now;
            }
            else { edSinVista.Text = Tx.T("edSinReproductor"); edSinVista.Visible = true; }
            // Miniaturas y forma de onda, en segundo plano (con el ffmpeg de la app)
            int id = edCarga; string ruta = edItem.Ruta; InfoVideo info = edItem.Info;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                List<Bitmap> cuadros = null;
                try { int ancho; byte[] b = Motor.Miniaturas(ruta, info, 16, 54, out ancho); if (b != null) cuadros = ABitmaps(b, ancho, 54); } catch (Exception) { }
                try { BeginInvoke(new Action(() => { if (id == edCarga) { edLinea.Cuadros = cuadros; edLinea.Invalidate(); } })); } catch (Exception) { }
                float[] picos = null;
                try { if (info.HasAudio) picos = Motor.Onda(ruta, info.Dur, 1200); } catch (Exception) { }
                try { BeginInvoke(new Action(() => { if (id == edCarga) { edLinea.Picos = picos; edLinea.Invalidate(); } })); } catch (Exception) { }
            });
            EdPintar();
        }

        void EdAsegurarReproductor()
        {
            if (rep != null || edWmp != null) return;
            try
            {
                edWmp = new Wmp { Dock = DockStyle.Fill };
                edVisor.Controls.Add(edWmp);
                edWmp.SendToBack();
                edWmp.CreateControl();
                object ocx = edWmp.Ocx;
                if (ocx == null) throw new InvalidOperationException();
                rep = new Reproductor(ocx);
            }
            catch (Exception) { rep = null; }
        }

        static List<Bitmap> ABitmaps(byte[] b, int w, int h)
        {
            var l = new List<Bitmap>();
            int tam = w * h * 3;
            for (int off = 0; off + tam <= b.Length; off += tam)
            {
                var bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
                BitmapData d = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
                for (int y = 0; y < h; y++) Marshal.Copy(b, off + y * w * 3, IntPtr.Add(d.Scan0, y * d.Stride), w * 3);
                bmp.UnlockBits(d);
                l.Add(bmp);
            }
            return l;
        }

        /* ---------- Vista previa ---------- */

        // Cada 50 ms: el cabezal sigue al reproductor, se saltan los tramos que no se conservan y se callan los silenciados.
        // El volumen arriba de 100 % y el desfase no se pueden oír en vivo (la nota lo dice); el resultado sí los aplica.
        void EdTick()
        {
            if (rep == null || edItem == null) return;
            if (edCargando)
            {
                if (rep.Tocando) { rep.Pausa(); rep.Pos = edLinea.Cabezal; edCargando = false; }
                else if ((DateTime.Now - edCargaT0).TotalSeconds > 4) { edCargando = false; edSinVista.Text = Tx.T("edSinVista"); edSinVista.Visible = true; }
                return;
            }
            bool tocando = rep.Tocando;
            edPlay.Text = tocando ? "❚❚" : "▶";
            edPlay.AccessibleName = Tx.T(tocando ? "edPausar" : "edReproducir");
            if (!tocando) return;
            var e = edItem.Ed;
            double pos = rep.Pos;
            var tr = e.Tramos();
            if (!tr.Any(x => pos >= x[0] - 0.05 && pos < x[1]))
            {
                var sig = tr.FirstOrDefault(x => x[0] > pos);
                if (sig == null) { rep.Pausa(); pos = tr.Count > 0 ? tr[tr.Count - 1][1] : e.Salida; rep.Pos = pos; }
                else { pos = sig[0]; rep.Pos = pos; }
            }
            bool silencio = e.Volumen < 0.005 || Edicion.Unir(e.Silencios).Any(s => pos >= s[0] && pos < s[1]);
            rep.Sonido(silencio, (int)Math.Round(Math.Min(1, e.Volumen) * 100));
            edLinea.Cabezal = pos;
            edLinea.Invalidate();
            edTiempo.Text = Tx.T("edEn", FmtT(pos), FmtT(e.Dur));
        }

        void EdPlayPausa()
        {
            if (rep == null || edItem == null) return;
            if (rep.Tocando) { rep.Pausa(); return; }
            var tr = edItem.Ed.Tramos();
            double pos = edLinea.Cabezal;
            if (tr.Count > 0 && (pos >= tr[tr.Count - 1][1] - 0.05 || pos < tr[0][0])) { pos = tr[0][0]; rep.Pos = pos; edLinea.Cabezal = pos; }
            rep.Play();
        }

        void EdIr(double t)
        {
            if (edItem == null) return;
            edLinea.Cabezal = Math.Max(0, Math.Min(edItem.Ed.Dur, t));
            if (rep != null) rep.Pos = edLinea.Cabezal;
            EdPintar();
        }

        void EdTecla(Keys k, bool mayus)
        {
            if (edItem == null) return;
            double paso = mayus ? 1 : 0.1;
            if (k == Keys.Left) EdIr(edLinea.Cabezal - paso);
            else if (k == Keys.Right) EdIr(edLinea.Cabezal + paso);
            else if (k == Keys.Home) EdIr(0);
            else if (k == Keys.End) EdIr(edItem.Ed.Dur);
            else if (k == Keys.Space) EdPlayPausa();
            else if (k == Keys.Escape) { edLinea.Desde = edLinea.Hasta = null; EdPintar(); }
        }

        /* ---------- Operaciones ---------- */

        // "Desde aquí" / "Hasta aquí": cada extremo por separado (un clic en la línea solo mueve el cabezal).
        void EdExtremo(bool desde)
        {
            if (desde) edLinea.Desde = edLinea.Cabezal; else edLinea.Hasta = edLinea.Cabezal;
            EdPintar();
        }

        double[] EdRango()
        {
            if (!edLinea.Desde.HasValue || !edLinea.Hasta.HasValue) return null;
            return new[] { Math.Min(edLinea.Desde.Value, edLinea.Hasta.Value), Math.Max(edLinea.Desde.Value, edLinea.Hasta.Value) };
        }

        void EdPonerRecorte(string cual, double t)
        {
            if (edItem == null) return;
            var e = edItem.Ed;
            if (cual == "in") e.Entrada = Math.Max(0, Math.Min(t, e.Salida - Edicion.MinTramo));
            else e.Salida = Math.Min(e.Dur, Math.Max(t, e.Entrada + Edicion.MinTramo));
            EdCambio();
        }

        void EdQuitarTramo()
        {
            var r = EdRango();
            if (edItem == null || r == null || r[1] - r[0] < Edicion.MinTramo) return;
            var prueba = edItem.Ed.Copia();
            prueba.Cortes.Add(r);
            if (prueba.Tramos().Count == 0) { edSel.Text = Tx.T("edTodo"); edSel.ForeColor = C.Danger; return; }
            edItem.Ed.Cortes.Add(r);
            edLinea.Desde = edLinea.Hasta = null;
            EdCambio();
            edLinea.Focus();   // el botón queda desactivado: sin esto, el foco salta a los ajustes y los desplaza
        }

        void EdSilenciar()
        {
            var r = EdRango();
            if (edItem == null || r == null || r[1] - r[0] < Edicion.MinTramo) return;
            edItem.Ed.Silencios.Add(r);
            edLinea.Desde = edLinea.Hasta = null;
            EdCambio();
            edLinea.Focus();
        }

        void EdMoverAudio(double d)
        {
            if (edItem == null) return;
            edItem.Ed.Desfase = Math.Max(-5, Math.Min(5, Math.Round((edItem.Ed.Desfase + d) * 1000) / 1000));
            EdCambio();
        }

        void EdDeshacerTodo()
        {
            if (edItem == null || !edItem.Ed.HayCambios()) return;
            if (MessageBox.Show(this, Tx.T("edResetConfirm"), "Video Lite", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            edItem.Ed = Edicion.Vacia(edItem.Info.Dur);
            edLinea.Ed = edItem.Ed;
            edLinea.Desde = edLinea.Hasta = null;
            EdCambio();
        }

        // Cada cambio deja el video listo para procesarse otra vez (como en la web); el resultado anterior sigue en su carpeta.
        void EdCambio()
        {
            if (edItem != null && edItem.Estado == "hecho" && !trabajando) edItem.Estado = "listo";
            EdPintar();
            Cambio();
        }

        /* ---------- Pintado ---------- */

        void EdPintar()
        {
            if (edRaiz == null) return;
            bool hay = edItem != null && !trabajando;
            foreach (Control c in new Control[] { edEmpieza, edTermina, edDesde, edHasta, edVol, edDes, edMenos, edMas, edLinea }) c.Enabled = hay;
            edPlay.Enabled = hay && rep != null;
            edCambiarVideo.Enabled = !trabajando;
            edQuitarVideo.Enabled = hay;
            if (edItem == null)
            {
                edTiempo.Text = ""; edQueda.Text = ""; edSel.Text = ""; edVolTxt.Text = "100 %";
                edQuitar.Enabled = edSilenciar.Enabled = edReset.Enabled = false;
                edLista.Controls.Clear();
                edLinea.Invalidate();
                return;
            }
            var e = edItem.Ed;
            edTiempo.Text = Tx.T("edEn", FmtT(edLinea.Cabezal), FmtT(e.Dur));
            edQueda.Text = Tx.T("edQueda", FmtT(e.Duracion()));
            edActualizando = true;
            edVol.Value = Math.Max(0, Math.Min(300, (int)Math.Round(e.Volumen * 100)));
            edDes.Value = (decimal)Math.Max(-5, Math.Min(5, e.Desfase));
            edActualizando = false;
            edVolTxt.Text = Math.Round(e.Volumen * 100).ToString(CultureInfo.InvariantCulture) + " %";

            // El tramo marcado: qué falta para poder usarlo
            var r = EdRango();
            bool valido = r != null && r[1] - r[0] >= Edicion.MinTramo;
            edSel.ForeColor = C.Ink2;
            if (r != null) edSel.Text = valido ? Tx.T("edSel", FmtT(r[0]), FmtT(r[1]), FmtT(r[1] - r[0])) : Tx.T("edSelCorto");
            else if (edLinea.Desde.HasValue) edSel.Text = Tx.T("edSelInicio", FmtT(edLinea.Desde.Value));
            else if (edLinea.Hasta.HasValue) edSel.Text = Tx.T("edSelFin", FmtT(edLinea.Hasta.Value));
            else edSel.Text = Tx.T("edSinSel");
            edQuitar.Enabled = edSilenciar.Enabled = hay && valido;
            edReset.Enabled = hay && e.HayCambios();

            // La lista de cambios, cada uno con su "Deshacer"
            edLista.SuspendLayout();
            edLista.Controls.Clear();
            var filas = new List<Tuple<string, Action>>();
            if (e.Entrada > 0.01 || e.Salida < e.Dur - 0.01) filas.Add(Tuple.Create(Tx.T("edItemRecorte", FmtT(e.Entrada), FmtT(e.Salida)), (Action)(() => { e.Entrada = 0; e.Salida = e.Dur; })));
            foreach (var c in Edicion.Unir(e.Cortes)) { var cc = c; filas.Add(Tuple.Create(Tx.T("edItemQuitado", FmtT(c[0]), FmtT(c[1])), (Action)(() => { e.Cortes = Edicion.Unir(e.Cortes).Where(x => !(x[0] == cc[0] && x[1] == cc[1])).ToList(); }))); }
            foreach (var s in Edicion.Unir(e.Silencios)) { var ss = s; filas.Add(Tuple.Create(Tx.T("edItemSilencio", FmtT(s[0]), FmtT(s[1])), (Action)(() => { e.Silencios = Edicion.Unir(e.Silencios).Where(x => !(x[0] == ss[0] && x[1] == ss[1])).ToList(); }))); }
            if (Math.Abs(e.Volumen - 1) > 0.001) filas.Add(Tuple.Create(Tx.T("edItemVolumen", Math.Round(e.Volumen * 100).ToString(CultureInfo.InvariantCulture)), (Action)(() => { e.Volumen = 1; })));
            if (Math.Abs(e.Desfase) > 0.0005) filas.Add(Tuple.Create(Tx.T("edItemDesfase", Math.Round(e.Desfase * 1000).ToString(CultureInfo.InvariantCulture)), (Action)(() => { e.Desfase = 0; })));
            if (filas.Count == 0) edLista.Controls.Add(new Label { Text = Tx.T("edNoCambios"), AutoSize = true, Font = fChico, ForeColor = C.Ink3, Margin = new Padding(0, 0, 0, 2) });
            foreach (var f in filas)
            {
                var fila = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
                fila.Controls.Add(new Label { Text = f.Item1, AutoSize = true, Font = fChico, Margin = new Padding(0, 3, 6, 0) });
                var deshacer = Enlace(null, null, Tx.T("edDeshacer"));
                var accion = f.Item2;
                deshacer.Enabled = hay;
                deshacer.LinkClicked += (s, ev) => { accion(); EdCambio(); };
                fila.Controls.Add(deshacer);
                edLista.Controls.Add(fila);
            }
            edLista.ResumeLayout(true);
            edLinea.Invalidate();
            EdAjustarVisor();
        }
    }
}
