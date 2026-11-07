using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace TripBook
{
    /// <summary>候车间隔条形图（自绘，随窗口宽度自适应）</summary>
    public class WaitChart : FrameworkElement
    {
        public static readonly DependencyProperty WaitsProperty =
            DependencyProperty.Register("Waits", typeof(IEnumerable<WaitItem>), typeof(WaitChart),
                new FrameworkPropertyMetadata(null,
                    FrameworkPropertyMetadataOptions.AffectsMeasure |
                    FrameworkPropertyMetadataOptions.AffectsRender));

        public IEnumerable<WaitItem> Waits
        {
            get { return (IEnumerable<WaitItem>)GetValue(WaitsProperty); }
            set { SetValue(WaitsProperty, value); }
        }

        List<WaitItem> Items
        {
            get
            {
                var w = Waits;
                return w == null ? new List<WaitItem>() : w.ToList();
            }
        }

        const double RowH = 30;

        protected override Size MeasureOverride(Size availableSize)
        {
            double w = double.IsInfinity(availableSize.Width) ? 420 : availableSize.Width;
            return new Size(w, Math.Max(24, Items.Count * RowH + 2));
        }

        static readonly Typeface Mono = new Typeface("Consolas");
        static readonly Typeface Ui = new Typeface("Microsoft YaHei UI");

        protected override void OnRender(DrawingContext dc)
        {
            var items = Items;
            if (items.Count == 0) return;

            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var dim = Palette.Brush(Palette.TextDim);
            var sub = Palette.Brush(Palette.TextSub);

            var probe = new FormattedText("00:00 → 00:00", CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Mono, 11, dim, dpi);
            double leftW = probe.Width + 14;
            var probe2 = new FormattedText("00h00min", CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Mono, 11, sub, dpi);
            double rightW = probe2.Width + 12;

            double width = ActualWidth;
            double barMax = Math.Max(50, width - leftW - rightW - 10);
            int peak = Math.Max(1, items.Max(x => x.Minutes));

            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                double y = i * RowH;

                var label = new FormattedText(it.Range, CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight, Mono, 11, dim, dpi);
                dc.DrawText(label, new Point(0, y + (RowH - label.Height) / 2));

                double bw = Math.Max(6, barMax * (it.Minutes / (double)peak));
                var brush = new LinearGradientBrush(
                    Color.FromArgb(200, 0x4C, 0x8D, 0xFF),
                    Color.FromArgb(170, 0x33, 0xD6, 0xB0), 0);
                var rect = new Rect(leftW, y + (RowH - 12) / 2, bw, 12);
                dc.DrawRoundedRectangle(brush, null, rect, 4, 4);

                var mins = new FormattedText(Fmt.Duration(it.Minutes), CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight, Mono, 11, sub, dpi);
                dc.DrawText(mins, new Point(leftW + bw + 10, y + (RowH - mins.Height) / 2));
            }
        }
    }
}
