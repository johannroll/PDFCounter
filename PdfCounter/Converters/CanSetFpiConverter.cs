using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;
using PdfCounter.Models;
using PdfCounter.ViewModels;

namespace PdfCounter.Converters
{
    public sealed class CanSetFpiConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            var vm    = values.Count > 0 ? values[0] as MainWindowViewModel : null;
            var field = values.Count > 1 ? values[1] as ExtractField : null;

            if (vm is null || field is null)
                return false;

            bool isThisChecked = field.IsFirstPageIdentifier;
            bool anyOtherChecked = vm.Fields.Any(f => !ReferenceEquals(f, field) && f.IsFirstPageIdentifier);

            return isThisChecked || !anyOtherChecked;
        }
    }
}
