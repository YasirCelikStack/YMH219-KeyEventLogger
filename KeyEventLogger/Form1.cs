namespace KeyEventLogger
{
    /// <summary>
    /// Ana pencere: üstte kontroller ve filtre, ortada istatistik kartları,
    /// solda tuş kayıt listesi, sağda klavye ısı haritası ve etkinlik grafiği.
    /// </summary>
    public partial class Form1 : Form
    {
        private readonly KeyEventRecorder _recorder = new();

        // Saat aralığı filtresi: null ise tüm kayıtlar gösterilir
        private TimeRange? _filter;

        // Isı haritası parlaması için hızlı, istatistik ve grafik için yavaş güncelleme
        private readonly System.Windows.Forms.Timer _uiTimer = new() { Interval = 100 };
        private int _tickCount;

        // ---------- Üst çubuk ----------
        private readonly Button _btnStart = new() { Text = "●  Kaydı Başlat", Width = 140, Height = 34, Tag = "danger" };
        private readonly Button _btnStop = new() { Text = "■  Durdur", Width = 100, Height = 34, Enabled = false };
        private readonly Button _btnClear = new() { Text = "Temizle", Width = 90, Height = 34 };
        private readonly Button _btnSave = new() { Text = "💾  Kaydet (.txt / .csv)", Width = 180, Height = 34, Tag = "accent" };
        private readonly Label _lblStatus = new() { AutoSize = true, Margin = new Padding(14, 9, 0, 0), Font = new Font("Segoe UI", 10, FontStyle.Bold) };

        // ---------- Filtre ----------
        private readonly DateTimePicker _dtpFrom = CreateDateTimePicker();
        private readonly DateTimePicker _dtpTo = CreateDateTimePicker();
        private readonly Button _btnApplyFilter = new() { Text = "Filtrele", Width = 80, Height = 28 };
        private readonly Button _btnClearFilter = new() { Text = "Filtreyi Kaldır", Width = 110, Height = 28, Enabled = false };

        // ---------- İstatistik kartları ----------
        private readonly Label _statTotal = StatValue();
        private readonly Label _statDistinct = StatValue();
        private readonly Label _statSpeed = StatValue();
        private readonly Label _statDuration = StatValue();
        private readonly Label _lblTopKeys = new() { Dock = DockStyle.Fill, ForeColor = Palette.Muted, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(4, 0, 0, 0) };

        // ---------- İçerik ----------
        private readonly ListBox _lstLog = new() { Dock = DockStyle.Fill, Font = new Font("Consolas", 10), BorderStyle = BorderStyle.None, IntegralHeight = false };
        private readonly Label _lblListInfo = new() { Dock = DockStyle.Bottom, Height = 24, ForeColor = Palette.Muted, TextAlign = ContentAlignment.MiddleLeft };
        private readonly HeatmapView _heatmap = new() { Dock = DockStyle.Fill };
        private readonly ActivityChartView _chart = new() { Dock = DockStyle.Fill };

        public Form1()
        {
            InitializeComponent();
            BuildUi();

            // KeyPreview = true: tuş olayları önce forma gelir, hangi kontrol odakta olursa olsun.
            // Bu sadece BU pencere aktifken çalışır (föydeki güvenlik sınırına uygun).
            KeyPreview = true;
            KeyDown += Form1_KeyDown;
            KeyPress += Form1_KeyPress;
            KeyUp += Form1_KeyUp;

            _btnStart.Click += BtnStart_Click;
            _btnStop.Click += BtnStop_Click;
            _btnClear.Click += BtnClear_Click;
            _btnSave.Click += BtnSave_Click;
            _btnApplyFilter.Click += BtnApplyFilter_Click;
            _btnClearFilter.Click += BtnClearFilter_Click;

            // Tab ve ok tuşları normalde "kontroller arası gezinme" için kullanılır ve KeyDown'a gelmez.
            // IsInputKey = true diyerek bunları da yakalayabiliyoruz.
            _lstLog.PreviewKeyDown += (s, e) => { if (_recorder.IsRecording) e.IsInputKey = true; };

            _heatmap.Bind(_recorder);
            _chart.Bind(_recorder);
            _uiTimer.Tick += UiTimer_Tick;
            _uiTimer.Start();
            FormClosed += (s, e) => _uiTimer.Stop();

            // Varsayılan filtre aralığı: bugünün tamamı
            _dtpFrom.Value = DateTime.Today;
            _dtpTo.Value = DateTime.Today.AddDays(1).AddSeconds(-1);

            UpdateStatus();
            UpdateStats();
        }

        private static DateTimePicker CreateDateTimePicker() => new()
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd.MM.yyyy HH:mm:ss",
            ShowUpDown = true, // takvim yerine yukarı/aşağı okları: saniyeyi ayarlamak kolay olsun
            Width = 165
        };

        // =====================================================================
        // Arayüz kurulumu
        // =====================================================================

        private void BuildUi()
        {
            Text = "Key Event Logger - YMH219";
            Font = new Font("Segoe UI", 9.75f);
            BackColor = Palette.Background;
            ForeColor = Palette.Text;
            Size = new Size(1120, 720);
            MinimumSize = new Size(940, 600);
            StartPosition = FormStartPosition.CenterScreen;

            // Üst çubuk: butonlar + durum
            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 54, Padding = new Padding(12, 10, 12, 0), BackColor = Palette.Card };
            toolbar.Controls.AddRange(new Control[] { _btnStart, _btnStop, _btnClear, _btnSave, _lblStatus });
            foreach (Button button in new[] { _btnStart, _btnStop, _btnClear, _btnSave }) button.Margin = new Padding(0, 0, 8, 0);

            // Filtre satırı
            var filterRow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(12, 6, 12, 0), BackColor = Palette.Card };
            var lblRange = new Label { Text = "Saat aralığı filtresi:", AutoSize = true, Margin = new Padding(0, 6, 6, 0), ForeColor = Palette.Muted };
            var lblDash = new Label { Text = "–", AutoSize = true, Margin = new Padding(2, 6, 2, 0) };
            _dtpFrom.Margin = new Padding(0, 2, 0, 0);
            _dtpTo.Margin = new Padding(0, 2, 8, 0);
            _btnClearFilter.Margin = new Padding(6, 0, 0, 0);
            filterRow.Controls.AddRange(new Control[] { lblRange, _dtpFrom, lblDash, _dtpTo, _btnApplyFilter, _btnClearFilter });
            filterRow.Paint += (s, e) => BottomLine(e.Graphics, filterRow);

            // İstatistik kartları
            var stats = new TableLayoutPanel { Dock = DockStyle.Top, Height = 118, ColumnCount = 4, RowCount = 2, Padding = new Padding(8, 10, 8, 0) };
            for (int i = 0; i < 4; i++) stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            stats.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
            stats.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            stats.Controls.Add(StatCard("Toplam tuş", _statTotal), 0, 0);
            stats.Controls.Add(StatCard("Farklı tuş", _statDistinct), 1, 0);
            stats.Controls.Add(StatCard("Hız (tuş / dk)", _statSpeed), 2, 0);
            stats.Controls.Add(StatCard("Kayıt süresi", _statDuration), 3, 0);
            stats.Controls.Add(_lblTopKeys, 0, 1);
            stats.SetColumnSpan(_lblTopKeys, 4);

            // Sol: kayıt listesi
            var logCard = Card("Tuş kayıtları", _lstLog);
            ((Panel)logCard.Controls[0]).Controls.Add(_lblListInfo);

            // Sağ: ısı haritası (üst) + etkinlik grafiği (alt)
            var heatCard = Card("Klavye ısı haritası", _heatmap);
            heatCard.Dock = DockStyle.Top;
            heatCard.Height = 310;
            var chartCard = Card("Etkinlik (son 2 dakika, 5 sn'lik dilimler)", _chart);

            var right = new Panel { Dock = DockStyle.Fill };
            right.Controls.Add(chartCard);
            right.Controls.Add(heatCard);

            var split = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1, SplitterWidth = 4, BackColor = Palette.Background };
            split.Panel1.Controls.Add(logCard);
            split.Panel2.Controls.Add(right);
            Shown += (s, e) => split.SplitterDistance = (int)(380 * DeviceDpiScale());

            // Dock sırası: Fill olan kontrol önce eklenir, Top/Bottom olanlar sonra.
            // Top olanlardan EN SON eklenen en üstte durur.
            Controls.Add(split);
            Controls.Add(stats);
            Controls.Add(filterRow);
            Controls.Add(toolbar);

            foreach (Button button in new[] { _btnStart, _btnStop, _btnClear, _btnSave, _btnApplyFilter, _btnClearFilter })
            {
                StyleButton(button);
            }
        }

        private static Label StatValue() => new()
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 20, FontStyle.Bold),
            ForeColor = Palette.Text,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "0"
        };

        private static Control StatCard(string title, Label value)
        {
            var card = new Panel { Dock = DockStyle.Fill, Margin = new Padding(4), Padding = new Padding(14, 8, 8, 4), BackColor = Palette.Card };
            card.Paint += (s, e) => Border(e.Graphics, card);
            card.Controls.Add(value);
            card.Controls.Add(new Label { Text = title, Dock = DockStyle.Top, Height = 20, ForeColor = Palette.Muted });
            return card;
        }

        /// <summary>Başlıklı beyaz kart: içerik kontrolü kartı doldurur.</summary>
        private static Panel Card(string title, Control content)
        {
            var outer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 12) };
            var card = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 6, 12, 10), BackColor = Palette.Card };
            card.Paint += (s, e) => Border(e.Graphics, card);
            content.BackColor = Palette.Card;
            card.Controls.Add(content);
            card.Controls.Add(new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Height = 28,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            });
            outer.Controls.Add(card);
            return outer;
        }

        private static void Border(Graphics g, Control control)
        {
            using var pen = new Pen(Palette.Border);
            g.DrawRectangle(pen, 0, 0, control.Width - 1, control.Height - 1);
        }

        private static void BottomLine(Graphics g, Control control)
        {
            using var pen = new Pen(Palette.Border);
            g.DrawLine(pen, 0, control.Height - 1, control.Width, control.Height - 1);
        }

        private static void StyleButton(Button button)
        {
            string? kind = button.Tag as string;
            Color back = kind switch
            {
                "danger" => Palette.Recording,
                "accent" => Palette.Accent,
                _ => Palette.Card
            };

            button.FlatStyle = FlatStyle.Flat;
            button.Cursor = Cursors.Hand;
            button.BackColor = back;
            button.ForeColor = kind == null ? Palette.Text : Color.White;
            button.FlatAppearance.BorderColor = kind == null ? Palette.Border : back;
            button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(back, kind == null ? 0.05f : 0.15f);
            button.Font = new Font("Segoe UI", 9.75f, kind == null ? FontStyle.Regular : FontStyle.Bold);
        }

        private float DeviceDpiScale()
        {
            using Graphics g = CreateGraphics();
            return g.DpiX / 96f;
        }

        // =====================================================================
        // Klavye olayları
        // Sıra: KeyDown (fiziksel tuş) → KeyPress (üretilen karakter, sadece karakter tuşlarında) → KeyUp
        // =====================================================================

        /// <summary>Kullanıcı filtre kutusuna saat yazıyorsa tuşlar kaydedilmez ve normal çalışır.</summary>
        private bool IsEditingFilter => _dtpFrom.Focused || _dtpTo.Focused;

        private void Form1_KeyDown(object? sender, KeyEventArgs e)
        {
            if (IsEditingFilter) return;

            var entry = _recorder.RecordKeyDown(e.KeyCode, e.Modifiers);

            if (_recorder.IsRecording)
            {
                // Kayıt sırasında tuşun başka bir işe yaramasını engelle (ör. okların ListBox'ta seçimi kaydırması).
                // SuppressKeyPress KULLANMIYORUZ: KeyPress olayı gelsin ki tuşun ürettiği gerçek karakteri (ş, ğ, Ö) öğrenelim.
                e.Handled = true;
            }

            if (entry == null) return; // kayıt kapalı ya da tuş basılı tutuluyor (tekrar)

            if (IsVisible(entry)) AddToList(entry);
            _heatmap.Flash(entry.Key);
            UpdateStats();
        }

        private void Form1_KeyPress(object? sender, KeyPressEventArgs e)
        {
            if (IsEditingFilter || !_recorder.IsRecording) return;

            e.Handled = true; // karakter listeye veya butona gitmesin

            var entry = _recorder.RecordCharacter(e.KeyChar);
            if (entry == null) return;

            // Bu kayıt en son eklenen kayıt olduğu için (görünüyorsa) listenin en altındadır: satırı güncelle
            if (IsVisible(entry) && _lstLog.Items.Count > 0)
            {
                _lstLog.Items[_lstLog.Items.Count - 1] = entry.ToString();
            }
            _heatmap.Invalidate(); // tuş etiketi öğrenildi (ör. Oem1 → Ş)
            UpdateStats();
        }

        private void Form1_KeyUp(object? sender, KeyEventArgs e)
        {
            _recorder.RecordKeyUp(e.KeyCode);
        }

        private void UiTimer_Tick(object? sender, EventArgs e)
        {
            _heatmap.Tick();

            // Saniyede bir: hız, süre ve grafik (tuşa basılmasa da zaman ilerledikçe değişir)
            if (++_tickCount % 10 == 0)
            {
                _chart.Invalidate();
                UpdateLiveStats();
            }
        }

        // =====================================================================
        // Butonlar
        // =====================================================================

        private void BtnStart_Click(object? sender, EventArgs e)
        {
            _recorder.Start();
            UpdateStatus();
            _lstLog.Focus(); // odak butonda kalırsa Space/Enter butona basar; odağı listeye al
        }

        private void BtnStop_Click(object? sender, EventArgs e)
        {
            _recorder.Stop();
            UpdateStatus();
        }

        private void BtnClear_Click(object? sender, EventArgs e)
        {
            if (_recorder.TotalCount > 0 &&
                MessageBox.Show("Tüm tuş kayıtları ve istatistikler silinsin mi?", "Temizle",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                FocusListIfRecording();
                return;
            }

            _recorder.Clear();
            RefreshList();
            UpdateStats();
            _heatmap.Invalidate();
            _chart.Invalidate();
            FocusListIfRecording();
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            // Filtre açıksa sadece görünen (filtreye uyan) kayıtlar kaydedilir
            IReadOnlyList<KeyLogEntry> entries = GetVisibleEntries();

            if (entries.Count == 0)
            {
                string message = _filter == null
                    ? "Kaydedilecek tuş kaydı yok."
                    : "Seçilen saat aralığında kaydedilecek tuş kaydı yok.";
                MessageBox.Show(message, "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dialog = new SaveFileDialog
            {
                Filter = "Metin dosyası (*.txt)|*.txt|CSV - Excel ile açılır (*.csv)|*.csv",
                FileName = $"keylog_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (dialog.ShowDialog() != DialogResult.OK) return;

            try
            {
                LogFileWriter.Save(dialog.FileName, entries, _filter?.ToString());
                MessageBox.Show($"{entries.Count} kayıt dosyaya yazıldı.", "Kaydedildi",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) // izin yok, disk dolu, dosya başka programda açık vb.
            {
                MessageBox.Show($"Dosya kaydedilemedi:\n{ex.Message}", "Hata",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                FocusListIfRecording();
            }
        }

        // =====================================================================
        // Saat aralığı filtresi
        // =====================================================================

        private void BtnApplyFilter_Click(object? sender, EventArgs e)
        {
            DateTime from = TruncateToSeconds(_dtpFrom.Value);
            DateTime to = TruncateToSeconds(_dtpTo.Value);

            if (from > to)
            {
                MessageBox.Show("Başlangıç zamanı bitiş zamanından sonra olamaz.", "Geçersiz aralık",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Bitiş saniyesi de dahil olsun: 12:00:05 seçildiyse 12:00:05.999'a kadar olan kayıtlar
            _filter = new TimeRange(from, to.AddSeconds(1).AddTicks(-1));
            _btnClearFilter.Enabled = true;

            RefreshList();
            UpdateStats();
            FocusListIfRecording();
        }

        private void BtnClearFilter_Click(object? sender, EventArgs e)
        {
            _filter = null;
            _btnClearFilter.Enabled = false;

            RefreshList();
            UpdateStats();
            FocusListIfRecording();
        }

        private bool IsVisible(KeyLogEntry entry) => _filter == null || _filter.Value.Contains(entry.Time);

        private IReadOnlyList<KeyLogEntry> GetVisibleEntries() =>
            _filter == null ? _recorder.Entries : _recorder.Filter(_filter.Value);

        private static DateTime TruncateToSeconds(DateTime value) =>
            new(value.Year, value.Month, value.Day, value.Hour, value.Minute, value.Second, value.Kind);

        // =====================================================================
        // Arayüz güncelleme
        // =====================================================================

        private void AddToList(KeyLogEntry entry)
        {
            _lstLog.Items.Add(entry.ToString());
            _lstLog.TopIndex = _lstLog.Items.Count - 1; // en alta kaydır
        }

        /// <summary>Listeyi baştan doldurur (filtre değiştiğinde veya temizlendiğinde).</summary>
        private void RefreshList()
        {
            _lstLog.BeginUpdate(); // yüzlerce satır eklenirken liste titremesin
            _lstLog.Items.Clear();
            foreach (KeyLogEntry entry in GetVisibleEntries())
            {
                _lstLog.Items.Add(entry.ToString());
            }
            _lstLog.EndUpdate();

            if (_lstLog.Items.Count > 0) _lstLog.TopIndex = _lstLog.Items.Count - 1;
        }

        private void FocusListIfRecording()
        {
            if (_recorder.IsRecording) _lstLog.Focus();
        }

        private void UpdateStatus()
        {
            bool rec = _recorder.IsRecording;
            _lblStatus.Text = rec ? "● KAYIT YAPILIYOR — sadece bu pencere aktifken" : "● Durduruldu";
            _lblStatus.ForeColor = rec ? Palette.Recording : Palette.Muted;
            _btnStart.Enabled = !rec;
            _btnStop.Enabled = rec;
        }

        private void UpdateStats()
        {
            _statTotal.Text = _recorder.TotalCount.ToString();
            _statDistinct.Text = _recorder.DistinctKeyCount.ToString();

            var top = _recorder.GetTopKeys(5).Select(p => $"{p.Key} ({p.Value})");
            string topText = _recorder.TotalCount == 0 ? "-" : string.Join("   ", top);
            _lblTopKeys.Text = $"En sık basılanlar:   {topText}";

            _lblListInfo.Text = _filter == null
                ? $"{_lstLog.Items.Count} kayıt"
                : $"Filtre: {_lstLog.Items.Count} / {_recorder.TotalCount} kayıt gösteriliyor";

            UpdateLiveStats();
        }

        /// <summary>Zamana bağlı değerler: dakikadaki tuş hızı ve toplam kayıt süresi.</summary>
        private void UpdateLiveStats()
        {
            _statSpeed.Text = _recorder.KeysPerMinute(DateTime.Now).ToString();

            TimeSpan d = _recorder.RecordingDuration;
            _statDuration.Text = d.TotalHours >= 1 ? $"{(int)d.TotalHours}:{d:mm\\:ss}" : d.ToString(@"mm\:ss");
        }
    }
}
