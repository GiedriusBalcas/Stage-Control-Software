using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.devices
{
    public class LinearPositionerDevice : IPositionerDevice
    {
        private float _position =0f;
        private float _speed =0f;
        private string _deviceID;

        public string Name => _deviceID;


        public float Position { get => _position; set => _position = value; }
        public float Speed { get => _speed; set => _speed = value; }

        public LinearPositionerDevice(string deviceID)
        {
            this._deviceID = deviceID;
        }
    }
}
