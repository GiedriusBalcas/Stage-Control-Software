using standa_controller_software.device_manager.attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.devices.shutter
{
    public class ShutterDevice : BaseShutterDevice
    {
        
        //[DynamicPropertyAttribute]
        //[DisplayPropertyAttribute]
        //public int DelayOn { get; set; }
        //[DynamicPropertyAttribute]
        //[DisplayPropertyAttribute]
        //public int DelayOff { get; set; }
        ////public bool IsOn { get { } set { } }
        //private bool _isOn;


        //public bool IsOn
        //{
        //    get { return _isOn; }
        //    set { _isOn = value; StateChanged?.Invoke(); }
        //}


        //public event Action? StateChanged;

        public ShutterDevice(char name, string id) : base(name, id)
        {
        }

        public override BaseDevice GetCopy()
        {
            return new ShutterDevice(Name,ID) { DelayOff = this.DelayOff, DelayOn = this.DelayOn, IsOn = this.IsOn };
        }

    }
}
