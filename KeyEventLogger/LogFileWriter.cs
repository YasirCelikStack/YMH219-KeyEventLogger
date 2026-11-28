using System.Text;

namespace KeyEventLogger
{
    /// <summary>
    /// Kayıtları dosyaya yazar: okunabilir .txt veya Excel'de açılabilen .csv.
    /// Dosya işiyle ilgili her şey burada toplanır.
    /// </summary>
    public static class LogFileWriter
    {
        /// <param name="filterDescription">Filtre uygulandıysa açıklaması (dosya başlığına yazılır), yoksa null.</param>
        public static void Save(string path, IReadOnlyList<KeyLogEntry> entries, string? filterDescription = null)
        {
            if (Path.GetExtension(path).Equals(".csv", StringComparison.OrdinalIgnoreCase))
            {
                SaveCsv(path, entries);
                return;
            }

            // using: blok bitince dosya otomatik kapanır (hata olsa bile).
            using var writer = new StreamWriter(path, append: false, Encoding.UTF8);

            writer.WriteLine("Key Event Logger - Kayıt Dosyası");
            writer.WriteLine($"Oluşturulma: {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
            if (filterDescription != null)
            {
                writer.WriteLine($"Saat aralığı filtresi: {filterDescription}");
            }
            writer.WriteLine($"Toplam kayıt: {entries.Count}");
            writer.WriteLine(new string('-', 40));

            foreach (var entry in entries)
            {
                writer.WriteLine(entry.ToString());
            }
        }

        /// <summary>
        /// CSV (virgülle ayrılmış değerler): her satır bir tuş olayı.
        /// Türkçe Excel ayırıcı olarak noktalı virgül bekler; UTF-8 BOM sayesinde Türkçe karakterler bozulmaz.
        /// </summary>
        public static void SaveCsv(string path, IReadOnlyList<KeyLogEntry> entries)
        {
            using var writer = new StreamWriter(path, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            writer.WriteLine("Tarih;Saat;Tuş;Karakter;Ctrl;Shift;Alt");
            foreach (KeyLogEntry entry in entries)
            {
                string character = entry.HasPrintableCharacter ? entry.Character.GetValueOrDefault().ToString() : "";
                writer.WriteLine(string.Join(";",
                    entry.Time.ToString("dd.MM.yyyy"),
                    entry.Time.ToString("HH:mm:ss.fff"),
                    Escape(entry.Key.ToString()),
                    Escape(character),
                    entry.Modifiers.HasFlag(Keys.Control) ? "1" : "0",
                    entry.Modifiers.HasFlag(Keys.Shift) ? "1" : "0",
                    entry.Modifiers.HasFlag(Keys.Alt) ? "1" : "0"));
            }
        }

        /// <summary>Ayırıcı veya tırnak içeren değeri tırnak içine alır (CSV kuralı).</summary>
        private static string Escape(string value)
        {
            if (value.IndexOfAny(new[] { ';', '"', '\n', '\r' }) < 0) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
