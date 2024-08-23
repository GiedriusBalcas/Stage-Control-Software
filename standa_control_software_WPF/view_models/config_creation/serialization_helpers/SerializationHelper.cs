using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_control_software_WPF.view_models.config_creation.serialization_helpers
{
    public static class SerializationHelper
    {
        public static ConfigurationSer CreateSeriazableObject(ConfigurationViewModel Configuration)
        {
            
            var config = Configuration;
                
            var configSer = new ConfigurationSer()
            {
                Name = config.Name,
                XToolPositionDependancy = config.XToolDependancy,
                YToolPositionDependancy = config.YToolDependancy,
                ZToolPositionDependancy = config.ZToolDependancy,
            };

            foreach (var controller in config.Controllers)
            {
                var controllerSer = new ControllerSer()
                {
                    Name = controller.Name,
                    SelectedControllerType = controller.SelectedControllerType
                };
                foreach (var prop in controller.ControllerProperties)
                {
                    controllerSer.ControllerProperties.Add(new PropertyDisplayItemSer()
                    {
                        PropertyName = prop.PropertyName,
                        PropertyValue = prop.PropertyValue
                    });
                }

                foreach (var device in controller.Devices)
                {
                    var deviceSer = new DeviceSer()
                    {
                        Name = device.Name,
                        SelectedDeviceType = device.SelectedDeviceType
                    };
                    foreach (var prop in device.DeviceProperties)
                    {
                        deviceSer.DeviceProperties.Add(new PropertyDisplayItemSer()
                        {
                            PropertyName = prop.PropertyName,
                            PropertyValue = prop.PropertyValue
                        });
                    }
                    controllerSer.Devices.Add(deviceSer);
                }
                configSer.Controllers.Add(controllerSer);
            }
            return configSer;
        }

        public static ConfigurationViewModel DeserializeObject(ConfigurationSer configSer, ConfigurationCreationViewModel Configuration)
        {
            var config = new ConfigurationViewModel(Configuration)
            {
                Name = configSer.Name,
                XToolDependancy = configSer.XToolPositionDependancy,
                YToolDependancy = configSer.YToolPositionDependancy,
                ZToolDependancy = configSer.ZToolPositionDependancy,
            };

            foreach (var controllerSer in configSer.Controllers)
            {
                var controller = new ControllerConfigViewModel(config)
                {
                    Name = controllerSer.Name,
                    SelectedControllerType = controllerSer.SelectedControllerType
                };
                foreach (var propSer in controllerSer.ControllerProperties)
                {
                    controller.UpdatePropertyValue(propSer.PropertyName, propSer.PropertyValue);
                }

                foreach (var deviceSer in controllerSer.Devices)
                {
                    var device = new DeviceConfigViewModel(controller)
                    {
                        Name = deviceSer.Name,
                        SelectedDeviceType = deviceSer.SelectedDeviceType
                    };
                    foreach (var propSer in deviceSer.DeviceProperties)
                    {
                        device.UpdatePropertyValue(propSer.PropertyName, propSer.PropertyValue);
                    }
                    controller.Devices.Add(device);
                }

                config.Controllers.Add(controller);
            }
            
            return config;
        }

    }
}
