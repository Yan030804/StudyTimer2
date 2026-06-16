using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using StudyTimer.Core.Models;
using StudyTimer.Core.Services;

namespace StudyTimer.App.Controls;

public sealed class PieChart : FrameworkElement
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IEnumerable<SubjectSharePoint>), typeof(PieChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    private readonly List<SliceHit> _slices = [];

    public IEnumerable<SubjectSharePoint>? ItemsSource
    {
        get => (IEnumerable<SubjectSharePoint>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        _slices.Clear();
        var points = ItemsSource?.Where(item => item.Duration > TimeSpan.Zero).ToArray()
            ?? Array.Empty<SubjectSharePoint>();
        if (points.Length == 0 || ActualWidth < 180 || ActualHeight < 160)
        {
            DrawCenteredText(drawingContext, "暂无科目占比数据",
                new Rect(0, 0, ActualWidth, ActualHeight), 14, Brushes.Gray);
            return;
        }

        var legendWidth = ActualWidth >= 360 ? Math.Min(190, ActualWidth * 0.48) : 0;
        var chartBounds = new Rect(0, 0, ActualWidth - legendWidth, ActualHeight);
        var radius = Math.Max(36, Math.Min(chartBounds.Width, chartBounds.Height) * 0.34);
        var center = new Point(chartBounds.Left + chartBounds.Width / 2, chartBounds.Top + chartBounds.Height / 2);
        var startAngle = -90.0;

        for (var index = 0; index < points.Length; index++)
        {
            var point = points[index];
            var sweep = index == points.Length - 1
                ? 270 - startAngle
                : 360 * point.Percentage / 100;
            var brush = new SolidColorBrush(ParseColor(point.Color));
            brush.Freeze();

            if (points.Length == 1)
            {
                drawingContext.DrawEllipse(brush, null, center, radius, radius);
                _slices.Add(new SliceHit(point, center, radius, -180, 180, Rect.Empty));
            }
            else
            {
                var geometry = CreateSlice(center, radius, startAngle, sweep);
                drawingContext.DrawGeometry(brush, null, geometry);
                _slices.Add(new SliceHit(point, center, radius, startAngle, startAngle + sweep, Rect.Empty));
            }

            startAngle += sweep;
        }

        drawingContext.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromRgb(255, 255, 255)), 2),
            center, radius, radius);

        if (legendWidth > 0)
        {
            DrawLegend(drawingContext, points, ActualWidth - legendWidth + 8, 10, legendWidth - 8);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var position = e.GetPosition(this);
        SliceHit? hit = _slices.FirstOrDefault(item => item.Contains(position));
        ToolTip = hit is null
            ? null
            : $"{hit.Point.SubjectName}: {DurationFormatter.Friendly(hit.Point.Duration)} ({hit.Point.Percentage:0.#}%)";
    }

    private void DrawLegend(
        DrawingContext context,
        IReadOnlyList<SubjectSharePoint> points,
        double left,
        double top,
        double width)
    {
        var y = top;
        foreach (var point in points.Take(6))
        {
            var color = new SolidColorBrush(ParseColor(point.Color));
            color.Freeze();
            context.DrawRoundedRectangle(color, null, new Rect(left, y + 5, 10, 10), 3, 3);
            DrawText(context, point.SubjectName, new Point(left + 16, y), 12,
                new SolidColorBrush(Color.FromRgb(23, 32, 51)), width - 16, TextAlignment.Left);
            DrawText(context, $"{DurationFormatter.Friendly(point.Duration)} · {point.Percentage:0.#}%",
                new Point(left + 16, y + 18), 10, Brushes.Gray, width - 16, TextAlignment.Left);

            var bounds = new Rect(left, y, width, 34);
            var index = _slices.FindIndex(item => item.Point.SubjectId == point.SubjectId);
            if (index >= 0)
            {
                _slices[index] = _slices[index] with { LegendBounds = bounds };
            }

            y += 38;
        }

        if (points.Count > 6)
        {
            DrawText(context, $"另有 {points.Count - 6} 个科目", new Point(left, y + 4),
                10, Brushes.Gray, width, TextAlignment.Left);
        }
    }

    private static StreamGeometry CreateSlice(Point center, double radius, double startAngle, double sweep)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        var start = PointAtAngle(center, radius, startAngle);
        var end = PointAtAngle(center, radius, startAngle + sweep);
        context.BeginFigure(center, true, true);
        context.LineTo(start, true, false);
        context.ArcTo(end, new Size(radius, radius), 0, sweep > 180, SweepDirection.Clockwise, true, false);
        geometry.Freeze();
        return geometry;
    }

    private static Point PointAtAngle(Point center, double radius, double angle)
    {
        var radians = angle * Math.PI / 180;
        return new Point(center.X + radius * Math.Cos(radians), center.Y + radius * Math.Sin(radians));
    }

    private static Color ParseColor(string value)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(value)!;
        }
        catch (FormatException)
        {
            return Color.FromRgb(102, 112, 133);
        }
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

    private sealed record SliceHit(
        SubjectSharePoint Point,
        Point Center,
        double Radius,
        double StartAngle,
        double EndAngle,
        Rect LegendBounds)
    {
        public bool Contains(Point position)
        {
            if (!LegendBounds.IsEmpty && LegendBounds.Contains(position))
            {
                return true;
            }

            var distance = Math.Sqrt(Math.Pow(position.X - Center.X, 2) + Math.Pow(position.Y - Center.Y, 2));
            if (distance > Radius)
            {
                return false;
            }

            var angle = Math.Atan2(position.Y - Center.Y, position.X - Center.X) * 180 / Math.PI;
            if (angle < -90)
            {
                angle += 360;
            }

            var start = StartAngle < -90 ? StartAngle + 360 : StartAngle;
            var end = EndAngle < -90 ? EndAngle + 360 : EndAngle;
            return angle >= start && angle <= end;
        }
    }
}
