using opentk_painter_library;
using opentk_painter_library.common;
using opentk_painter_library.render_objects;
using standa_controller_software.device_manager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace standa_control_software_WPF.view_models.system_control.control.render
{
    public class ToolPointLayerViewModel : BaseRenderLayer
    {
        private readonly OrbitalCamera _camera;
        private readonly ToolInformation _toolInformation;

        private readonly Vector4 _dotColorEngaged;
        private readonly Vector4 _dotColorDisengaged;
        private PointObjectCollection _pointCollection;
        private Vector3 _dotPosition;

        private UniformMatrix4 _viewUniform;
        private UniformMatrix4 _projectionUniform;

        public ToolPointLayerViewModel(OrbitalCamera camera, ToolInformation toolInformation) : base()
        {
            _camera = camera;
            _toolInformation = toolInformation;
            _dotColorEngaged = new Vector4(1, 0, 0, 1);
            _dotColorDisengaged = new Vector4(0, 1, 0, 1);
            _dotPosition = new Vector3(0, 0, 0);
            _pointCollection = new PointObjectCollection();

            _vertexShader = "#version 330 core\r\nlayout (location = 0) in vec3 aPosition;\r\nlayout (location = 1) in vec4 aColor;\r\n\r\nout vec4 vertexColor;\r\n\r\nuniform mat4 view;\r\nuniform mat4 projection;\r\n\r\nvoid main()\r\n{\r\n    gl_Position = projection * view * vec4(aPosition, 1.0);\r\n    vertexColor = aColor;\r\n}";
            _fragmentShader = "#version 330 core\r\nin vec4 vertexColor;\r\n\r\nout vec4 FragColor;\r\n\r\nvoid main()\r\n{\r\n    FragColor = vertexColor;\r\n}";

            _viewUniform = new UniformMatrix4("view", _camera.GetViewMatrix());
            _projectionUniform = new UniformMatrix4("projection", _camera.GetProjectionMatrix());

            _uniforms = [_viewUniform, _projectionUniform];
            _shader = new Shader(_uniforms, _vertexShader, _fragmentShader);

            _pointCollection.AddPoint(_dotPosition, 20, _toolInformation.IsOn ? _dotColorEngaged : _dotColorDisengaged);

            this.AddObjectCollection(_pointCollection);
        }

        public override void InitializeLayer()
        {
            base.InitializeLayer();
        }

        public override void OnRenderFrameStart()
        {
            UpdateToolPointLayer();
        }

        public override void UpdateUniforms()
        {
            _viewUniform.Value = _camera.GetViewMatrix();
            _projectionUniform.Value = _camera.GetProjectionMatrix();
        }
        private void UpdateToolPointLayer()
        {
            _toolInformation.RecalculateToolPosition(); // Recalculate positions before drawing

            Vector3 currentPointPos = _toolInformation.Position;
            if (_dotPosition != currentPointPos)
            {
                _dotPosition = currentPointPos;
                _pointCollection.ClearCollection();
                _pointCollection.AddPoint(_dotPosition, 20, _toolInformation.IsOn ? _dotColorEngaged : _dotColorDisengaged);
                InitializeCollections();
            }
        }
    }
}
