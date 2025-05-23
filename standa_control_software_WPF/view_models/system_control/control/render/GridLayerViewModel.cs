﻿using opentk_painter_library;
using opentk_painter_library.common;
using opentk_painter_library.render_objects;
using standa_controller_software.device_manager;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using System.Xml.Serialization;

namespace standa_control_software_WPF.view_models.system_control.control.render
{
    /// <summary>
    /// ViewModel responsible for rendering and managing background grid lines and boundary box.
    /// </summary>
    public class GridLayerViewModel : BaseRenderLayer, INotifyPropertyChanged
    {
        private readonly OrbitalCamera _camera;
        private readonly ToolInformation _toolInformation;
        private UniformMatrix4 _viewUniform;
        private UniformMatrix4 _projectionUniform;
        private LineObjectCollection _lineCollection;
        private Vector4 _gridColor = new Vector4(0.3f, 0.3f, 0.3f, 0.1f);
        private float _distanceToGrid = 1f;
        private double _gridSpacing;

        public double GridSpacing { get => _gridSpacing; private set { _gridSpacing = value; OnPropertyChanged(nameof(GridSpacing)); } }
        public event PropertyChangedEventHandler? PropertyChanged;

        public GridLayerViewModel(OrbitalCamera camera, ToolInformation toolInformation)
        {
            _camera = camera;
            _toolInformation = toolInformation;
            _lineCollection = new LineObjectCollection() { lineWidth = 1 };

            _vertexShader = """
                #version 330 core

                layout(location = 0) in vec3 aPosition;
                layout(location = 1) in vec4 aColor;

                out vec4 vertexColor;
                out vec4 clipSpacePos; // Pass clip space position to fragment shader

                uniform mat4 projection;
                uniform mat4 view;

                void main()
                {
                    // Calculate world position (assuming model matrix is identity)
                    vec4 worldPosition = vec4(aPosition, 1.0);

                    // Calculate clip space position without the view matrix
                    clipSpacePos = projection * view * worldPosition;

                    // Compute final position with view matrix for correct rendering
                    gl_Position = projection * view * worldPosition;

                    // Pass the vertex color
                    vertexColor = aColor;
                }

                """;
            _fragmentShader = """
                #version 330 core

                in vec4 vertexColor;
                in vec4 clipSpacePos; // Received from vertex shader

                out vec4 FragColor;

                void main()
                {
                    // Perform perspective division to get NDC coordinates
                    vec3 ndc = clipSpacePos.xyz / clipSpacePos.w;

                    // Calculate the distance from the fragment to the closest edge in NDC
                    float distX = 1.0 - abs(ndc.x);
                    float distY = 1.0 - abs(ndc.y);
                    float edgeDist = min(distX, distY);

                    // Define the width of the fade effect in NDC space (0.0 to 1.0)
                    float fadeWidth = 0.2;

                    // Compute the alpha value based on edge distance
                    float alpha = clamp(edgeDist / fadeWidth * vertexColor.a, 0.0, 1);

                    // Set the fragment color with the computed alpha
                    FragColor = vec4(vertexColor.rgb, alpha);
                }

                """;

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
            _gridColor = new Vector4(0.3f, 0.3f, 0.3f, 0.1f);

            base.InitializeLayer();
        }
        private void UpdateGrid(float minX, float minY, float minZ, float maxX, float maxY, float maxZ, float dx, float dy)
        {
            if (minX > maxX || minY > maxY)
                throw new Exception("Maximum grid bound coordinate exceeds minimum grid bound.");

            _lineCollection.ClearCollection();
            var startCoordinateX = minX >= 0 ?
                (Math.Floor(minX / dx) + 1) * dx :
                Math.Floor(minX / dx) * dx;

            var startCoordinateY = minY >= 0 ?
                (Math.Floor(minY / dy) + 1) * dy :
                Math.Floor(minY / dy) * dy;

            var numberOfLinesX = Math.Floor((maxX - minX) / dx);
            var numberOfLinesY = Math.Floor((maxY - minY) / dy);

            for (int i = 1; i <= numberOfLinesX; i++)
            {
                float xas = (float)(startCoordinateX + i * dx);
                if(Math.Abs(xas) > 0.1 && Math.Abs(xas - minY) > 0.1 && Math.Abs(xas - maxY) > 0.1)
                    _lineCollection.AddLine(new Vector3(xas, minY, 0f), new Vector3(xas, maxY, 0f), _gridColor);
            }
            for (int j = 1; j <= numberOfLinesY; j++)
            {
                float yas = (float)(startCoordinateY + j * dy);
                if(Math.Abs(yas) > 0.1 && Math.Abs(yas - minY) > 0.1 && Math.Abs(yas - maxY) > 0.1)
                    _lineCollection.AddLine(new Vector3(minX, yas, 0f), new Vector3(maxX, yas, 0f), _gridColor);
            }

            // center lines
            _lineCollection.AddLine(new Vector3(minX, 0, 0.001f), new Vector3(maxX, 0, 0.001f), new Vector4(0.8f, 0.8f, 0.8f, 0.1f));
            _lineCollection.AddLine(new Vector3(0, minY, 0.001f), new Vector3(0, maxY, 0.001f), new Vector4(0.8f, 0.8f, 0.8f, 0.1f));
            // edge lines
            _lineCollection.AddLine(new Vector3(minX, minY, 0f), new Vector3(maxX, minY, 0f), new Vector4(0.8f, 0.8f, 0.8f, 0.1f));
            _lineCollection.AddLine(new Vector3(minX, maxY, 0f), new Vector3(maxX, maxY, 0f), new Vector4(0.8f, 0.8f, 0.8f, 0.1f));
            _lineCollection.AddLine(new Vector3(minX, minY, 0f), new Vector3(minX, maxY, 0f), new Vector4(0.8f, 0.8f, 0.8f, 0.1f));
            _lineCollection.AddLine(new Vector3(maxX, minY, 0f), new Vector3(maxX, maxY, 0f), new Vector4(0.8f, 0.8f, 0.8f, 0.1f));


            // painting the bounding box
            var boundingBoxColor = new Vector4(1,0,0,0.05f);
            // lower rectangle
            _lineCollection.AddLine(new Vector3(minX, minY, minZ), new Vector3(minX, maxY, minZ), boundingBoxColor);
            _lineCollection.AddLine(new Vector3(maxX, minY, minZ), new Vector3(maxX, maxY, minZ), boundingBoxColor);
            _lineCollection.AddLine(new Vector3(minX, maxY, minZ), new Vector3(maxX, maxY, minZ), boundingBoxColor);
            _lineCollection.AddLine(new Vector3(minX, minY, minZ), new Vector3(maxX, minY, minZ), boundingBoxColor);
            // higher rectangle
            _lineCollection.AddLine(new Vector3(minX, minY, maxZ), new Vector3(minX, maxY, maxZ), boundingBoxColor);
            _lineCollection.AddLine(new Vector3(maxX, minY, maxZ), new Vector3(maxX, maxY, maxZ), boundingBoxColor);
            _lineCollection.AddLine(new Vector3(minX, maxY, maxZ), new Vector3(maxX, maxY, maxZ), boundingBoxColor);
            _lineCollection.AddLine(new Vector3(minX, minY, maxZ), new Vector3(maxX, minY, maxZ), boundingBoxColor);
            // vertical lines
            _lineCollection.AddLine(new Vector3(minX, minY, minZ), new Vector3(minX, minY, maxZ), boundingBoxColor);
            _lineCollection.AddLine(new Vector3(minX, maxY, minZ), new Vector3(minX, maxY, maxZ), boundingBoxColor);
            _lineCollection.AddLine(new Vector3(maxX, maxY, minZ), new Vector3(maxX, maxY, maxZ), boundingBoxColor);
            _lineCollection.AddLine(new Vector3(maxX, minY, minZ), new Vector3(maxX, minY, maxZ), boundingBoxColor);


            InitializeCollections();

        }
        public static float ScaleValueInverse(float inputResult)
        {
            if (inputResult <= 0)
                throw new ArgumentException("Result must be a positive number.");

            // Find the closest valid result
            float closestResult = FindClosestValidResult(inputResult, out int n, out int k);
            return closestResult;
        }
        /// <summary>
        /// Finds the closest valid result generated by ScaleValueInverse.
        /// Outputs the corresponding order (n) and k values.
        /// </summary>
        private static float FindClosestValidResult(float input, out int order, out int k)
        {
            // Initialize variables
            order = 0;
            k = 1;
            float power = 1;
            float closest = 0;
            float minDifference = float.MaxValue;

            // To prevent infinite loops, set a reasonable maximum order
            int maxOrder = 20;

            for (int currentOrder = 0; currentOrder <= maxOrder; currentOrder++)
            {
                power = (float)Math.Pow(10, currentOrder);

                // Iterate k from 1 to 10
                for (int currentK = 1; currentK <= 10; currentK++)
                {
                    float currentResult = (currentK - 1) * power + power; // Simplifies to k * power

                    float difference = Math.Abs(currentResult - input);

                    if (difference < minDifference)
                    {
                        minDifference = difference;
                        closest = currentResult;
                        order = currentOrder;
                        k = currentK;
                    }

                    // Early exit if exact match is found
                    if (difference == 0)
                        return closest;
                }
            }

            return closest;
        }
        public override void OnRenderFrameStart()
        {
            // check if camera Distance has changed.
            if (_distanceToGrid != _camera.Distance + Math.Abs(_camera.ReferencePosition.Y))
            {
                var distance = _camera.Distance + Math.Abs(_camera.ReferencePosition.Y);
                distance = Math.Max(1, distance);
                distance = ScaleValueInverse((int)distance);
                distance = Math.Max(distance, 10);
                GridSpacing = distance * 0.1;
                
                UpdateGrid(_toolInformation.MinimumCoordinates.X, _toolInformation.MinimumCoordinates.Y, _toolInformation.MinimumCoordinates.Z, _toolInformation.MaximumCoordinates.X, _toolInformation.MaximumCoordinates.Y, _toolInformation.MaximumCoordinates.Z, (float)GridSpacing, (float)GridSpacing);
                _distanceToGrid = _camera.Distance + Math.Abs(_camera.ReferencePosition.Y); ;
            }
        }
        public override void UpdateUniforms()
        {
            _viewUniform.Value = _camera.GetViewMatrix();
            _projectionUniform.Value = _camera.GetProjectionMatrix();
        }
    }
}
