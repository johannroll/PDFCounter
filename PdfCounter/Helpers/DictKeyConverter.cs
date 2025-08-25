using System;
using System.Collections.Generic;
using Avalonia.Data.Converters;
using System.Globalization;

public sealed class DictKeyConverter : IValueConverter
{
    private readonly string _key;
    public DictKeyConverter(string key) => _key = key;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is IDictionary<string, string> d && d.TryGetValue(_key, out var v))
            return v;
        return "";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is IDictionary<string, string> d)
            d[_key] = value?.ToString() ?? "";
        return value;
    }
}
