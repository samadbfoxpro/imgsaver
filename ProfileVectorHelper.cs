using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace imgsaver
{
    public static class ProfileVectorHelper
    {
        public static readonly (string Key, string Name, string GeometryData)[] AvailableIcons = new[]
        {
            ("user", "کاربر", "M12,4A4,4 0 0,1 16,8A4,4 0 0,1 12,12A4,4 0 0,1 8,8A4,4 0 0,1 12,4M12,14C16.42,14 20,15.79 20,18V20H4V18C4,15.79 7.58,14 12,14Z"),
            ("briefcase", "کار و بیزینس", "M10,2H14A2,2 0 0,1 16,4V6H20A2,2 0 0,1 22,8V19A2,2 0 0,1 20,21H4A2,2 0 0,1 2,19V8A2,2 0 0,1 4,6H8V4A2,2 0 0,1 10,2M14,6V4H10V6H14Z"),
            ("gamepad", "گیمینگ", "M21,6H3A2,2 0 0,0 1,8V16A2,2 0 0,0 3,18H21A2,2 0 0,0 23,16V8A2,2 0 0,0 21,6M6,13H5V11H6V10H7V11H8V13H7V14H6V13M15.5,14A1.5,1.5 0 1,1 17,12.5A1.5,1.5 0 0,1 15.5,14M18.5,11A1.5,1.5 0 1,1 20,9.5A1.5,1.5 0 0,1 18.5,11Z"),
            ("code", "برنامه‌نویسی", "M14.6,16.6L19.2,12L14.6,7.4L16,6L22,12L16,18L14.6,16.6M9.4,16.6L4.8,12L9.4,7.4L8,6L2,12L8,18L9.4,16.6Z"),
            ("rocket", "پروژه و سرعت", "M2.81,14.12L5.64,11.29L8.46,14.12L5.64,16.95L2.81,14.12M21.19,2.81C17.65,2.81 14.82,4.93 13.41,7.05L7.05,13.41L10.59,16.95L16.95,10.59C19.07,9.18 21.19,6.35 21.19,2.81Z"),
            ("laptop", "لپ‌تاپ", "M20,18H4V6H20M20,4H4C2.89,4 2,4.89 2,6V18A2,2 0 0,0 4,20H20A2,2 0 0,0 22,18V6C22,4.89 21.1,4 20,4Z"),
            ("palette", "طراحی و هنر", "M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A3.5,3.5 0 0,0 15.5,18.5C15.5,17.5 14.7,16.7 13.7,16.7H12.3A2.3,2.3 0 0,1 10,14.4A2.3,2.3 0 0,1 12.3,12.1H14A6,6 0 0,0 20,6.1C20,3.8 16.4,2 12,2M6.5,12A1.5,1.5 0 1,1 8,10.5A1.5,1.5 0 0,1 6.5,12M9.5,8A1.5,1.5 0 1,1 11,6.5A1.5,1.5 0 0,1 9.5,8M14.5,8A1.5,1.5 0 1,1 16,6.5A1.5,1.5 0 0,1 14.5,8M17.5,12A1.5,1.5 0 1,1 19,10.5A1.5,1.5 0 0,1 17.5,12Z"),
            ("star", "ستاره", "M12,17.27L18.18,21L16.54,13.97L22,9.24L14.81,8.62L12,2L9.19,8.62L2,9.24L7.45,13.97L5.82,21L12,17.27Z"),
            ("bot", "ربات و هوش مصنوعی", "M12,2A2,2 0 0,1 14,4C14,4.74 13.6,5.39 13,5.73V7H14A7,7 0 0,1 21,14H22A1,1 0 0,1 23,15V18A1,1 0 0,1 22,19H21V20A2,2 0 0,1 19,22H5A2,2 0 0,1 3,20V19H2A1,1 0 0,1 1,18V15A1,1 0 0,1 2,14H3A7,7 0 0,1 10,7H11V5.73C10.4,5.39 10,4.74 10,4A2,2 0 0,1 12,2M7.5,13A2.5,2.5 0 0,0 5,15.5A2.5,2.5 0 0,0 7.5,18A2.5,2.5 0 0,0 10,15.5A2.5,2.5 0 0,0 7.5,13M16.5,13A2.5,2.5 0 0,0 14,15.5A2.5,2.5 0 0,0 16.5,18A2.5,2.5 0 0,0 19,15.5A2.5,2.5 0 0,0 16.5,13Z"),
            ("lightning", "انرژی و سرعت", "M7,2V13H10V22L17,10H13L17,2H7Z"),
            ("coffee", "کافه و آرامش", "M2,21H20V19H2M20,8H18V5H20M20,3H4V13A4,4 0 0,0 8,17H14A4,4 0 0,0 18,13V10H20A2,2 0 0,0 22,8V5C22,3.89 21.1,3 20,3Z"),
            ("heart", "علاقه‌مندی", "M12,21.35L10.55,20.03C5.4,15.36 2,12.27 2,8.5C2,5.41 4.42,3 7.5,3C9.24,3 10.91,3.81 12,5.08C13.09,3.81 14.76,3 16.5,3C19.58,3 22,5.41 22,8.5C22,12.27 18.6,15.36 13.45,20.03L12,21.35Z")
        };

        public static Geometry GetGeometry(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return Geometry.Parse(AvailableIcons[0].GeometryData);

            var item = AvailableIcons.FirstOrDefault(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (item.GeometryData != null)
                return Geometry.Parse(item.GeometryData);

            // Backward compatibility with previous emojis
            string mappedKey = key switch
            {
                "👤" => "user",
                "💼" => "briefcase",
                "🎮" => "gamepad",
                "📚" or "💻" => "laptop",
                "🚀" => "rocket",
                "🎨" => "palette",
                "🌟" or "⭐" => "star",
                "🐱" or "🦊" => "bot",
                "⚡" => "lightning",
                "☕" => "coffee",
                _ => "user"
            };

            var mappedItem = AvailableIcons.FirstOrDefault(x => x.Key.Equals(mappedKey, StringComparison.OrdinalIgnoreCase));
            return Geometry.Parse(mappedItem.GeometryData ?? AvailableIcons[0].GeometryData);
        }
    }
}
