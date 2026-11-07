using System;
using System.Windows.Media;

namespace TripBook
{
    /// <summary>统一配色（界面与导出的网页保持一致）</summary>
    public static class Palette
    {
        public const string Bg = "#0D1015";
        public const string Sidebar = "#11151C";
        public const string Panel = "#151A22";
        public const string Card = "#1A212C";
        public const string CardHi = "#1F2734";
        public const string Border = "#252E3C";
        public const string BorderHi = "#334054";
        public const string Text = "#E9EFF8";
        public const string TextSub = "#9AA5B8";
        public const string TextDim = "#66707F";
        public const string Accent = "#4C8DFF";
        public const string AccentDark = "#2E6BD8";
        public const string Teal = "#33D6B0";
        public const string Amber = "#FFB454";
        public const string Rose = "#FF6B81";
        public const string Violet = "#A98BFF";
        public const string Sky = "#5FD0FF";

        public static readonly string[] LinePalette =
        {
            "#4C8DFF", "#33D6B0", "#FFB454", "#A98BFF",
            "#5FD0FF", "#FF8FA3", "#8BD450", "#FFC978"
        };

        public static string LineColorString(string name)
        {
            if (string.IsNullOrEmpty(name)) return Accent;
            int h = 0;
            foreach (var ch in name) h = (h * 131 + ch) % 100000;
            return LinePalette[h % LinePalette.Length];
        }

        public static Color ToColor(string hex)
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }

        public static Brush Brush(string hex)
        {
            var b = new SolidColorBrush(ToColor(hex));
            b.Freeze();
            return b;
        }

        public static Brush BrushA(string hex, double alpha)
        {
            var c = ToColor(hex);
            var b = new SolidColorBrush(Color.FromArgb((byte)Math.Round(255 * Math.Max(0, Math.Min(1, alpha))),
                                                       c.R, c.G, c.B));
            b.Freeze();
            return b;
        }

        public static Brush LineBrush(string name)
        {
            return Brush(LineColorString(name));
        }

        /// <summary>动作对应颜色：上车蓝、下车青</summary>
        public static string ActionColor(string action)
        {
            if (action == "上车") return Accent;
            if (action == "下车") return Teal;
            return TextDim;
        }
    }
}
