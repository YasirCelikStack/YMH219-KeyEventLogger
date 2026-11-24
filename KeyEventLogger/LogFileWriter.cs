namespace KeyEventLogger
{
    /// <summary>
    /// Kayıtları .txt dosyasına yazar. Dosya işiyle ilgili her şey burada toplanır.
    /// </summary>
    public static class LogFileWriter
    {
        public static void Save(string path, IReadOnlyList<KeyLogEntry> entries)
        {
            // using: blok bitince dosya otomatik kapanır (hata olsa bile).
            using var writer = new StreamWriter(path, append: false, System.Text.Encoding.UTF8);

            writer.WriteLine("Key Event Logger - Kayıt Dosyası");
            writer.WriteLine($"Oluşturulma: {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
            writer.WriteLine($"Toplam kayıt: {entries.Count}");
            writer.WriteLine(new string('-', 40));

            foreach (var entry in entries)
            {
                writer.WriteLine(entry.ToString());
            }
        }
    }
}
