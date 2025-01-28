using Microsoft.Extensions.Logging;
using opentk_painter_library;
using opentk_painter_library.common;
using standa_control_software_WPF.view_models.system_control.control.render;
using standa_controller_software.command_manager;
using standa_controller_software.device_manager;
using System.Collections.Concurrent;


namespace standa_control_software_WPF.view_models.system_control.control
{
    /// <summary>
    /// ViewModel responsible for managing painting operations, rendering layers, and camera controls.
    /// It orchestrates various render layers, handles rendering states, and processes command queues for painting.
    /// </summary>
    public class PainterManagerViewModel : ViewModelBase
    {
        private readonly ControllerManager _controllerManager;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<PainterManagerViewModel> _logger;
        private readonly OrbitalCamera _camera;
        private double _gridSpacing = 0;
        private bool _isRendering = true;
        
        public CameraViewModel CameraViewModel { get; private set; }
        public double GridSpacing 
        { 
            get => _gridSpacing; 
            private set { _gridSpacing = value; OnPropertyChanged(nameof(GridSpacing));} 
        }
        public List<BaseRenderLayer> RenderLayers = [];
        public CommandLayerViewModel CommandLayer;
        public GridLayerViewModel GridLayer { get; private set; }
        public OrientationArrowsLayerViewModel OrientationLayer { get; private set; }
        public bool IsRendering
        {
            get { return _isRendering; }
            set 
            { 
                _isRendering = value; 
                OnPropertyChanged(nameof(IsRendering));
            }
        }

        public PainterManagerViewModel(ControllerManager controllerManager, ILoggerFactory loggerFactory)
        {
            _controllerManager = controllerManager;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<PainterManagerViewModel>();

            _camera = new OrbitalCamera(1, 45);
            CameraViewModel = new CameraViewModel(this,_controllerManager, _camera);

            CommandLayer = new CommandLayerViewModel(_controllerManager, _loggerFactory.CreateLogger<CommandLayerViewModel>(), _loggerFactory, _camera);
            RenderLayers.Add(CommandLayer);

            var toolPointLayer = new ToolPointLayerViewModel(_camera, _controllerManager.ToolInformation!);
            RenderLayers.Add(toolPointLayer);

            GridLayer = new GridLayerViewModel(_camera, _controllerManager.ToolInformation!);
            RenderLayers.Add(GridLayer);

            OrientationLayer = new OrientationArrowsLayerViewModel(_camera);
            RenderLayers.Add(OrientationLayer);
        }


        /// <summary>
        /// Asynchronously processes and paints a queue of command lines.
        /// Each command array in the queue is handled by the CommandLayer.
        /// </summary>
        /// <param name="commandLines">An enumerable of command arrays to be painted.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task PaintCommandQueue(IEnumerable<Command[]> commandLines)
        {
            await CommandLayer.PaintCommandQueue(commandLines);
        }

        /// <summary>
        /// Initializes all render layers by setting them as initialized,
        /// and invoking their respective initialization methods for collections, shaders, and layer-specific setups.
        /// </summary>
        public void InitializeLayers()
        {
            foreach (var layer in RenderLayers)
            {
                layer.IsGLInitialized = true; 
                layer.InitializeCollections();
                layer.InitializeShader();
                layer.InitializeLayer();
            }
        }

        /// <summary>
        /// Deinitializes all render layers by disposing of their buffers and shader programs,
        /// and marking them as not initialized.
        /// </summary>
        public void DeinitializeLayers()
        {
            foreach (var layer in RenderLayers)
            {
                layer.DisposeBuffers();
                layer.DisposeShaderProgram();
                layer.IsGLInitialized = false;
            }
        }

        /// <summary>
        /// Draws a single frame by updating uniforms and rendering each layer,
        /// provided that rendering is currently enabled.
        /// </summary>
        public void DrawFrame()
        {
            if (IsRendering)
            {
                foreach (var layer in RenderLayers)
                {
                    layer.UpdateUniforms();
                    layer.DrawLayer();
                }
            }
        }
    }
}
