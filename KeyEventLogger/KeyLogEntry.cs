namespace KeyEventLogger
{
    /// <summary>
    /// Tek bir tuş olayını temsil eder: hangi tuş, hangi değiştiricilerle (Ctrl/Shift/Alt), ne zaman basıldı
    /// ve (varsa) tuşun ürettiği gerçek karakter.
    /// </summary>
    public class KeyLogEntry
    {
        public DateTime Time { get; }
        public Keys Key { get; }
        public Keys Modifiers { get; }

        /// <summary>
        /// KeyPress olayından gelen gerçek karakter (ör. ş, Ğ, @). Karakter üretmeyen tuşlarda (F5, oklar, Shift) null kalır.
        /// KeyDown sadece fiziksel tuşu (Keys.Oem1) bildirir; klavye düzenine göre hangi harfin yazıldığını KeyPress söyler.
        /// </summary>
        public char? Character { get; private set; }

        public KeyLogEntry(Keys key, Keys modifiers)
        {
            Time = DateTime.Now;
            Key = key;
            Modifiers = modifiers;
        }

        /// <summary>KeyPress geldiğinde kaydedici tarafından çağrılır.</summary>
        internal void SetCharacter(char character)
        {
            Character = character;
        }

        /// <summary>Ekranda gösterilebilir bir karakter var mı? (Kontrol karakterleri ve boşluk hariç.)</summary>
        public bool HasPrintableCharacter =>
            Character is char c && !char.IsControl(c) && !char.IsWhiteSpace(c);

        /// <summary>
        /// Tuşun okunur adı. Örnek: "Ctrl+Shift+S", "Space", "Shift", "Oem1".
        /// </summary>
        public string KeyName
        {
            get
            {
                // Shift/Ctrl/Alt tek başına basıldıysa sadece adını yaz ("Shift+ShiftKey" gibi tuhaf çıktı olmasın).
                switch (Key)
                {
                    case Keys.ShiftKey: return "Shift";
                    case Keys.ControlKey: return "Ctrl";
                    case Keys.Menu: return "Alt";
                }

                string prefix = "";
                if (Modifiers.HasFlag(Keys.Control)) prefix += "Ctrl+";
                if (Modifiers.HasFlag(Keys.Shift)) prefix += "Shift+";
                if (Modifiers.HasFlag(Keys.Alt)) prefix += "Alt+";

                return prefix + Key.ToString();
            }
        }

        /// <summary>Listede gösterilecek ad: karakter varsa karakterin kendisi (ş), yoksa tuş adı (Ctrl+S, F5).</summary>
        public string DisplayName => HasPrintableCharacter ? Character.GetValueOrDefault().ToString() : KeyName;

        /// <summary>
        /// İstatistikte kullanılan ad. Büyük/küçük harf ayrımı yapılmaz: "a" ile "Shift+A" aynı tuş sayılır.
        /// </summary>
        public string StatName
        {
            get
            {
                if (HasPrintableCharacter) return char.ToLower(Character.GetValueOrDefault()).ToString();

                return Key switch
                {
                    Keys.ShiftKey => "Shift",
                    Keys.ControlKey => "Ctrl",
                    Keys.Menu => "Alt",
                    _ => Key.ToString()
                };
            }
        }

        public override string ToString()
        {
            string text = $"[{Time:HH:mm:ss.fff}] {DisplayName}";

            // Karakter gösteriliyorsa hangi tuştan geldiğini de yaz: "ş   (Oem1)".
            // Böylece KeyDown (fiziksel tuş) ile KeyPress (karakter) farkı da görülür.
            if (HasPrintableCharacter) text += $"   ({KeyName})";

            return text;
        }
    }
}
