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

            _vertexShader = "#version 330 core\r\n\r\nlayout(location = 0) in vec3 aPosition;\r\nlayout(location = 1) in vec4 aColor;\r\n\r\nout vec4 vertexColor;\r\nout vec4 clipSpacePos; // Pass clip space position to fragment shader\r\n\r\nuniform mat4 projection;\r\nuniform mat4 view;\r\n\r\nvoid main()\r\n{\r\n    // Calculate world position (assuming model matrix is identity)\r\n    vec4 worldPosition = vec4(aPosition, 1.0);\r\n\r\n    // Calculate clip space position without the view matrix\r\n    clipSpacePos = projection * view * worldPosition;\r\n\r\n    // Compute final position with view matrix for correct rendering\r\n    gl_Position = projection * view * worldPosition;\r\n\r\n    // Pass the vertex color\r\n    vertexColor = aColor;\r\n}\r\n";
            _fragmentShader = "#version 330 core\r\n\r\nin vec4 vertexColor;\r\nin vec4 clipSpacePos; // Received from vertex shader\r\n\r\nout vec4 FragColor;\r\n\r\nvoid main()\r\n{\r\n    // Perform perspective division to get NDC coordinates\r\n    vec3 ndc = clipSpacePos.xyz / clipSpacePos.w;\r\n\r\n    // Calculate the distance from the fragment to the closest edge in NDC\r\n    float distX = 1.0 - abs(ndc.x);\r\n    float distY = 1.0 - abs(ndc.y);\r\n    float edgeDist = min(distX, distY);\r\n\r\n    // Define the width of the fade effect in NDC space (0.0 to 1.0)\r\n    float fadeWidth = 0.2;\r\n\r\n    // Compute the alpha value based on edge distance\r\n    float alpha = clamp(edgeDist / fadeWidth * vertexColor.a, 0.0, 1);\r\n\r\n    // Set the fragment color with the computed alpha\r\n    FragColor = vec4(vertexColor.rgb, alpha);\r\n}\r\n";

            _viewUniform = new UniformMatrix4("view", _camera.GetViewMatrix());
            _projectionUniform = new UniformMatrix4("projection", _camera.GetProjectionMatrix());
            _uniforms = [_viewUniform, _projectionUniform];
            _shader = new Shader(_uniforms, _vertexShader, _fragmentShader);

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
