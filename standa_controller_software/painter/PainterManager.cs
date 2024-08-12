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

        public void UpdateCommandLayerCollection()
        {
            //  THIS AINT WORKING.

            //_commandLayer.DisposeBuffers();
            //_commandLayer.DisposeShaderProgram();
            //_commandLayer.ClearCollections();

            //_commandLayer.AddObjectCollection(_lineCollection);

            //_commandLayer.InitializeCollections();
            //_commandLayer.InitializeShaders();

        }

        //public void RenderLayers()
        //{
        //    foreach (var layer in _renderLayers)
        //    {
        //        layer.DrawLayer();
        //    }
        //}

        //internal void UpdateCameraOrbitAngle(float dx, float dy)
        //{
        //    var sensitivity = .5f;
        //    _camera.Yaw += dx * sensitivity;
        //    _camera.Pitch += dy * sensitivity;

        //    _refCamera.Yaw = _camera.Yaw;
        //    _refCamera.Pitch = _camera.Pitch;

        //    UpdateUniforms();
        //}

        //internal void UpdateCameraRefence(float dx, float dy)
        //{
        //    var sensitivity = 0.9f;
        //    _camera.ReferencePosition += _camera.Up * sensitivity * dy * _camera.Distance;
        //    _camera.ReferencePosition += _camera.Right * sensitivity * dx * _camera.Distance;
        //    UpdateUniforms();
        //}

        //internal void UpdateCameraDistance(float dr)
        //{
        //    var sensitivity = 1f;
        //    _camera.Distance -= dr * sensitivity;
        //    UpdateUniforms();
        //}

        //internal void UpdateCameraSettings(float aspectRatio, float fovy)
        //{
        //    _camera.FovY = fovy;
        //    _camera.AspectRatio = aspectRatio;

        //    _refCamera.AspectRatio = aspectRatio;
        //    UpdateUniforms();
        //}

        internal void ExecuteCameraViewXY()
        {
            _camera.Pitch = 90;
            _camera.Yaw = 90;

            _refCamera.Pitch = 90;
            _refCamera.Yaw = 90;
        }

        internal void ExecuteCameraViewXZ()
        {
            _camera.Pitch = 0;
            _camera.Yaw = 90;

            _refCamera.Pitch = 0;
            _refCamera.Yaw = 90;
        }

        internal void ExecuteCameraViewYZ()
        {
            _camera.Pitch = 0;
            _camera.Yaw = 0;

            _refCamera.Pitch = 0;
            _refCamera.Yaw = 0;
        }

        internal void SnapObjectToFit()
        {
            var positions = new List<Vector3>();

            positions.AddRange(_commandLayer.GetCollectionsVerteces()
                .Select(vertex => new System.Numerics.Vector3(vertex.X, vertex.Y, vertex.Z))
                );
            
            _camera.FitObject(positions);
        }

        public async Task PaintCommands()
        {
            var rules = new Dictionary<Type, Type> { { typeof(BasePositionerController), typeof(PositionerController_Virtual) } };
            var controllerManager_virtual = _controllerManager.CreateACopy(rules);
            var commandManager_virtual = new CommandManager(controllerManager_virtual);

            var commandLines = _commandManager.GetCommandQueueList();
            //var controllerManager = 

            foreach (var commandLine in commandLines)
            {
                controllerManager_virtual.ToolInformation.RecalculateToolPosition();
                var startPositions = controllerManager_virtual.ToolInformation.Position;

                await commandManager_virtual.ExecuteCommandLine(commandLine);

                controllerManager_virtual.ToolInformation.RecalculateToolPosition();
                var endPositions = controllerManager_virtual.ToolInformation.Position;

                //drawLine(startPosition, endPosition);
            }
        }
    }
}
