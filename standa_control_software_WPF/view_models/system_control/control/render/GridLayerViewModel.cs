using opentk_painter_library;
using opentk_painter_library.common;
using opentk_painter_library.render_objects;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace standa_control_software_WPF.view_models.system_control.control.render
{
    public class GridLayerViewModel : BaseRenderLayer, INotifyPropertyChanged
    {
        private readonly OrbitalCamera _camera;
        private UniformMatrix4 _viewUniform;
        private UniformMatrix4 _projectionUniform;

        private LineObjectCollection _lineCollection;
        private Vector4 _gridColor = new Vector4(0.3f, 0.3f, 0.3f, 0.1f);
        private float _distanceToGrid = 1f;
        private double _gridSpacing;

        public double GridSpacing { get => _gridSpacing; private set { _gridSpacing = value; OnPropertyChanged(nameof(GridSpacing)); } }
        public event PropertyChangedEventHandler PropertyChanged;

        public GridLayerViewModel(OrbitalCamera camera)
        {
            _camera = camera;

            _lineCollection = new LineObjectCollection() { lineWidth = 3 };

            _vertexShaderSource = "C:\\Users\\giedr\\Desktop\\importsnt\\Csharp\\Standa Stage Control Environment\\standa_controller_software\\openTK_painter\\grid-shaders\\VertexShader.vert";
            _fragmentShaderSource = "C:\\Users\\giedr\\Desktop\\importsnt\\Csharp\\Standa Stage Control Environment\\standa_controller_software\\openTK_painter\\grid-shaders\\FragmentShader.frag";

            _viewUniform = new UniformMatrix4("view", _camera.GetViewMatrix());
            _projectionUniform = new UniformMatrix4("projection", _camera.GetProjectionMatrix());
            _uniforms = [_viewUniform, _projectionUniform];
            _shader = new Shader(_uniforms, _vertexShaderSource, _fragmentShaderSource);

            this.AddObjectCollection(_lineCollection);

        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public override void InitializeLayer()
        {

            //UpdateGrid(100, 100, 10, 10, 0, 0);
            _gridColor = new Vector4(0.3f, 0.3f, 0.3f, 0.1f);
            //_distanceToGrid = 0f;

            base.InitializeLayer();
        }

        private void UpdateGrid(float width, float length, int numberOfLinesX, int numberOfLinesY, float centerX, float centerY)
        {

            _lineCollection.ClearCollection();
            for (int i = 1; i < numberOfLinesX; i++)
            {
                var xas = -width / 2 + width / numberOfLinesX * i;
                _lineCollection.AddLine(new Vector3(xas + centerX, -length / 2 + centerY, 0f), new Vector3(xas + centerX, length / 2 + centerY, 0f), _gridColor);
            }
            for (int j = 1; j < numberOfLinesY; j++)
            {
                var yas = -length / 2 + length / numberOfLinesY * j;
                _lineCollection.AddLine(new Vector3(-width / 2 + centerX, yas + centerY, 0f), new Vector3(width / 2 + centerX, yas + centerY, 0f), _gridColor);
            }
            InitializeCollections();
        }
        public override void OnRenderFrameStart()
        {
           
            // check if camera Distance has changed.
            if (_distanceToGrid != _camera.Distance)
            {
                var distance = _camera.Distance + _camera.ReferencePosition.Y; // Math.Abs(CommandLayer.Camera.CameraPosition.Y) + 
                var centerX = _camera.ReferencePosition.X;
                var centerY = _camera.ReferencePosition.Z;

                distance = Math.Max(distance, 10);

                var widthMax = Math.Min(distance * 100, 10000);

                // let's make possible dx values of [0.1um .5um 1um 5um 10um 50um 100um 500 um 1mm]

                GridSpacing = distance * 0.1;
                int numberOfLines = (int)(widthMax / GridSpacing);

                UpdateGrid(widthMax, widthMax, numberOfLines, numberOfLines, 0, 0);
                _distanceToGrid = _camera.Distance;
            }
            
        }

        public override void UpdateUniforms()
        {
            _viewUniform.Value = _camera.GetViewMatrix();
            _projectionUniform.Value = _camera.GetProjectionMatrix();
        }
    }
}
