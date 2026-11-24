namespace KeyEventLogger
{
    /// <summary>
    /// Kayıt mantığını yönetir: başlat/durdur, kayıt ekle, temizle, istatistik.
    /// Form bu sınıfı kullanır ama kayıtların nasıl tutulduğunu bilmek zorunda değildir.
    /// </summary>
    public class KeyEventRecorder
    {
        private readonly List<KeyLogEntry> _entries = new();
        private readonly Dictionary<Keys, int> _keyCounts = new();

        // Basılı tutulan tuşlar. Tuş basılı kaldığında Windows KeyDown'ı tekrar tekrar gönderir;
        // bu küme sayesinde her basışı sadece bir kez kaydediyoruz. KeyUp gelince tuş kümeden çıkar.
        private readonly HashSet<Keys> _pressedKeys = new();

        public bool IsRecording { get; private set; }

        public IReadOnlyList<KeyLogEntry> Entries => _entries;

        public int TotalCount => _entries.Count;

        public void Start() => IsRecording = true;

        public void Stop()
        {
            IsRecording = false;
            _pressedKeys.Clear();
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

            _keyCounts.TryGetValue(key, out int count);
            _keyCounts[key] = count + 1;

            return entry;
        }

        /// <summary>
        /// KeyUp geldiğinde çağrılır; tuş artık basılı değil.
        /// </summary>
        public void RecordKeyUp(Keys key)
        {
            _pressedKeys.Remove(key);
        }

        public void Clear()
        {
            _entries.Clear();
            _keyCounts.Clear();
            _pressedKeys.Clear();
        }

        /// <summary>
        /// En sık basılan n tuşu (tuş, adet) olarak döndürür.
        /// </summary>
        public List<KeyValuePair<Keys, int>> GetTopKeys(int n)
        {
            return _keyCounts
                .OrderByDescending(pair => pair.Value)
                .Take(n)
                .ToList();
        }
    }
}
