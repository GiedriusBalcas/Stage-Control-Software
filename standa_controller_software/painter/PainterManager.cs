using opentk_painter_library.common;
using opentk_painter_library.render_objects;
using opentk_painter_library;
using standa_controller_software.command_manager;
using standa_controller_software.device_manager;
using standa_controller_software.device_manager.controller_interfaces;
using System.Numerics;
using System;
using standa_controller_software.device_manager.devices.shutter;
using standa_controller_software.device_manager.controller_interfaces.shutter;
using standa_controller_software.device_manager.controller_interfaces.positioning;
using standa_controller_software.device_manager.controller_interfaces.master_controller;
using standa_controller_software.device_manager.controller_interfaces.sync;
using System.Collections.Concurrent;
using System.Configuration;

namespace standa_controller_software.painter
{
    public class PainterManager
    {
        private readonly ConcurrentQueue<string> _log;
        private readonly ControllerManager _controllerManager;

        public OrbitalCamera _camera;

        public RenderLayer CommandLayer;
        public RenderLayer ToolPointLayer;
        public RenderLayer OrientationLayer;
        public RenderLayer GridLayer;
        public RenderLayer TestLayer;
        public RenderLayer GridTextLayer;
        public RenderLayer GridMarkLayer;
        private LineObjectCollection _lineCollection;
        private TextObjectCollection _textCollection;

        private List<RenderLayer> _renderLayers { get; set; }
        public double GridSpacing { get; private set; }
        public PainterManager(CommandManager commandManager, ControllerManager controllerManager, ConcurrentQueue<string> log)
        {
            _log = log;
            _controllerManager = controllerManager;
            _camera = new OrbitalCamera(1, 45);

            _lineCollection = new LineObjectCollection();

            CommandLayer = CreateCommandLayer();
            ToolPointLayer = CreateToolPointLayer();
            OrientationLayer = CreateOrientationArrowsLayer(CommandLayer);
            GridLayer = CreateGridLayer();
            //TestLayer = CreateTestLayer();
            //GridTextLayer = CreateTestTexturesLayer();
            //GridMarkLayer = CreateMarkLayer();


            _renderLayers = [CommandLayer, ToolPointLayer, OrientationLayer, GridLayer, TestLayer, GridTextLayer];
        }
        public void AddRenderLayer(RenderLayer renderLayer)
        {
            _renderLayers.Add(renderLayer);
        }

        public void RemoveRenderLayer(RenderLayer renderLayer)
        {
            _renderLayers.Remove(renderLayer);
        }

        public void ClearRenderLayers()
        {
            _renderLayers.Clear();
        }

        private RenderLayer CreateMarkLayer()
        {
            var vertexShaderSource = "C:\\Users\\giedr\\Desktop\\importsnt\\Csharp\\Standa Stage Control Environment\\standa_controller_software\\openTK_painter\\default-shaders\\VertexShader.vert";
            var fragmentShaderSource = "C:\\Users\\giedr\\Desktop\\importsnt\\Csharp\\Standa Stage Control Environment\\standa_controller_software\\openTK_painter\\default-shaders\\FragmentShader.frag";

            var camera = new OrbitalCamera(1, 45);

            var viewUniform = new UniformMatrix4("view", camera.GetViewMatrix());
            var projectionUniform = new UniformMatrix4("projection", camera.GetProjectionMatrix());

            Action updateUniforms = () =>
            {
                viewUniform.Value = camera.GetViewMatrix();
                projectionUniform.Value = camera.GetProjectionMatrix();
            };

            var layer = new RenderLayer(vertexShaderSource, fragmentShaderSource, [viewUniform, projectionUniform],camera,  updateUniforms);

            var lines = new LineObjectCollection() { lineWidth = 3f };
            lines.AddLine(new Vector3(-20f, 0f, 0f), new Vector3(20f, 0f, 0f), new Vector4(1, 1, 1, 1));
            layer.AddObjectCollection(lines);
            
            return layer;
        }

        private RenderLayer CreateTestTexturesLayer()
        {
            var vertexShaderSource = "C:\\Users\\giedr\\Desktop\\importsnt\\Csharp\\Standa Stage Control Environment\\standa_controller_software\\openTK_painter\\texture-shaders\\VertexShader.vert";
            var fragmentShaderSource = "C:\\Users\\giedr\\Desktop\\importsnt\\Csharp\\Standa Stage Control Environment\\standa_controller_software\\openTK_painter\\texture-shaders\\FragmentShader.frag";

            var viewUniform = new UniformMatrix4("view", _camera.GetViewMatrix());
            var projectionUniform = new UniformMatrix4("projection", _camera.GetProjectionMatrix());
            var textureUniform = new UniformSampler2D("textureSampler", 0);

            var OnInitializationAction = () =>
            {
                //var filePath = "C:\\Users\\giedr\\Pictures\\Screenshots\\Zoneplate.png";
                //int textureId = TextureHelper.LoadTextureFromFile(filePath);

                //var rectangles = new RectangleTextureObjectCollection(textureId);

                //rectangles.AddRectangle(
                //    new Vector3(-100, 100, 0),
                //    new Vector3(100, 100, 0),
                //    new Vector3(100, -100, 0),
                //    new Vector3(-100, -100, 0),
                //    new Vector2(0,0),
                //    new Vector2(1,0),
                //    new Vector2(1,1),
                //    new Vector2(0,1)
                //    );

                //TestTexturesLayer.AddObjectCollection(rectangles);



                // On initialization in PainterManager or similar
                var fontName = "Arial";
                var atlas = new FontAtlas(fontName, 30f);

                _textCollection = new TextObjectCollection(atlas, -500f, -500f);
                _textCollection.SetString("300 um");
                //_textCollection.InitializeBuffers();
                GridTextLayer.AddObjectCollection(_textCollection);

            };

            var camera = new OrbitalCamera(1, 45);
            camera.Distance = 100;


            camera.IsOrthographic = true;


            Action updateUniforms = () =>
            {
                viewUniform.Value = camera.GetViewMatrix();
                projectionUniform.Value = camera.GetProjectionMatrix();
            };

            var testLayer = new RenderLayer(vertexShaderSource, fragmentShaderSource, [viewUniform, projectionUniform, textureUniform], camera, updateUniforms, UpdateTestTexturesLater, OnInitializationAction);




            return testLayer;
        }

        private void UpdateTestTexturesLater()
        {

            //GridTextLayer.Camera.Pitch = 0;
            //GridTextLayer.Camera.Yaw = 90;
            //GridTextLayer.Camera.Distance = 2000;
            //GridTextLayer.Camera.AspectRatio = CommandLayer.Camera.AspectRatio;
            
        }

        private RenderLayer CreateOrientationArrowsLayer(RenderLayer commandLayer)
        {
            var vertexShaderSource = "C:\\Users\\giedr\\OneDrive\\Desktop\\importsnt\\Csharp\\Standa Stage Control Environment\\standa_controller_software\\openTK_painter\\reference_shaders\\VertexShader_TranlateToEdge.vert";
            var fragmentShaderSource = "C:\\Users\\giedr\\OneDrive\\Desktop\\importsnt\\Csharp\\Standa Stage Control Environment\\standa_controller_software\\ConsoleApplication_For_Tests\\Shaders\\LineDrawingLayer\\FragmentShader.frag";

            var camera = new OrbitalCamera(1,45);

            var viewUniform = new UniformMatrix4("view", camera.GetViewMatrix());
            var projectionUniform = new UniformMatrix4("projection", camera.GetProjectionMatrix());

            Action updateUniforms = () =>
            {
                viewUniform.Value = camera.GetViewMatrix();
                projectionUniform.Value = camera.GetProjectionMatrix();
            };

            var orientationLayer = new RenderLayer(vertexShaderSource, fragmentShaderSource, [viewUniform, projectionUniform], camera, updateUniforms);

            //orientationLayer.Camera.IsOrthographic = true;
            //orientationLayer.Camera.IsTrackingTool = false;
            //orientationLayer.Camera.ReferencePosition = new OpenTK.Mathematics.Vector3(0, 0, 0);
            //orientationLayer.Camera.Distance = 20f;
            var referenceLines = new LineObjectCollection() 
            {
                lineWidth = 5f,
            };

            referenceLines.AddLine(new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector4(1, 0, 0, 1));
            referenceLines.AddLine(new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector4(0, 1, 0, 1));
            referenceLines.AddLine(new Vector3(0, 0, 0), new Vector3(0, 0, 1), new Vector4(0, 0, 1, 1));

            orientationLayer.AddObjectCollection(referenceLines);

            return orientationLayer;
        }

        private RenderLayer CreateToolPointLayer()
        {
            var vertexShaderSource = "C:\\Users\\giedr\\OneDrive\\Desktop\\importsnt\\Csharp\\Standa Stage Control Environment\\standa_controller_software\\ConsoleApplication_For_Tests\\Shaders\\LineDrawingLayer\\VertexShader.vert";
            var fragmentShaderSource = "C:\\Users\\giedr\\OneDrive\\Desktop\\importsnt\\Csharp\\Standa Stage Control Environment\\standa_controller_software\\ConsoleApplication_For_Tests\\Shaders\\LineDrawingLayer\\FragmentShader.frag";

            var viewUniform = new UniformMatrix4("view", _camera.GetViewMatrix());
            var projectionUniform = new UniformMatrix4("projection", _camera.GetProjectionMatrix());

            Action updateUniforms = () =>
            {
                viewUniform.Value = _camera.GetViewMatrix();
                projectionUniform.Value = _camera.GetProjectionMatrix();
            };

            return new RenderLayer(vertexShaderSource, fragmentShaderSource, [viewUniform, projectionUniform], _camera, updateUniforms, UpdateToolPointLayer);
        }
        private RenderLayer CreateTestLayer()
        {
            var vertexShaderSource = "C:\\Users\\giedr\\Desktop\\importsnt\\Csharp\\Standa Stage Control Environment\\standa_controller_software\\openTK_painter\\default-shaders\\VertexShader.vert";
            var fragmentShaderSource = "C:\\Users\\giedr\\Desktop\\importsnt\\Csharp\\Standa Stage Control Environment\\standa_controller_software\\openTK_painter\\default-shaders\\FragmentShader.frag";

            var viewUniform = new UniformMatrix4("view", _camera.GetViewMatrix());
            var projectionUniform = new UniformMatrix4("projection", _camera.GetProjectionMatrix());

            Action updateUniforms = () =>
            {
                viewUniform.Value = _camera.GetViewMatrix();
                projectionUniform.Value = _camera.GetProjectionMatrix();
            };


            var testLayer = new RenderLayer(vertexShaderSource, fragmentShaderSource, [viewUniform, projectionUniform], _camera, updateUniforms, UpdateTestLater);

            var rectangle = new RectangleObjectCollection();
            rectangle.AddRectangle(
                new Vector3(-100, 100, 100), // top-left 
                new Vector3(100, 100, 100), // top-right
                new Vector3(100, -100, 100), // bottom-right
                new Vector3(-100, -100, 100), // bottom-left
                new Vector4(1, 1, 1, 1)
                );

            testLayer.AddObjectCollection(rectangle);

            return testLayer;
        }

        private void UpdateTestLater()
        {

            TestLayer.ClearCollections();

            var rectangles = new RectangleObjectCollection();
            rectangles.AddRectangle(
                new Vector3(-100 + 500, 100, 100), // top-left 
                new Vector3(100 + 500, 100, 100), // top-right
                new Vector3(100 + 500, -100, 10), // bottom-right
                new Vector3(-100 + 500, -100, 10), // bottom-left
                new Vector4(1, 0, 0, 0.5f)
                );

            TestLayer.AddObjectCollection(rectangles);
            TestLayer.InitializeCollections();
        }

        private RenderLayer CreateGridLayer()
        {
            var vertexShaderSource = "C:\\Users\\giedr\\Desktop\\importsnt\\Csharp\\Standa Stage Control Environment\\standa_controller_software\\openTK_painter\\grid-shaders\\VertexShader.vert";
            var fragmentShaderSource = "C:\\Users\\giedr\\Desktop\\importsnt\\Csharp\\Standa Stage Control Environment\\standa_controller_software\\openTK_painter\\grid-shaders\\FragmentShader.frag";

            var viewUniform = new UniformMatrix4("view", _camera.GetViewMatrix());
            var projectionUniform = new UniformMatrix4("projection", _camera.GetProjectionMatrix());

            Action updateUniforms = () =>
            {
                viewUniform.Value = _camera.GetViewMatrix();
                projectionUniform.Value = _camera.GetProjectionMatrix();
            };

            var gridLayer = new RenderLayer(vertexShaderSource, fragmentShaderSource, [viewUniform, projectionUniform], _camera, updateUniforms, UpdateGridLinesIfNeeded);

            var gridLines = CreateGrid(100, 100, 10, 10, 0, 0);

            gridLayer.AddObjectCollection(gridLines);

            return gridLayer;
        }

        private LineObjectCollection CreateGrid(float width, float length, int numberOfLinesX, int numberOfLinesY, float centerX, float centerY )
        {
            var gridLines = new LineObjectCollection()
            {
                lineWidth = 1f,
            };

            var gridColor = new Vector4(0.3f, 0.3f, 0.3f, 1);
            
            for (int i = 1; i < numberOfLinesX; i++)
            {
                var xas = -width/2 + width / numberOfLinesX * i;
                gridLines.AddLine(new Vector3(xas + centerX, -length/ 2 + centerY, 0f), new Vector3(xas + centerX, length/ 2 + centerY, 0f), gridColor);
            }
            for (int j = 1; j < numberOfLinesY; j++)
            {
                var yas = -length/2 + length / numberOfLinesY * j;
                gridLines.AddLine(new Vector3(-width/2 + centerX, yas + centerY, 0f), new Vector3(width/2 + centerX, yas + centerY, 0f), gridColor);
            }

            return gridLines;
        }
        private void UpdateGridLinesIfNeeded()
        {
            // check if camera Distance has changed.
            if (true)
            {
                //var distance = CommandLayer.Camera.Distance; // Math.Abs(CommandLayer.Camera.CameraPosition.Y) + 
                //var centerX = CommandLayer.Camera.ReferencePosition.X;
                //var centerY = CommandLayer.Camera.ReferencePosition.Z;

                //distance = Math.Max(distance, 10);

                //GridLayer.ClearCollections();

                //var widthMax = Math.Min(distance*100,10000);
                
                //// let's make possible dx values of [0.1um .5um 1um 5um 10um 50um 100um 500 um 1mm]
                
                //GridSpacing = distance * 0.1;
                //int numberOfLines = (int)(widthMax / GridSpacing);

                //GridLayer.AddObjectCollection(CreateGrid(widthMax, widthMax, numberOfLines, numberOfLines, 0,0));
                //GridLayer.InitializeCollections();

            }
        }

        public List<RenderLayer> GetRenderLayers()
        {
            return _renderLayers;
        }


        public void UpdateToolPointLayer()
        {
            _controllerManager.ToolInformation.RecalculateToolPosition() ; // Recalculate positions before drawing

            var toolIndeces = ToolPointLayer.RenderCollections.FirstOrDefault()?.GetIndices();
            Vector3 currentPointPos = toolIndeces is not null && toolIndeces.Length == 3 ? new Vector3(toolIndeces[0], toolIndeces[1], toolIndeces[2]) : new Vector3(0, 0, 0);
            if (true || _controllerManager.ToolInformation.Position != currentPointPos)
            {
                ToolPointLayer.ClearCollections() ;
                var pointCollection = new PointObjectCollection();
                pointCollection.AddPoint(_controllerManager.ToolInformation.Position, 20, _controllerManager.ToolInformation.IsOn ? new Vector4(1,0,0,1) : new Vector4(1,1,0,1));
                ToolPointLayer.AddObjectCollection(pointCollection);
                //_toolPointLayer.UpdateUniforms();
                ToolPointLayer.InitializeCollections();
            }
        }


        public RenderLayer CreateCommandLayer()
        {

            var vertexShaderSource = "C:\\Users\\giedr\\OneDrive\\Desktop\\importsnt\\Csharp\\Standa Stage Control Environment\\standa_controller_software\\ConsoleApplication_For_Tests\\Shaders\\LineDrawingLayer\\VertexShader.vert";
            var fragmentShaderSource = "C:\\Users\\giedr\\OneDrive\\Desktop\\importsnt\\Csharp\\Standa Stage Control Environment\\standa_controller_software\\ConsoleApplication_For_Tests\\Shaders\\LineDrawingLayer\\FragmentShader.frag";

            var viewUniform = new UniformMatrix4("view", _camera.GetViewMatrix());
            var projectionUniform = new UniformMatrix4("projection", _camera.GetProjectionMatrix());

            Action updateUniforms = () => 
                {
                    viewUniform.Value = _camera.GetViewMatrix();
                    projectionUniform.Value = _camera.GetProjectionMatrix();
                };

            var renderLayer = new RenderLayer(vertexShaderSource, fragmentShaderSource, [viewUniform, projectionUniform], _camera, updateUniforms);
            renderLayer.AddObjectCollection(_lineCollection);

            return renderLayer;
        }

        
        public void PaintCommandQueue(IEnumerable<Command[]> commandLines)
        {
            
            var controllerManager_virtual = _controllerManager.CreateAVirtualCopy();


            var masterPainterController = new PositionAndShutterController_Painter("painter",_log, _lineCollection, controllerManager_virtual.ToolInformation);
            controllerManager_virtual.AddController(masterPainterController);
            foreach(var (controllerName, controller) in controllerManager_virtual.Controllers)
            {
                if(controller != masterPainterController)
                {
                    controller.MasterController = masterPainterController;
                    masterPainterController.AddSlaveController(controller, controllerManager_virtual.ControllerLocks[controllerName]);
                }
            }
            
            var commandManager_virtual = new CommandManager(controllerManager_virtual, new System.Collections.Concurrent.ConcurrentQueue<string>());
            
            CommandLayer.ClearCollections();
            _lineCollection.ClearCollection();

            foreach (var commandLine in commandLines)
            {
                commandManager_virtual.TryExecuteCommandLine(commandLine).GetAwaiter().GetResult();
            }

            CommandLayer.AddObjectCollection(_lineCollection);
        }


    }
}
