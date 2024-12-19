
using opentk_painter_library;
using standa_controller_software.command_manager;
using standa_controller_software.device_manager.controller_interfaces.master_controller;
using standa_controller_software.device_manager;
using opentk_painter_library.common;
using System.Collections.Concurrent;
using opentk_painter_library.render_objects;

namespace standa_control_software_WPF.view_models.system_control.control.render
{
    public class CommandLayerViewModel : BaseRenderLayer
    {
        private readonly ControllerManager _controllerManager;
        private readonly ConcurrentQueue<string> _log;
        private readonly OrbitalCamera _camera;
        
        private UniformMatrix4 _viewUniform;
        private UniformMatrix4 _projectionUniform;
        
        private LineObjectCollection _lineCollection;

        public CommandLayerViewModel(ControllerManager controllerManager, ConcurrentQueue<string> log, OrbitalCamera camera)
        {
            _controllerManager = controllerManager;
            _log = log;
            _camera = camera;
            _lineCollection = new LineObjectCollection(); 
            
            _vertexShaderSource = "C:\\Users\\giedr\\Desktop\\importsnt\\Csharp\\Standa Stage Control Environment\\standa_controller_software\\openTK_painter\\default-shaders\\VertexShader.vert";
            _fragmentShaderSource = "C:\\Users\\giedr\\Desktop\\importsnt\\Csharp\\Standa Stage Control Environment\\standa_controller_software\\openTK_painter\\default-shaders\\FragmentShader.frag";

            _viewUniform = new UniformMatrix4("view", _camera.GetViewMatrix());
            _projectionUniform = new UniformMatrix4("projection", _camera.GetProjectionMatrix());

            _uniforms = [_viewUniform, _projectionUniform];
            _shader = new Shader(_uniforms, _vertexShaderSource, _fragmentShaderSource);

            this.AddObjectCollection(_lineCollection);
        }
        public override void InitializeLayer()
        {
            this.ClearCollections();
            this.AddObjectCollection(_lineCollection);


            base.InitializeLayer();
        }
        public override void OnRenderFrameStart()
        {
        }

        public override void UpdateUniforms()
        {
            _viewUniform.Value = _camera.GetViewMatrix();
            _projectionUniform.Value = _camera.GetProjectionMatrix();
        }
        public void PaintCommandQueue(IEnumerable<Command[]> commandLines) 
        {

            var controllerManager_virtual = _controllerManager.CreateAVirtualCopy();

            var masterPainterController = new PositionAndShutterController_Painter("painter", _log, _lineCollection, controllerManager_virtual.ToolInformation);
            controllerManager_virtual.AddController(masterPainterController);
            foreach (var (controllerName, controller) in controllerManager_virtual.Controllers)
            {
                if (controller != masterPainterController)
                {
                    controller.MasterController = masterPainterController;
                    masterPainterController.AddSlaveController(controller, controllerManager_virtual.ControllerLocks[controllerName]);
                }
            }

            var commandManager_virtual = new CommandManager(controllerManager_virtual, new ConcurrentQueue<string>());

            _lineCollection.ClearCollection();

            foreach (var commandLine in commandLines)
            {
                commandManager_virtual.TryExecuteCommandLine(commandLine).GetAwaiter().GetResult();
            }
            InitializeCollections();

        }


    }
}
