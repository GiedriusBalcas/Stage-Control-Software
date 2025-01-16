using Microsoft.Extensions.Logging;
using standa_controller_software.command_manager;
using standa_controller_software.device_manager.controller_interfaces.positioning;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.controller_interfaces.sync
{
    public class SyncController_Sim : BaseSyncController
    {
        private Queue<ExecutionInformation> _buffer = new Queue<ExecutionInformation>();
        public Dictionary<char, Action> _positionerSyncInMap = new Dictionary<char, Action>();
        public Action<bool> _shutterChangeState;
        private ConcurrentBag<char> _gotSyncOutFrom = new ConcurrentBag<char>();

        public List<char> _sendSyncInTo = new List<char>();
        Stopwatch millis = new Stopwatch();
        private QueueState _queueState = QueueState.Waiting;
        private bool _movementFlag;
        private bool _relaunchFlag = true;
        private int _maxBufferSize = 6;
        private bool _allowedToRun = true;

        public event Action<string> SendMessage;
        public enum QueueState
        {
            Running,
            Waiting
        }

        public SyncController_Sim(string name, ILoggerFactory loggerFactory) : base(name, loggerFactory)
        {
            _logger = _loggerFactory.CreateLogger<SyncController_Sim>();
        }

        public void GotSyncOut(char deviceName)
        {
            //_logger.LogDebug($"Got SyncOut from: {deviceName}");

            _gotSyncOutFrom.Add(deviceName);
        }
        public override Task ForceStop()
        {
            _buffer.Clear();
            _allowedToRun = false;
            _queueState = QueueState.Waiting;
            return Task.CompletedTask;
        }

        protected override Task Stop(Command command, SemaphoreSlim semaphore)
        {
            _buffer.Clear();
            _allowedToRun = false;
            _queueState = QueueState.Waiting;
            return Task.CompletedTask;
        }
        protected override Task AddSyncBufferItem_implementation(char[] Devices, bool Launch, float Rethrow, bool Shutter, float Shutter_delay_on, float Shutter_delay_off)
        {
            var executionInformation = new ExecutionInformation
            {
                Devices = Devices,
                Launch = Launch,
                Rethrow = Rethrow ,
                Shutter = Shutter,
                Shutter_delay_off = Shutter_delay_off,
                Shutter_delay_on = Shutter_delay_on
            };

            if (_buffer.Count >= _maxBufferSize)
                _logger.LogError($"Buffered item count surpasses maximum allowed item size.");

            _buffer.Enqueue(executionInformation);

            return Task.CompletedTask;
        }
        protected override Task UpdateStatesAsync(Command command, SemaphoreSlim semaphore)
        {
            return Task.CompletedTask;
        }
        protected override Task StartQueueExecution(Command command, SemaphoreSlim semaphore)
        {
            // Detach ExecuteQueue to run on a separate thread
            Task.Run(() => ExecuteQueue());

            return Task.CompletedTask;
        }
        protected override async Task<int> GetBufferCount(Command command, SemaphoreSlim semaphore)
        {
            int currentSize = _buffer.Count;
            return _maxBufferSize - currentSize;
        }
        protected override Task ConnectDevice_implementation(BaseDevice device)
        {
            return Task.CompletedTask;
        }


        private async Task ExecuteQueue()
        {
            _allowedToRun = true;
            var timer = new Stopwatch();
            bool lastItemTaken = false;
            var startTime = timer.ElapsedMilliseconds;
            while (!lastItemTaken && _allowedToRun)
            {
                bool has_item = _buffer.TryDequeue(out var exec_info);
                if (has_item)
                {
                    var waitingForSyncOutsFrom = new List<char>();
                    bool has_next_item = _buffer.TryPeek(out var exec_info_next);
                    if (exec_info.Launch)
                    {
                        foreach (var device in exec_info.Devices)
                        {
                            waitingForSyncOutsFrom.Add(device);
                        }

                        _= SendPulse(exec_info.Devices);
                        timer.Restart();
                    }

                    if (has_next_item)
                    {
                        _ = Task.Run(() => SendMessage?.Invoke("0x01"));
                    }
                    else
                    {
                        lastItemTaken = true;
                        _ = Task.Run(() => SendMessage?.Invoke("0x03"));
                    }
                    var rethrow_ms = exec_info.Rethrow;
                    var shutter_on = exec_info.Shutter_delay_on;
                    var shutter_off = exec_info.Shutter_delay_off;
                    bool shutter_pending_on = !float.IsNaN(shutter_on);
                    bool shutter_pending_off = !float.IsNaN(shutter_off);
                    timer.Restart();

                    while (_allowedToRun)
                    {
                        if (waitingForSyncOutsFrom.All(syncOut => _gotSyncOutFrom.Contains(syncOut)))
                        {
                            _gotSyncOutFrom.Clear();
                            break;
                        }

                        var elapsed_ms = timer.ElapsedMilliseconds;
                        if (shutter_pending_on && elapsed_ms >= shutter_on)
                        {
                            _shutterChangeState?.Invoke(true);
                            shutter_pending_on = false;
                        }
                        
                        if (shutter_pending_off && elapsed_ms >= shutter_off)
                        {
                            _shutterChangeState?.Invoke(false);
                            shutter_pending_off = false;
                        }

                        if(rethrow_ms >0 && elapsed_ms >= rethrow_ms)
                        {
                            break;
                        }

                        await Task.Delay(10);
                    }

                    if (shutter_pending_off)
                    {
                        _shutterChangeState?.Invoke(false);
                    }

                    waitingForSyncOutsFrom = new List<char>();

                    if (has_next_item)
                    {
                        foreach (var device in exec_info_next.Devices)
                        {
                            waitingForSyncOutsFrom.Add(device);
                        }
                        await SendPulse(exec_info_next.Devices);
                        timer.Restart();
                    }
                }
                else
                {
                    lastItemTaken = true;
                }
            }
            _ = Task.Run(() => SendMessage?.Invoke("0x02"));
        }

        private Task SendPulse(char[] devices)
        {
            foreach (var device in devices)
            {
                Task.Run(() => _positionerSyncInMap[device].Invoke());       
            }
            return Task.CompletedTask;
        }


    }
}
