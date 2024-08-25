using standa_controller_software.command_manager;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace standa_controller_software.device_manager.controller_interfaces.positioning
{
    public class PositionerController_Virtual : BasePositionerController
    {
        //----------Virtual axes private data---------------
        private class DeviceInformation
        {
            private float _acceleration = 1000;
            private float _deceleration = 1000;
            private float _speed = 100;
            public float Acceleration
            {
                get { return _acceleration; }
                set { _acceleration = Math.Min(value, this.MaxAcceleration); ; }
            }
            public float Deceleration
            {
                get { return _deceleration; }
                set { _deceleration = Math.Min(value, this.MaxDeceleration); ; }
            }
            public float Speed
            {
                get { return _speed; }
                set { _speed = Math.Min(value, this.MaxSpeed); ; }
            }
            public float CurrentPosition { get; set; } = 0;
            public float CurrentSpeed { get; set; } = 0;
            public float MaxAcceleration { get; set; } = 10000;
            public float MaxDeceleration { get; set; } = 10000;
            public float MaxSpeed { get; set; } = 1000;
            public uint MoveStatus { get; set; } = 0;
        }
        private ConcurrentDictionary<char, DeviceInformation> _deviceInfo = new ConcurrentDictionary<char, DeviceInformation>();

        //---------------------------------------------------

        public PositionerController_Virtual(string name) : base(name) { }
        
        public override void AddDevice(BaseDevice device)
        {
            base.AddDevice(device);

            if (device is BasePositionerDevice positioningDevice)
            {
                _deviceInfo.TryAdd(positioningDevice.Name, new DeviceInformation());
            }
        }


        protected override Task UpdateMoveSettings(Command command, BasePositionerDevice device, CancellationToken cancellationToken, SemaphoreSlim semaphore)
        {
            float speedValue = (float)(command.Parameters[0]);
            float accelValue = (float)(command.Parameters[1]);
            float decelValue = (float)(command.Parameters[2]);
            
            device.Speed = speedValue;
            device.Acceleration = accelValue;
            device.Deceleration = decelValue;

            return Task.CompletedTask;
        }


        protected override Task WaitUntilStop(Command command, BasePositionerDevice device, CancellationToken cancellationToken, SemaphoreSlim semaphore)
        {
            return Task.CompletedTask;
        }


        public override Task UpdateStateAsync(ConcurrentQueue<string> log)
        {
            return Task.CompletedTask;
        }

        public override BaseController GetCopy()
        {
            var controller = new VirtualPositionerController(Name);
            foreach (var device in Devices)
            {
                controller.AddDevice(device.Value);
            }
            return controller;
        }

        protected override Task MoveAbsolute(Command command, BasePositionerDevice device, CancellationToken cancellationToken, SemaphoreSlim semaphore)
        {
            device.CurrentPosition = (float)command.Parameters[0];
            semaphore.Release();
            return Task.CompletedTask;
         }
    }
}
