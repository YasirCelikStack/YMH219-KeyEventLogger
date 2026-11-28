using System.Drawing.Drawing2D;

namespace KeyEventLogger
{
    /// <summary>Uygulamanın renkleri (tek yerde toplanır).</summary>
    public static class Palette
    {
        public static readonly Color Background = Color.FromArgb(243, 244, 248);
        public static readonly Color Card = Color.White;
        public static readonly Color Text = Color.FromArgb(30, 33, 40);
        public static readonly Color Muted = Color.FromArgb(108, 114, 128);
        public static readonly Color Border = Color.FromArgb(218, 221, 230);
        public static readonly Color Accent = Color.FromArgb(79, 110, 247);
        public static readonly Color Recording = Color.FromArgb(229, 57, 53);
        public static readonly Color KeyIdle = Color.FromArgb(236, 238, 243);
    }

    /// <summary>Klavyedeki tek bir tuşun yeri: hangi tuş kodu, varsayılan etiket ve genişlik (tuş birimi).</summary>
    public readonly record struct KeyCap(Keys Key, string Label, float Width = 1f);

    /// <summary>
    /// Türkçe Q klavye düzeni üzerinde ısı haritası çizer: çok basılan tuş sıcak (kırmızı), az basılan soğuk (mavi) renkte.
    /// Çizim kontrolden ayrıdır; böylece aynı sınıf hem formda hem testte kullanılabilir.
    /// </summary>
    public sealed class HeatmapRenderer : IDisposable
    {
        // Satır satır klavye. Etiketler, tuşun ürettiği karakter henüz öğrenilmediyse gösterilir.
        public static readonly KeyCap[][] Layout =
        {
            new KeyCap[]
            {
                new(Keys.Oem3, "\""), new(Keys.D1, "1"), new(Keys.D2, "2"), new(Keys.D3, "3"), new(Keys.D4, "4"),
                new(Keys.D5, "5"), new(Keys.D6, "6"), new(Keys.D7, "7"), new(Keys.D8, "8"), new(Keys.D9, "9"),
                new(Keys.D0, "0"), new(Keys.Oem8, "*"), new(Keys.OemMinus, "-"), new(Keys.Back, "⌫", 2f)
            },
            new KeyCap[]
            {
                new(Keys.Tab, "Tab", 1.5f), new(Keys.Q, "Q"), new(Keys.W, "W"), new(Keys.E, "E"), new(Keys.R, "R"),
                new(Keys.T, "T"), new(Keys.Y, "Y"), new(Keys.U, "U"), new(Keys.I, "I"), new(Keys.O, "O"), new(Keys.P, "P"),
                new(Keys.Oem4, "Ğ"), new(Keys.Oem6, "Ü"), new(Keys.Return, "Enter", 1.5f)
            },
            new KeyCap[]
            {
                new(Keys.Capital, "Caps", 1.75f), new(Keys.A, "A"), new(Keys.S, "S"), new(Keys.D, "D"), new(Keys.F, "F"),
                new(Keys.G, "G"), new(Keys.H, "H"), new(Keys.J, "J"), new(Keys.K, "K"), new(Keys.L, "L"),
                new(Keys.Oem1, "Ş"), new(Keys.Oem7, "İ"), new(Keys.Oemcomma, ",", 2.25f)
            },
            new KeyCap[]
            {
                new(Keys.ShiftKey, "Shift", 1.25f), new(Keys.OemBackslash, "<"), new(Keys.Z, "Z"), new(Keys.X, "X"),
                new(Keys.C, "C"), new(Keys.V, "V"), new(Keys.B, "B"), new(Keys.N, "N"), new(Keys.M, "M"),
                new(Keys.Oem2, "Ö"), new(Keys.Oem5, "Ç"), new(Keys.OemPeriod, "."), new(Keys.ShiftKey, "Shift", 2.75f)
            },
            new KeyCap[]
            {
                new(Keys.ControlKey, "Ctrl", 1.5f), new(Keys.LWin, "Win", 1.25f), new(Keys.Menu, "Alt", 1.25f),
                new(Keys.Space, "Space", 6.5f), new(Keys.Menu, "Alt Gr", 1.25f), new(Keys.Apps, "Menü", 1.25f),
                new(Keys.ControlKey, "Ctrl", 2f)
            }
        };

        private const float RowUnits = 15f; // her satırın toplam genişliği (tuş birimi)

        // Soğuktan sıcağa renk geçişi
        private static readonly Color[] HeatStops =
        {
            Color.FromArgb(144, 202, 249), Color.FromArgb(77, 182, 172), Color.FromArgb(156, 204, 101),
            Color.FromArgb(255, 213, 79), Color.FromArgb(255, 152, 0), Color.FromArgb(239, 83, 80)
        };

        private readonly Font _labelFont;
        private readonly Font _countFont;
        private readonly Font _legendFont;

        public HeatmapRenderer(FontFamily family)
        {
            _labelFont = new Font(family, 9.5f, FontStyle.Bold);
            _countFont = new Font(family, 7f);
            _legendFont = new Font(family, 8f);
        }

        /// <param name="lastKey">Son basılan tuş: çevresine vurgu halkası çizilir (canlı geri bildirim).</param>
        /// <param name="lastKeyGlow">Vurgunun gücü (1 → yeni basıldı, 0 → söndü).</param>
        public void Draw(Graphics g, Rectangle bounds, IReadOnlyDictionary<Keys, int> counts,
                         IReadOnlyDictionary<Keys, char> learned, Keys? lastKey, float lastKeyGlow)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            const int legendHeight = 22;
            float gap = Math.Max(3f, bounds.Width / 260f);
            float unit = Math.Min((bounds.Width - gap) / RowUnits, (bounds.Height - legendHeight - gap * 6) / 5f);
            float keyboardWidth = unit * RowUnits;
            float left = bounds.X + (bounds.Width - keyboardWidth) / 2;
            float top = bounds.Y + 2;

            int max = counts.Count == 0 ? 0 : counts.Values.Max();

            using var labelFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using var countFormat = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Far };

            for (int row = 0; row < Layout.Length; row++)
            {
                float x = left;
                float y = top + row * (unit + gap);

                foreach (KeyCap cap in Layout[row])
                {
                    var rect = new RectangleF(x + gap / 2, y, cap.Width * unit - gap, unit);
                    counts.TryGetValue(cap.Key, out int count);

                    Color fill = count == 0 || max == 0 ? Palette.KeyIdle : Heat(MathF.Sqrt(count / (float)max));
                    using (GraphicsPath path = Rounded(rect, unit * 0.16f))
                    {
                        using (var brush = new SolidBrush(fill)) g.FillPath(brush, path);
                        using (var pen = new Pen(Color.FromArgb(40, 0, 0, 0))) g.DrawPath(pen, path);

                        if (lastKey == cap.Key && lastKeyGlow > 0)
                        {
                            using var glow = new Pen(Color.FromArgb((int)(255 * lastKeyGlow), Palette.Accent), 3f);
                            g.DrawPath(glow, path);
                        }
                    }

                    string label = learned.TryGetValue(cap.Key, out char c) && cap.Width <= 1f ? c.ToString() : cap.Label;
                    Color textColor = count > 0 && max > 0 && count / (float)max > 0.55f ? Color.White : Palette.Text;
                    using (var textBrush = new SolidBrush(textColor))
                    {
                        g.DrawString(label, _labelFont, textBrush, rect, labelFormat);
                        if (count > 0)
                        {
                            var countRect = RectangleF.Inflate(rect, -3, -2);
                            g.DrawString(count.ToString(), _countFont, textBrush, countRect, countFormat);
                        }
                    }

                    x += cap.Width * unit;
                }
            }

            // Gösterge: az ← renk şeridi → çok
            float legendY = top + 5 * (unit + gap) + 2;
            float barWidth = Math.Min(220, keyboardWidth * 0.4f);
            var bar = new RectangleF(left + (keyboardWidth - barWidth) / 2, legendY + 5, barWidth, 10);
            using (var gradient = new LinearGradientBrush(bar, HeatStops[0], HeatStops[^1], 0f))
            {
                var blend = new ColorBlend
                {
                    Colors = HeatStops,
                    Positions = Enumerable.Range(0, HeatStops.Length).Select(i => i / (float)(HeatStops.Length - 1)).ToArray()
                };
                gradient.InterpolationColors = blend;
                using GraphicsPath barPath = Rounded(bar, 5);
                g.FillPath(gradient, barPath);
            }

            using var muted = new SolidBrush(Palette.Muted);
            g.DrawString("az", _legendFont, muted, bar.X - 22, bar.Y - 3);
            g.DrawString(max > 0 ? $"çok ({max})" : "çok", _legendFont, muted, bar.Right + 6, bar.Y - 3);
        }

        /// <summary>0..1 arası değeri soğuk-sıcak renk geçişine çevirir.</summary>
        private static Color Heat(float t)
        {
            t = Math.Clamp(t, 0f, 1f) * (HeatStops.Length - 1);
            int i = Math.Min((int)t, HeatStops.Length - 2);
            float f = t - i;
            Color a = HeatStops[i], b = HeatStops[i + 1];
            return Color.FromArgb((int)(a.R + (b.R - a.R) * f), (int)(a.G + (b.G - a.G) * f), (int)(a.B + (b.B - a.B) * f));
        }

        internal static GraphicsPath Rounded(RectangleF r, float radius)
        {
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            var path = new GraphicsPath();
            if (d <= 0)
            {
                path.AddRectangle(r);
                return path;
            }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public void Dispose()
        {
            _labelFont.Dispose();
            _countFont.Dispose();
            _legendFont.Dispose();
        }
    }

    /// <summary>Son 2 dakikadaki tuş etkinliğini sütun grafiği olarak çizer.</summary>
    public sealed class ActivityChartRenderer : IDisposable
    {
        private readonly Font _font;

        public ActivityChartRenderer(FontFamily family)
        {
            _font = new Font(family, 8f);
        }

        /// <param name="buckets">Her aralıktaki tuş sayısı (son eleman en yeni).</param>
        /// <param name="bucketSeconds">Bir sütunun kaç saniyeyi temsil ettiği.</param>
        public void Draw(Graphics g, Rectangle bounds, int[] buckets, int bucketSeconds)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            const int axisWidth = 28;
            const int bottomLabels = 18;
            var plot = new RectangleF(bounds.X + axisWidth, bounds.Y + 6, bounds.Width - axisWidth - 6, bounds.Height - bottomLabels - 8);
            if (plot.Width <= 10 || plot.Height <= 10 || buckets.Length == 0) return;

            int max = Math.Max(5, buckets.Max());
            int niceMax = (int)(Math.Ceiling(max / 10.0) * 10); // ekseni 10'un katına yuvarla (yarısı da tam sayı olsun)

            using var gridPen = new Pen(Color.FromArgb(232, 234, 240));
            using var muted = new SolidBrush(Palette.Muted);
            using var right = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
            using var center = new StringFormat { Alignment = StringAlignment.Center };

            // Yatay kılavuz çizgileri: 0, yarı, tam
            for (int i = 0; i <= 2; i++)
            {
                float value = niceMax * i / 2f;
                float y = plot.Bottom - plot.Height * i / 2f;
                g.DrawLine(gridPen, plot.Left, y, plot.Right, y);
                g.DrawString(((int)value).ToString(), _font, muted, new RectangleF(bounds.X, y - 8, axisWidth - 4, 16), right);
            }

            float slot = plot.Width / buckets.Length;
            float barWidth = Math.Max(2, slot * 0.68f);

            for (int i = 0; i < buckets.Length; i++)
            {
                if (buckets[i] == 0) continue;

                float h = plot.Height * buckets[i] / niceMax;
                var bar = new RectangleF(plot.Left + i * slot + (slot - barWidth) / 2, plot.Bottom - h, barWidth, h);

                // En yeni sütun vurgu renginde, eskiler biraz soluk
                int alpha = i == buckets.Length - 1 ? 255 : 150 + 100 * i / buckets.Length;
                using var brush = new SolidBrush(Color.FromArgb(alpha, Palette.Accent));
                using GraphicsPath path = HeatmapRenderer.Rounded(bar, Math.Min(3, barWidth / 2));
                g.FillPath(brush, path);
            }

            // Alt etiketler: en eski ve "şimdi"
            int totalSeconds = buckets.Length * bucketSeconds;
            string oldest = totalSeconds >= 60 ? $"{totalSeconds / 60} dk önce" : $"{totalSeconds} sn önce";
            g.DrawString(oldest, _font, muted, plot.Left, plot.Bottom + 3);
            SizeF now = g.MeasureString("şimdi", _font);
            g.DrawString("şimdi", _font, muted, plot.Right - now.Width, plot.Bottom + 3);
        }

        public void Dispose() => _font.Dispose();
    }

    /// <summary>Isı haritasını gösteren kontrol (çizimi HeatmapRenderer yapar).</summary>
    public class HeatmapView : Control
    {
        private readonly HeatmapRenderer _renderer = new(new FontFamily("Segoe UI"));
        private KeyEventRecorder? _recorder;
        private Keys? _lastKey;
        private DateTime _lastKeyTime;

        public HeatmapView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Palette.Card;
        }

        public void Bind(KeyEventRecorder recorder) => _recorder = recorder;

        /// <summary>Bir tuşa basıldı: o tuş kısa bir süre parlasın.</summary>
        public void Flash(Keys key)
        {
            _lastKey = key;
            _lastKeyTime = DateTime.Now;
            Invalidate();
        }

        /// <summary>Parlama sönerken yeniden çiz (zamanlayıcıdan çağrılır).</summary>
        public void Tick()
        {
            if (_lastKey != null && (DateTime.Now - _lastKeyTime).TotalMilliseconds < 700) Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            if (_recorder == null) return;

            float glow = 1f - (float)Math.Min(1, (DateTime.Now - _lastKeyTime).TotalMilliseconds / 600);
            _renderer.Draw(e.Graphics, ClientRectangle, _recorder.KeyCounts, _recorder.LearnedCharacters, _lastKey, glow);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _renderer.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>Etkinlik grafiğini gösteren kontrol.</summary>
    public class ActivityChartView : Control
    {
        public const int BucketSeconds = 5;
        public const int BucketCount = 24; // 24 × 5 sn = son 2 dakika

        private readonly ActivityChartRenderer _renderer = new(new FontFamily("Segoe UI"));
        private KeyEventRecorder? _recorder;

        public ActivityChartView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Palette.Card;
        }

        public void Bind(KeyEventRecorder recorder) => _recorder = recorder;

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            if (_recorder == null) return;
            _renderer.Draw(e.Graphics, ClientRectangle, _recorder.GetActivity(DateTime.Now, BucketSeconds, BucketCount), BucketSeconds);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _renderer.Dispose();
            base.Dispose(disposing);
        }
    }
}
