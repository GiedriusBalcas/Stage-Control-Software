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

        var rules = new Dictionary<Type, Type> { { typeof(BasePositionerController), typeof(PositionerController_Virtual) } };
        var controllerManager_virtual = _controllerManager.CreateACopy(rules);
        var commandManager_virtual = new CommandManager(controllerManager_virtual);



        var definitions = SetUpFunctionDefinitions(commandManager_virtual, controllerManager_virtual);

        // Set-up text interpreter
        
        var _textInterpreter = new TextInterpreterWrapper
        {
            DefinitionLibrary = definitions,
        };

        
        // Read text input
        
        string fileContent = "width = 500;\r\nlength = 150;\r\nheight = 100;\r\n\r\n\r\nfor( k=1; k<10; k++)\r\n{\r\n\tfor( i=1; i<10; i++)\r\n\t{\r\n\t\tdirection = (-1)^i;\r\n\t\tif(direction == 1)\r\n\t\t{\r\n\t\t\tshutter(\"s\", 0);\r\n\t\t}\r\n\t\telse\r\n\t\t{\r\n\t\t\tshutter(\"s\", 1);\r\n\r\n\t\t}\r\n\r\n\t\txas = i*width/10 - width/2;\r\n\t\tyas = direction*length/2;\r\n\t\tzas = height/10*k;\r\n\t\tmoveA(\"xyz\", xas, yas, zas);\r\n\t\t\r\n\t}\r\n}\r\n";

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
        
        //var pointCollection = new PointObjectCollection();
        //pointCollection.AddPoint(_controllerManager.ToolInformation.Position, 50, new Vector4(0, 1, 1, 1));
        
        //_toolPointLayer.AddObjectCollection(pointCollection);


        Task.Run(() => ExecuteCommandQueue(commandManager_virtual, controllerManager_virtual));
        // Run painter

        try
        {
            using (var window = new Window(_painterManager.GetRenderLayers()))
            {
                window.Run();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }


    }

    private static async Task ExecuteCommandQueue(CommandManager commandManager_virtual, ControllerManager controllerManager_virtual)
    {

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

            var posX = _controllerManager.TryGetDevice<IPositionerDevice>("x", out IPositionerDevice deviceX) ? deviceX.Position : 0;
            var posY = _controllerManager.TryGetDevice<IPositionerDevice>("y", out IPositionerDevice deviceY) ? deviceY.Position : 0;
            var posZ = _controllerManager.TryGetDevice<IPositionerDevice>("z", out IPositionerDevice deviceZ) ? deviceZ.Position : 0;
            Console.WriteLine($"x: {posX} \t y: {posY} \t z: {posZ}");
        }

        Console.WriteLine("After Starting:");
        Console.WriteLine(_commandManager.GetCommandQueueAsString());

        Console.WriteLine("Log:");

        _commandManager.PrintLog();

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

        var controller = new VirtualPositionerController("FirstController");
        var deviceX = new LinearPositionerDevice("x") { Acceleration = 10000000, Deceleration = 10000000, MaxAcceleration = 200000000000, MaxDeceleration = 4000, MaxSpeed = 200, Position = 0, Speed = 200 };
        var deviceY = new LinearPositionerDevice("y") { Acceleration = 10000000, Deceleration = 10000000, MaxAcceleration = 200000000000, MaxDeceleration = 4000, MaxSpeed = 200, Position = 0, Speed = 200 }; ;
        var deviceZ = new LinearPositionerDevice("z") { Acceleration = 10000000, Deceleration = 10000000, MaxAcceleration = 200000000000, MaxDeceleration = 4000, MaxSpeed = 200, Position = 0, Speed = 200 }; ;

        var shutterDevice = new ShutterDevice_Virtual("s") { DelayOff = 0, DelayOn = 0, IsOn = false };
        var controller2 = new VirtualPositionerController("SecondController");
        var shutterController = new ShutterController_Virtual("Shutter-controller");
        controller.AddDevice(deviceX);
        controller.AddDevice(deviceY);
        controller2.AddDevice(deviceZ);
        shutterController.AddDevice(shutterDevice);

        controllerManager.AddController(controller);
        controllerManager.AddController(controller2);
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
        var toolInfo = new ToolInformation(controllerManager.GetDevices<IPositionerDevice>(), new ShutterDevice_Virtual("s"), toolPositionFunctionX);
        controllerManager.ToolInformation = toolInfo;

        return controllerManager;
    }

    
}