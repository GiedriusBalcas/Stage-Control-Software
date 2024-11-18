using standa_controller_software.command_manager;
using standa_controller_software.custom_functions.definitions;
using standa_controller_software.device_manager;
using standa_controller_software.device_manager.controller_interfaces.master_controller;
using standa_controller_software.device_manager.controller_interfaces.positioning;
using standa_controller_software.device_manager.controller_interfaces.shutter;
using standa_controller_software.device_manager.controller_interfaces.sync;
using text_parser_library;

namespace standa_controller_software.custom_functions
{
    public class FunctionManager
    {
        private readonly ControllerManager _controllerManager;
        private readonly CommandManager _commandManager;
        private ControllerManager _controllerManager_virtual;
        private CommandManager _commandManager_virtual;
        private float _allocatedTime;
        public Definitions Definitions {  get; private set; }
        public FunctionManager(ControllerManager controllerManager, CommandManager commandManager)
        {
            _controllerManager = controllerManager;
            _commandManager = commandManager;

            InitializeDefinitions();
        }

        public void InitializeDefinitions()
        {
            _controllerManager_virtual = _controllerManager.CreateAVirtualCopy();
            _commandManager_virtual = new CommandManager(_controllerManager_virtual, new System.Collections.Concurrent.ConcurrentQueue<string>());
            _commandManager_virtual.ClearQueue();
            
            Definitions = new Definitions();
            var changeShutterStateFunction = new ChangeShutterStateFunction(_commandManager_virtual, _controllerManager_virtual);
            var changeShutterStateForIntervalFunction = new ChangeShutterStateForIntervalFunction(_commandManager_virtual, _controllerManager_virtual);
            var jumpFuntion = new JumpAbsoluteFunction(_commandManager_virtual, _controllerManager_virtual, changeShutterStateFunction);
            var lineFunction = new LineAbsoluteFunction(_commandManager_virtual, _controllerManager_virtual, jumpFuntion, changeShutterStateFunction);
            var arcFunction = new ArcAbsoluteFunction(_commandManager_virtual, _controllerManager_virtual, jumpFuntion, changeShutterStateFunction);
            var setPowerFunction = new SetPowerFunction(_commandManager_virtual, _controllerManager_virtual, jumpFuntion);

            Definitions.AddFunction("jumpA", jumpFuntion);
            Definitions.AddFunction("lineA", lineFunction);
            Definitions.AddFunction("arcA", arcFunction);
            Definitions.AddFunction("setPower", setPowerFunction);
            Definitions.AddFunction("shutter", changeShutterStateFunction);
            Definitions.AddFunction("shutterInterval", changeShutterStateForIntervalFunction);
            Definitions.AddFunction("set", new SetDeviceProperty(_controllerManager_virtual));
            
            Definitions.AddVariable("PI", (float)Math.PI);
            Definitions.AddVariable("null", null);
        }

        public IEnumerable<Command[]> ExtractCommands()
        {
            return _commandManager_virtual.GetCommandQueueList();
        }

        public void ClearCommandQueue()
        {
            _commandManager_virtual.ClearQueue();
        }
    }
}
