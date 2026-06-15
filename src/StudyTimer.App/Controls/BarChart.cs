using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using StudyTimer.Core.Models;
using StudyTimer.Core.Services;

namespace StudyTimer.App.Controls;

public sealed class BarChart : FrameworkElement
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IEnumerable<ChartPoint>), typeof(BarChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    private readonly List<(Rect Bounds, ChartPoint Point)> _bars = [];

    public IEnumerable<ChartPoint>? ItemsSource
    {
        get => (IEnumerable<ChartPoint>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        _bars.Clear();
        var points = ItemsSource?.ToArray() ?? Array.Empty<ChartPoint>();
        if (points.Length == 0 || ActualWidth < 100 || ActualHeight < 100)
        {
            DrawCenteredText(drawingContext, "暂无学习数据", new Rect(0, 0, ActualWidth, ActualHeight), 14, Brushes.Gray);
            return;
        }

        const double left = 44;
        const double right = 12;
        const double top = 24;
        const double bottom = 58;
        var plotWidth = Math.Max(1, ActualWidth - left - right);
        var plotHeight = Math.Max(1, ActualHeight - top - bottom);
        var maxHours = Math.Max(1, points.Max(point => point.Hours));
        var gridPen = new Pen(new SolidColorBrush(Color.FromRgb(228, 231, 236)), 1);

        for (var index = 0; index <= 4; index++)
        {
            var y = top + plotHeight * index / 4;
            drawingContext.DrawLine(gridPen, new Point(left, y), new Point(left + plotWidth, y));
            var value = maxHours * (4 - index) / 4;
            DrawText(drawingContext, $"{value:0.#}h", new Point(4, y - 8), 10, Brushes.Gray, 36, TextAlignment.Right);
        }

        var slotWidth = plotWidth / points.Length;
        var barWidth = Math.Min(44, Math.Max(12, slotWidth * 0.56));
        var barBrush = new SolidColorBrush(Color.FromRgb(79, 70, 229));

        for (var index = 0; index < points.Length; index++)
        {
            var point = points[index];
            var height = point.Hours <= 0 ? 0 : Math.Max(3, point.Hours / maxHours * plotHeight);
            var x = left + index * slotWidth + (slotWidth - barWidth) / 2;
            var y = top + plotHeight - height;
            var rect = new Rect(x, y, barWidth, height);
            if (height > 0)
            {
                drawingContext.DrawRoundedRectangle(barBrush, null, rect, 5, 5);
            }

            _bars.Add((new Rect(x, top, barWidth, plotHeight), point));
            DrawCenteredText(drawingContext, point.Label,
                new Rect(left + index * slotWidth, top + plotHeight + 8, slotWidth, bottom - 8),
                10, Brushes.DimGray);

            if (height > 0)
            {
                DrawCenteredText(drawingContext, $"{point.Hours:0.#}",
                    new Rect(x - 8, Math.Max(0, y - 20), barWidth + 16, 18), 10, Brushes.DimGray);
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var position = e.GetPosition(this);
        var hit = _bars.FirstOrDefault(item => item.Bounds.Contains(position));
        ToolTip = hit.Point is null
            ? null
            : $"{hit.Point.Label.Replace("\n", " ")}：{DurationFormatter.Friendly(hit.Point.Duration)}";
    }

    private void DrawCenteredText(DrawingContext context, string text, Rect bounds, double fontSize, Brush brush)
    {
        var formatted = CreateText(text, fontSize, brush, bounds.Width, TextAlignment.Center);
        context.DrawText(formatted,
            new Point(bounds.Left, bounds.Top + Math.Max(0, (bounds.Height - formatted.Height) / 2)));
    }

    private void DrawText(DrawingContext context, string text, Point point, double fontSize, Brush brush,
        double width, TextAlignment alignment) =>
        context.DrawText(CreateText(text, fontSize, brush, width, alignment), point);

    private FormattedText CreateText(string text, double fontSize, Brush brush, double width, TextAlignment alignment) =>
        new(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface("Microsoft YaHei UI"), fontSize, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            MaxTextWidth = Math.Max(1, width),
            TextAlignment = alignment,
            Trimming = TextTrimming.CharacterEllipsis
        };
}
