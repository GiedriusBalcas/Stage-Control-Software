using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using opentk_painter_library;
using opentk_painter_library.common;
using opentk_painter_library.render_objects;
using standa_controller_software.command_manager;
using standa_controller_software.device_manager;
using standa_controller_software.device_manager.controller_interfaces;
using standa_controller_software.device_manager.devices.shutter;
using standa_controller_software.device_manager.devices;
using standa_controller_software.painter;
using static Antlr4.Runtime.Atn.SemanticContext;
using standa_controller_software.custom_functions;
using text_parser_library;
class Program
{
    private static ControllerManager _controllerManager;
    private static CommandManager _commandManager;
    private static PainterManager _painterManager;
    private static RenderLayer _renderLayer;

    [STAThread]
    static void Main(string[] args)
    {
        _controllerManager = new ControllerManager();

        var controller = new VirtualPositionerController("FirstController");
        var deviceX = new LinearPositionerDevice("x") { Acceleration = 1000, Deceleration = 1000, MaxAcceleration = 2000, MaxDeceleration = 4000, MaxSpeed = 200, Position = 0, Speed = 200 };
        var deviceY = new LinearPositionerDevice("y") { Acceleration = 1000, Deceleration = 1000, MaxAcceleration = 2000, MaxDeceleration = 4000, MaxSpeed = 200, Position = 0, Speed = 200 }; ;
        var deviceZ = new LinearPositionerDevice("z") { Acceleration = 1000, Deceleration = 1000, MaxAcceleration = 2000, MaxDeceleration = 4000, MaxSpeed = 200, Position = 0, Speed = 200 }; ;

        var controller2 = new VirtualPositionerController("SecondController");
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


        var rules = new Dictionary<Type, Type> { { typeof(BasePositionerController), typeof(PositionerController_Virtual) } };
        var controllerManager_virtual = _controllerManager.CreateACopy(rules);
        var commandManager_virtual = new CommandManager(controllerManager_virtual);

        var _definitions = new Definitions();
        _definitions.AddFunction("moveA", new MoveAbsolutePositionFunction(commandManager_virtual, controllerManager_virtual));
        _definitions.AddVariable("PI", (float)Math.PI);

        // Set-up text interpreter
        var _textInterpreter = new TextInterpreterWrapper
        {
            DefinitionLibrary = _definitions
        };

        //string filePath = Path.Combine(/*TestContext.CurrentContext.TestDirectory*/, "test_scripts", "moveA-function-test-script.txt");
        string fileContent = "moveA(\"xyz\", 10,50,50);\r\nmoveA(\"x\", 0);\r\nmoveA(\"y\", 0);\r\nmoveA(\"z\", 0); ";


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



        foreach (var commandLine in commandManager_virtual.GetCommandQueueList())
        {
            _commandManager.EnqueueCommandLine(commandLine);
        }


        _painterManager = new PainterManager(_commandManager, _controllerManager);
        var lines =  _painterManager.PaintCommands();
        _renderLayer = _painterManager.CreateCommandLayer();

        //lineCollection.InitializeBuffers();
        _renderLayer.AddObjectCollection(lines);

        try
        {
            using (var window = new Window([_renderLayer]))
            {
                window.Run();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    
}