using opentk_painter_library.common;
using opentk_painter_library.render_objects;
using opentk_painter_library;
using standa_controller_software.command_manager;
using standa_controller_software.device_manager;
using standa_controller_software.device_manager.controller_interfaces;
using System.Numerics;
using System;

namespace standa_controller_software.painter
{
    public class PainterManager
    {
        private CommandManager _commandManager;
        private ControllerManager _controllerManager;


        private OrbitalCamera _camera;
        private OrbitalCamera _refCamera;
        private RenderLayer _commandLayer;
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
            
            _renderLayers = [_commandLayer];
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

            return new RenderLayer(vertexShaderSource, fragmentShaderSource);
        }

        public LineObjectCollection PaintCommands()
        {
            var rules = new Dictionary<Type, Type> { { typeof(BasePositionerController), typeof(PositionerController_Virtual) } };
            var controllerManager_virtual = _controllerManager.CreateACopy(rules);
            var commandManager_virtual = new CommandManager(controllerManager_virtual);

            var commandLines = _commandManager.GetCommandQueueList();
            //var controllerManager = 
            var renderObjects = new LineObjectCollection();

            foreach (var commandLine in commandLines)
            {
                var startPositions = controllerManager_virtual.ToolInformation.CalculateToolPositionUpdate();

                commandManager_virtual.ExecuteCommandLine(commandLine).GetAwaiter().GetResult();

                var endPositions = controllerManager_virtual.ToolInformation.CalculateToolPositionUpdate();

                if(endPositions != startPositions)
                    renderObjects.AddLine(startPositions, endPositions, LineColor);
            }

            return renderObjects;
        }
    }
}
