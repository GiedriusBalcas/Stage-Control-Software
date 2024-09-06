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
using standa_controller_software.device_manager.controller_interfaces.positioning;
using System.Timers;
using standa_controller_software.device_manager.controller_interfaces.master_controller;
class Program
{
    private static ControllerManager _controllerManager;
    private static CommandManager _commandManager;
    private static PainterManager _painterManager;
    private static RenderLayer _lineLayer;
    private static RenderLayer _toolPointLayer;
    private static FunctionManager _functionDefinitionLibrary;
    private static TextInterpreterWrapper _textInterpreter;
    private static TaskCompletionSource<bool> _readTextCompletionSource = new TaskCompletionSource<bool>();
    private static System.Timers.Timer _checkCompletionTimer;
    [STAThread]
    static void Main(string[] args)
    {
        _controllerManager = SetupSystemControllers();
        _commandManager = new CommandManager(_controllerManager);

        Task.Run(() => _commandManager.UpdateStatesAsync());

        _functionDefinitionLibrary = new FunctionManager(_controllerManager, _commandManager);
        _textInterpreter = new TextInterpreterWrapper() { DefinitionLibrary = _functionDefinitionLibrary.Definitions };
        _painterManager = new PainterManager(_commandManager, _controllerManager);

        ReadText();

        Task.Run(() => ExecuteCommandQueue());


        LaunchWindow();
        // Start the console input handling in a separate thread
        //var inputThread = new Thread(() => HandleTestsInput());
        //inputThread.Start();

        // Run the window on the main thread

    }

    private static void LaunchWindow()
    {
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
    

    private static void ReadText()
    {

        string filePath = "C:\\Users\\giedr\\OneDrive\\Desktop\\importsnt\\Csharp\\Standa Stage Control Environment\\standa_controller_software\\NUnit_tests\\test_scripts\\masterController-test-script.txt";
        var inputText = File.ReadAllText(filePath);
        
        try
        {
            _functionDefinitionLibrary.InitializeDefinitions();
            _textInterpreter.ReadInput(inputText);
            var commands = _functionDefinitionLibrary.ExtractCommands();
            _painterManager.PaintCommandQueue(commands);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private static async void ExecuteCommandQueue()
    {

        Thread.Sleep(500);

        Console.WriteLine("Before Starting:");
        Console.WriteLine(_commandManager.GetCommandQueueAsString());

        _commandManager.ClearQueue();
        foreach (var commandLine in _functionDefinitionLibrary.ExtractCommands())
        {
            _commandManager.EnqueueCommandLine(commandLine);
        }
        _ = Task.Run(() => _commandManager.Start());
        _ = Task.Run(() => {
            do
            {
                Thread.Sleep(200);
                _commandManager.PrintLog();
            } while (_commandManager.CurrentState == CommandManagerState.Processing);
        });

        Console.WriteLine("Log:");
        

        Console.WriteLine("After Starting:");
        Console.WriteLine(_commandManager.GetCommandQueueAsString());
    }

    private static Definitions SetUpFunctionDefinitions(CommandManager commandManager, ControllerManager controllerManager)
    {

        var _definitions = new Definitions();
        _definitions.AddFunction("moveA", new MoveAbsolutePositionFunction(commandManager, controllerManager));
        //_definitions.AddFunction("shutter", new ChangeShutterStateFunction(commandManager, controllerManager));
        _definitions.AddVariable("PI", (float)Math.PI);

        return _definitions;
    }

    private static ControllerManager SetupSystemControllers()
    {


        var deviceX = new LinearPositionerDevice('x', "") { Acceleration = 100000, Deceleration = 100000, MaxAcceleration = 100000, MaxDeceleration = 100000, MaxSpeed = 5000, Speed = 200, CurrentPosition = 0, CurrentSpeed = 0 };
        var deviceY = new LinearPositionerDevice('y', "") { Acceleration = 100000, Deceleration = 100000, MaxAcceleration = 100000, MaxDeceleration = 100000, MaxSpeed = 5000, Speed = 200, CurrentPosition = 0, CurrentSpeed = 0 }; 
        var deviceZ = new LinearPositionerDevice('z', "") { Acceleration = 100000, Deceleration = 100000, MaxAcceleration = 100000, MaxDeceleration = 100000, MaxSpeed = 5000, Speed = 200, CurrentPosition = 0, CurrentSpeed = 0 }; 

        var shutterDevice = new ShutterDevice('s', "") { DelayOff = 50, DelayOn = 50, IsOn = false };

        var masterController = new PositionAndShutterController_Sim("master") { IsQuable = true };
        var posController1 = new PositionerController_Sim("FirstController") { MasterController = masterController };
        var posController2 = new PositionerController_Sim("SecondController") { MasterController = masterController };
        var shutterController = new ShutterController_Virtual("Shutter-controller");

        posController1.AddDevice(deviceX);
        posController1.AddDevice(deviceY);
        posController2.AddDevice(deviceZ);
        shutterController.AddDevice(shutterDevice);

        masterController.AddSlaveController(posController1);
        masterController.AddSlaveController(posController2);

        var controllerManager = new ControllerManager();
        controllerManager.AddController(posController1);
        controllerManager.AddController(posController2);
        controllerManager.AddController(shutterController);
        controllerManager.AddController(masterController);

        var toolPositionFunctionX = (Dictionary<char, float> positions) =>
        {
            return new Vector3()
            {
                X = positions.ContainsKey('x') ? positions['x'] : 0,
                Y = positions.ContainsKey('y') ? positions['y'] : 0,
                Z = positions.ContainsKey('z') ? positions['z'] : 0
            };
        };
        var toolInfo = new ToolInformation(controllerManager.GetDevices<BasePositionerDevice>(), shutterDevice, toolPositionFunctionX);
        controllerManager.ToolInformation = toolInfo;

        return controllerManager;
    }

    
}