using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PersonalTaskManager.App.Infrastructure;

public sealed class GuidEqualsToVisibilityConverter : IMultiValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var isEqual = values.Length >= 2 &&
                      values[0] is Guid currentId &&
                      values[1] is Guid editingId &&
                      currentId == editingId;

        if (Invert)
        {
            isEqual = !isEqual;
        }

        return isEqual ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
