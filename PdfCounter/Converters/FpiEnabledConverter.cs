using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace PdfCounter.Converters
{
    public sealed class FpiEnabledConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type t, object? p, CultureInfo c)
        {
            bool thisChecked = values.Count > 0 && values[0] is bool b0 && b0;
            bool anyChecked = values.Count > 1 && values[1] is bool b1 && b1;
            return thisChecked || !anyChecked;
        }
    }
}