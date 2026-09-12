using System.Globalization;
using Avalonia.Data.Converters;

namespace GradeBook.App.Converters;

public sealed class BoolToActiveLabelConverter : IValueConverter
{
    public static readonly BoolToActiveLabelConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "Deactivate" : "Reactivate";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
