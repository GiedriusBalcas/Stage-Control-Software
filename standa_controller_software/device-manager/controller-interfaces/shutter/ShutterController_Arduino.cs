using Antlr4.Runtime;
using standa_controller_software.command_manager;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Concurrent;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.controller_interfaces.shutter
{
    public class ShutterController_Arduino : BaseShutterController
    {
        private class DeviceInformation
        {
            public bool IsOn = false;
            public int DelayOn = 1000;
            public int DelayOff = 100;
            public SerialPort SerialPort;
        }

        private readonly ConcurrentDictionary<char, DeviceInformation> _deviceInfo = new();

        public ShutterController_Arduino(string name) : base(name)
        {
            
        }
        public override void AddDevice(BaseDevice device)
        {
            base.AddDevice(device);
            if (device is BaseShutterDevice shutterDevice)
            {
                var port = new SerialPort
                {
                    PortName = "COM14",  // Replace with your Arduino's COM port
                    BaudRate = 115200,    // Ensure this matches the baud rate set in Arduino
                    Parity = Parity.None,
                    DataBits = 8,
                    StopBits = StopBits.One,
                    Handshake = Handshake.None,
                    RtsEnable = true
                };

                _deviceInfo.TryAdd(shutterDevice.Name, new DeviceInformation
                {
                    DelayOff = shutterDevice.DelayOff,
                    DelayOn = shutterDevice.DelayOn,
                    SerialPort = port
                });
                _deviceInfo[shutterDevice.Name].SerialPort.Open();
            }
        }

        public override void ConnectDevice(BaseDevice device, SemaphoreSlim semaphore)
        {
            if (device is BaseShutterDevice shutterDevice && _deviceInfo.TryGetValue(shutterDevice.Name, out var deviceInformation))
            {
                var port = new SerialPort
                {
                    PortName = device.ID,  // Replace with your Arduino's COM port
                    BaudRate = 115200,    // Ensure this matches the baud rate set in Arduino
                    Parity = Parity.None,
                    DataBits = 8,
                    StopBits = StopBits.One,
                    Handshake = Handshake.None,
                    RtsEnable = true
                };

                deviceInformation.SerialPort = port;
                deviceInformation.DelayOn = shutterDevice.DelayOn;
                deviceInformation.DelayOff = shutterDevice.DelayOff;
                deviceInformation.SerialPort.Open();

            }
            base.ConnectDevice(device, semaphore);
        }

        public override BaseController GetCopy()
        {
            var controller = new ShutterController_Virtual(Name);
            foreach (var device in Devices)
            {
                controller.AddDevice(device.Value.GetCopy());
            }

            return controller;
        }

        protected override async Task SetDelayAsync(Command command, BaseShutterDevice device, CancellationToken token)
        {
            var delayOn = (uint)command.Parameters[0];
            var delayOff = (uint)command.Parameters[1];

            byte[] callCommand = { 0x01 }; // SET_DELAY command
            byte[] delayOnBytes = BitConverter.GetBytes(delayOn);
            byte[] delayOffBytes = BitConverter.GetBytes(delayOff);

            var serialPort = _deviceInfo[device.Name].SerialPort;
            await serialPort.BaseStream.WriteAsync(callCommand, 0, callCommand.Length);
            await serialPort.BaseStream.WriteAsync(delayOnBytes, 0, delayOnBytes.Length);
            await serialPort.BaseStream.WriteAsync(delayOffBytes, 0, delayOffBytes.Length);
        }

        public override async Task UpdateStateAsync(ConcurrentQueue<string> log)
        {
            foreach (var device in Devices)
            {
                //byte[] command = { 0x04 }; // SHUTTER_ON command
                //var serialPort = _deviceInfo[device.Key].SerialPort;

                //await serialPort.BaseStream.WriteAsync(command, 0, command.Length);
                //await serialPort.BaseStream.FlushAsync(); // Ensure all data is sent

                //// Read acknowledgment from Arduino
                //string ack = await ReadFromSerialAsync(serialPort, new CancellationToken());

                //var state = ack == "1" ? true : false;

                //device.Value.IsOn = state;
                //log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: Updated state for device {device.Value.Name}, State: {device.Value.IsOn}");
            }
        }

        protected override async Task ChangeState(Command command, BaseShutterDevice device, CancellationToken token)
        {
            var state = (bool)command.Parameters[0];

            if (state)
            {
                await ShutterOnAsync(device, token);
            }
            else
            {
                await ShutterOffAsync(device, token);
            }
        }

        private async Task ShutterOnAsync(BaseShutterDevice device, CancellationToken token)
        {
            byte[] command = { 0x02 }; // SHUTTER_ON command
            var serialPort = _deviceInfo[device.Name].SerialPort;

            await serialPort.BaseStream.WriteAsync(command, 0, command.Length);
            await serialPort.BaseStream.FlushAsync(); // Ensure all data is sent

            // Read acknowledgment from Arduino
            //string ackOn = await ReadFromSerialAsync(serialPort, token);
            //Console.WriteLine($"ShutterOn Ack: {ackOn}");

            //if (ack != "O") // Expecting 'O' for SHUTTER_ON acknowledgment
            //{
            //    throw new InvalidOperationException($"Unexpected acknowledgment: {ack}");
            //}
        }

        private async Task ShutterOffAsync(BaseShutterDevice device, CancellationToken token)
        {
            byte[] command = { 0x03 }; // SHUTTER_OFF command
            var serialPort = _deviceInfo[device.Name].SerialPort;

            await serialPort.BaseStream.WriteAsync(command, 0, command.Length);
            await serialPort.BaseStream.FlushAsync(); // Ensure all data is sent

            // Read acknowledgment from Arduino
            //string ackFF = await ReadFromSerialAsync(serialPort, token);
            //Console.WriteLine($"ShutterOff Ack: {ackFF}");
        }

        protected override async Task ChangeStateOnInterval(Command command, BaseShutterDevice device, CancellationToken token)
        {
            //uint duration = (uint)((float)command.Parameters[0] * 1000000);

            //// Create a byte array containing the command and the encoded duration
            //byte[] commandBytes = new byte[5]; // 1 byte for command + 4 bytes for duration
            //commandBytes[0] = 0x05; // SHUTTER_DURATION command
            //Array.Copy(BitConverter.GetBytes(duration), 0, commandBytes, 1, 4);

            //var serialPort = _deviceInfo[device.Name].SerialPort;
            //await serialPort.BaseStream.WriteAsync(commandBytes, 0, commandBytes.Length);

            //// Optionally, handle Arduino responses if necessary
        }

        private async Task<string> ReadFromSerialAsync(SerialPort serialPort, CancellationToken token)
        {
            var buffer = new byte[1];
            var builder = new StringBuilder();

            while (!token.IsCancellationRequested)
            {
                if (serialPort.BytesToRead > 0)
                {
                    int bytesRead = await serialPort.BaseStream.ReadAsync(buffer, 0, 1, token);
                    if (bytesRead > 0)
                    {
                        builder.Append((char)buffer[0]);

                        // Assuming acknowledgment is a single character
                        if (builder.Length > 0)
                        {
                            break;
                        }
                    }
                }
                //await Task.Delay(10); // Small delay to avoid busy waiting
            }

            return builder.ToString().Trim();
        }

       
    }
}
