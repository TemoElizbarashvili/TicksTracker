using System.Globalization;
using System.Windows.Data;

namespace ExeTickTracker.UI.Helpers;

internal class SecondsToHeightConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
        {
            return 0d;
        }

        if (values[0] is not double valueSeconds ||
            values[1] is not double maxSeconds ||
            maxSeconds <= 0)
        {
            return 0d;
        }

        var baseHeight = parameter is double p ? p : 100d;

        return baseHeight * (valueSeconds / maxSeconds);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

