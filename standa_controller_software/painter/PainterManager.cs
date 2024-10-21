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

namespace standa_controller_software.painter
{
    public class PainterManager
    {
        private readonly CommandManager _commandManager;
        private readonly ControllerManager _controllerManager;


        private OrbitalCamera _camera;
        private OrbitalCamera _refCamera;
        private RenderLayer _commandLayer;
        private RenderLayer _toolPointLayer;
        private LineObjectCollection _lineCollection;

        public Vector4 LineColor { get; set; } = new Vector4(0, 0, 1, 1);

        private List<RenderLayer> _renderLayers { get; set; }
        public bool IsTrackingTool;
        public bool IsOrthographic
        {
            get => _camera.IsOrthographic;
            set
            {
                _camera.IsOrthographic = value;
            }
        }


        public PainterManager(CommandManager commandManager, ControllerManager controllerManager)
        {
            _commandManager = commandManager;
            _controllerManager = controllerManager;

            _lineCollection = new LineObjectCollection();

            _commandLayer = CreateCommandLayer();
            _toolPointLayer = CreateToolPointLayer();
            _toolPointLayer.Camera = _commandLayer.Camera;

            _renderLayers = [_commandLayer, _toolPointLayer];
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

        public RenderLayer GetCommandLayer()
        {
            return _commandLayer;
        }

        public void UpdateToolPointLayer()
        {
            _controllerManager.ToolInformation.RecalculateToolPosition() ; // Recalculate positions before drawing

            var toolIndeces = _toolPointLayer.RenderCollections.FirstOrDefault()?.GetIndices();
            Vector3 currentPointPos = toolIndeces is not null && toolIndeces.Length == 3 ? new Vector3(toolIndeces[0], toolIndeces[1], toolIndeces[2]) : new Vector3(0, 0, 0);
            if (_controllerManager.ToolInformation.Position != currentPointPos)
            {
                _toolPointLayer.ClearCollections() ;
                var pointCollection = new PointObjectCollection();
                pointCollection.AddPoint(_controllerManager.ToolInformation.Position, 20, _controllerManager.ToolInformation.IsOn ? new Vector4(1,0,0,1) : new Vector4(1,1,0,1));
                _toolPointLayer.AddObjectCollection(pointCollection);
                //_toolPointLayer.UpdateUniforms();
                _toolPointLayer.InitializeCollections();
            }
        }

        public void AddLine(Vector3 start, Vector3 end)
        {
            
            _lineCollection.AddLine(start, end, LineColor);
        }

        public void ClearCollections()
        {
            _commandLayer.DisposeBuffers();

            _commandLayer.RenderCollections.Clear();
            _lineCollection.ClearCollection();
        }

        public RenderLayer CreateCommandLayer()
        {

            var vertexShaderSource = "C:\\Users\\giedr\\OneDrive\\Desktop\\importsnt\\Csharp\\Standa Stage Control Environment\\standa_controller_software\\ConsoleApplication_For_Tests\\Shaders\\LineDrawingLayer\\VertexShader.vert";

            var fragmentShaderSource = "C:\\Users\\giedr\\OneDrive\\Desktop\\importsnt\\Csharp\\Standa Stage Control Environment\\standa_controller_software\\ConsoleApplication_For_Tests\\Shaders\\LineDrawingLayer\\FragmentShader.frag";

            var renderLayer = new RenderLayer(vertexShaderSource, fragmentShaderSource);
            renderLayer.AddObjectCollection(_lineCollection);

            return new RenderLayer(vertexShaderSource, fragmentShaderSource);
        }

        public LineObjectCollection PaintCommands()
        {
            var rules = new Dictionary<Type, Type> 
            {
                { typeof(BasePositionerController), typeof(PositionerController_Virtual) },
                { typeof(BaseShutterController), typeof(ShutterController_Virtual) }
            };
            var controllerManager_virtual = _controllerManager.CreateACopy(rules);
            var commandManager_virtual = new CommandManager(controllerManager_virtual, new System.Collections.Concurrent.ConcurrentQueue<string>());

            var commandLines = _commandManager.GetCommandQueueList();
            var renderObjects = new LineObjectCollection();
            
            bool wasEngaged = false;
            controllerManager_virtual.ToolInformation.EngagedStateChanged += () => wasEngaged = true;
            foreach (var commandLine in commandLines)
            {
                wasEngaged = false;
                var startPositions = controllerManager_virtual.ToolInformation.CalculateToolPositionUpdate();
                commandManager_virtual.ExecuteCommandLine(commandLine).GetAwaiter().GetResult();

                var endPositions = controllerManager_virtual.ToolInformation.CalculateToolPositionUpdate();

                if (controllerManager_virtual.ToolInformation.IsOn)
                    wasEngaged = true;

                var lineColor = wasEngaged
                    ? new Vector4(1, 0, 0, 1)
                    : new Vector4(1, 1, 0, 1);

                if(endPositions != startPositions)
                    renderObjects.AddLine(startPositions, endPositions, lineColor);
            }

            return renderObjects;
        }
        public void PaintCommandQueue(IEnumerable<Command[]> commandLines)
        {
            var rules = new Dictionary<Type, Type>
            {
                { typeof(BasePositionerController), typeof(PositionerController_Virtual) },
                { typeof(BaseShutterController), typeof(ShutterController_Virtual) },
                { typeof(BaseSyncController), typeof(SyncController_Sim) },
                { typeof(BaseMasterController), typeof(PositionAndShutterController_Virtual) }
            };
            var controllerManager_virtual = _controllerManager.CreateACopy(rules);


            var masterPainterController = new PositionAndShutterController_Painter("painter", _lineCollection, controllerManager_virtual.ToolInformation);
            controllerManager_virtual.AddController(masterPainterController);
            foreach(var (controllerName, controller) in controllerManager_virtual.Controllers)
            {
                controller.MasterController = masterPainterController;
                masterPainterController.AddSlaveController(controller, controllerManager_virtual.ControllerLocks[controllerName]);
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

        private void ToolInformation_EngagedStateChanged()
        {
            throw new NotImplementedException();
        }

    }
}
