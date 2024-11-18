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

        public SyncController_Sim(string name, ConcurrentQueue<string> log) : base(name, log)
        {
        }

        public void GotSyncOut(char deviceName)
        {
            //_log?.Enqueue($"Got SyncOut from: {deviceName}");

            _gotSyncOutFrom.Add(deviceName);
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
                _log?.Enqueue($"===============================dafuk======================================================");

            _buffer.Enqueue(executionInformation);

            return Task.CompletedTask;
        }
        protected override Task UpdateStatesAsync(Command command, SemaphoreSlim semaphore)
        {
            return Task.CompletedTask;
        }
        protected override async Task StartQueueExecution(Command command, SemaphoreSlim semaphore)
        {
            _ = ExecuteQueue(semaphore);
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

        //private async Task ExecuteQueue(SemaphoreSlim semaphore)
        //{
        //    _allowedToRun = true;
        //    _log.Enqueue("Starting executing on sync executer controller");
        //    if (_queueState == QueueState.Running)
        //        return;

        //    _queueState = QueueState.Running;

        //    millis.Restart();
        //    _log.Enqueue($"execution running with buffer element count: {_buffer.Count}");

        //    while (_buffer.Count > 0)
        //    {
        //        var executionInformation = _buffer.Dequeue();
        //        if (_buffer.Count == 0)
        //            SendMessage.Invoke("0x03");

        //        // which devices sync out will we be awaiting here?

        //        var waitingForSyncOutsFrom = new List<char>();
        //        foreach (var device in executionInformation.Devices)
        //        {
        //            waitingForSyncOutsFrom.Add(device);
        //        }

        //        // where to send the sync ins for next command?
        //        _sendSyncInTo.Clear();
        //        var shutterDelayOn = 0f;
        //        var shutterDelayOff = 0f;
        //        var nextCommandUsesShutter = false;
        //        if (_buffer.Count > 0)
        //        {
        //            var nextBufferItem = _buffer.Peek();
        //            foreach (var device in nextBufferItem.Devices)
        //            {
        //                _sendSyncInTo.Add(device);
        //            }
        //            if (nextBufferItem.Shutter)
        //            {
        //                nextCommandUsesShutter = true;
        //                shutterDelayOff = nextBufferItem.Shutter_delay_off;
        //                shutterDelayOn = nextBufferItem.Shutter_delay_on;
        //            }
        //        }


        //        _log.Enqueue($"dequed from buffer. On execition sendSyncInTo: {string.Join(' ', _sendSyncInTo.ToArray())}");

        //        // send sync_in to all targetted devices if launch is pending.
        //        if (executionInformation.Launch)
        //        {
        //            _log.Enqueue($"launching: {string.Join(' ', executionInformation.Devices)}");
        //            SendSyncIn(executionInformation.Devices);
        //        }

        //        var rethrowPending = true;
        //        //var shutterOnPending = executionInformation.Shutter;
        //        //var shutterOffPending = executionInformation.Shutter;

        //        while (_allowedToRun)
        //        {
        //            var time = millis.ElapsedMilliseconds;
        //            // check if we sync outs from devices of curent movement.
        //            if (waitingForSyncOutsFrom.All(syncOut => _gotSyncOutFrom.Contains(syncOut)))
        //            {
        //                _gotSyncOutFrom.Clear();
        //                if (nextCommandUsesShutter)
        //                    await SendSyncIn(_sendSyncInTo.ToArray(), shutterDelayOn, shutterDelayOff);
        //                else
        //                    SendSyncIn(_sendSyncInTo.ToArray());
        //                millis.Restart();
        //                break;
        //            }
        //            if(executionInformation.Rethrow != 0 && executionInformation.Rethrow <= time)
        //            {

        //                _gotSyncOutFrom.Clear();
        //                if (nextCommandUsesShutter)
        //                    await SendSyncIn(_sendSyncInTo.ToArray(), shutterDelayOn, shutterDelayOff);
        //                else
        //                    SendSyncIn(_sendSyncInTo.ToArray());
        //                break;
        //            }


        //            await Task.Delay(100);
        //        }


        //        SendMessage.Invoke("0x01");

        //        _log.Enqueue($" finish.");
        //    }
        //    _queueState = QueueState.Waiting;
        //    _log.Enqueue($" Queue is finished.");
        //    SendMessage.Invoke("0x02");

        //}

        private async Task ExecuteQueue(SemaphoreSlim semaphore)
        {
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
                        SendMessage.Invoke("0x01");
                    }
                    else
                    {
                        lastItemTaken = true;
                        SendMessage.Invoke("0x03");
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
            SendMessage.Invoke("0x02");
        }

        private Task SendPulse(char[] devices)
        {
            foreach (var device in devices)
            {
                _positionerSyncInMap[device].Invoke();       
            }
            return Task.CompletedTask;
        }

        private async Task SendSyncIn(char[] devices, float delayOn, float delayOff)
        {
            var tasks = new List<Task>();

            // Log the SyncIn call
            _log?.Enqueue($"{DateTime.Now:HH:mm:ss.fff} Sending SyncIns to: {string.Join(' ', devices)}");

            // Create and store tasks for all device calls
            foreach (var device in devices)
            {
                var call = _positionerSyncInMap[device];
                tasks.Add(Task.Run(() => call.Invoke()));
            }

            // Add tasks for the shutter state changes
            tasks.Add(Task.Run(async () =>
            {
                if (!float.IsNaN(delayOn))
                {
                    await Task.Delay((int)(delayOn * 1000)); // Convert delayOn to milliseconds
                    _shutterChangeState?.Invoke(true);
                }
            }));

            tasks.Add(Task.Run(async () =>
            {
                if (!float.IsNaN(delayOff))
                {
                    await Task.Delay((int)(delayOff * 1000)); // Convert delayOff to milliseconds
                    _shutterChangeState?.Invoke(false);
                }
            }));

            // Await all tasks to complete
            await Task.WhenAll(tasks);
        }
        private void SendSyncIn(char[] devices)
        {
            Action[] calls = new Action[devices.Length];
            int indexer = 0;

            foreach (var device in devices)
            {
                calls[indexer] = _positionerSyncInMap[device];
                indexer++;
            }
            _log?.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")} Sending SyncIns to: {string.Join(' ', devices)}");


            foreach (var call in calls)
            {
                call.Invoke();
            }
        }

    }
}
