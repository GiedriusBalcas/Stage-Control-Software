using standa_controller_software.command_manager;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.controller_interfaces.shutter
{
    public class ShutterController_Arduino : BaseShutterController
    {
        // Constants matching the Arduino code
        private const byte START_BYTE = 0xFF;
        private const byte RESPONSE_START_BYTE = 0xAB;

        // Command IDs
        private const byte CMD_CHANGE_SHUTTER_STATE = 0x01; // CMD_ID_1
        private const byte CMD_CHANGE_STATE_FOR_INTERVAL = 0x02; // CMD_ID_2
        private const byte CMD_GET_SHUTTER_STATE = 0x03; // CMD_GET_SHUTTER_STATE

        // Response Codes
        private const byte RESP_SHUTTER_STATE = 0x01;
        private const byte RESP_CMD_SUCCESS = 0x02;
        private const byte RESP_CMD_ERROR = 0x03;

        private SerialPort serialPort;

        
        public ShutterController_Arduino(string name, ConcurrentQueue<string> log) : base(name, log)
        {
        }


        protected override async Task InitializeController(Command command, SemaphoreSlim semaphore)
        {
            await base.InitializeController(command, semaphore);

            serialPort = new SerialPort(this.ID, 9600, Parity.None, 8, StopBits.One)
            {
                DtrEnable = true,
                RtsEnable = true,
                ReadTimeout = 2000, // Adjust as necessary
                WriteTimeout = 500
            };
            serialPort.Open();

            // Flush any existing data
            serialPort.DiscardInBuffer();
            serialPort.DiscardOutBuffer();
        }
        protected override Task UpdateStatesAsync(Command command, SemaphoreSlim semaphore)
        {
            var currentState = GetState(out bool state);
            Devices.First().Value.IsOn = state;
            return Task.CompletedTask;
        }

        protected override async Task ChangeState_implementation(BaseShutterDevice device, bool wantedState)
        {
            EnsurePortIsOpen();

            // Construct the command packet
            byte[] payload = new byte[] { (byte)(wantedState ? 1 : 0) };
            byte[] packet = ConstructPacket(CMD_CHANGE_SHUTTER_STATE, payload);

            // Send the packet
            serialPort.Write(packet, 0, packet.Length);

            // Read and process the response
            ResponsePacket response = await ReadResponse();

            var processedResponse = ProcessResponse(response);
            if (processedResponse == false)
                _log.Enqueue("Arduino_shutter: error response received.");
        }
        protected override async Task ChangeStateOnInterval_implementation(BaseShutterDevice device, float duration)
        {
            EnsurePortIsOpen();

            // Construct the command packet
            byte[] payload = BitConverter.GetBytes(duration);
            byte[] packet = ConstructPacket(CMD_CHANGE_STATE_FOR_INTERVAL, payload);

            // Send the packet
            serialPort.Write(packet, 0, packet.Length);

            // Read and process the response
            ResponsePacket response = await ReadResponse();

            var processedResponse = ProcessResponse(response);
            if (processedResponse == false)
                _log.Enqueue("Arduino_shutter: error response received.");

            await Task.Delay((int)(duration * 1000));

        }
        protected override Task ConnectDevice_implementation(BaseDevice device)
        {
            return Task.CompletedTask;
        }

        private bool GetState(out bool shutterState)
        {
            shutterState = false;
            EnsurePortIsOpen();

            // Construct the command packet
            byte[] packet = ConstructPacket(CMD_GET_SHUTTER_STATE, null);

            // Send the packet
            serialPort.Write(packet, 0, packet.Length);

            // Read and process the response
            ResponsePacket response = ReadResponse().GetAwaiter().GetResult();

            if (response.ResponseCode == RESP_SHUTTER_STATE && response.PayloadLength == 1)
            {
                shutterState = response.Payload[0] != 0;
                return true;
            }
            else if (response.ResponseCode == RESP_CMD_ERROR)
            {
                return false;
            }
            else
            {
                throw new Exception($"Unexpected response code: {response.ResponseCode}");
            }
        }
        private void EnsurePortIsOpen()
        {
            if (serialPort == null || !serialPort.IsOpen)
                throw new InvalidOperationException("Serial port is not open.");
        }
        private byte[] ConstructPacket(byte commandId, byte[] payload)
        {
            payload ??= new byte[0];
            byte payloadLength = (byte)payload.Length;
            byte checksum = CalculateChecksum(commandId, payloadLength, payload);

            byte[] packet = new byte[1 + 1 + 1 + payloadLength + 1];
            int index = 0;
            packet[index++] = START_BYTE;
            packet[index++] = commandId;
            packet[index++] = payloadLength;
            Array.Copy(payload, 0, packet, index, payloadLength);
            index += payloadLength;
            packet[index] = checksum;

            return packet;
        }
        private async Task<ResponsePacket> ReadResponse()
        {
            int timeout = serialPort.ReadTimeout;
            DateTime startTime = DateTime.Now;

            while ((DateTime.Now - startTime).TotalMilliseconds < timeout)
            {
                if (serialPort.BytesToRead > 0)
                {
                    int firstByte = serialPort.ReadByte();
                    if (firstByte == RESPONSE_START_BYTE)
                    {
                        // Read response code and payload length
                        byte responseCode = (byte)serialPort.ReadByte();
                        byte payloadLength = (byte)serialPort.ReadByte();

                        // Read payload
                        byte[] payload = new byte[payloadLength];
                        if (payloadLength > 0)
                        {
                            serialPort.Read(payload, 0, payloadLength);
                        }

                        // Read checksum
                        byte checksum = (byte)serialPort.ReadByte();

                        // Validate checksum
                        byte calculatedChecksum = CalculateChecksum(responseCode, payloadLength, payload);
                        if (checksum != calculatedChecksum)
                        {
                            throw new Exception("Checksum mismatch in response.");
                        }

                        return new ResponsePacket(responseCode, payloadLength, payload);
                    }
                    else
                    {
                        // Not the start byte, continue reading
                        continue;
                    }
                }
                else
                {
                    await Task.Delay(1); // Wait for data
                }
            }

            throw new TimeoutException("Timeout waiting for response.");
        }
        private bool ProcessResponse(ResponsePacket response)
        {
            if (response.ResponseCode == RESP_CMD_SUCCESS)
            {
                return true;
            }
            else if (response.ResponseCode == RESP_CMD_ERROR)
            {
                return false;
            }
            else
            {
                throw new Exception($"Unexpected response code: {response.ResponseCode}");
            }
        }
        private byte CalculateChecksum(byte code, byte payloadLength, byte[] payload)
        {
            int sum = code + payloadLength;
            if (payload != null)
            {
                foreach (byte b in payload)
                {
                    sum += b;
                }
            }
            return (byte)(sum % 256);
        }
        private class ResponsePacket
        {
            public byte ResponseCode { get; }
            public byte PayloadLength { get; }
            public byte[] Payload { get; }

            public ResponsePacket(byte responseCode, byte payloadLength, byte[] payload)
            {
                ResponseCode = responseCode;
                PayloadLength = payloadLength;
                Payload = payload;
            }
        }



    }
}
