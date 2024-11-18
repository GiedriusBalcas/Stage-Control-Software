using opentk_painter_library.common;
using opentk_painter_library.render_objects;
using opentk_painter_library;
using standa_controller_software.command_manager;
using standa_controller_software.device_manager;
using standa_controller_software.device_manager.controller_interfaces;
using System.Numerics;
using System;
using standa_controller_software.device_manager.devices.shutter;
using standa_controller_software.device_manager.controller_interfaces.shutter;
using standa_controller_software.device_manager.controller_interfaces.positioning;
using standa_controller_software.device_manager.controller_interfaces.master_controller;
using standa_controller_software.device_manager.controller_interfaces.sync;
using System.Collections.Concurrent;

namespace standa_controller_software.painter
{
    public class PainterManager
    {
        private readonly ConcurrentQueue<string> _log;
        private readonly ControllerManager _controllerManager;


        private RenderLayer _commandLayer;
        private RenderLayer _toolPointLayer;
        private RenderLayer _orientationLayer;
        private LineObjectCollection _lineCollection;

        private List<RenderLayer> _renderLayers { get; set; }


        public PainterManager(CommandManager commandManager, ControllerManager controllerManager, ConcurrentQueue<string> log)
        {
            _log = log;
            _controllerManager = controllerManager;

            _lineCollection = new LineObjectCollection();

            _commandLayer = CreateCommandLayer();
            _toolPointLayer = CreateToolPointLayer();
            _orientationLayer = CreateOrientationArrowsLayer();
            _toolPointLayer.Camera = _commandLayer.Camera;

            _renderLayers = [_commandLayer, _toolPointLayer];
        }

        private RenderLayer CreateOrientationArrowsLayer()
        {
            var vertexShaderSource = "C:\\Users\\giedr\\OneDrive\\Desktop\\importsnt\\Csharp\\Standa Stage Control Environment\\standa_controller_software\\ConsoleApplication_For_Tests\\Shaders\\LineDrawingLayer\\VertexShader.vert";

            var fragmentShaderSource = "C:\\Users\\giedr\\OneDrive\\Desktop\\importsnt\\Csharp\\Standa Stage Control Environment\\standa_controller_software\\ConsoleApplication_For_Tests\\Shaders\\LineDrawingLayer\\FragmentShader.frag";

            return new RenderLayer(vertexShaderSource, fragmentShaderSource);
        }

        private RenderLayer CreateToolPointLayer()
        {
            var vertexShaderSource = "C:\\Users\\giedr\\OneDrive\\Desktop\\importsnt\\Csharp\\Standa Stage Control Environment\\standa_controller_software\\ConsoleApplication_For_Tests\\Shaders\\LineDrawingLayer\\VertexShader.vert";

            var fragmentShaderSource = "C:\\Users\\giedr\\OneDrive\\Desktop\\importsnt\\Csharp\\Standa Stage Control Environment\\standa_controller_software\\ConsoleApplication_For_Tests\\Shaders\\LineDrawingLayer\\FragmentShader.frag";

            return new RenderLayer(vertexShaderSource, fragmentShaderSource, UpdateToolPointLayer);
        }

        public List<RenderLayer> GetRenderLayers()
        {
            return _renderLayers;
        }


        public void UpdateToolPointLayer()
        {
            _controllerManager.ToolInformation.RecalculateToolPosition() ; // Recalculate positions before drawing

            var toolIndeces = _toolPointLayer.RenderCollections.FirstOrDefault()?.GetIndices();
            Vector3 currentPointPos = toolIndeces is not null && toolIndeces.Length == 3 ? new Vector3(toolIndeces[0], toolIndeces[1], toolIndeces[2]) : new Vector3(0, 0, 0);
            if (true || _controllerManager.ToolInformation.Position != currentPointPos)
            {
                _toolPointLayer.ClearCollections() ;
                var pointCollection = new PointObjectCollection();
                pointCollection.AddPoint(_controllerManager.ToolInformation.Position, 20, _controllerManager.ToolInformation.IsOn ? new Vector4(1,0,0,1) : new Vector4(1,1,0,1));
                _toolPointLayer.AddObjectCollection(pointCollection);
                //_toolPointLayer.UpdateUniforms();
                _toolPointLayer.InitializeCollections();
            }
        }


        public RenderLayer CreateCommandLayer()
        {

            var vertexShaderSource = "C:\\Users\\giedr\\OneDrive\\Desktop\\importsnt\\Csharp\\Standa Stage Control Environment\\standa_controller_software\\ConsoleApplication_For_Tests\\Shaders\\LineDrawingLayer\\VertexShader.vert";

            var fragmentShaderSource = "C:\\Users\\giedr\\OneDrive\\Desktop\\importsnt\\Csharp\\Standa Stage Control Environment\\standa_controller_software\\ConsoleApplication_For_Tests\\Shaders\\LineDrawingLayer\\FragmentShader.frag";

            var renderLayer = new RenderLayer(vertexShaderSource, fragmentShaderSource);
            renderLayer.AddObjectCollection(_lineCollection);

            return new RenderLayer(vertexShaderSource, fragmentShaderSource);
        }

        
        public void PaintCommandQueue(IEnumerable<Command[]> commandLines)
        {
            
            var controllerManager_virtual = _controllerManager.CreateAVirtualCopy();


            var masterPainterController = new PositionAndShutterController_Painter("painter",_log, _lineCollection, controllerManager_virtual.ToolInformation);
            controllerManager_virtual.AddController(masterPainterController);
            foreach(var (controllerName, controller) in controllerManager_virtual.Controllers)
            {
                if(controller != masterPainterController)
                {
                    controller.MasterController = masterPainterController;
                    masterPainterController.AddSlaveController(controller, controllerManager_virtual.ControllerLocks[controllerName]);
                }
            }
            
            var commandManager_virtual = new CommandManager(controllerManager_virtual, new System.Collections.Concurrent.ConcurrentQueue<string>());
            
            _commandLayer.ClearCollections();
            _lineCollection.ClearCollection();

            foreach (var commandLine in commandLines)
            {
                commandManager_virtual.ExecuteCommandLine(commandLine).GetAwaiter().GetResult();
            }

            _commandLayer.AddObjectCollection(_lineCollection);
        }


    }
}
