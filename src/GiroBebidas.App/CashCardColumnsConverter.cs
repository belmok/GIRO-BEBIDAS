using System.Globalization;
using System.Windows.Data;

namespace GiroBebidas.App;

public sealed class CashCardColumnsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is double width && width >= 1350 ? 4 : 2;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
