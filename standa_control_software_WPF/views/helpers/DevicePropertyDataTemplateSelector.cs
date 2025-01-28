using System;
using System.Windows;
using System.Windows.Controls;
using standa_control_software_WPF.view_models.config_creation.system_properties;

namespace standa_control_software_WPF.views.helpers
{
    public class DevicePropertyDataTemplateSelector : DataTemplateSelector
    {
        // Make DataTemplate properties nullable
        public DataTemplate? StringTemplate { get; set; }
        public DataTemplate? NumericTemplate { get; set; }
        public DataTemplate? BooleanTemplate { get; set; }
        public DataTemplate? CustomFieldTemplate { get; set; }
        public DataTemplate? CharTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is DevicePropertyDisplayItem propertyItem)
            {
                if (propertyItem.PropertyType == typeof(string))
                {
                    if (StringTemplate != null)
                        return StringTemplate;
                }
                else if (propertyItem.PropertyType == typeof(char))
                {
                    if (CharTemplate != null)
                        return CharTemplate;
                }
                else if (propertyItem.PropertyType == typeof(int) ||
                         propertyItem.PropertyType == typeof(double) ||
                         propertyItem.PropertyType == typeof(float))
                {
                    if (NumericTemplate != null)
                        return NumericTemplate;
                }
                else if (propertyItem.PropertyType == typeof(bool))
                {
                    if (BooleanTemplate != null)
                        return BooleanTemplate;
                }

                // Fallback to CustomFieldTemplate if set
                if (CustomFieldTemplate != null)
                    return CustomFieldTemplate;
            }

            // Optionally, return the base implementation or throw an exception
            return base.SelectTemplate(item, container);
        }
    }
}
