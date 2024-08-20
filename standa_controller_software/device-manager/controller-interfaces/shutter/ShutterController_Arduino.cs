using Antlr4.Runtime;
using standa_controller_software.command_manager;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;


namespace standa_controller_software.device_manager.controller_interfaces.shutter
{
    public class ShutterController_Arduino : BaseShutterController
    {

        private class DeviceInformation
        {
            public bool _isOn = false;
            public int _delayOn = 1000;
            public int _delayOff = 100;
            public SerialPort _serialPort;
        }
        private ConcurrentDictionary<string, DeviceInformation> _deviceInfo = new ConcurrentDictionary<string, DeviceInformation>();

        public ShutterController_Arduino(string name) : base(name)
        {
        }


        public override void AddDevice(IDevice device)
        {
            base.AddDevice(device);
            if (device is IShutterDevice shuttterDevice)
            {

                var port = new SerialPort();
                port.PortName = "COM14";  // Replace with your Arduino's COM port
                port.BaudRate = 115200;  // Ensure this matches the baud rate set in Arduino
                port.Parity = Parity.None;
                port.DataBits = 8;
                port.StopBits = StopBits.One;
                port.Handshake = Handshake.None;
                port.RtsEnable = true;

                _deviceInfo.TryAdd(shuttterDevice.Name, new DeviceInformation()
                {
                    _delayOff = shuttterDevice.DelayOff,
                    _delayOn = shuttterDevice.DelayOn,
                    _serialPort = port
                });
                _deviceInfo[shuttterDevice.Name]._serialPort.Open();
            }
        }
        public override IController GetCopy()
        {
            var controller = new ShutterController_Virtual(Name);
            foreach (var device in Devices)
            {
                controller.AddDevice(device.Value.GetCopy());
            }

            return controller;
        }
        protected override async Task SetDelayAsync(Command command, IShutterDevice device, CancellationToken token)
        {
            var delayOn = (uint)command.Parameters[0];
            var delayOff = (uint)command.Parameters[1];

            byte[] Callcommand = new byte[] { 0x01 }; // SET_DELAY command
            byte[] delayOnBytes = BitConverter.GetBytes(delayOn);
            byte[] delayOffBytes = BitConverter.GetBytes(delayOff);

            await _deviceInfo[device.Name]._serialPort.BaseStream.WriteAsync(Callcommand, 0, Callcommand.Length);
            await _deviceInfo[device.Name]._serialPort.BaseStream.WriteAsync(delayOnBytes, 0, delayOnBytes.Length);
            await _deviceInfo[device.Name]._serialPort.BaseStream.WriteAsync(delayOffBytes, 0, delayOffBytes.Length);

        }
        public override async Task UpdateStateAsync(ConcurrentQueue<string> log)
        {
            foreach (var device in Devices)
            {
                byte[] command = new byte[] { 0x04 }; // GET_STATE command
                await _deviceInfo[device.Key]._serialPort.BaseStream.WriteAsync(command, 0, command.Length);

                // Give Arduino a bit of time to respond
                //await Task.Delay(100);

                bool state = false;

                try
                {
                    string response = await ReadFromSerialAsync(_deviceInfo[device.Key]._serialPort, new CancellationToken());
                    state = response == "1" ? true : false;
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Operation was canceled.");
                }
                device.Value.IsOn = state;
                log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: Updated state for device {device.Value.Name}, State: {device.Value.IsOn}");
            }
        }


        protected override async Task ChangeState(Command command, IShutterDevice device, CancellationToken token)
        {
            var state = (bool)command.Parameters[0];

            if (state)
                await ShutterOnAsync(device, token);
            else
                await ShutterOffAsync(device, token);

        }


        private async Task ShutterOnAsync(IShutterDevice device, CancellationToken token)
        {
            byte[] command = new byte[] { 0x02 }; // SHUTTER_ON command
            await _deviceInfo[device.Name]._serialPort.BaseStream.WriteAsync(command, 0, command.Length);

        }

        private async Task ShutterOffAsync(IShutterDevice device, CancellationToken token)
        {
            byte[] command = new byte[] { 0x03 }; // SHUTTER_OFF command
            await _deviceInfo[device.Name]._serialPort.BaseStream.WriteAsync(command, 0, command.Length);
        }

        protected override async Task ChangeStateOnInterval(Command command, IShutterDevice device, CancellationToken token)
        {
            uint duration = (uint)((float)command.Parameters[0] * 1000000);

            // Create a byte array containing the command and the encoded duration
            byte[] commandBytes = new byte[5]; // 1 byte for command + 4 bytes for duration
            commandBytes[0] = 0x05; // SHUTTER_DURATION command
            Array.Copy(BitConverter.GetBytes(duration), 0, commandBytes, 1, 4);

            Console.WriteLine($"Duration being sent: {duration}");
            Console.WriteLine("Bytes being sent:");
            foreach (byte b in commandBytes)
            {
                Console.Write($"{b:X2} ");
            }
            Console.WriteLine();
            
            
            // Send the command to the Arduino
            await _deviceInfo[device.Name]._serialPort.BaseStream.WriteAsync(commandBytes, 0, commandBytes.Length);

            //// Listen for the response from Arduino
            //try
            //{
            //    string response = await ReadFromSerialAsync(_deviceInfo[device.Name]._serialPort, token);
            //    Console.WriteLine($"Arduino response: {response}");
            //}
            //catch (OperationCanceledException)
            //{
            //    Console.WriteLine("Operation was canceled.");
            //}

            /// Might be that arduino can only handle one task at a time. So we need a semaphore handling better than now.
            /// Currently the semaphores are not being treated by the UpdateStatesAsync calls, cause of the WaitUntilStop method.
            /// Might have to release and start the semaphores inside of my controller classes.
            /// Which is quite honestly favorable in most cases.
            /// This way I can control the logic with different kind of controllers.
            /// Also, I need a way to not await in UpdateState when semaphore is found locked and just skip that controller entirely.
        }

        private async Task<string> ReadFromSerialAsync(SerialPort serialPort, CancellationToken token)
        {
            var buffer = new byte[256];
            var builder = new StringBuilder();

            while (!token.IsCancellationRequested)
            {
                if (serialPort.BytesToRead > 0)
                {
                    int bytesRead = await serialPort.BaseStream.ReadAsync(buffer, 0, buffer.Length, token);
                    builder.Append(serialPort.Encoding.GetString(buffer, 0, bytesRead));

                    // If response is expected to end with a newline or specific character, check for it here
                    if (builder.ToString().Contains("\n")) // Assuming response ends with newline
                    {
                        break;
                    }
                }
                await Task.Delay(10); // Small delay to avoid busy waiting
            }

            return builder.ToString().Trim(); // Trim any extra whitespace/newlines
        }

    }
}
