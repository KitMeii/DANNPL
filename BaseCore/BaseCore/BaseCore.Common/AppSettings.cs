namespace BaseCore.Common
{
    public class AppSettings
    {
        public string Secret { get; set; }
        public int MinuteExpiredToken { get; set; }
        public bool IsProduction { get; set; }
        public string MediaHost { get; set; }
        public string LocalMediaHost { get; set; }
    }
}
