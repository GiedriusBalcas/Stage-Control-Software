using Microsoft.Extensions.Logging;
using standa_controller_software.command_manager;
using standa_controller_software.device_manager.devices;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace standa_controller_software.device_manager.controller_interfaces.sync
{
    public class SyncController_Sim : BaseSyncController
    {
        private Queue<ExecutionInformation> _buffer = new Queue<ExecutionInformation>();
        private ConcurrentBag<char> _gotSyncOutFrom = new ConcurrentBag<char>();
        private int _maxBufferSize = 6;
        private bool _allowedToRun = true;

        public Dictionary<char, Action> PositionerSyncInMap = new Dictionary<char, Action>();
        public Action<bool>? ShutterChangeState;
        public event Action<string>? SendMessage;

        public SyncController_Sim(string name, ILoggerFactory loggerFactory) : base(name, loggerFactory)
        {
            _logger = _loggerFactory.CreateLogger<SyncController_Sim>();
        }

        public void GotSyncOut(char deviceName)
        {
            //_logger.LogInformation($"Got SyncOut from: {deviceName}");

            _gotSyncOutFrom.Add(deviceName);
        }
        public override Task ForceStop()
        {
            _buffer.Clear();
            _allowedToRun = false;
            return Task.CompletedTask;
        }

        protected override Task Stop(Command command, SemaphoreSlim semaphore)
        {
            _buffer.Clear();
            _allowedToRun = false;
            return Task.CompletedTask;
        }
        protected override Task AddSyncBufferItem_implementation(char[] Devices, bool Launch, float Rethrow, bool Shutter, float Shutter_delay_on, float Shutter_delay_off)
        {
            var executionInformation = new ExecutionInformation
            {
                Devices = Devices,
                Launch = Launch,
                Rethrow = Rethrow,
                Shutter = Shutter,
                Shutter_delay_off = Shutter_delay_off,
                Shutter_delay_on = Shutter_delay_on
            };

            if (_buffer.Count >= _maxBufferSize)
                _logger.LogError($"Buffered item count surpasses maximum allowed item size.");

            _buffer.Enqueue(executionInformation);
            _logger.LogDebug($"Added Sync Buffer Item: devices: {string.Join(',', Devices)} | launch: {Launch} | rethrow: {Rethrow} | shutter: {Shutter} | shutter_on: {Shutter_delay_on} | shutter_off: {Shutter_delay_off}.");

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
            _logger.LogDebug($"StartQueueExecution encountered.");

            return Task.CompletedTask;
        }
        protected override Task<int> GetBufferCount(Command command, SemaphoreSlim semaphore)
        {
            int currentSize = _buffer.Count;
            var result = _maxBufferSize - currentSize;
            _logger.LogDebug($"buffer count: {result}.");

            return Task.FromResult(result);
        }
        protected override Task ConnectDevice_implementation(BaseDevice device)
        {
            return Task.CompletedTask;
        }

        private async Task ExecuteQueue()
        {
            _logger.LogDebug($"Execute queue encountered.");

            _allowedToRun = true;
            var timer = new Stopwatch();
            bool lastItemTaken = false;
            var startTime = timer.ElapsedMilliseconds;
            var waitingForSyncOutsFrom = new List<char>();
            _gotSyncOutFrom.Clear();

            while (!lastItemTaken && _allowedToRun)
            {
                _logger.LogDebug($"Execute queue. There are items in queue and is allowed to run");

                bool has_item = _buffer.TryDequeue(out var exec_info);
                if (has_item)
                {
                    bool has_next_item = _buffer.TryPeek(out var exec_info_next);
                    if (exec_info.Launch)
                    {
                        foreach (var device in exec_info.Devices)
                        {
                            waitingForSyncOutsFrom.Add(device);
                            _gotSyncOutFrom.Clear();

                        }

                        _ = SendPulse(exec_info.Devices);
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
                            _logger.LogDebug($"Execute queue. Got all of the sync out signals from devices: {string.Join(',', waitingForSyncOutsFrom)}.");

                            _gotSyncOutFrom.Clear();
                            break;
                        }

                        var elapsed_ms = timer.ElapsedMilliseconds;
                        if (shutter_pending_on && elapsed_ms >= shutter_on)
                        {
                            _logger.LogDebug($"Execute queue. shutter on.");

                            ShutterChangeState?.Invoke(true);
                            shutter_pending_on = false;
                        }

                        if (shutter_pending_off && elapsed_ms >= shutter_off)
                        {
                            _logger.LogDebug($"Execute queue. shutter off.");

                            ShutterChangeState?.Invoke(false);
                            shutter_pending_off = false;
                        }

                        if (rethrow_ms > 0 && elapsed_ms >= rethrow_ms)
                        {
                            _logger.LogDebug($"Execute queue. rethrow encountered.");

                            break;
                        }

                        await Task.Delay(10);
                    }

                    if (shutter_pending_off)
                    {
                        _logger.LogDebug($"Execute queue. shutter off.");
                        ShutterChangeState?.Invoke(false);
                    }

                    waitingForSyncOutsFrom = new List<char>();

                    if (has_next_item)
                    {
                        foreach (var device in exec_info_next.Devices)
                        {
                            waitingForSyncOutsFrom.Add(device);
                        }
                        _gotSyncOutFrom.Clear();

                        await SendPulse(exec_info_next.Devices);
                        _logger.LogDebug($"Execute queue. Sent pulses and awaiting: {string.Join(',', waitingForSyncOutsFrom)}. Restarting timer.");
                        timer.Restart();
                    }
                }
                else
                {
                    _logger.LogDebug($"Execute queue. Ran out of items.");

                    lastItemTaken = true;
                }
            }
            _logger.LogDebug($"Execute queue. Ran out of queued items or allowed to run: {_allowedToRun}.");

            _ = Task.Run(() => SendMessage?.Invoke("0x02"));
        }
        private Task SendPulse(char[] devices)
        {
            foreach (var device in devices)
            {
                Task.Run(() => PositionerSyncInMap[device].Invoke());
            }
            _logger.LogDebug($"Send pulse encountered. Devices: {string.Join(',', devices)}.");

            return Task.CompletedTask;
        }
    }
}
