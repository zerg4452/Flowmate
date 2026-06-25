using System.Globalization;
using System.Windows.Data;

namespace PersonalTaskManager.App.Infrastructure;

public sealed class EnumEqualsToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is not null && parameter is not null &&
               string.Equals(value.ToString(), parameter.ToString(), StringComparison.Ordinal);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter is string text)
        {
            var enumType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (enumType.IsEnum)
            {
                return Enum.Parse(enumType, text);
            }
        }

        return Binding.DoNothing;
    }
}
