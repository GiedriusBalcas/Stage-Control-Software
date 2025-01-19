using Microsoft.Extensions.Logging;
using opentk_painter_library;
using opentk_painter_library.common;
using standa_control_software_WPF.view_models.system_control.control.render;
using standa_controller_software.command_manager;
using standa_controller_software.device_manager;
using System.Collections.Concurrent;


namespace standa_control_software_WPF.view_models.system_control.control
{
    public class PainterManagerViewModel : ViewModelBase
    {
        private readonly ControllerManager _controllerManager;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<PainterManagerViewModel> _logger;
        
        private readonly OrbitalCamera _camera;
        
        private double _gridSpacing = 0;
        public CameraViewModel CameraViewModel { get; private set; }
        public double GridSpacing 
        { 
            get => _gridSpacing; 
            private set { _gridSpacing = value; OnPropertyChanged(nameof(GridSpacing));} 
        }

        public List<BaseRenderLayer> RenderLayers = new List<BaseRenderLayer>();
        public CommandLayerViewModel CommandLayer;
        public GridLayerViewModel GridLayer { get; private set; }
        public OrientationArrowsLayerViewModel OrientationLayer { get; private set; }

        public PainterManagerViewModel(ControllerManager controllerManager, CommandManager commandManager, ILoggerFactory loggerFactory)
        {
            //CameraViewModel = new CameraViewModel(_painterManager, _controllerManager);
            _controllerManager = controllerManager;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<PainterManagerViewModel>();

            //_painterManager = new PainterManager(commandManager, _controllerManager, _log);

            _camera = new OrbitalCamera(1, 45);
            CameraViewModel = new CameraViewModel(this,_controllerManager, _camera);

            CreateRenderLayers();
        }

        private void CreateRenderLayers()
        {
            CommandLayer = new CommandLayerViewModel(_controllerManager, _loggerFactory.CreateLogger<CommandLayerViewModel>(), _loggerFactory, _camera);
            RenderLayers.Add(CommandLayer);

            var toolPointLayer = new ToolPointLayerViewModel(_camera, _controllerManager.ToolInformation);
            RenderLayers.Add(toolPointLayer);

            GridLayer = new GridLayerViewModel(_camera, _controllerManager.ToolInformation);
            RenderLayers.Add(GridLayer);

            //var orientationLayer = CreateOrientationArrowsLayer();
            OrientationLayer = new OrientationArrowsLayerViewModel(_camera);
            RenderLayers.Add(OrientationLayer);

            //var gridLayer = CreateGridLayer();

        }

        public void PaintCommandQueue(IEnumerable<Command[]> commandLines)
        {
            CommandLayer.PaintCommandQueue(commandLines);
        }

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

        public void DeinitializeLayers()
        {
            foreach (var layer in RenderLayers)
            {
                layer.DisposeBuffers();
                layer.DisposeShaderProgram();
                layer.IsGLInitialized = false;
            }
        }

        public void DrawFrame()
        {
            foreach (var layer in RenderLayers)
            {
                layer.UpdateUniforms();
                layer.DrawLayer();
            }
        }
    }
}
