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
        public Definitions Definitions {  get; private set; }
        public FunctionManager(ControllerManager controllerManager, CommandManager commandManager)
        {
            _controllerManager = controllerManager;
            _commandManager = commandManager;

            InitializeDefinitions();
        }

        public void InitializeDefinitions()
        {
            var rules = new Dictionary<Type, Type>
            {
                { typeof(BasePositionerController), typeof(PositionerController_Virtual) },
                { typeof(BaseShutterController), typeof(ShutterController_Virtual) },
                { typeof(BaseSyncController), typeof(SyncController_Sim) },
                { typeof(BaseMasterController), typeof(PositionAndShutterController_Virtual)}
            };
            _controllerManager_virtual = _controllerManager.CreateACopy(rules);
            _commandManager_virtual = new CommandManager(_controllerManager_virtual, new System.Collections.Concurrent.ConcurrentQueue<string>());
            _commandManager_virtual.ClearQueue();
            
            Definitions = new Definitions();
            Definitions.AddFunction("moveA", new MoveAbsolutePositionFunction(_commandManager_virtual, _controllerManager_virtual));
            Definitions.AddFunction("jumpA", new JumpAbsoluteFunction(_commandManager_virtual, _controllerManager_virtual));
            Definitions.AddFunction("arcA", new ArcAbsoluteFunction(_commandManager_virtual, _controllerManager_virtual));
            //Definitions.AddFunction("arcA", new MoveArcAbsoluteFunction(_commandManager_virtual, _controllerManager_virtual));
            //Definitions.AddFunction("shutter", new ChangeShutterStateFunction(_commandManager_virtual, _controllerManager_virtual));
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
