using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using standa_controller_software.command_manager;
using standa_controller_software.device_manager;
using standa_controller_software.device_manager.controller_interfaces;
using standa_controller_software.device_manager.devices.shutter;
using standa_controller_software.device_manager.devices;
using standa_controller_software.painter;
using standa_controller_software.custom_functions;
using text_parser_library;
using opentk_painter_library;
using standa_controller_software.custom_functions.definitions;
using standa_controller_software.device_manager.controller_interfaces.shutter;
using opentk_painter_library.render_objects;
using System.Runtime.CompilerServices;
class Program
{
    private static ControllerManager _controllerManager;
    private static CommandManager _commandManager;
    private static PainterManager _painterManager;
    private static RenderLayer _lineLayer;
    private static RenderLayer _toolPointLayer;

    [STAThread]
    static void Main(string[] args)
    {
        _controllerManager = SetupSystemControllers();

        // Set-up command manager and definitions
        
        _commandManager = new CommandManager(_controllerManager);

        // Set-up Definitions

        var rules = new Dictionary<Type, Type> 
        {
            { typeof(BasePositionerController), typeof(PositionerController_Virtual) },
            { typeof(BaseShutterController), typeof(ShutterController_Virtual) }
        };
        var controllerManager_virtual = _controllerManager.CreateACopy(rules);
        var commandManager_virtual = new CommandManager(controllerManager_virtual);



        var definitions = SetUpFunctionDefinitions(commandManager_virtual, controllerManager_virtual);

        // Set-up text interpreter
        
        var _textInterpreter = new TextInterpreterWrapper
        {
            DefinitionLibrary = definitions,
        };


        // Read text input

        string filePath = "C:\\Users\\giedr\\OneDrive\\Desktop\\importsnt\\Csharp\\Standa Stage Control Environment\\standa_controller_software\\NUnit_tests\\test_scripts\\cube-moveA-function-test-script.txt";
        string fileContent = File.ReadAllText(filePath);

        try
        {
            _textInterpreter.ReadInput(fileContent);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            throw new Exception(_textInterpreter.State.Message);
        }

        // Copy command queue to real command-manager

        foreach (var commandLine in commandManager_virtual.GetCommandQueueList())
        {
            _commandManager.EnqueueCommandLine(commandLine);
        }

        // Create painter-manager

        _painterManager = new PainterManager(_commandManager, _controllerManager);
        var lines =  _painterManager.PaintCommands();
        _lineLayer = _painterManager.GetCommandLayer();
        
        _lineLayer.AddObjectCollection(lines);


        // Run painter
        Task.Run(() => ExecuteCommandQueue(commandManager_virtual, controllerManager_virtual));
        Task.Run(() => _commandManager.UpdateStatesAsync());

        try
        {
            using (var window = new Window(_painterManager.GetRenderLayers()))
            {
                window.Run();
                Console.WriteLine("Display activated");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }


    }

    private static async Task ExecuteCommandQueue(CommandManager commandManager_virtual, ControllerManager controllerManager_virtual)
    {

        Thread.Sleep(1000);

        Console.WriteLine("Before Starting:");
        Console.WriteLine(_commandManager.GetCommandQueueAsString());
        
        _commandManager.Start();

        Console.WriteLine("Log:");
        while (_commandManager.CurrentState == CommandManagerState.Processing)
        {
            Thread.Sleep(100);
            _commandManager.PrintLog();
        }

        Console.WriteLine("After Starting:");
        Console.WriteLine(_commandManager.GetCommandQueueAsString());
    }

    private static Definitions SetUpFunctionDefinitions(CommandManager commandManager, ControllerManager controllerManager)
    {

        var _definitions = new Definitions();
        _definitions.AddFunction("moveA", new MoveAbsolutePositionFunction(commandManager, controllerManager));
        _definitions.AddFunction("shutter", new ChangeShutterStateFunction(commandManager, controllerManager));
        _definitions.AddVariable("PI", (float)Math.PI);

        return _definitions;
    }

    private static ControllerManager SetupSystemControllers()
    {
        var controllerManager = new ControllerManager();

        var controller1 = new VirtualPositionerController("FirstController");
        var controller2 = new VirtualPositionerController("SecondController");
        var controller3 = new VirtualPositionerController("ThirdController");
        var deviceX = new LinearPositionerDevice("x") { Acceleration = 1000000, Deceleration = 1000000, MaxAcceleration = 10000, MaxDeceleration = 10000, MaxSpeed = 1000000, Speed = 200, CurrentPosition = 0, CurrentSpeed = 0 };
        var deviceY = new LinearPositionerDevice("y") { Acceleration = 1000000, Deceleration = 1000000, MaxAcceleration = 10000, MaxDeceleration = 10000, MaxSpeed = 1000000, Speed = 200, CurrentPosition = 0, CurrentSpeed = 0 }; ;
        var deviceZ = new LinearPositionerDevice("z") { Acceleration = 1000000, Deceleration = 1000000, MaxAcceleration = 10000, MaxDeceleration = 10000, MaxSpeed = 1000000, Speed = 200, CurrentPosition = 0, CurrentSpeed = 0 }; ;

        var shutterDevice = new ShutterDevice_Virtual("s") { DelayOff = 5, DelayOn = 5, IsOn = false };
        var shutterController = new VirtualShutterController("Shutter-controller");
        controller1.AddDevice(deviceX);
        controller2.AddDevice(deviceY);
        controller3.AddDevice(deviceZ);
        shutterController.AddDevice(shutterDevice);

        controllerManager.AddController(controller1);
        controllerManager.AddController(controller2);
        controllerManager.AddController(controller3);
        controllerManager.AddController(shutterController);

        var toolPositionFunctionX = (Dictionary<string, float> positions) =>
        {
            return new Vector3()
            {
                X = positions.ContainsKey("x") ? positions["x"] : 0,
                Y = positions.ContainsKey("y") ? positions["y"] : 0,
                Z = positions.ContainsKey("z") ? positions["z"] : 0
            };
        };
        var toolInfo = new ToolInformation(controllerManager.GetDevices<IPositionerDevice>(), shutterDevice, toolPositionFunctionX);
        controllerManager.ToolInformation = toolInfo;

        return controllerManager;
    }

    
}