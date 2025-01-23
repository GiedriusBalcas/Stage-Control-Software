using OpenTK.Graphics.OpenGL;
using opentk_painter_library;
using opentk_painter_library.common;
using opentk_painter_library.render_objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace standa_control_software_WPF.view_models.system_control.control.render
{
    public class OrientationArrowsLayerViewModel : BaseRenderLayer
    {
        private readonly OrbitalCamera _sceneCamera;
        private readonly OrbitalCamera _camera;
        private LineObjectCollection _lineCollection;

        private UniformMatrix4 _viewUniform;
        private UniformMatrix4 _projectionUniform;
        private Text3DObjectCollection _textCollection;
        public OrientationArrowsLayerViewModel(OrbitalCamera camera)
        {
            _sceneCamera = camera;
            _camera = new OrbitalCamera(camera.AspectRatio, camera.FovY);
            _camera.Pitch = camera.Pitch;
            _camera.Yaw = camera.Yaw;
            _camera.Distance = 100;

            _lineCollection = new LineObjectCollection() { lineWidth = 3 };

            _lineCollection.AddLine(new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector4(1, 0, 0, 1));
            _lineCollection.AddLine(new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector4(0, 1, 0, 1));
            _lineCollection.AddLine(new Vector3(0, 0, 0), new Vector3(0, 0, 1), new Vector4(0, 0, 1, 1));

            _vertexShader = "#version 330 core\r\nlayout (location = 0) in vec3 aPosition;\r\nlayout (location = 1) in vec4 aColor;\r\n\r\nout vec4 vertexColor;\r\n\r\nuniform mat4 view;\r\nuniform mat4 projection;\r\nmat4 translation = mat4(\r\n    1.0, 0.0, 0.0, 0.0, // Column 1\r\n    0.0, 1.0, 0.0, 0.0, // Column 2\r\n    0.0, 0.0, 1.0, 0.0, // Column 3\r\n    0.9, -0.85, 0.0, 1.0  // Column 4 (Translation)\r\n);\r\n\r\nvoid main()\r\n{\r\n    gl_Position = translation * projection * view * vec4(aPosition, 1.0);\r\n    vertexColor = aColor;\r\n}";
            _fragmentShader = "#version 330 core\r\nin vec4 vertexColor;\r\n\r\nout vec4 FragColor;\r\n\r\nvoid main()\r\n{\r\n    FragColor = vertexColor;\r\n}";

            _viewUniform = new UniformMatrix4("view", _camera.GetViewMatrix());
            _projectionUniform = new UniformMatrix4("projection", _camera.GetProjectionMatrix());
            _uniforms = [_viewUniform, _projectionUniform];
            _shader = new Shader(_uniforms, _vertexShader, _fragmentShader);

            this.AddObjectCollection(_lineCollection);
        }

        public override void InitializeLayer()
        {
            base.InitializeLayer();
        }

        public override void OnRenderFrameStart()
        {
            GL.Disable(EnableCap.DepthTest);
        }

        public override void UpdateUniforms()
        {
            _camera.Pitch = _sceneCamera.Pitch;
            _camera.Yaw = _sceneCamera.Yaw;
            _camera.Distance = 20;
            _camera.AspectRatio = _sceneCamera.AspectRatio;

            _viewUniform.Value = _camera.GetViewMatrix();
            _projectionUniform.Value = _camera.GetProjectionMatrix();
        }
    }
}
