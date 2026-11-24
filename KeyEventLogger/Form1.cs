namespace KeyEventLogger
{
    public partial class Form1 : Form
    {
        private readonly KeyEventRecorder _recorder = new();

        // Arayüz kontrolleri (Designer yerine kodla oluşturuyoruz)
        private readonly Button _btnStart = new() { Text = "Başlat", Width = 90 };
        private readonly Button _btnStop = new() { Text = "Durdur", Width = 90, Enabled = false };
        private readonly Button _btnClear = new() { Text = "Temizle", Width = 90 };
        private readonly Button _btnSave = new() { Text = "Kaydet (.txt)", Width = 110 };
        private readonly Label _lblStatus = new() { AutoSize = true, Margin = new Padding(15, 8, 0, 0) };
        private readonly ListBox _lstLog = new() { Dock = DockStyle.Fill, Font = new Font("Consolas", 10) };
        private readonly Label _lblStats = new() { Dock = DockStyle.Bottom, Height = 28, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0) };

        public Form1()
        {
            InitializeComponent();
            BuildUi();

            // KeyPreview = true: tuş olayları önce forma gelir, hangi kontrol odakta olursa olsun.
            // Bu sadece BU pencere aktifken çalışır (föydeki güvenlik sınırına uygun).
            KeyPreview = true;
            KeyDown += Form1_KeyDown;
            KeyUp += Form1_KeyUp;

            _btnStart.Click += BtnStart_Click;
            _btnStop.Click += BtnStop_Click;
            _btnClear.Click += BtnClear_Click;
            _btnSave.Click += BtnSave_Click;

            // Tab ve ok tuşları normalde "kontroller arası gezinme" için kullanılır ve KeyDown'a gelmez.
            // IsInputKey = true diyerek bunları da yakalayabiliyoruz.
            _lstLog.PreviewKeyDown += (s, e) => { if (_recorder.IsRecording) e.IsInputKey = true; };

            UpdateStatus();
            UpdateStats();
        }

        private void BuildUi()
        {
            Text = "Key Event Logger";
            Size = new Size(620, 480);
            MinimumSize = new Size(500, 350);
            StartPosition = FormStartPosition.CenterScreen;

            var topPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(6)
            };
            topPanel.Controls.AddRange(new Control[] { _btnStart, _btnStop, _btnClear, _btnSave, _lblStatus });

            // Dock sırası: Fill olan kontrol önce eklenir, Top/Bottom olanlar sonra.
            Controls.Add(_lstLog);
            Controls.Add(topPanel);
            Controls.Add(_lblStats);
        }

        // ---------- Klavye olayları ----------

        private void Form1_KeyDown(object? sender, KeyEventArgs e)
        {
            var entry = _recorder.RecordKeyDown(e.KeyCode, e.Modifiers);

            if (_recorder.IsRecording)
            {
                // Kayıt sırasında tuşun başka bir işe yaramasını engelle
                // (ör. Space'in butona basması, okların ListBox'ta seçimi kaydırması).
                e.Handled = true;
                e.SuppressKeyPress = true;
            }

            if (entry == null) return; // kayıt kapalı ya da tuş basılı tutuluyor (tekrar)

            _lstLog.Items.Add(entry.ToString());
            _lstLog.TopIndex = _lstLog.Items.Count - 1; // en alta kaydır
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
            _lstLog.Items.Clear();
            UpdateStats();
            if (_recorder.IsRecording) _lstLog.Focus();
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            if (_recorder.TotalCount == 0)
            {
                MessageBox.Show("Kaydedilecek tuş kaydı yok.", "Bilgi",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                LogFileWriter.Save(dialog.FileName, _recorder.Entries);
                MessageBox.Show($"{_recorder.TotalCount} kayıt dosyaya yazıldı.", "Kaydedildi",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) // izin yok, disk dolu, dosya başka programda açık vb.
            {
                MessageBox.Show($"Dosya kaydedilemedi:\n{ex.Message}", "Hata",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (_recorder.IsRecording) _lstLog.Focus();
            }
        }

        // ---------- Arayüz güncelleme ----------

        private void UpdateStatus()
        {
            bool rec = _recorder.IsRecording;
            _lblStatus.Text = rec ? "● KAYIT YAPILIYOR" : "● Durduruldu";
            _lblStatus.ForeColor = rec ? Color.Red : Color.Gray;
            _lblStatus.Font = new Font(Font, rec ? FontStyle.Bold : FontStyle.Regular);
            _btnStart.Enabled = !rec;
            _btnStop.Enabled = rec;
        }

        private void UpdateStats()
        {
            var top = _recorder.GetTopKeys(3)
                .Select(p => $"{p.Key} ({p.Value})");
            string topText = _recorder.TotalCount == 0 ? "-" : string.Join(", ", top);
            _lblStats.Text = $"Toplam tuş: {_recorder.TotalCount}   |   En sık: {topText}";
        }
    }
}
