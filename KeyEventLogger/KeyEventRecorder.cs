namespace KeyEventLogger
{
    /// <summary>
    /// Kayıt mantığını yönetir: başlat/durdur, kayıt ekle, temizle, filtrele, istatistik.
    /// Form bu sınıfı kullanır ama kayıtların nasıl tutulduğunu bilmek zorunda değildir.
    /// </summary>
    public class KeyEventRecorder
    {
        private readonly List<KeyLogEntry> _entries = new();
        private readonly Dictionary<Keys, int> _keyCounts = new();

        // Her fiziksel tuşun en son ürettiği karakter (ısı haritasında tuşun üzerine yazılır).
        // Böylece hangi klavye düzeni kullanılırsa kullanılsın tuşlar doğru etiketlenir (ş, ğ, ö...).
        private readonly Dictionary<Keys, char> _learnedCharacters = new();

        // Basılı tutulan tuşlar. Tuş basılı kaldığında Windows KeyDown'ı tekrar tekrar gönderir;
        // bu küme sayesinde her basışı sadece bir kez kaydediyoruz. KeyUp gelince tuş kümeden çıkar.
        private readonly HashSet<Keys> _pressedKeys = new();

        // Olay sırası: KeyDown → KeyPress (karakter üreten tuşlarda) → KeyUp.
        // KeyDown'da oluşan kayıt, hemen ardından gelecek KeyPress'in karakterini bekler.
        private KeyLogEntry? _waitingForCharacter;

        // Kayıt süresi: önceki kayıtların toplamı + (kayıt sürüyorsa) şu anki kaydın başlangıcı
        private TimeSpan _recordedBefore;
        private DateTime? _recordingStartedAt;

        private static readonly System.Globalization.CultureInfo TurkishCulture = new("tr-TR");

        public bool IsRecording { get; private set; }

        public IReadOnlyList<KeyLogEntry> Entries => _entries;
        public IReadOnlyDictionary<Keys, int> KeyCounts => _keyCounts;
        public IReadOnlyDictionary<Keys, char> LearnedCharacters => _learnedCharacters;

        public int TotalCount => _entries.Count;

        /// <summary>Kaç farklı tuşa basıldı.</summary>
        public int DistinctKeyCount => _keyCounts.Count;

        /// <summary>Toplam kayıt süresi (durdurulan aralıklar sayılmaz).</summary>
        public TimeSpan RecordingDuration =>
            _recordedBefore + (_recordingStartedAt != null ? DateTime.Now - _recordingStartedAt.Value : TimeSpan.Zero);

        public void Start()
        {
            if (IsRecording) return;
            IsRecording = true;
            _recordingStartedAt = DateTime.Now;
        }

        public void Stop()
        {
            if (!IsRecording) return;
            IsRecording = false;
            _pressedKeys.Clear();
            _waitingForCharacter = null;

            if (_recordingStartedAt != null) _recordedBefore += DateTime.Now - _recordingStartedAt.Value;
            _recordingStartedAt = null;
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

            _keyCounts.TryGetValue(key, out int count);
            _keyCounts[key] = count + 1;

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

            // Ctrl/Alt'sız basılan yazdırılabilir karakteri, tuşun etiketi olarak öğren
            if (entry.HasPrintableCharacter && !entry.Modifiers.HasFlag(Keys.Control) && !entry.Modifiers.HasFlag(Keys.Alt))
            {
                _learnedCharacters[entry.Key] = char.ToUpper(character, TurkishCulture); // i → İ, ı → I
            }
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
            _keyCounts.Clear();
            _pressedKeys.Clear();
            _waitingForCharacter = null;
            _recordedBefore = TimeSpan.Zero;
            _recordingStartedAt = IsRecording ? DateTime.Now : null;
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

        /// <summary>Son 60 saniyedeki tuş sayısı = dakikadaki tuş hızı.</summary>
        public int KeysPerMinute(DateTime now)
        {
            DateTime from = now.AddSeconds(-60);
            int count = 0;

            // Kayıtlar zamana göre sıralı: sondan başa doğru say, 60 sn'den eskiye gelince dur
            for (int i = _entries.Count - 1; i >= 0 && _entries[i].Time >= from; i--) count++;
            return count;
        }

        /// <summary>
        /// Etkinlik grafiği için: son (bucketCount × bucketSeconds) saniyeyi eşit aralıklara böler,
        /// her aralıktaki tuş sayısını döndürür. Dizinin son elemanı en yeni aralıktır.
        /// </summary>
        public int[] GetActivity(DateTime now, int bucketSeconds, int bucketCount)
        {
            var buckets = new int[bucketCount];
            DateTime start = now.AddSeconds(-bucketSeconds * bucketCount);

            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                DateTime time = _entries[i].Time;
                if (time < start) break;

                int index = (int)((time - start).TotalSeconds / bucketSeconds);
                if (index >= 0 && index < bucketCount) buckets[index]++;
            }
            return buckets;
        }
    }
}
