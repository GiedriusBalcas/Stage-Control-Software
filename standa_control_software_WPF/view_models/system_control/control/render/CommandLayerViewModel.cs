
using Microsoft.Extensions.Logging;
using opentk_painter_library;
using opentk_painter_library.common;
using opentk_painter_library.render_objects;
using standa_controller_software.command_manager;
using standa_controller_software.device_manager;
using standa_controller_software.device_manager.controller_interfaces.master_controller;

namespace standa_control_software_WPF.view_models.system_control.control.render
{
    /// <summary>
    /// ViewModel responsible for rendering and managing command-related graphical layers.
    /// </summary>
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
            
            _vertexShader = """
                #version 330 core
                layout (location = 0) in vec3 aPosition;
                layout (location = 1) in vec4 aColor;

                out vec4 vertexColor;

                uniform mat4 view;
                uniform mat4 projection;

                void main()
                {
                    gl_Position = projection * view * vec4(aPosition, 1.0);
                    vertexColor = aColor;
                }
                """;
            _fragmentShader = """
                #version 330 core
                in vec4 vertexColor;

                out vec4 FragColor;

                void main()
                {
                    FragColor = vertexColor;
                }
                """;

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
        /// <summary>
        /// Asynchronously processes and paints a queue of command lines.
        /// It creates a virtual copy of the controller manager, sets up a master painter controller,
        /// executes each command line, and introduces a delay every 100 command lines to prevent UI thread blockage.
        /// </summary>
        /// <param name="commandLines">An enumerable of command arrays to be painted.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task PaintCommandQueue(IEnumerable<Command[]> commandLines) 
        {

            var controllerManager_virtual = _controllerManager.CreateAVirtualCopy();

            var masterPainterController = new PositionAndShutterController_Painter("painter", _loggerFactory, _lineCollection, controllerManager_virtual.ToolInformation!);
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

                // Only every 100th line:
                // We are forced to render on the ui thread, so giving some breathing room.
                if (counter % 100 == 0)
                {
                    await Task.Delay(10);
                }
            }
            InitializeCollections();

        }


    }
}
