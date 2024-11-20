using OpenTK.Graphics.ES11;
using standa_controller_software.command_manager;
using standa_controller_software.command_manager.command_parameter_library;
using standa_controller_software.device_manager.controller_interfaces.positioning;
using standa_controller_software.device_manager.controller_interfaces.shutter;
using standa_controller_software.device_manager.controller_interfaces.sync;
using standa_controller_software.device_manager.devices;
using System.Collections.Concurrent;
using System.Threading;

namespace standa_controller_software.device_manager.controller_interfaces.master_controller
{
    public partial class PositionAndShutterController_Pico : BaseMasterPositionerAndShutterController
    {
        
        private SyncController_Pico _syncController;
        public PositionAndShutterController_Pico(string name, ConcurrentQueue<string> log)  : base(name, log)
        {
            
        }

        public override void AddSlaveController(BaseController controller, SemaphoreSlim controllerLock)
        {
            if (controller is BaseShutterController shutterController)
            {
                SlaveControllers.Add(shutterController.Name, shutterController);
                SlaveControllersLocks.Add(shutterController.Name, controllerLock);
            }
            else if (controller is BasePositionerController positionerController)
            {
                SlaveControllers.Add(positionerController.Name, positionerController);
                SlaveControllersLocks.Add(positionerController.Name, controllerLock);
            }
            else if (controller is SyncController_Pico syncController)
            {

                SlaveControllers.Add(syncController.Name, syncController);
                SlaveControllersLocks.Add(syncController.Name, controllerLock);
                _syncController = syncController;
                _syncController.BufferHasFreeSpace += async () => await OnSyncControllerBufferSpaceAvailable();
                _syncController.ExecutionCompleted += () => OnSyncControllerExecutionEnd();
                _syncController.LastBufferItemTaken += () => OnSyncControllerLastBufferItemTaken();

            }
        }
        public override Task ForceStop()
        {
            _processingCompletionSource.TrySetResult(true);
            _processingLastItemTakenSource.TrySetResult(true);
            return Task.CompletedTask;

        }
        private void OnSyncControllerExecutionEnd()
        {
            if(_processingCompletionSource is not null)
                _processingCompletionSource.TrySetResult(true);
            if (_processingLastItemTakenSource is not null)
                _processingLastItemTakenSource.TrySetResult(true);

            _log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: master: Sync controller signaled that execution finalized.");
        }
        private void OnSyncControllerLastBufferItemTaken()
        {
            if(_processingLastItemTakenSource is not null)
                _processingLastItemTakenSource.TrySetResult(true);

            _log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: master: Sync controller signaled that last item was taken.");
        }
        private async Task OnSyncControllerBufferSpaceAvailable()
        {
           _log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: master: Sync controller signaled buffer has free slot");
            await SendCommandIfAvailable();
        }
        protected override Task Stop(Command command, SemaphoreSlim semaphore)
        {
            _log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: master: stop encountered.");
            return Task.CompletedTask;
            //_buffer.Clear();
            //_launchPending = true;
            //_processingCompletionSource?.TrySetResult(true);
            //_processingLastItemTakenSource?.TrySetResult(true);
            //// lets ask the sync controller to just launch 40 times for now.
            //// TODO: do something better here.

            //char[] deviceNames = SlaveControllers.Values
            //    .Where(controller => controller is BasePositionerController)
            //    .SelectMany(controller => controller.GetDevices())
            //    .Select(device => device.Name)
            //    .ToArray();




            //await _syncController.AddBufferItem(
            //deviceNames,
            //true,
            //1, //   [ms]
            //false,
            //0,
            //0);
            //for (int i = 0; i < 40; i++)
            //{
            //    await _syncController.AddBufferItem(
            //    deviceNames,
            //    false,
            //    1, //   [ms]
            //    false,
            //    0,
            //    0);
            //}



            //// Set the flag to indicate processing has started
            //_processingCompletionSource = new TaskCompletionSource<bool>();
            //_processingLastItemTakenSource = new TaskCompletionSource<bool>();

            //await _syncController.StartExecution();

            //foreach (var (controllerName, slaveSemaphore) in SlaveControllersLocks)
            //{
            //    if (slaveSemaphore.CurrentCount == 0)
            //        slaveSemaphore.Release();
            //}
            //await _processingCompletionSource.Task;

            //_processingCompletionSource?.TrySetResult(true);
            //_processingLastItemTakenSource?.TrySetResult(true);



        }
    }
}
