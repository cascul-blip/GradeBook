using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace GradeBook.App.Converters;

/// <summary>
/// Converts between the score TextBox's string Text and the underlying decimal Score. Without this,
/// clearing the box to an empty string (e.g. backspacing the default 0) throws an unhandled
/// InvalidCastException from Avalonia's default string-to-decimal conversion. Grades are always shown
/// and stored as whole numbers — any fractional input is rounded rather than rejected.
/// </summary>
public sealed class DecimalScoreConverter : IValueConverter
{
    public static readonly DecimalScoreConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is decimal d ? Math.Round(d, 0, MidpointRounding.AwayFromZero).ToString("F0", culture) : value?.ToString();

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string text || string.IsNullOrWhiteSpace(text))
        {
            return 0m;
        }

        return decimal.TryParse(text, NumberStyles.Number, culture, out var result)
            ? Math.Round(result, 0, MidpointRounding.AwayFromZero)
            : BindingOperations.DoNothing;
    }
}
