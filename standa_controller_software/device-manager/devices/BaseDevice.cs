using standa_controller_software.device_manager.attributes;
using standa_controller_software.device_manager.controller_interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.devices
{
    public abstract class BaseDevice
    {
        [DisplayPropertyAttribute]
        public char Name { get; }
        [DisplayPropertyAttribute]
        public string ID { get; }

        public event EventHandler ConnectionStateChanged;
        private bool _isConnected = false;
        public bool IsConnected
        {
            get { return _isConnected; }
            set { _isConnected = value;  ConnectionStateChanged?.Invoke(this, EventArgs.Empty); }
        }
        public abstract BaseDevice GetCopy();
        public BaseDevice(char name, string id)
        {
            Name = name;
            ID = id;
        }

    }
}
