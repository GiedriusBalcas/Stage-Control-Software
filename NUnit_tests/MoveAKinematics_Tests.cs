
using standa_controller_software.command_manager;
using standa_controller_software.device_manager;
using standa_controller_software.device_manager.controller_interfaces;
using standa_controller_software.device_manager.devices;
using standa_controller_software.custom_functions;
using System.Windows.Input;
using text_parser_library;
using NUnit.Framework.Internal.Commands;
using System.Numerics;
using standa_controller_software.device_manager.devices.shutter;

namespace NUnit_tests
{
    internal class MoveAKinematics_Tests
    {
        /// <summary>
        /// Testing command queue formation for moveA multi-axis linear movements with dynamic kinematic parameters.
        /// We will need virtual controller manager, that holds the information of the positioners.
        /// Let's create a shallow copy method in the controller manager ? no, cause I will need to change their types for the painter config. Unless I could crate a map [whichTypeToChange] = "TypeToChangeTo" to copy the current system interchanging it's components with virtual controllers. NAH cant come up with a type safe solution.
        /// </summary>
        /// 

        /// Create ControllerManager
        /// Create VirtualControllerManager
        /// Create CommandManager
        /// CreateTextParser
        /// Parse Text Input using VirtualControllerManager?-> Generate Command Queue in CommandManager
        /// Draw with CommandQueue
        /// Execute 

        private ControllerManager _controllerManager;
        private CommandManager _commandManager;
        private TextInterpreterWrapper _textInterpreter;
        private Definitions _definitions;

        [SetUp]
        public void Setup()
        {

            // First, set-up controller manager. This acts as the one got from the system configuration.
            _controllerManager = new ControllerManager();

            var controller = new PositionerController_Virtual("FirstController");
            var deviceX = new LinearPositionerDevice("x") { Acceleration = 10000000, Deceleration = 10000000, MaxAcceleration = 20000000, MaxDeceleration = 4000000000, MaxSpeed = 20000000, Position = 0, Speed = 2000000 };
            var deviceY = new LinearPositionerDevice("y") { Acceleration = 100000, Deceleration = 1000, MaxAcceleration = 2000, MaxDeceleration = 400000, MaxSpeed = 2000, Position = 0, Speed = 2000 }; ;
            var deviceZ = new LinearPositionerDevice("z") { Acceleration = 100000, Deceleration = 1000, MaxAcceleration = 2000, MaxDeceleration = 400000, MaxSpeed = 2000, Position = 0, Speed = 2000 }; ;

            var controller2 = new PositionerController_Virtual("SecondController");
            controller.AddDevice(deviceX);
            controller.AddDevice(deviceY);
            controller2.AddDevice(deviceZ);

            _controllerManager.AddController(controller);
            _controllerManager.AddController(controller2);
            
            var toolPositionFunctionX = (Dictionary<string, float> positions) =>
            {
                return new Vector3()
                {
                    X = positions.ContainsKey("x") ? positions["x"] : 0,
                    Y = positions.ContainsKey("y") ? positions["y"] : 0,
                    Z = positions.ContainsKey("z") ? positions["z"] : 0
                };
            };
            var toolInfo = new ToolInformation(_controllerManager.GetDevices<IPositionerDevice>(), new ShutterDevice_Virtual("s"), toolPositionFunctionX);
            _controllerManager.ToolInformation = toolInfo;



            // Set-up command manager and definitions
            _commandManager = new CommandManager(_controllerManager);

            
        }

        [Test]
        public void XYPositioningTest_Test()
        {
            var rules = new Dictionary<Type, Type> { { typeof(BasePositionerController), typeof(PositionerController_Virtual) } };
            var controllerManager_virtual = _controllerManager.CreateACopy(rules);
            var commandManager_virtual = new CommandManager(controllerManager_virtual);
            
            _definitions = new Definitions();
            _definitions.AddFunction("moveA", new MoveAbsolutePositionFunction(commandManager_virtual, controllerManager_virtual));
            _definitions.AddVariable("PI", (float)Math.PI);

            // Set-up text interpreter
            _textInterpreter = new TextInterpreterWrapper
            {
                DefinitionLibrary = _definitions
            };

            string filePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "test_scripts", "cube-moveA-function-test-script.txt");
            string fileContent = File.ReadAllText(filePath);


            //_textInterpreter.ReadInput(fileContent);

            try
            {
                _textInterpreter.ReadInput(fileContent);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw new Exception(_textInterpreter.State.Message);
            }

            Console.WriteLine("Before Starting:");

            var queuelog = commandManager_virtual.GetCommandQueueAsString();

            Console.WriteLine(commandManager_virtual.GetCommandQueueAsString());


            foreach (var commandLine in commandManager_virtual.GetCommandQueueList())
            {
                _commandManager.EnqueueCommandLine(commandLine);
            }
            Task.Run(() => _commandManager.UpdateStatesAsync());
            _commandManager.Start();


            var currentQueue = _commandManager.GetCommandQueueAsString();
            while (_commandManager.CurrentState == CommandManagerState.Processing)
            {
                Thread.Sleep(100);
                currentQueue = _commandManager.GetCommandQueueAsString();

                var posX = _controllerManager.TryGetDevice<IPositionerDevice>("x", out IPositionerDevice deviceX)? deviceX.Position : 0;
                var posY = _controllerManager.TryGetDevice<IPositionerDevice>("y", out IPositionerDevice deviceY)? deviceY.Position : 0;
                var posZ = _controllerManager.TryGetDevice<IPositionerDevice>("z", out IPositionerDevice deviceZ)? deviceZ.Position : 0;
                Console.WriteLine($"x: {posX} \t y: {posY} \t z: {posZ}");
            }

            Console.WriteLine("After Starting:");
            Console.WriteLine(_commandManager.GetCommandQueueAsString());

            Console.WriteLine("Log:");

            _commandManager.PrintLog();

            Assert.Pass();

        }

    }
}
