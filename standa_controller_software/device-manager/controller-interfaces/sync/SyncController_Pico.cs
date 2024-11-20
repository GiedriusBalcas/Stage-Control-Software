using standa_controller_software.command_manager;
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

        // Replace commandResponseTcs with pendingCommand
        private PendingCommand pendingCommand;
        private TaskCompletionSource<int> bufferItemCountTcs;
        private readonly object responseLock = new object();

        // Events to notify subscribers
        public event Action ExecutionCompleted;
        public event Action BufferHasFreeSpace;
        public event Action LastBufferItemTaken;

        // Buffer for incoming data
        private readonly List<byte> dataBuffer = new List<byte>();
        private CancellationTokenSource readCancellationTokenSource;

        // Define the PendingCommand class
        private class PendingCommand
        {
            public byte CommandId { get; set; }
            public TaskCompletionSource<bool> Tcs { get; set; }
        }

        public SyncController_Pico(string name, ConcurrentQueue<string> log) : base(name, log)
        {
        }

        public override async Task ForceStop()
        {
            try
            {
                await ClearBuffer();
            }
            catch (Exception ex)
            {
                _log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: Exception during Stop: {ex.Message}");
            }
            finally
            {
                // Do not close the serial port or cancel the reading task
            }
        }
        protected override Task InitializeController(Command command, SemaphoreSlim semaphore)
        {
            base.InitializeController(command, semaphore);

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
        protected override async Task AddSyncBufferItem_implementation(char[] Devices, bool Launch, float Rethrow, bool Shutter, float Shutter_delay_on, float Shutter_delay_off)
        {
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

                // Initialize the TaskCompletionSource
                var tcs = new TaskCompletionSource<bool>();
                lock (responseLock)
                {
                    if (pendingCommand != null)
                    {
                        throw new InvalidOperationException("Another command is already being processed.");
                    }
                    pendingCommand = new PendingCommand { CommandId = CMD_ADD_BUFFER_ITEM, Tcs = tcs };
                }

                // Send the packet
                if (!serialPort.IsOpen)
                {
                    throw new InvalidOperationException("Serial port is closed.");
                }
                serialPort.Write(packet, 0, packet.Length);
                _log.Enqueue($"picoWrapper: Packet Sent: \nDevices = {string.Join(' ', Devices)} \nLaunch = {Launch}\nRethrow = {Rethrow / 1000}\nShutter_on = {Shutter_delay_on}\nShutter_off = {Shutter_delay_off}");
                _log.Enqueue($"pico: sending packet: [{string.Join(' ', packet)}]");

                // Wait for the response with timeout
                bool success = await WaitForResponseAsync(tcs.Task, timeoutMilliseconds: 10000);

                if (!success)
                {
                    string commandName = GetCommandName(CMD_ADD_BUFFER_ITEM);
                    throw new Exception($"Error executing '{commandName}' command.");
                }
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                lock (responseLock)
                {
                    pendingCommand = null;
                }
                commandSemaphore.Release();
            }
        }
        protected override async Task StartQueueExecution(Command command, SemaphoreSlim semaphore) 
        {
            _ = ExecuteQueue();
        }
        protected override async Task<int> GetBufferCount(Command command, SemaphoreSlim semaphore)
        {
            await commandSemaphore.WaitAsync();
            try
            {
                // Construct the packet
                byte[] packet = ConstructSimpleCommandPacket(CMD_BUFFER_ITEM_COUNT);

                // Initialize the TaskCompletionSource
                var tcs = new TaskCompletionSource<int>();
                lock (responseLock)
                {
                    if (bufferItemCountTcs != null)
                    {
                        throw new InvalidOperationException("picoWrapper: Another command is already being processed.");
                    }
                    bufferItemCountTcs = tcs;
                }

                // Send the packet
                if (!serialPort.IsOpen)
                {
                    throw new InvalidOperationException("picoWrapper: Serial port is closed.");
                }
                serialPort.Write(packet, 0, packet.Length);

                // Wait for the response with timeout
                int count = await WaitForResponseAsync(tcs.Task, timeoutMilliseconds: 1000);

                return 254 - count;
            }
            catch (Exception)
            {
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

        protected override async Task UpdateStatesAsync(Command command, SemaphoreSlim semaphore)
        {
            // Implement any state updates if necessary
            await Task.CompletedTask;
        }
        protected override async Task Stop(Command command, SemaphoreSlim semaphore)
        {
            try
            {
                await ClearBuffer();
            }
            catch (Exception ex)
            {
                _log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: Exception during Stop: {ex.Message}");
            }
            finally
            {
                // Do not close the serial port or cancel the reading task
            }
        }
        protected override Task ConnectDevice_implementation(BaseDevice device)
        {
            return Task.CompletedTask;
        }
        
        private async Task ExecuteQueue()
        {
            await commandSemaphore.WaitAsync();

            try
            {
                // Construct the packet
                byte[] packet = ConstructSimpleCommandPacket(CMD_START_EXECUTION);

                // Initialize the TaskCompletionSource
                var tcs = new TaskCompletionSource<bool>();
                lock (responseLock)
                {
                    if (pendingCommand != null)
                    {
                        throw new InvalidOperationException("picoWrapper: Another command is already being processed.");
                    }
                    pendingCommand = new PendingCommand { CommandId = CMD_START_EXECUTION, Tcs = tcs };
                }

                // Send the packet
                if (!serialPort.IsOpen)
                {
                    throw new InvalidOperationException("picoWrapper: Serial port is closed.");
                }
                serialPort.Write(packet, 0, packet.Length);

                // Wait for the response with timeout
                bool success = await WaitForResponseAsync(tcs.Task, timeoutMilliseconds: 1000);

                if (!success)
                {
                    string commandName = GetCommandName(CMD_START_EXECUTION);
                    throw new Exception($"picoWrapper: Error executing '{commandName}' command.");
                }
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                lock (responseLock)
                {
                    pendingCommand = null;
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
        // Method to construct the packet for AddBufferItem
        private byte[] ConstructAddBufferItemPacket(ExecutionInformation executionInfo)
        {
            const byte PAYLOAD_LENGTH = 17; // Updated payload length

            byte launch = (byte)(executionInfo.Launch ? 0x01 : 0x00);

            // Map Devices to LED pins
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
                        throw new ArgumentException($"picoWrapper: Unknown device '{device}'");
                    }
                }
                else
                {
                    ledPins[i] = 0xFF; // Unused
                }
            }

            // Convert floats to bytes (little-endian)
            byte[] rethrowBytes = BitConverter.GetBytes(executionInfo.Rethrow);
            byte[] shutterDelayOnBytes = BitConverter.GetBytes(executionInfo.Shutter_delay_on);
            byte[] shutterDelayOffBytes = BitConverter.GetBytes(executionInfo.Shutter_delay_off);

            // Construct the payload
            byte[] payload = new byte[PAYLOAD_LENGTH];
            int index = 0;
            payload[index++] = launch;
            Array.Copy(ledPins, 0, payload, index, 4);
            index += 4;
            Array.Copy(rethrowBytes, 0, payload, index, 4);
            index += 4;
            Array.Copy(shutterDelayOnBytes, 0, payload, index, 4);
            index += 4;
            Array.Copy(shutterDelayOffBytes, 0, payload, index, 4);

            // Compute checksum
            byte checksum = CMD_ADD_BUFFER_ITEM ^ PAYLOAD_LENGTH;
            foreach (byte b in payload)
            {
                checksum ^= b;
            }

            // Construct the packet
            byte[] packet = new byte[1 + 1 + 1 + PAYLOAD_LENGTH + 1];
            int packetIndex = 0;
            packet[packetIndex++] = START_BYTE;
            packet[packetIndex++] = CMD_ADD_BUFFER_ITEM;
            packet[packetIndex++] = PAYLOAD_LENGTH;
            Array.Copy(payload, 0, packet, packetIndex, PAYLOAD_LENGTH);
            packetIndex += PAYLOAD_LENGTH;
            packet[packetIndex] = checksum;

            return packet;
        }
        // Helper method to wait for a response with timeout
        private async Task<T> WaitForResponseAsync<T>(Task<T> task, int timeoutMilliseconds = 1000)
        {
            if (await Task.WhenAny(task, Task.Delay(timeoutMilliseconds)) == task)
            {
                return await task;
            }
            else
            {
                throw new TimeoutException("picoWrapper: No response received from the device within the timeout period.");
            }
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
                        byte[] receivedData = buffer.Take(bytesRead).ToArray();
                        //_log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: picoWrapper: Raw Data Received: [{string.Join(", ", receivedData.Select(b => $"0x{b:X2}"))}]");
                        //ProcessTextData(receivedData);
                        lock (dataBuffer)
                        {
                            dataBuffer.AddRange(receivedData);
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
                _log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: picoWrapper: Exception in ReadSerialDataAsync: {ex.Message}");
            }
        }
        // Updated ProcessReceivedData method
        private void ProcessReceivedData()
        {
            lock (dataBuffer)
            {
                while (dataBuffer.Count > 0)
                {
                    int startIndex = dataBuffer.FindIndex(b => b == RESPONSE_START_BYTE);

                    if (startIndex == -1)
                    {
                        // No RESPONSE_START_BYTE found, process all data as text
                        ProcessTextData(dataBuffer.ToArray());
                        dataBuffer.Clear();
                        return;
                    }
                    else if (startIndex > 0)
                    {
                        // Process data before RESPONSE_START_BYTE as text
                        byte[] textData = dataBuffer.GetRange(0, startIndex).ToArray();
                        ProcessTextData(textData);
                        dataBuffer.RemoveRange(0, startIndex);
                    }

                    // Now dataBuffer[0] == RESPONSE_START_BYTE
                    if (TryParseProtocolMessage(dataBuffer, out int messageLength, out int responseCode, out byte[] payload))
                    {
                        // Remove the message bytes from dataBuffer
                        dataBuffer.RemoveRange(0, messageLength);
                        HandleResponse(responseCode, payload);
                    }
                    else
                    {
                        // Not enough data to parse a full message
                        break;
                    }
                }
            }
        }
        // Method to process text data, filtering out non-printable characters
        private void ProcessTextData(byte[] data)
        {
            // Filter out non-printable characters
            byte[] printableData = data.Where(b => b >= 32 && b <= 126).ToArray();
            string text = Encoding.ASCII.GetString(printableData);
            if (!string.IsNullOrWhiteSpace(text))
            {
                _log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: picoWrapper: Received Text: {text}");
            }
        }
        // Updated TryParseProtocolMessage method
        private bool TryParseProtocolMessage(List<byte> buffer, out int messageLength, out int responseCode, out byte[] payload)
        {
            messageLength = 0;
            responseCode = 0;
            payload = null;

            // Need at least 4 bytes for header and checksum
            if (buffer.Count < 4)
            {
                return false; // Not enough data
            }

            if (buffer[0] != RESPONSE_START_BYTE)
            {
                // Should not happen since we checked this in ProcessReceivedData
                buffer.RemoveAt(0);
                return false;
            }

            responseCode = buffer[1];
            byte payloadLength = buffer[2];
            int totalMessageLength = 1 + 1 + 1 + payloadLength + 1; // Start Byte + Response Code + Payload Length + Payload + Checksum

            if (buffer.Count < totalMessageLength)
            {
                return false; // Not enough data
            }

            // Extract the message bytes
            byte[] messageBytes = buffer.GetRange(0, totalMessageLength).ToArray();

            // Validate checksum
            byte calculatedChecksum = (byte)(responseCode ^ payloadLength);
            for (int i = 3; i < 3 + payloadLength; i++)
            {
                calculatedChecksum ^= messageBytes[i];
            }

            byte receivedChecksum = messageBytes[totalMessageLength - 1];

            if (calculatedChecksum != receivedChecksum)
            {
                _log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: picoWrapper: Checksum mismatch in response. Expected: 0x{calculatedChecksum:X2}, Received: 0x{receivedChecksum:X2}");
                // Remove the RESPONSE_START_BYTE and continue parsing
                buffer.RemoveAt(0);
                return false;
            }

            // Extract payload
            payload = new byte[payloadLength];
            if (payloadLength > 0)
            {
                Array.Copy(messageBytes, 3, payload, 0, payloadLength);
            }

            messageLength = totalMessageLength;
            return true;
        }
        // Updated HandleResponse method
        private void HandleResponse(int responseCode, byte[] payload)
        {
            switch (responseCode)
            {
                case RESP_CMD_SUCCESS:
                    {
                        PendingCommand cmd = null;
                        lock (responseLock)
                        {
                            cmd = pendingCommand;
                            pendingCommand = null;
                        }

                        if (cmd != null)
                        {
                            string commandName = GetCommandName(cmd.CommandId);
                            _log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: picoWrapper: Command '{commandName}' executed successfully.");
                            cmd.Tcs.SetResult(true);
                        }
                        else
                        {
                            _log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: picoWrapper: Received success response with no pending command.");
                        }
                    }
                    break;

                case RESP_CMD_ERROR:
                    {
                        PendingCommand cmd = null;
                        lock (responseLock)
                        {
                            cmd = pendingCommand;
                            pendingCommand = null;
                        }

                        if (cmd != null)
                        {
                            string commandName = GetCommandName(cmd.CommandId);
                            _log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: picoWrapper: Command '{commandName}' failed.");
                            cmd.Tcs.SetResult(false);
                        }
                        else
                        {
                            _log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: picoWrapper: Received error response with no pending command.");
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
                                _log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: picoWrapper: Buffer item count: {count}");
                                countTcs.SetResult(count);
                            }
                            else
                            {
                                _log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: picoWrapper: Invalid payload length for BUFFER_ITEM_COUNT response.");
                                countTcs.SetException(new Exception("Invalid payload length for BUFFER_ITEM_COUNT response."));
                            }
                        }
                        else
                        {
                            _log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: picoWrapper: Received BUFFER_ITEM_COUNT response with no pending request.");
                        }
                    }
                    break;

                case BUFFER_STATUS_FREE_SPACE:
                    _log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: picoWrapper: Buffer has free space available.");
                    BufferHasFreeSpace?.Invoke();
                    break;

                case BUFFER_STATUS_LAST_ITEM:
                    _log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: picoWrapper: Last buffer item has been taken.");
                    LastBufferItemTaken?.Invoke();
                    break;

                case EXECUTION_STATUS_END:
                    _log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: picoWrapper: Execution of all commands completed.");
                    ExecutionCompleted?.Invoke();
                    break;

                default:
                    _log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: picoWrapper: Unknown response code: 0x{responseCode:X2}");
                    break;
            }
        }
        // Implement the GetCommandName method
        private string GetCommandName(byte commandId)
        {
            switch (commandId)
            {
                case CMD_ADD_BUFFER_ITEM: return "ADD_BUFFER_ITEM";
                case CMD_CLEAR_BUFFER: return "CLEAR_BUFFER";
                case CMD_START_EXECUTION: return "START_EXECUTION";
                case CMD_BUFFER_ITEM_COUNT: return "BUFFER_ITEM_COUNT";
                default: return $"Unknown Command 0x{commandId:X2}";
            }
        }
        private async Task ClearBuffer()
        {
            await commandSemaphore.WaitAsync();

            try
            {
                // Construct the packet
                byte[] packet = ConstructSimpleCommandPacket(CMD_CLEAR_BUFFER);

                // Initialize the TaskCompletionSource
                var tcs = new TaskCompletionSource<bool>();
                lock (responseLock)
                {
                    if (pendingCommand != null)
                    {
                        throw new InvalidOperationException("Another command is already being processed.");
                    }
                    pendingCommand = new PendingCommand { CommandId = CMD_CLEAR_BUFFER, Tcs = tcs };
                }

                // Send the packet
                if (!serialPort.IsOpen)
                {
                    throw new InvalidOperationException("Serial port is closed.");
                }
                serialPort.Write(packet, 0, packet.Length);

                // Wait for the response with timeout
                bool success = await WaitForResponseAsync(tcs.Task, timeoutMilliseconds: 10000);

                if (!success)
                {
                    string commandName = GetCommandName(CMD_CLEAR_BUFFER);
                    throw new Exception($"Error executing '{commandName}' command.");
                }
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                lock (responseLock)
                {
                    pendingCommand = null;
                }
                commandSemaphore.Release();
            }
        }
    }
}
