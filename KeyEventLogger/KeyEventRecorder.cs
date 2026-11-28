namespace KeyEventLogger
{
    /// <summary>
    /// Kayıt mantığını yönetir: başlat/durdur, kayıt ekle, temizle, filtrele, istatistik.
    /// Form bu sınıfı kullanır ama kayıtların nasıl tutulduğunu bilmek zorunda değildir.
    /// </summary>
    public class KeyEventRecorder
    {
        private readonly List<KeyLogEntry> _entries = new();

        // Basılı tutulan tuşlar. Tuş basılı kaldığında Windows KeyDown'ı tekrar tekrar gönderir;
        // bu küme sayesinde her basışı sadece bir kez kaydediyoruz. KeyUp gelince tuş kümeden çıkar.
        private readonly HashSet<Keys> _pressedKeys = new();

        // Olay sırası: KeyDown → KeyPress (karakter üreten tuşlarda) → KeyUp.
        // KeyDown'da oluşan kayıt, hemen ardından gelecek KeyPress'in karakterini bekler.
        private KeyLogEntry? _waitingForCharacter;

        public bool IsRecording { get; private set; }

        public IReadOnlyList<KeyLogEntry> Entries => _entries;

        public int TotalCount => _entries.Count;

        public void Start() => IsRecording = true;

        public void Stop()
        {
            IsRecording = false;
            _pressedKeys.Clear();
            _waitingForCharacter = null;
        }

        /// <summary>
        /// KeyDown geldiğinde çağrılır. Kayıt kapalıysa veya tuş zaten basılıysa (tekrar) null döner.
        /// </summary>
        public KeyLogEntry? RecordKeyDown(Keys key, Keys modifiers)
        {
            if (!IsRecording) return null;
            if (!_pressedKeys.Add(key)) return null; // Add false dönerse tuş zaten basılı demektir

            var entry = new KeyLogEntry(key, modifiers);
            _entries.Add(entry);
            _waitingForCharacter = entry;

            return entry;
        }

        /// <summary>
        /// KeyPress geldiğinde çağrılır: karakteri son KeyDown kaydına ekler.
        /// Karakteri bekleyen kayıt yoksa (ör. basılı tutma tekrarı) null döner.
        /// </summary>
        public KeyLogEntry? RecordCharacter(char character)
        {
            if (!IsRecording || _waitingForCharacter == null) return null;

            KeyLogEntry entry = _waitingForCharacter;
            _waitingForCharacter = null;
            entry.SetCharacter(character);
            return entry;
        }

        /// <summary>
        /// KeyUp geldiğinde çağrılır; tuş artık basılı değil.
        /// </summary>
        public void RecordKeyUp(Keys key)
        {
            _pressedKeys.Remove(key);

            // Bu tuş karakter üretmeden bırakıldı (F5, oklar...): artık karakter beklenmez
            if (_waitingForCharacter?.Key == key) _waitingForCharacter = null;
        }

        public void Clear()
        {
            _entries.Clear();
            _pressedKeys.Clear();
            _waitingForCharacter = null;
        }

        /// <summary>Verilen tarih/saat aralığındaki kayıtlar.</summary>
        public List<KeyLogEntry> Filter(TimeRange range)
        {
            return _entries.Where(entry => range.Contains(entry.Time)).ToList();
        }

        /// <summary>
        /// En sık basılan n tuşu (ad, adet) olarak döndürür. "a" ile "A" aynı tuş sayılır.
        /// </summary>
        public List<KeyValuePair<string, int>> GetTopKeys(int n)
        {
            return _entries
                .GroupBy(entry => entry.StatName)
                .Select(group => new KeyValuePair<string, int>(group.Key, group.Count()))
                .OrderByDescending(pair => pair.Value)
                .Take(n)
                .ToList();
        }
    }
}
