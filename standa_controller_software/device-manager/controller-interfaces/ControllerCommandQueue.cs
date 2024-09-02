namespace standa_controller_software.device_manager.controller_interfaces
{
        public struct QueuedItem
        {
            public Func<Task<bool>> boolCheck;
            public Func<Task> functionBody;
        }
        public class ControllerCommandQueue
        {
            private List<QueuedItem> _queueItems = new List<QueuedItem>();

            public void AddQueueItem(QueuedItem queuedItem)
            {
                _queueItems.Add(queuedItem);
            }

            public void Start()
            {
                Task.Run(() => ProcessQueueItems());
            }

            private async Task ProcessQueueItems()
            {
                while(_queueItems.Count > 0)
                {
                    // check each bool check and execute if needed. 
                    // if bool check fails, remove item.
                    foreach(QueuedItem queuedItem in _queueItems)
                    {
                        if (await queuedItem.boolCheck())
                        {
                            await queuedItem.functionBody.Invoke();
                        }
                        else
                        {
                            _queueItems.Remove(queuedItem);
                        }
                    }
                    await Task.Delay(1);
                }
            }

            public async Task WaintUntilDoneAsync()
            {
                while(_queueItems.Count > 0)
                {
                    await Task.Delay(1);
                }
            }
        }
}
