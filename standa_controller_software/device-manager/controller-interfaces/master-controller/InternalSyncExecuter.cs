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
                if (_queueState == QueueState.Running)
                    return;

                millis.Restart();

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
                    if (_buffer.Count > 0)
                    {
                        var nextBufferItem = _buffer.Peek();
                        foreach (var device in nextBufferItem.Devices)
                        {
                            _sendSyncInTo.Add(device);
                        }
                    }
                    
                    
                    log.Enqueue($"dequed from buffer. On execition sendSyncInTo: {string.Join(' ', _sendSyncInTo.ToArray())}");

                    // send sync_in to all targetted devices if launch is pending.
                    if (executionInformation.Launch)
                    {
                        log.Enqueue($"launching: {string.Join(' ', executionInformation.Devices)}");
                        SendSyncIn(executionInformation.Devices);
                    }


                    while (true)
                    {
                        // check if we sync outs from devices of curent movement.
                        if (waitingForSyncOutsFrom.All(syncOut => _gotSyncOutFrom.Contains(syncOut)))
                        {
                            _gotSyncOutFrom.Clear();
                            SendSyncIn(_sendSyncInTo.ToArray());
                            break;
                        }


                        await Task.Yield();
                    }



                    log.Enqueue($" finish.");
                }
                log.Enqueue($" Queue is finished.");

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
                int maxSize = 100000000;
                int currentSize = _buffer.Count;

                return maxSize - currentSize;
            }
        }

       
    }
}
