using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TripBook
{
    /// <summary>非空字符串 → 显示</summary>
    public class NotEmptyVisConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var s = value as string;
            bool empty = string.IsNullOrWhiteSpace(s);
            bool invert = parameter as string == "invert";
            return (empty ^ invert) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type t, object p, CultureInfo c)
        {
            throw new NotSupportedException();
        }
    }

    public class NotNullVisConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool none = value == null;
            bool invert = parameter as string == "invert";
            return (none ^ invert) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type t, object p, CultureInfo c)
        {
            throw new NotSupportedException();
        }
    }

    public class BoolVisConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool b = value is bool && (bool)value;
            if (parameter as string == "invert") b = !b;
            return b ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type t, object p, CultureInfo c)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>线路名 → 稳定配色画刷</summary>
    public class LineBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var name = value as string ?? "";
            switch (parameter as string)
            {
                case "bg": return Palette.BrushA(Palette.LineColorString(name), 0.16);
                case "border": return Palette.BrushA(Palette.LineColorString(name), 0.30);
                default: return Palette.Brush(Palette.LineColorString(name));
            }
        }

        public object ConvertBack(object value, Type t, object p, CultureInfo c)
        {
            throw new NotSupportedException();
        }
    }
}
