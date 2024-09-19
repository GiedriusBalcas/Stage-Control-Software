using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;
using standa_control_software_WPF.view_models.config_creation;
using standa_controller_software.device_manager.controller_interfaces;

namespace standa_control_software_WPF.views.helpers
{
    public class PropertyDataTemplateSelector : DataTemplateSelector
    {
        public DataTemplate StringTemplate { get; set; }
        public DataTemplate NumericTemplate { get; set; }
        public DataTemplate BooleanTemplate { get; set; }
        public DataTemplate CustomFieldTemplate { get; set; }
        public DataTemplate CharTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is PropertyDisplayItem propertyItem)
            {
                if (propertyItem.PropertyType == typeof(string))
                {
                    return StringTemplate;
                }
                else if (propertyItem.PropertyType == typeof(char))
                {
                    return CharTemplate;
                }
                else if (propertyItem.PropertyType == typeof(int) || propertyItem.PropertyType == typeof(double) || propertyItem.PropertyType == typeof(float))
                {
                    return NumericTemplate;
                }
                else if (propertyItem.PropertyType == typeof(bool))
                {
                    return BooleanTemplate;
                }
            }

            return CustomFieldTemplate;
        }
    }
}
