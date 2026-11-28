namespace KeyEventLogger
{
    public partial class Form1 : Form
    {
        private readonly KeyEventRecorder _recorder = new();

        // Saat aralığı filtresi: null ise tüm kayıtlar gösterilir
        private TimeRange? _filter;

        // Durum yazısının fontları bir kez oluşturulur (her güncellemede yeni Font oluşturmak bellek israfı olur)
        private readonly Font _statusFontBold;
        private readonly Font _statusFontRegular;

        // Arayüz kontrolleri (Designer yerine kodla oluşturuyoruz)
        private readonly Button _btnStart = new() { Text = "Başlat", Width = 90 };
        private readonly Button _btnStop = new() { Text = "Durdur", Width = 90, Enabled = false };
        private readonly Button _btnClear = new() { Text = "Temizle", Width = 90 };
        private readonly Button _btnSave = new() { Text = "Kaydet (.txt)", Width = 110 };
        private readonly Label _lblStatus = new() { AutoSize = true, Margin = new Padding(15, 8, 0, 0) };
        private readonly ListBox _lstLog = new() { Dock = DockStyle.Fill, Font = new Font("Consolas", 10) };
        private readonly Label _lblStats = new() { Dock = DockStyle.Bottom, Height = 28, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0) };

        // Filtre kontrolleri
        private readonly DateTimePicker _dtpFrom = CreateDateTimePicker();
        private readonly DateTimePicker _dtpTo = CreateDateTimePicker();
        private readonly Button _btnApplyFilter = new() { Text = "Filtrele", Width = 75 };
        private readonly Button _btnClearFilter = new() { Text = "Filtreyi Kaldır", Width = 105, Enabled = false };

        public Form1()
        {
            InitializeComponent();

            _statusFontBold = new Font(Font, FontStyle.Bold);
            _statusFontRegular = new Font(Font, FontStyle.Regular);

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

        private void BuildUi()
        {
            Text = "Key Event Logger";
            Size = new Size(720, 500);
            MinimumSize = new Size(700, 380);
            StartPosition = FormStartPosition.CenterScreen;

            var topPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(6)
            };
            topPanel.Controls.AddRange(new Control[] { _btnStart, _btnStop, _btnClear, _btnSave, _lblStatus });

            var filterPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 36,
                Padding = new Padding(6, 0, 6, 4)
            };
            var lblRange = new Label { Text = "Saat aralığı:", AutoSize = true, Margin = new Padding(3, 7, 3, 0) };
            var lblDash = new Label { Text = "–", AutoSize = true, Margin = new Padding(0, 7, 0, 0) };
            filterPanel.Controls.AddRange(new Control[] { lblRange, _dtpFrom, lblDash, _dtpTo, _btnApplyFilter, _btnClearFilter });

            // Dock sırası: Fill olan kontrol önce eklenir, Top/Bottom olanlar sonra.
            // Top olanlardan EN SON eklenen en üstte durur: önce filtre, sonra butonlar.
            Controls.Add(_lstLog);
            Controls.Add(filterPanel);
            Controls.Add(topPanel);
            Controls.Add(_lblStats);
        }

        // ---------- Klavye olayları ----------
        // Sıra: KeyDown (fiziksel tuş) → KeyPress (üretilen karakter, sadece karakter tuşlarında) → KeyUp

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
            UpdateStats();
        }

        private void Form1_KeyUp(object? sender, KeyEventArgs e)
        {
            _recorder.RecordKeyUp(e.KeyCode);
        }

        // ---------- Butonlar ----------

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
            _recorder.Clear();
            RefreshList();
            UpdateStats();
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
                Filter = "Metin dosyası (*.txt)|*.txt",
                FileName = $"keylog_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
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

        // ---------- Saat aralığı filtresi ----------

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

        // ---------- Arayüz güncelleme ----------

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
            _lblStatus.Text = rec ? "● KAYIT YAPILIYOR" : "● Durduruldu";
            _lblStatus.ForeColor = rec ? Color.Red : Color.Gray;
            _lblStatus.Font = rec ? _statusFontBold : _statusFontRegular;
            _btnStart.Enabled = !rec;
            _btnStop.Enabled = rec;
        }

        private void UpdateStats()
        {
            var top = _recorder.GetTopKeys(3)
                .Select(p => $"{p.Key} ({p.Value})");
            string topText = _recorder.TotalCount == 0 ? "-" : string.Join(", ", top);
            string shown = _filter == null ? "" : $"   |   Gösterilen: {_lstLog.Items.Count}";
            _lblStats.Text = $"Toplam tuş: {_recorder.TotalCount}{shown}   |   En sık: {topText}";
        }
    }
}
