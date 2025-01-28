using Microsoft.Extensions.Logging;
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
        private readonly ILoggerFactory _loggerFactory;
        private readonly ControllerManager _controllerManager;
        private ControllerManager? _controllerManager_virtual;
        private CommandManager? _commandManager_virtual;
        public Definitions Definitions {  get; private set; }
        public FunctionManager(ControllerManager controllerManager, ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _controllerManager = controllerManager;
            Definitions = new Definitions();

            InitializeDefinitions();
        }

        public void InitializeDefinitions()
        {
            _controllerManager_virtual = _controllerManager.CreateAVirtualCopy();
            _commandManager_virtual = new CommandManager(_controllerManager_virtual, _loggerFactory.CreateLogger<CommandManager>());
            _commandManager_virtual.ClearQueue();
            
            Definitions = new Definitions();
            var changeShutterStateFunction = new ChangeShutterStateFunction(_commandManager_virtual, _controllerManager_virtual);
            var changeShutterStateForIntervalFunction = new ChangeShutterStateForIntervalFunction(_commandManager_virtual, _controllerManager_virtual);
            var jumpFuntion = new JumpAbsoluteFunction(_commandManager_virtual, _controllerManager_virtual);
            var lineFunction = new LineAbsoluteFunction(_commandManager_virtual, _controllerManager_virtual, jumpFuntion, changeShutterStateFunction);
            var arcFunction = new ArcAbsoluteFunction(_commandManager_virtual, _controllerManager_virtual, jumpFuntion, changeShutterStateFunction);
            var setPowerFunction = new SetPowerFunction(_commandManager_virtual, _controllerManager_virtual, jumpFuntion);

            Definitions.AddFunction("jumpA", jumpFuntion);
            Definitions.AddFunction("lineA", lineFunction);
            Definitions.AddFunction("arcA", arcFunction);
            Definitions.AddFunction("setPower", setPowerFunction);
            Definitions.AddFunction("shutter", changeShutterStateFunction);
            Definitions.AddFunction("shutterInterval", changeShutterStateForIntervalFunction);
            Definitions.AddFunction("set", new SetDeviceProperty(_controllerManager_virtual, _commandManager_virtual));
            
            Definitions.AddVariable("PI", (float)Math.PI);
            Definitions.AddVariable("null", null);
        }

        public IEnumerable<Command[]> ExtractCommands()
        {
            if(_commandManager_virtual is not null)
                return _commandManager_virtual.GetCommandQueueList();
            else
                return Enumerable.Empty<Command[]>();
        }

        public void ClearCommandQueue()
        {
            _commandManager_virtual?.ClearQueue();
        }
    }
}
