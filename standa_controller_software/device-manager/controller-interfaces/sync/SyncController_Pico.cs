using standa_controller_software.device_manager.attributes;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.controller_interfaces.sync
{
    public class SyncController_Pico : BaseSyncController
    {
        public struct ExecutionInformation
        {
            public char[] Devices;
            public bool Launch;
            public float Rethrow;
            public bool Shutter;
            public float Shutter_delay_on;
            public float Shutter_delay_off;
        }

        // Define constants
        const byte START_BYTE = 0xAA; // Command start byte
        const byte RESPONSE_START_BYTE = 0xAB; // Response start byte

        // Command IDs
        const byte CMD_ADD_BUFFER_ITEM = 0x01;
        const byte CMD_CLEAR_BUFFER = 0x02;
        const byte CMD_BUFFER_ITEM_COUNT = 0x03;
        const byte CMD_START_EXECUTION = 0x04;

        // Response Codes
        const byte RESP_CMD_SUCCESS = 0x01;
        const byte RESP_CMD_ERROR = 0x02;
        const byte RESP_BUFFER_ITEM_COUNT = 0x04;

        const byte BUFFER_STATUS_FREE_SPACE = 0x10;
        const byte BUFFER_STATUS_LAST_ITEM = 0x11;

        const byte EXECUTION_STATUS_END = 0x20;

        [DisplayPropertyAttribute]
        public char FirstDevice { get; set; }

        [DisplayPropertyAttribute]
        public char SecondDevice { get; set; }

        [DisplayPropertyAttribute]
        public char ThirdDevice { get; set; }

        [DisplayPropertyAttribute]
        public char FourthDevice { get; set; }

        private SerialPort serialPort;
        private Dictionary<char, byte> _deviceToPinMap = new Dictionary<char, byte>();

        private readonly SemaphoreSlim commandSemaphore = new SemaphoreSlim(1, 1);

        private TaskCompletionSource<bool> commandResponseTcs;
        private TaskCompletionSource<int> bufferItemCountTcs;
        private readonly object responseLock = new object();

        // Events to notify subscribers
        public event Action ExecutionCompleted;
        public event Action BufferHasFreeSpace;
        public event Action LastBufferItemTaken;

        // Buffer for incoming data
        private readonly ConcurrentQueue<byte> dataQueue = new ConcurrentQueue<byte>();
        private CancellationTokenSource readCancellationTokenSource;

        public SyncController_Pico(string name) : base(name)
        {
        }

        public override Task InitializeController(SemaphoreSlim semaphore, ConcurrentQueue<string> log)
        {
            base.InitializeController(semaphore, log);

            _deviceToPinMap[FirstDevice] = 0x06;
            _deviceToPinMap[SecondDevice] = 0x07;
            _deviceToPinMap[ThirdDevice] = 0x08;
            _deviceToPinMap[FourthDevice] = 0x09;

            // Initialize the serial port
            serialPort = new SerialPort(this.ID, 115200, Parity.None, 8, StopBits.One);
            serialPort.DtrEnable = true;
            serialPort.RtsEnable = true;
            serialPort.Open();

            // Start asynchronous reading
            readCancellationTokenSource = new CancellationTokenSource();
            Task.Run(() => ReadSerialDataAsync(readCancellationTokenSource.Token));

            return Task.CompletedTask;
        }

        // Method to add buffer item
        public async Task AddBufferItem(char[] Devices, bool Launch, float Rethrow, bool Shutter, float Shutter_delay_on, float Shutter_delay_off)
        {
            //_log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: --AddBuffer--");

            await commandSemaphore.WaitAsync();

            try
            {
                var executionInfo = new ExecutionInformation
                {
                    Devices = Devices,
                    Launch = Launch,
                    Rethrow = Rethrow,
                    Shutter = Shutter,
                    Shutter_delay_off = Shutter_delay_off,
                    Shutter_delay_on = Shutter_delay_on
                };

                // Construct the packet
                byte[] packet = ConstructAddBufferItemPacket(executionInfo);

                // Log the packet being sent
                //_log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: Sending AddBufferItem command: {BitConverter.ToString(packet)}");

                // Initialize the TaskCompletionSource
                var tcs = new TaskCompletionSource<bool>();

                lock (responseLock)
                {
                    if (commandResponseTcs != null)
                    {
                        throw new InvalidOperationException("Another command is already being processed.");
                    }
                    commandResponseTcs = tcs;
                }

                // Send the packet
                if (!serialPort.IsOpen)
                {
                    throw new InvalidOperationException("Serial port is closed.");
                }
                serialPort.Write(packet, 0, packet.Length);
                //_log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: --AddBuffer--serial wrote--");

                // Wait for the response with timeout
                bool success = await WaitForResponseAsync(tcs.Task, timeoutMilliseconds: 5000);

                //_log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: --AddBuffer--awaited response--");

                if (!success)
                {
                    throw new Exception("Error executing ADD_BUFFER_ITEM command.");
                }
            }
            catch (Exception ex)
            {
                //_log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: Exception in AddBufferItem: {ex.Message}");
                throw;
            }
            finally
            {
                lock (responseLock)
                {
                    commandResponseTcs = null;
                }
                commandSemaphore.Release();
            }
        }

        // ClearBuffer method
        public async Task ClearBuffer()
        {
            //_log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: --ClearBuffer--");

            await commandSemaphore.WaitAsync();

            try
            {
                // Construct the packet
                byte[] packet = ConstructSimpleCommandPacket(CMD_CLEAR_BUFFER);

                // Log the packet being sent
                //_log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: Sending ClearBuffer command: {BitConverter.ToString(packet)}");

                // Initialize the TaskCompletionSource
                var tcs = new TaskCompletionSource<bool>();

                lock (responseLock)
                {
                    if (commandResponseTcs != null)
                    {
                        throw new InvalidOperationException("Another command is already being processed.");
                    }
                    commandResponseTcs = tcs;
                }

                // Send the packet
                if (!serialPort.IsOpen)
                {
                    throw new InvalidOperationException("Serial port is closed.");
                }
                serialPort.Write(packet, 0, packet.Length);

                // Wait for the response with timeout
                bool success = await WaitForResponseAsync(tcs.Task, timeoutMilliseconds: 5000);

                if (!success)
                {
                    throw new Exception("Error executing CLEAR_BUFFER command.");
                }
            }
            catch (TimeoutException ex)
            {
                //_log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: Timeout in ClearBuffer: {ex.Message}");
                // Optionally, decide not to rethrow the exception
            }
            catch (Exception ex)
            {
                //_log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: Exception in ClearBuffer: {ex.Message}");
                // Optionally, rethrow or handle the exception
            }
            finally
            {
                lock (responseLock)
                {
                    commandResponseTcs = null;
                }
                commandSemaphore.Release();
            }
        }

        // GetBufferItemCount method
        public async Task<int> GetBufferItemCount()
        {
            //_log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: --GetBufferCount--");

            await commandSemaphore.WaitAsync();

            try
            {
                // Construct the packet
                byte[] packet = ConstructSimpleCommandPacket(CMD_BUFFER_ITEM_COUNT);

                // Log the packet being sent

                // Initialize the TaskCompletionSource
                var tcs = new TaskCompletionSource<int>();

                lock (responseLock)
                {
                    if (bufferItemCountTcs != null)
                    {
                        throw new InvalidOperationException("Another command is already being processed.");
                    }
                    bufferItemCountTcs = tcs;
                }

                // Send the packet
                if (!serialPort.IsOpen)
                {
                    throw new InvalidOperationException("Serial port is closed.");
                }
                serialPort.Write(packet, 0, packet.Length);
                //_log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: Sending GetBufferItemCount command: {BitConverter.ToString(packet)}");

                // Wait for the response with timeout
                int count = await WaitForResponseAsync(tcs.Task, timeoutMilliseconds: 5000);

                return 254 - count;
            }
            catch (Exception ex)
            {
                //_log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: Exception in GetBufferItemCount: {ex.Message}");
                throw;
            }
            finally
            {
                lock (responseLock)
                {
                    bufferItemCountTcs = null;
                }
                commandSemaphore.Release();
            }
        }

        // StartExecution method
        public async Task StartExecution()
        {
            //_log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: --StartExec--");

            await commandSemaphore.WaitAsync();

            try
            {
                // Construct the packet
                byte[] packet = ConstructSimpleCommandPacket(CMD_START_EXECUTION);

                // Log the packet being sent
                //_log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: Sending StartExecution command: {BitConverter.ToString(packet)}");

                // Initialize the TaskCompletionSource
                var tcs = new TaskCompletionSource<bool>();

                lock (responseLock)
                {
                    if (commandResponseTcs != null)
                    {
                        throw new InvalidOperationException("Another command is already being processed.");
                    }
                    commandResponseTcs = tcs;
                }

                // Send the packet
                if (!serialPort.IsOpen)
                {
                    throw new InvalidOperationException("Serial port is closed.");
                }
                serialPort.Write(packet, 0, packet.Length);

                // Wait for the response with timeout
                bool success = await WaitForResponseAsync(tcs.Task, timeoutMilliseconds: 5000);

                if (!success)
                {
                    throw new Exception("Error executing START_EXECUTION command.");
                }
            }
            catch (Exception ex)
            {
                //_log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: Exception in StartExecution: {ex.Message}");
                throw;
            }
            finally
            {
                lock (responseLock)
                {
                    commandResponseTcs = null;
                }
                commandSemaphore.Release();
            }
        }

        // Helper method to construct simple command packets
        private byte[] ConstructSimpleCommandPacket(byte commandId)
        {
            byte payloadLength = 0x00;

            // Compute checksum
            byte checksum = (byte)(commandId ^ payloadLength);

            // Construct the packet
            byte[] packet = new byte[4]; // Start Byte + Command ID + Payload Length + Checksum
            int index = 0;
            packet[index++] = START_BYTE;
            packet[index++] = commandId;
            packet[index++] = payloadLength;
            packet[index++] = checksum;

            return packet;
        }

        // Helper method to construct the packet for AddBufferItem
        private byte[] ConstructAddBufferItemPacket(ExecutionInformation executionInfo)
        {
            const byte PAYLOAD_LENGTH = 9;

            byte launch = (byte)(executionInfo.Launch ? 0x01 : 0x00);

            // Map Devices to LEDpins
            byte[] ledPins = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                if (i < executionInfo.Devices.Length)
                {
                    char device = executionInfo.Devices[i];
                    if (_deviceToPinMap.TryGetValue(device, out byte pin))
                    {
                        ledPins[i] = pin;
                    }
                    else
                    {
                        throw new ArgumentException($"Unknown device '{device}'");
                    }
                }
                else
                {
                    ledPins[i] = 0xFF; // Unused
                }
            }

            // Convert Rethrow to bytes (float)
            byte[] rethrowBytes = BitConverter.GetBytes(executionInfo.Rethrow);

            // Construct the payload
            byte[] payload = new byte[PAYLOAD_LENGTH];
            payload[0] = launch;
            Array.Copy(ledPins, 0, payload, 1, 4);
            Array.Copy(rethrowBytes, 0, payload, 5, 4);

            // Compute checksum
            byte checksum = CMD_ADD_BUFFER_ITEM ^ PAYLOAD_LENGTH;
            foreach (byte b in payload)
            {
                checksum ^= b;
            }

            // Construct the packet
            byte[] packet = new byte[1 + 1 + 1 + PAYLOAD_LENGTH + 1];
            int index = 0;
            packet[index++] = START_BYTE;
            packet[index++] = CMD_ADD_BUFFER_ITEM;
            packet[index++] = PAYLOAD_LENGTH;
            Array.Copy(payload, 0, packet, index, PAYLOAD_LENGTH);
            index += PAYLOAD_LENGTH;
            packet[index++] = checksum;

            return packet;
        }

        // Asynchronous method to read serial data
        private async Task ReadSerialDataAsync(CancellationToken cancellationToken)
        {
            try
            {
                byte[] buffer = new byte[1024];
                while (!cancellationToken.IsCancellationRequested)
                {
                    int bytesRead = await serialPort.BaseStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (bytesRead > 0)
                    {
                        //_log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: Bytes to process encountered.");

                        // Enqueue received bytes
                        for (int i = 0; i < bytesRead; i++)
                        {
                            dataQueue.Enqueue(buffer[i]);
                        }

                        // Process received data
                        ProcessReceivedData();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Reading was canceled
            }
            catch (Exception ex)
            {
                //_log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: Exception in ReadSerialDataAsync: {ex.Message}");
            }
        }

        // Method to process received data
        private void ProcessReceivedData()
        {
            while (dataQueue.TryPeek(out byte b))
            {
                if (b == RESPONSE_START_BYTE)
                {
                    // Try to parse a protocol message
                    if (TryParseProtocolMessage(out int responseCode, out byte[] payload))
                    {
                        HandleResponse(responseCode, payload);
                    }
                    else
                    {
                        // Not enough data yet
                        break;
                    }
                }
                else
                {
                    // It's text data (debug messages)
                    StringBuilder sb = new StringBuilder();
                    while (dataQueue.TryDequeue(out byte textByte))
                    {
                        if (textByte == RESPONSE_START_BYTE)
                        {
                            // Put it back for the next iteration
                            dataQueue.Enqueue(textByte);
                            break;
                        }
                        else
                        {
                            sb.Append((char)textByte);
                        }
                    }
                    string text = sb.ToString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        //_log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: Received Text: {text}");
                    }
                }
            }
        }

        // Method to parse protocol messages
        private bool TryParseProtocolMessage(out int responseCode, out byte[] payload)
        {
            responseCode = 0;
            payload = null;

            byte[] header = new byte[3]; // Start Byte + Response Code + Payload Length

            if (dataQueue.Count < header.Length)
            {
                return false; // Not enough data
            }

            // Peek header bytes
            byte[] headerBytes = dataQueue.Take(header.Length).ToArray();

            if (headerBytes[0] != RESPONSE_START_BYTE)
            {
                // Invalid start byte, dequeue and discard
                dataQueue.TryDequeue(out _);
                return false;
            }

            responseCode = headerBytes[1];
            byte payloadLength = headerBytes[2];
            int totalMessageLength = 1 + 1 + 1 + payloadLength + 1;

            if (dataQueue.Count < totalMessageLength)
            {
                return false; // Not enough data
            }

            // Dequeue the full message
            byte[] messageBytes = new byte[totalMessageLength];
            for (int i = 0; i < totalMessageLength; i++)
            {
                dataQueue.TryDequeue(out messageBytes[i]);
            }

            // Validate checksum
            byte calculatedChecksum = (byte)(responseCode ^ payloadLength);
            for (int i = 3; i < 3 + payloadLength; i++)
            {
                calculatedChecksum ^= messageBytes[i];
            }

            byte receivedChecksum = messageBytes[totalMessageLength - 1];

            if (calculatedChecksum != receivedChecksum)
            {
                //_log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: Checksum mismatch in response.");
                return false;
            }
            else
            {
                //_log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: Response parsed successfully. Response Code: {responseCode}");
            }

            // Extract payload
            payload = new byte[payloadLength];
            if (payloadLength > 0)
            {
                Array.Copy(messageBytes, 3, payload, 0, payloadLength);
            }

            return true;
        }

        // Method to handle responses
        private void HandleResponse(int responseCode, byte[] payload)
        {
            switch (responseCode)
            {
                case RESP_CMD_SUCCESS:
                    {
                        TaskCompletionSource<bool> tcs = null;
                        lock (responseLock)
                        {
                            tcs = commandResponseTcs;
                            commandResponseTcs = null;
                        }

                        if (tcs != null)
                        {
                            //_log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: --response: success: true --");
                            tcs.SetResult(true);
                        }
                        else
                        {
                            //_log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: Received success response with no pending command.");
                        }
                    }
                    break;

                case RESP_CMD_ERROR:
                    {
                        TaskCompletionSource<bool> tcs = null;
                        lock (responseLock)
                        {
                            tcs = commandResponseTcs;
                            commandResponseTcs = null;
                        }

                        if (tcs != null)
                        {
                            //_log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: --response: success: false --");
                            tcs.SetResult(false);
                        }
                        else
                        {
                            //_log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: Received error response with no pending command.");
                        }
                    }
                    break;

                case RESP_BUFFER_ITEM_COUNT:
                    {
                        TaskCompletionSource<int> countTcs = null;
                        lock (responseLock)
                        {
                            countTcs = bufferItemCountTcs;
                            bufferItemCountTcs = null;
                        }

                        if (countTcs != null)
                        {
                            if (payload.Length >= 1)
                            {
                                int count = payload[0]; // Assuming count is a single byte
                                //_log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: --response count: {count} --");
                                countTcs.SetResult(count);
                            }
                            else
                            {
                                //_log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: --response count: Invalid payload length for BUFFER_ITEM_COUNT response.--");
                                countTcs.SetException(new Exception("Invalid payload length for BUFFER_ITEM_COUNT response."));
                            }
                        }
                        else
                        {
                            //_log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: --response count: Received BUFFER_ITEM_COUNT response with no pending request.--");
                        }
                    }
                    break;

                case BUFFER_STATUS_FREE_SPACE:
                    //_log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: --response: Buffer has free space available.--");
                    BufferHasFreeSpace?.Invoke();
                    break;

                case BUFFER_STATUS_LAST_ITEM:
                    //_log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: --response: Last buffer item has been taken.--");
                    LastBufferItemTaken?.Invoke();
                    break;

                case EXECUTION_STATUS_END:
                    //_log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: --response: Execution of all commands completed.--");
                    ExecutionCompleted?.Invoke();
                    break;

                default:
                    //_log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: --response: Unknown response code: 0x{responseCode:X2}--");
                    break;
            }
        }

        // Helper method to wait for a response with timeout
        private async Task<T> WaitForResponseAsync<T>(Task<T> task, int timeoutMilliseconds = 5000)
        {
            if (await Task.WhenAny(task, Task.Delay(timeoutMilliseconds)) == task)
            {
                return await task;
            }
            else
            {
                throw new TimeoutException("No response received from the microcontroller within the timeout period.");
            }
        }

        // Implement abstract methods...
        public override void AddDevice(BaseDevice device)
        {
            throw new NotImplementedException();
        }

        public override Task ConnectDevice(BaseDevice device, SemaphoreSlim semaphore)
        {
            throw new NotImplementedException();
        }

        public override BaseController GetCopy()
        {
            var controller = new SyncController_Pico(Name)
            {
                MasterController = this.MasterController,
                ID = this.ID,
            };

            return controller;
        }

        public override List<BaseDevice> GetDevices()
        {
            return new List<BaseDevice>();
        }

        public override Task UpdateStatesAsync(ConcurrentQueue<string> log)
        {
            return Task.CompletedTask;
        }

        public override async Task Stop(SemaphoreSlim semaphore, ConcurrentQueue<string> log)
        {
            try
            {
                await ClearBuffer();
            }
            catch (Exception ex)
            {
                //_log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: Exception during Stop: {ex.Message}");
                // Optionally, decide whether to rethrow or ignore the exception
            }
            finally
            {
                // Stop the reading task
                //readCancellationTokenSource.Cancel();
                // Optionally close the serial port if appropriate
                // serialPort.Close();
            }
        }
    }
}










//using standa_controller_software.device_manager.attributes;
//using standa_controller_software.device_manager.devices;
//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.IO.Ports;
//using System.Linq;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;

//namespace standa_controller_software.device_manager.controller_interfaces.sync
//{
//    public class SyncController_Pico : BaseSyncController
//    {
//        public struct ExecutionInformation
//        {
//            public char[] Devices;
//            public bool Launch;
//            public float Rethrow;
//            public bool Shutter;
//            public float Shutter_delay_on;
//            public float Shutter_delay_off;
//        }

//        // Define constants
//        const byte START_BYTE = 0xAA; // Command start byte
//        const byte RESPONSE_START_BYTE = 0xAB; // Response start byte

//        // Command IDs
//        const byte CMD_ADD_BUFFER_ITEM = 0x01;
//        const byte CMD_CLEAR_BUFFER = 0x02;
//        const byte CMD_BUFFER_ITEM_COUNT = 0x03;
//        const byte CMD_START_EXECUTION = 0x04;

//        // Response Codes
//        const byte RESP_CMD_SUCCESS = 0x01;
//        const byte RESP_CMD_ERROR = 0x02;
//        const byte RESP_BUFFER_ITEM_COUNT = 0x04;

//        const byte BUFFER_STATUS_FREE_SPACE = 0x10;
//        const byte BUFFER_STATUS_LAST_ITEM = 0x11;

//        const byte EXECUTION_STATUS_END = 0x20;

//        [DisplayPropertyAttribute]
//        public char FirstDevice { get; set; }

//        [DisplayPropertyAttribute]
//        public char SecondDevice { get; set; }

//        [DisplayPropertyAttribute]
//        public char ThirdDevice { get; set; }

//        [DisplayPropertyAttribute]
//        public char FourthDevice { get; set; }

//        private SerialPort serialPort;
//        private Dictionary<char, byte> _deviceToPinMap = new Dictionary<char, byte>();

//        private readonly SemaphoreSlim commandSemaphore = new SemaphoreSlim(1, 1);

//        private TaskCompletionSource<bool> commandResponseTcs;
//        private TaskCompletionSource<int> bufferItemCountTcs;
//        private readonly object responseLock = new object();

//        // Events to notify subscribers
//        public event Action ExecutionCompleted;
//        public event Action BufferHasFreeSpace;
//        public event Action LastBufferItemTaken;

//        // Buffer for incoming data
//        private List<byte> dataBuffer = new List<byte>();

//        public SyncController_Pico(string name) : base(name)
//        {
//        }

//        public override Task InitializeController(SemaphoreSlim semaphore, ConcurrentQueue<string> log)
//        {
//            base.InitializeController(semaphore, log);

//            _deviceToPinMap[FirstDevice] = 0x06;
//            _deviceToPinMap[SecondDevice] = 0x07;
//            _deviceToPinMap[ThirdDevice] = 0x08;
//            _deviceToPinMap[FourthDevice] = 0x09;

//            // Initialize the serial port
//            serialPort = new SerialPort(this.ID, 115200, Parity.None, 8, StopBits.One);
//            serialPort.DtrEnable = true;
//            serialPort.RtsEnable = true;
//            serialPort.Open();
//            serialPort.DataReceived += DataReceivedHandler;

//            return Task.CompletedTask;
//        }

//        // Method to add buffer item
//        public async Task AddBufferItem(char[] Devices, bool Launch, float Rethrow, bool Shutter, float Shutter_delay_on, float Shutter_delay_off)
//        {
//            //_log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: --AddBuffer--");
//            await commandSemaphore.WaitAsync();
//            //_log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: --AddBuffer--semaphore acquired--");

//            try
//            {
//                var executionInfo = new ExecutionInformation
//                {
//                    Devices = Devices,
//                    Launch = Launch,
//                    Rethrow = Rethrow,
//                    Shutter = Shutter,
//                    Shutter_delay_off = Shutter_delay_off,
//                    Shutter_delay_on = Shutter_delay_on
//                };

//                // Construct the packet
//                byte[] packet = ConstructAddBufferItemPacket(executionInfo);

//                // Initialize the TaskCompletionSource
//                var tcs = new TaskCompletionSource<bool>();

//                lock (responseLock)
//                {
//                    if (commandResponseTcs != null)
//                    {
//                        throw new InvalidOperationException("Another command is already being processed.");
//                    }
//                    commandResponseTcs = tcs;
//                }

//                // Send the packet
//                serialPort.Write(packet, 0, packet.Length);
//                //_log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: --AddBuffer--serial wrote--");

//                // Wait for the response
//                bool success = await tcs.Task;
//                //_log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: --AddBuffer--awaited response--");

//                if (!success)
//                {
//                    throw new Exception("Error executing ADD_BUFFER_ITEM command.");
//                }
//            }
//            finally
//            {
//                lock (responseLock)
//                {
//                    commandResponseTcs = null;
//                }
//                commandSemaphore.Release();
//            }
//        }

//        // ClearBuffer method
//        public async Task ClearBuffer()
//        {
//            //_log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: --ClearBuffer--");

//            await commandSemaphore.WaitAsync();

//            try
//            {
//                // Construct the packet
//                byte[] packet = ConstructSimpleCommandPacket(CMD_CLEAR_BUFFER);

//                // Initialize the TaskCompletionSource
//                var tcs = new TaskCompletionSource<bool>();

//                lock (responseLock)
//                {
//                    if (commandResponseTcs != null)
//                    {
//                        throw new InvalidOperationException("Another command is already being processed.");
//                    }
//                    commandResponseTcs = tcs;
//                }

//                // Send the packet
//                serialPort.Write(packet, 0, packet.Length);

//                // Wait for the response
//                bool success = await tcs.Task;

//                if (!success)
//                {
//                    throw new Exception("Error executing CLEAR_BUFFER command.");
//                }
//            }
//            finally
//            {
//                lock (responseLock)
//                {
//                    commandResponseTcs = null;
//                }
//                commandSemaphore.Release();
//            }
//        }

//        // GetBufferItemCount method
//        public async Task<int> GetBufferItemCount()
//        {
//            //_log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: --GetBufferCount--");

//            await commandSemaphore.WaitAsync();

//            try
//            {
//                // Construct the packet
//                byte[] packet = ConstructSimpleCommandPacket(CMD_BUFFER_ITEM_COUNT);

//                // Initialize the TaskCompletionSource
//                var tcs = new TaskCompletionSource<int>();

//                lock (responseLock)
//                {
//                    if (bufferItemCountTcs != null)
//                    {
//                        throw new InvalidOperationException("Another command is already being processed.");
//                    }
//                    bufferItemCountTcs = tcs;
//                }

//                // Send the packet
//                serialPort.Write(packet, 0, packet.Length);

//                // Wait for the response
//                int count = await tcs.Task;

//                return 254 - count;
//            }
//            finally
//            {
//                lock (responseLock)
//                {
//                    bufferItemCountTcs = null;
//                }
//                commandSemaphore.Release();
//            }
//        }

//        // StartExecution method
//        public async Task StartExecution()
//        {
//            //_log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: --StartExec--");

//            await commandSemaphore.WaitAsync();

//            try
//            {
//                // Construct the packet
//                byte[] packet = ConstructSimpleCommandPacket(CMD_START_EXECUTION);

//                // Initialize the TaskCompletionSource
//                var tcs = new TaskCompletionSource<bool>();

//                lock (responseLock)
//                {
//                    if (commandResponseTcs != null)
//                    {
//                        throw new InvalidOperationException("Another command is already being processed.");
//                    }
//                    commandResponseTcs = tcs;
//                }

//                // Send the packet
//                serialPort.Write(packet, 0, packet.Length);

//                // Wait for the response
//                bool success = await tcs.Task;

//                if (!success)
//                {
//                    throw new Exception("Error executing START_EXECUTION command.");
//                }
//            }
//            finally
//            {
//                lock (responseLock)
//                {
//                    commandResponseTcs = null;
//                }
//                commandSemaphore.Release();
//            }
//        }

//        // Helper method to construct simple command packets
//        private byte[] ConstructSimpleCommandPacket(byte commandId)
//        {
//            byte payloadLength = 0x00;

//            // Compute checksum
//            byte checksum = (byte)(commandId ^ payloadLength);

//            // Construct the packet
//            byte[] packet = new byte[4]; // Start Byte + Command ID + Payload Length + Checksum
//            int index = 0;
//            packet[index++] = START_BYTE;
//            packet[index++] = commandId;
//            packet[index++] = payloadLength;
//            packet[index++] = checksum;

//            return packet;
//        }

//        // Helper method to construct the packet for AddBufferItem
//        private byte[] ConstructAddBufferItemPacket(ExecutionInformation executionInfo)
//        {
//            const byte PAYLOAD_LENGTH = 9;

//            byte launch = (byte)(executionInfo.Launch ? 0x01 : 0x00);

//            // Map Devices to LEDpins
//            byte[] ledPins = new byte[4];
//            for (int i = 0; i < 4; i++)
//            {
//                if (i < executionInfo.Devices.Length)
//                {
//                    char device = executionInfo.Devices[i];
//                    if (_deviceToPinMap.TryGetValue(device, out byte pin))
//                    {
//                        ledPins[i] = pin;
//                    }
//                    else
//                    {
//                        throw new ArgumentException($"Unknown device '{device}'");
//                    }
//                }
//                else
//                {
//                    ledPins[i] = 0xFF; // Unused
//                }
//            }

//            // Convert Rethrow to bytes (float)
//            byte[] rethrowBytes = BitConverter.GetBytes(executionInfo.Rethrow);
//            // Do not reverse bytes; assume little-endian

//            // Construct the payload
//            byte[] payload = new byte[PAYLOAD_LENGTH];
//            payload[0] = launch;
//            Array.Copy(ledPins, 0, payload, 1, 4);
//            Array.Copy(rethrowBytes, 0, payload, 5, 4);

//            // Compute checksum
//            byte checksum = CMD_ADD_BUFFER_ITEM ^ PAYLOAD_LENGTH;
//            foreach (byte b in payload)
//            {
//                checksum ^= b;
//            }

//            // Construct the packet
//            byte[] packet = new byte[1 + 1 + 1 + PAYLOAD_LENGTH + 1];
//            int index = 0;
//            packet[index++] = START_BYTE;
//            packet[index++] = CMD_ADD_BUFFER_ITEM;
//            packet[index++] = PAYLOAD_LENGTH;
//            Array.Copy(payload, 0, packet, index, PAYLOAD_LENGTH);
//            index += PAYLOAD_LENGTH;
//            packet[index++] = checksum;

//            return packet;
//        }

//        private readonly object dataReceivedLock = new object();

//        // DataReceivedHandler
//        private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
//        {
//            lock (dataReceivedLock)
//            {
//                try
//                {
//                    int bytesToRead = serialPort.BytesToRead;
//                    byte[] buffer = new byte[bytesToRead];
//                    serialPort.Read(buffer, 0, bytesToRead);

//                    // Add received bytes to the data buffer
//                    dataBuffer.AddRange(buffer);

//                    int index = 0;
//                    while (index < dataBuffer.Count)
//                    {
//                        byte b = dataBuffer[index];

//                        if (b == RESPONSE_START_BYTE)
//                        {
//                            // Attempt to parse a protocol message
//                            int messageStartIndex = index;

//                            // Ensure there are enough bytes for header
//                            if (dataBuffer.Count - index < 4)
//                            {
//                                // Wait for more data
//                                break;
//                            }

//                            byte responseCode = dataBuffer[index + 1];
//                            byte payloadLength = dataBuffer[index + 2];
//                            int totalMessageLength = 1 + 1 + 1 + payloadLength + 1;

//                            // Ensure the full message is available
//                            if (dataBuffer.Count - index < totalMessageLength)
//                            {
//                                // Wait for more data
//                                break;
//                            }

//                            // Extract the message
//                            byte[] message = dataBuffer.GetRange(index, totalMessageLength).ToArray();

//                            // Validate checksum
//                            byte calculatedChecksum = (byte)(responseCode ^ payloadLength);
//                            for (int i = 3; i < 3 + payloadLength; i++)
//                            {
//                                calculatedChecksum ^= dataBuffer[index + i];
//                            }

//                            byte receivedChecksum = dataBuffer[index + totalMessageLength - 1];

//                            if (calculatedChecksum != receivedChecksum)
//                            {
//                                //_log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: Checksum mismatch in response.");
//                                // Skip this byte and continue parsing
//                                index++;
//                                continue;
//                            }

//                            // Process the response
//                            byte[] payload = new byte[payloadLength];
//                            if (payloadLength > 0)
//                            {
//                                Array.Copy(dataBuffer.ToArray(), index + 3, payload, 0, payloadLength);
//                            }

//                            HandleResponse(responseCode, payload);

//                            // Move index past this message
//                            index += totalMessageLength;
//                        }
//                        else
//                        {
//                            // It's text data (debug messages)
//                            int textStartIndex = index;

//                            // Find the next RESPONSE_START_BYTE or end of buffer
//                            while (index < dataBuffer.Count && dataBuffer[index] != RESPONSE_START_BYTE)
//                            {
//                                index++;
//                            }

//                            int textLength = index - textStartIndex;
//                            if (textLength > 0)
//                            {
//                                string text = Encoding.ASCII.GetString(dataBuffer.GetRange(textStartIndex, textLength).ToArray());
//                                //_log.Enqueue(text);
//                            }
//                        }
//                    }

//                    // Remove processed bytes from data buffer
//                    if (index > 0)
//                    {
//                        dataBuffer.RemoveRange(0, index);
//                    }
//                }
//                catch (Exception ex)
//                {
//                    // Log the exception and continue
//                    //_log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: Exception in DataReceivedHandler: {ex.Message}");
//                }
//            }
//        }

//        // Method to handle responses
//        private void HandleResponse(int responseCode, byte[] payload)
//        {
//            switch (responseCode)
//            {
//                case RESP_CMD_SUCCESS:
//                    {
//                        TaskCompletionSource<bool> tcs = null;
//                        lock (responseLock)
//                        {
//                            tcs = commandResponseTcs;
//                            commandResponseTcs = null;
//                        }

//                        if (tcs != null)
//                        {
//                            //_log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: --response: success: true --");
//                            tcs.SetResult(true);
//                        }
//                        else
//                        {
//                            //_log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: Received success response with no pending command.");
//                        }
//                    }
//                    break;

//                case RESP_CMD_ERROR:
//                    {
//                        TaskCompletionSource<bool> tcs = null;
//                        lock (responseLock)
//                        {
//                            tcs = commandResponseTcs;
//                            commandResponseTcs = null;
//                        }

//                        if (tcs != null)
//                        {
//                            //_log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: --response: success: false --");
//                            tcs.SetResult(false);
//                        }
//                        else
//                        {
//                            //_log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: Received error response with no pending command.");
//                        }
//                    }
//                    break;

//                case RESP_BUFFER_ITEM_COUNT:
//                    {
//                        TaskCompletionSource<int> countTcs = null;
//                        lock (responseLock)
//                        {
//                            countTcs = bufferItemCountTcs;
//                            bufferItemCountTcs = null;
//                        }

//                        if (countTcs != null)
//                        {
//                            if (payload.Length >= 1)
//                            {
//                                int count = payload[0]; // Assuming count is a single byte
//                                //_log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: --response count: {count} --");
//                                countTcs.SetResult(count);
//                            }
//                            else
//                            {
//                                //_log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: --response count: Invalid payload length for BUFFER_ITEM_COUNT response.--");
//                                countTcs.SetException(new Exception("Invalid payload length for BUFFER_ITEM_COUNT response."));
//                            }
//                        }
//                        else
//                        {
//                            //_log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: --response count: Received BUFFER_ITEM_COUNT response with no pending request.--");
//                        }
//                    }
//                    break;

//                case BUFFER_STATUS_FREE_SPACE:
//                    //_log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: --response: Buffer has free space available.--");
//                    BufferHasFreeSpace?.Invoke();
//                    break;

//                case BUFFER_STATUS_LAST_ITEM:
//                    //_log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: --response: Last buffer item has been taken.--");
//                    LastBufferItemTaken?.Invoke();
//                    break;

//                case EXECUTION_STATUS_END:
//                    //_log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: --response: Execution of all commands completed.--");
//                    ExecutionCompleted?.Invoke();
//                    break;

//                default:
//                    //_log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: --response: Unknown response code: 0x{responseCode:X2}--");
//                    break;
//            }
//        }

//        // Implement abstract methods...
//        public override void AddDevice(BaseDevice device)
//        {
//            throw new NotImplementedException();
//        }

//        public override Task ConnectDevice(BaseDevice device, SemaphoreSlim semaphore)
//        {
//            throw new NotImplementedException();
//        }

//        public override BaseController GetCopy()
//        {
//            var controller = new SyncController_Pico(Name)
//            {
//                MasterController = this.MasterController,
//                ID = this.ID,
//            };

//            return controller;
//        }

//        public override List<BaseDevice> GetDevices()
//        {
//            return new List<BaseDevice>();
//        }

//        public override Task UpdateStatesAsync(ConcurrentQueue<string> log)
//        {
//            return Task.CompletedTask;
//        }

//        public override async Task Stop(SemaphoreSlim semaphore, ConcurrentQueue<string> log)
//        {
//            await ClearBuffer();
//        }
//    }
//}
