using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace standa_control_software_WPF.views.helpers
{
    public class TwoDecimalConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is float positionValue)
            {
                // Format the value with two decimal places
                return positionValue.ToString("F2", CultureInfo.InvariantCulture);
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Handle conversion back if necessary, typically for editing purposes
            if (float.TryParse(value.ToString(), out float result))
            {
                return result;
            }
            return value;
        }
    }
}
