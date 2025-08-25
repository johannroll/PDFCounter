using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace PdfCounter.Converters;
    public class EmptyStringToBoolConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            // values[0] = TextBlock.Text
            // values[1] = DataGridRow.Index
            var text = values.Count > 0 ? values[0] as string : null;

            int rowIndex = -1;
            if (values.Count > 1)
            {
                if (values[1] is int i) rowIndex = i;
                else if (values[1] is IConvertible c) rowIndex = c.ToInt32(CultureInfo.InvariantCulture);
            }

            // highlight only if NOT first row and text is null/whitespace
            return rowIndex > 0 && string.IsNullOrWhiteSpace(text);
        }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    public object ConvertBack(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();

}


