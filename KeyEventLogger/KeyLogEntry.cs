namespace KeyEventLogger
{
    /// <summary>
    /// Tek bir tuş olayını temsil eder: hangi tuş, hangi değiştiricilerle (Ctrl/Shift/Alt), ne zaman basıldı.
    /// </summary>
    public class KeyLogEntry
    {
        public DateTime Time { get; }
        public Keys Key { get; }
        public Keys Modifiers { get; }

        public KeyLogEntry(Keys key, Keys modifiers)
        {
            Time = DateTime.Now;
            Key = key;
            Modifiers = modifiers;
        }

        /// <summary>
        /// Ekranda/dosyada görünecek okunur ad. Örnek: "Ctrl+Shift+S", "Space", "Shift".
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

        public override string ToString()
        {
            return $"[{Time:HH:mm:ss.fff}] {KeyName}";
        }
    }
}
