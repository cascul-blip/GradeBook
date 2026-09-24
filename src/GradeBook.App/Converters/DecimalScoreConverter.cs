using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace GradeBook.App.Converters;

/// <summary>
/// Converts between the score TextBox's string Text and the underlying decimal Score. Without this,
/// clearing the box to an empty string (e.g. backspacing the default 0) throws an unhandled
/// InvalidCastException from Avalonia's default string-to-decimal conversion. Grades are always shown
/// and stored as whole numbers — any fractional input is rounded rather than rejected.
/// Only plain non-negative numbers are accepted: no sign (so "-5" is rejected) and no thousands
/// separator (so "1,5" isn't quietly read as 15). Rejected text leaves the saved score unchanged.
/// </summary>
public sealed class DecimalScoreConverter : IValueConverter
{
    public static readonly DecimalScoreConverter Instance = new();

    private const NumberStyles ScoreStyles = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is decimal d ? Format(d, culture) : value?.ToString();

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string text || string.IsNullOrWhiteSpace(text))
        {
            return 0m;
        }

        return TryParse(text, culture, out var result) ? result : BindingOperations.DoNothing;
    }

    public static string Format(decimal score, CultureInfo culture) =>
        Math.Round(score, 0, MidpointRounding.AwayFromZero).ToString("F0", culture);

    /// <summary>True for blank text (treated as 0) or a plain non-negative number, rounded to a whole number.</summary>
    public static bool TryParse(string? text, CultureInfo culture, out decimal score)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            score = 0m;
            return true;
        }

        if (decimal.TryParse(text, ScoreStyles, culture, out var parsed))
        {
            score = Math.Round(parsed, 0, MidpointRounding.AwayFromZero);
            return true;
        }

        score = 0m;
        return false;
    }
}
