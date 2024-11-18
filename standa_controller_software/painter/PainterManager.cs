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


        public RenderLayer CommandLayer;
        public RenderLayer ToolPointLayer;
        public RenderLayer OrientationLayer;
        private LineObjectCollection _lineCollection;

        private List<RenderLayer> _renderLayers { get; set; }


        public PainterManager(CommandManager commandManager, ControllerManager controllerManager, ConcurrentQueue<string> log)
        {
            _log = log;
            _controllerManager = controllerManager;

            _lineCollection = new LineObjectCollection();

            CommandLayer = CreateCommandLayer();
            ToolPointLayer = CreateToolPointLayer();
            OrientationLayer = CreateOrientationArrowsLayer(CommandLayer);
            ToolPointLayer.Camera = CommandLayer.Camera;
            
            _renderLayers = [CommandLayer, ToolPointLayer, OrientationLayer];
        }

        private RenderLayer CreateOrientationArrowsLayer(RenderLayer commandLayer)
        {
            var vertexShaderSource = "C:\\Users\\giedr\\OneDrive\\Desktop\\importsnt\\Csharp\\Standa Stage Control Environment\\standa_controller_software\\openTK_painter\\reference_shaders\\VertexShader_TranlateToEdge.vert";

            var fragmentShaderSource = "C:\\Users\\giedr\\OneDrive\\Desktop\\importsnt\\Csharp\\Standa Stage Control Environment\\standa_controller_software\\ConsoleApplication_For_Tests\\Shaders\\LineDrawingLayer\\FragmentShader.frag";
            var orientationLayer = new RenderLayer(vertexShaderSource, fragmentShaderSource);

            orientationLayer.Camera.IsOrthographic = true;
            orientationLayer.Camera.IsTrackingTool = false;
            orientationLayer.Camera.ReferencePosition = new OpenTK.Mathematics.Vector3(0, 0, 0);
            orientationLayer.Camera.Distance = 20f;
            var referenceLines = new LineObjectCollection() 
            {
                lineWidth = 5f,
            };

            referenceLines.AddLine(new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector4(1, 0, 0, 1));
            referenceLines.AddLine(new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector4(0, 1, 0, 1));
            referenceLines.AddLine(new Vector3(0, 0, 0), new Vector3(0, 0, 1), new Vector4(0, 0, 1, 1));

            orientationLayer.AddObjectCollection(referenceLines);

            return orientationLayer;
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

            var toolIndeces = ToolPointLayer.RenderCollections.FirstOrDefault()?.GetIndices();
            Vector3 currentPointPos = toolIndeces is not null && toolIndeces.Length == 3 ? new Vector3(toolIndeces[0], toolIndeces[1], toolIndeces[2]) : new Vector3(0, 0, 0);
            if (true || _controllerManager.ToolInformation.Position != currentPointPos)
            {
                ToolPointLayer.ClearCollections() ;
                var pointCollection = new PointObjectCollection();
                pointCollection.AddPoint(_controllerManager.ToolInformation.Position, 20, _controllerManager.ToolInformation.IsOn ? new Vector4(1,0,0,1) : new Vector4(1,1,0,1));
                ToolPointLayer.AddObjectCollection(pointCollection);
                //_toolPointLayer.UpdateUniforms();
                ToolPointLayer.InitializeCollections();
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
            
            CommandLayer.ClearCollections();
            _lineCollection.ClearCollection();

            foreach (var commandLine in commandLines)
            {
                commandManager_virtual.ExecuteCommandLine(commandLine).GetAwaiter().GetResult();
            }

            CommandLayer.AddObjectCollection(_lineCollection);
        }


    }
}
