using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace standa_control_software_WPF.views.helpers
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public bool Inverse { get; set; } // Optional, in case you need to invert the logic

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool boolValue = value is bool && (bool)value;
            if (Inverse)
                boolValue = !boolValue;

            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Visibility visibility = (Visibility)value;
            bool result = visibility == Visibility.Visible;
            return Inverse ? !result : result;
        }
    }
}
