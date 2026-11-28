namespace KeyEventLogger
{
    /// <summary>
    /// Kayıtları .txt dosyasına yazar. Dosya işiyle ilgili her şey burada toplanır.
    /// </summary>
    public static class LogFileWriter
    {
        /// <param name="filterDescription">Filtre uygulandıysa açıklaması (dosya başlığına yazılır), yoksa null.</param>
        public static void Save(string path, IReadOnlyList<KeyLogEntry> entries, string? filterDescription = null)
        {
            // using: blok bitince dosya otomatik kapanır (hata olsa bile).
            using var writer = new StreamWriter(path, append: false, System.Text.Encoding.UTF8);

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
    }
}
