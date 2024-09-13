using System.Collections.Concurrent;
using System.Diagnostics;

namespace standa_controller_software.device_manager.controller_interfaces.master_controller
{
    public partial class PositionAndShutterController_Sim
    {
        /// <summary>
        /// This is a simulation of a real master controller for positioning and shutter synchronization
        /// Master Controller work-flow:
        ///     gather all the QuableCommands before execution
        ///     Execute the collected QuableCommands
        ///     Await till the slave controllers have executed the commands fully
        /// </summary>
        private class InternalSyncExecuter
        {
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

            public event Action<string> SendMessage;
            public enum QueueState
            {
                Running,
                Waiting
            }
            public InternalSyncExecuter()
            {
                

            }
            public void AddBufferItem(ExecutionInformation executionInformation)
            {
                if (_buffer.Count >= _maxBufferSize)
                    _log?.Enqueue($"===============================dafuk======================================================");

                _buffer.Enqueue(executionInformation);
            }
            public void GotSyncOut(char deviceName)
            {
                _log?.Enqueue($"Got SyncOut from: {deviceName}");

                _gotSyncOutFrom.Add(deviceName);
            }
           
            
            //private void CheckAndRelaunchSyncIn()
            //{
            //    if (syncOutFlags.All(kvp => kvp.Value == true) && _relaunchFlag)
            //    {
            //        _relaunchFlag = false;
            //        _movementFlag = true;
            //        var devicesToRethrow = sendSyncInTo.Where(kvp => kvp.Value == true)
            //                      .Select(kvp => kvp.Key)
            //                      .ToArray();

            //        SendSyncIn(devicesToRethrow);
            //        _relaunchFlag = true;
            //    }
            //}



            public async Task ExecuteQueue(ConcurrentQueue<string> log)
            {
                _log = log;
                log.Enqueue("Starting executing on sync executer controller");
                if (_queueState == QueueState.Running)
                    return;

                _queueState = QueueState.Running;

                millis.Restart();
                log.Enqueue($"execution running with buffer element count: {_buffer.Count}");

                while (_buffer.Count > 0)
                {
                    var executionInformation = _buffer.Dequeue();


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
                    
                    
                    log.Enqueue($"dequed from buffer. On execition sendSyncInTo: {string.Join(' ', _sendSyncInTo.ToArray())}");

                    // send sync_in to all targetted devices if launch is pending.
                    if (executionInformation.Launch)
                    {
                        log.Enqueue($"launching: {string.Join(' ', executionInformation.Devices)}");
                        SendSyncIn(executionInformation.Devices);
                    }

                    var rethrowPending = true;
                    //var shutterOnPending = executionInformation.Shutter;
                    //var shutterOffPending = executionInformation.Shutter;

                    while (true)
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
                        //if (shutterOnPending && time >= executionInformation.Shutter_delay_on)
                        //{
                        //    shutterOnPending = false;
                        //    _shutterChangeState?.Invoke(true);
                        //}
                        //if (shutterOffPending && time >= executionInformation.Shutter_delay_off)
                        //{
                        //    shutterOffPending = false;
                        //    _shutterChangeState?.Invoke(false);
                        //}
                        //if (rethrowPending && millis.ElapsedMilliseconds > 90)
                        //{
                        //    _gotSyncOutFrom.Clear();
                        //    SendSyncIn(_sendSyncInTo.ToArray());
                        //    millis.Restart();
                        //    break;
                        //}


                        await Task.Delay(1);
                    }
                    //_shutterChangeState?.Invoke(false);


                    SendMessage.Invoke("0x01");

                    log.Enqueue($" finish.");
                }
                _queueState = QueueState.Waiting;
                log.Enqueue($" Queue is finished.");
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
                    await Task.Delay((int)(delayOn * 1000)); // Convert delayOn to milliseconds
                    _shutterChangeState?.Invoke(true);
                }));

                tasks.Add(Task.Run(async () =>
                {
                    await Task.Delay((int)(delayOff * 1000)); // Convert delayOff to milliseconds
                    _shutterChangeState?.Invoke(false);
                }));

                // Await all tasks to complete
                _ = Task.WhenAll(tasks);
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
}
