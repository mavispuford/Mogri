using System.Globalization;

namespace Mogri.Converters;

public class BoolToDoubleConverter : BindableObject, IValueConverter
{
    public static readonly BindableProperty TrueValueProperty = BindableProperty.Create(
        nameof(TrueValue), typeof(double), typeof(BoolToDoubleConverter), 1.0d);

    public double TrueValue
    {
        get => (double)GetValue(TrueValueProperty);
        set => SetValue(TrueValueProperty, value);
    }

    public static readonly BindableProperty FalseValueProperty = BindableProperty.Create(
        nameof(FalseValue), typeof(double), typeof(BoolToDoubleConverter), 0.0d);

    public double FalseValue
    {
        get => (double)GetValue(FalseValueProperty);
        set => SetValue(FalseValueProperty, value);
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? TrueValue : FalseValue;
        }
        return FalseValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
