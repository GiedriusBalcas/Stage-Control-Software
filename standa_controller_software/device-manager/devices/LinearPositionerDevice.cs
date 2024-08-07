using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.devices
{
    public class LinearPositionerDevice : IPositionerDevice
    {
        private int _position =0;
        private string _deviceID;

        public string DeviceId => _deviceID;


        public int Position { get => _position; set => _position = value; }


        public LinearPositionerDevice(string deviceID)
        {
            this._deviceID = deviceID;
        }
    }
}
