using System;

namespace imgsaver
{
    public class BrowserProfile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "Account 1";
        public string ColorHex { get; set; } = "#2ECC71";
        public string Icon { get; set; } = "👤";
        public DateTime LastUsed { get; set; } = DateTime.Now;
        public bool IsDefault { get; set; } = false;
        public string Description { get; set; } = "";
    }
}
