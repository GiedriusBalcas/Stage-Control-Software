
using opentk_painter_library;
using standa_controller_software.command_manager;
using standa_controller_software.device_manager.controller_interfaces.master_controller;
using standa_controller_software.device_manager;
using opentk_painter_library.common;
using System.Collections.Concurrent;
using opentk_painter_library.render_objects;
using Microsoft.Extensions.Logging;
using standa_control_software_WPF.view_models.logging;

namespace standa_control_software_WPF.view_models.system_control.control.render
{
    public class CommandLayerViewModel : BaseRenderLayer
    {
        private readonly ControllerManager _controllerManager;
        private readonly ILogger<CommandLayerViewModel> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly OrbitalCamera _camera;
        
        private UniformMatrix4 _viewUniform;
        private UniformMatrix4 _projectionUniform;
        
        private LineObjectCollection _lineCollection;

        public CommandLayerViewModel(ControllerManager controllerManager, ILogger<CommandLayerViewModel> logger, ILoggerFactory loggerFactory, OrbitalCamera camera)
        {
            _controllerManager = controllerManager;
            _logger = logger;
            _loggerFactory = loggerFactory;
            _camera = camera;
            _lineCollection = new LineObjectCollection(); 
            
            _vertexShader = "#version 330 core\r\nlayout (location = 0) in vec3 aPosition;\r\nlayout (location = 1) in vec4 aColor;\r\n\r\nout vec4 vertexColor;\r\n\r\nuniform mat4 view;\r\nuniform mat4 projection;\r\n\r\nvoid main()\r\n{\r\n    gl_Position = projection * view * vec4(aPosition, 1.0);\r\n    vertexColor = aColor;\r\n}";
            _fragmentShader = "#version 330 core\r\nin vec4 vertexColor;\r\n\r\nout vec4 FragColor;\r\n\r\nvoid main()\r\n{\r\n    FragColor = vertexColor;\r\n}";

            _viewUniform = new UniformMatrix4("view", _camera.GetViewMatrix());
            _projectionUniform = new UniformMatrix4("projection", _camera.GetProjectionMatrix());

            _uniforms = [_viewUniform, _projectionUniform];
            _shader = new Shader(_uniforms, _vertexShader, _fragmentShader);

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
        public async Task PaintCommandQueue(IEnumerable<Command[]> commandLines) 
        {

            var controllerManager_virtual = _controllerManager.CreateAVirtualCopy();

            var masterPainterController = new PositionAndShutterController_Painter("painter", _loggerFactory, _lineCollection, controllerManager_virtual.ToolInformation);
            controllerManager_virtual.AddController(masterPainterController);
            foreach (var (controllerName, controller) in controllerManager_virtual.Controllers)
            {
                if (controller != masterPainterController)
                {
                    controller.MasterController = masterPainterController;
                    masterPainterController.AddSlaveController(controller, controllerManager_virtual.ControllerLocks[controllerName]);
                }
            }

            var commandManager_virtual = new CommandManager(controllerManager_virtual, _loggerFactory.CreateLogger<CommandManager>());

            _lineCollection.ClearCollection();

            int counter = 0;
            foreach (var commandLine in commandLines)
            {
                counter++;
                await commandManager_virtual.TryExecuteCommandLine(commandLine);

                // Only every 10th line:
                if (counter % 10 == 0)
                {
                    InitializeCollections();
                    await Task.Delay(1);
                }
            }
            InitializeCollections();

        }


    }
}
