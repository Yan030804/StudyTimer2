using System.Globalization;

namespace StudyTimer.App.ViewModels;

public static class SubjectColorHelper
{
    public static string SoftBackground(string color) => MixWithWhite(color, 0.88);

    private static string MixWithWhite(string color, double whiteRatio)
    {
        if (!TryParse(color, out var red, out var green, out var blue))
        {
            return "#EEF2FF";
        }

        var foregroundRatio = 1 - whiteRatio;
        return string.Create(CultureInfo.InvariantCulture,
            $"#{Mix(red, foregroundRatio):X2}{Mix(green, foregroundRatio):X2}{Mix(blue, foregroundRatio):X2}");
    }

    private static int Mix(int channel, double foregroundRatio) =>
        (int)Math.Round(255 * (1 - foregroundRatio) + channel * foregroundRatio);

    private static bool TryParse(string color, out int red, out int green, out int blue)
    {
        red = green = blue = 0;
        if (color.Length != 7 || color[0] != '#')
        {
            return false;
        }

        return int.TryParse(color.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out red) &&
               int.TryParse(color.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out green) &&
               int.TryParse(color.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out blue);
    }
}
