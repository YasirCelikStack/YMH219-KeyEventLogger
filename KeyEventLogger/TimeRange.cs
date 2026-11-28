namespace KeyEventLogger
{
    /// <summary>
    /// Kayıtları filtrelemek için kullanılan tarih/saat aralığı. İki uç da dahildir.
    /// record struct: değer gibi davranan, eşitliği ve kopyalanması otomatik olan küçük bir tür.
    /// </summary>
    public readonly record struct TimeRange(DateTime From, DateTime To)
    {
        public bool Contains(DateTime time) => time >= From && time <= To;

        public override string ToString() => $"{From:dd.MM.yyyy HH:mm:ss} – {To:dd.MM.yyyy HH:mm:ss}";
    }
}
