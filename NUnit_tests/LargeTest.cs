
using standa_controller_software.command_manager;
using standa_controller_software.device_manager;
using standa_controller_software.device_manager.controllers;
using standa_controller_software.device_manager.devices;
using standa_controller_software.custom_functions;
using System.Windows.Input;
using text_parser_library;
using NUnit.Framework.Internal.Commands;

namespace NUnit_tests
{
    internal class LargeTest
    {
        private ControllerManager _controllerManager;
        private CommandManager _commandManager;
        private ParserWrapper _textInterpreter;

        [SetUp]
        public void Setup()
        {
            _controllerManager = new ControllerManager();
            _commandManager = new CommandManager(_controllerManager);
            _textInterpreter = new ParserWrapper();

            var command = new MoveAbsolutePositionFunction(_commandManager,_controllerManager);
            _textInterpreter.DefinitionLibrary.AddFunction("moveA", command);

            var controller = new VirtualPositionerController("FirstController");
            var deviceX = new LinearPositionerDevice("x");
            var deviceY = new LinearPositionerDevice("y");
            var deviceZ = new LinearPositionerDevice("z");

            var controller2 = new VirtualPositionerController("SecondController");


            controller.AddDevice(deviceX);
            controller.AddDevice(deviceY);
            controller2.AddDevice(deviceZ);

            _controllerManager.AddController(controller);
            _controllerManager.AddController(controller2);
        }

        [Test]
        public void XYPositioningTest_Test()
        {
            string filePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "test_scripts", "moveA-function-test-script.txt");
            string fileContent = File.ReadAllText(filePath);

            _textInterpreter.ReadInput(fileContent);

            Console.WriteLine("Before Starting:");
            Console.WriteLine(_commandManager.GetCommandQueue());

            _commandManager.Start();

            var currentQueue = _commandManager.GetCommandQueue();
            while (currentQueue != string.Empty)
            {
                Thread.Sleep(1000);
                currentQueue = _commandManager.GetCommandQueue();
            }

            //Thread.Sleep(10000);

            Console.WriteLine("After Starting:");
            Console.WriteLine(_commandManager.GetCommandQueue());

            Console.WriteLine("Log:");

            _commandManager.PrintLog();

            Assert.Pass();

        }
    }
}
