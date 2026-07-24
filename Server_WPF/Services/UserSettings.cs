namespace RemoteDesktopServer.Services
{
    public class UserSettings
    {
        public string Theme { get; set; } = "Light";
        public string FontFamily { get; set; } = "SF Pro Display";
        public int ServerPort { get; set; } = 5000;
        public string Pin { get; set; } = "123456";
        public int Quality { get; set; } = 100;
        public double Scale { get; set; } = 1.00;
        public int Fps { get; set; } = 120;
    }
}
