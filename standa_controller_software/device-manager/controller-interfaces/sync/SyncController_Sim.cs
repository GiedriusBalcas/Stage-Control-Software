using standa_controller_software.device_manager.controller_interfaces.positioning;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.controller_interfaces.sync
{
    public class SyncController_Sim : BaseSyncController
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

        private Queue<ExecutionInformation> _buffer = new Queue<ExecutionInformation>();
        public Dictionary<char, Action> _positionerSyncInMap = new Dictionary<char, Action>();
        public Action<bool> _shutterChangeState;
        private ConcurrentBag<char> _gotSyncOutFrom = new ConcurrentBag<char>();

        public List<char> _sendSyncInTo = new List<char>();
        Stopwatch millis = new Stopwatch();
        private QueueState _queueState = QueueState.Waiting;
        private bool _movementFlag;
        private ConcurrentQueue<string>? _log;
        private bool _relaunchFlag = true;
        private int _maxBufferSize = 6;
        private bool _allowedToRun = true;

        public event Action<string> SendMessage;
        public enum QueueState
        {
            Running,
            Waiting
        }

        public Task Stop()
        {
            _buffer.Clear();
            _allowedToRun = false;
            _queueState = QueueState.Waiting;
            return Task.CompletedTask;
        }

        public void AddBufferItem(char[] Devices, bool Launch, float Rethrow, bool Shutter, float Shutter_delay_on, float Shutter_delay_off)
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
        }
        public void GotSyncOut(char deviceName)
        {
            _log?.Enqueue($"Got SyncOut from: {deviceName}");

            _gotSyncOutFrom.Add(deviceName);
        }

        public SyncController_Sim(string name) : base(name)
        {
        }

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
            var controller = new SyncController_Sim(Name)
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
            //await Task.Delay(1100);
            return Task.CompletedTask;
        }

        public override Task Stop(SemaphoreSlim semaphore, ConcurrentQueue<string> log)
        {
            _buffer.Clear();
            _allowedToRun = false;
            _queueState = QueueState.Waiting;
            return Task.CompletedTask;
        }





        public async Task ExecuteQueue(SemaphoreSlim semaphore)
        {
            _allowedToRun = true;
            _log.Enqueue("Starting executing on sync executer controller");
            if (_queueState == QueueState.Running)
                return;

            _queueState = QueueState.Running;

            millis.Restart();
            _log.Enqueue($"execution running with buffer element count: {_buffer.Count}");

            while (_buffer.Count > 0)
            {
                var executionInformation = _buffer.Dequeue();
                if (_buffer.Count == 0)
                    SendMessage.Invoke("0x03");

                // which devices sync out will we be awaiting here?

                var waitingForSyncOutsFrom = new List<char>();
                foreach (var device in executionInformation.Devices)
                {
                    waitingForSyncOutsFrom.Add(device);
                }

                // where to send the sync ins for next command?
                _sendSyncInTo.Clear();
                var shutterDelayOn = 0f;
                var shutterDelayOff = 0f;
                var nextCommandUsesShutter = false;
                if (_buffer.Count > 0)
                {
                    var nextBufferItem = _buffer.Peek();
                    foreach (var device in nextBufferItem.Devices)
                    {
                        _sendSyncInTo.Add(device);
                    }
                    if (nextBufferItem.Shutter)
                    {
                        nextCommandUsesShutter = true;
                        shutterDelayOff = nextBufferItem.Shutter_delay_off;
                        shutterDelayOn = nextBufferItem.Shutter_delay_on;
                    }
                }


                _log.Enqueue($"dequed from buffer. On execition sendSyncInTo: {string.Join(' ', _sendSyncInTo.ToArray())}");

                // send sync_in to all targetted devices if launch is pending.
                if (executionInformation.Launch)
                {
                    _log.Enqueue($"launching: {string.Join(' ', executionInformation.Devices)}");
                    SendSyncIn(executionInformation.Devices);
                }

                var rethrowPending = true;
                //var shutterOnPending = executionInformation.Shutter;
                //var shutterOffPending = executionInformation.Shutter;

                while (_allowedToRun)
                {
                    var time = millis.ElapsedMilliseconds;
                    // check if we sync outs from devices of curent movement.
                    if (waitingForSyncOutsFrom.All(syncOut => _gotSyncOutFrom.Contains(syncOut)))
                    {
                        _gotSyncOutFrom.Clear();
                        if (nextCommandUsesShutter)
                            await SendSyncIn(_sendSyncInTo.ToArray(), shutterDelayOn, shutterDelayOff);
                        else
                            SendSyncIn(_sendSyncInTo.ToArray());
                        millis.Restart();
                        break;
                    }
                    if(executionInformation.Rethrow != 0 && executionInformation.Rethrow <= time)
                    {

                        _gotSyncOutFrom.Clear();
                        if (nextCommandUsesShutter)
                            await SendSyncIn(_sendSyncInTo.ToArray(), shutterDelayOn, shutterDelayOff);
                        else
                            SendSyncIn(_sendSyncInTo.ToArray());
                        break;
                    }


                    await Task.Delay(100);
                }


                SendMessage.Invoke("0x01");

                _log.Enqueue($" finish.");
            }
            _queueState = QueueState.Waiting;
            _log.Enqueue($" Queue is finished.");
            SendMessage.Invoke("0x02");

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

        public int CheckFreeItemSpace()
        {
            int currentSize = _buffer.Count;

            return _maxBufferSize - currentSize;
        }


    }
}
