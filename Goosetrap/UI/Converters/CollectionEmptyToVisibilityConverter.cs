using System;
using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Goosetrap.UI.Converters
{
    public class CollectionEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isEmpty = true;
            if (value is ICollection collection)
            {
                isEmpty = collection.Count == 0;
            }
            else if (value is int intVal)
            {
                isEmpty = intVal == 0;
            }

            bool invert = parameter != null && parameter.ToString() == "Invert";
            if (invert)
            {
                return isEmpty ? Visibility.Collapsed : Visibility.Visible;
            }
            else
            {
                return isEmpty ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
