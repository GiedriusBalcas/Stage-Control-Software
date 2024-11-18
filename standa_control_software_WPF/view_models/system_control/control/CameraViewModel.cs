using OpenTK.Mathematics;
using standa_control_software_WPF.view_models.commands;
using standa_controller_software.device_manager;
using standa_controller_software.painter;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Navigation;

namespace standa_control_software_WPF.view_models.system_control.control
{
    public class CameraViewModel : ViewModelBase
    {
        private readonly PainterManager _painterManager;
        private readonly ControllerManager _controllerManager;
        private float _yaw;
        private float _pitch;
        private float _distance = 10f;
        private float _fovy;
        private OpenTK.Mathematics.Vector3 _referencePosition;
        // let's try to translate using this?.
        private OpenTK.Mathematics.Vector3 _referencePosition_reference;

        private bool _isTrackingTool = false;
        private bool _isOrthographicView = false;

        public bool IsTrackingTool
        {
            get => _isTrackingTool;
            set
            {
                if(value != _isTrackingTool)
                {
                    _isTrackingTool = value;
                    _painterManager.CommandLayer.Camera.IsTrackingTool = _isTrackingTool;

                    if (_isTrackingTool)
                    {
                        _controllerManager.ToolInformation.PositionChanged += ToolInformation_PositionChanged;
                    }
                    else
                    {
                        _controllerManager.ToolInformation.PositionChanged -= ToolInformation_PositionChanged;
                    }
                    OnPropertyChanged(nameof(IsTrackingTool));
                }
            }
        }

        private void ToolInformation_PositionChanged(System.Numerics.Vector3 obj)
        {
            var toolPos = obj;
            var pos = new OpenTK.Mathematics.Vector3(toolPos.X, toolPos.Z, toolPos.Y);
            _painterManager.CommandLayer.Camera.ReferencePosition = pos;
        }

        public bool IsOrthographicView
        {
            get => _isOrthographicView;
            set
            {
                if(value != _isOrthographicView)
                {
                    _isOrthographicView = value;
                    _painterManager.CommandLayer.Camera.IsOrthographic = _isOrthographicView;
                    OnPropertyChanged(nameof(IsOrthographicView));
                }
            }
        }

        public float Yaw 
        { 
            get => _yaw; 
            set
            {
                if (value != _yaw)
                {
                    _yaw = value;
                    _painterManager.CommandLayer.Camera.Yaw = _yaw;
                    _painterManager.OrientationLayer.Camera.Yaw = _yaw;

                }
            } 
        }
        public float Pitch
        {
            get => _pitch;
            set
            {
                if (value != _pitch)
                {
                    if (_pitch > 90)
                        _pitch = 90;
                    else if (_pitch < -90)
                        _pitch = -90;
                    else
                        _pitch = value;
                    _painterManager.CommandLayer.Camera.Pitch = _pitch;
                    _painterManager.OrientationLayer.Camera.Pitch = _pitch;
                }
            }
        }
        public float Distance 
        { 
            get => _distance;
            set
            {
                if(-15 < value && value <= 55)
                {
                    _distance = value;
                    var distanceCalc = ScaleValue((int)_distance);
                    _painterManager.CommandLayer.Camera.Distance = distanceCalc;
                }
            }
        }


        public float Fovy { get => _fovy; set => _fovy = value; }

        private Vector2 _referencePositionXYDifference = new Vector2(0,0);
        public Vector2 ReferencePositionXY 
        {
            get => _referencePositionXYDifference;
            set 
            {
                if(value != _referencePositionXYDifference)
                {
                    _referencePositionXYDifference = value;
                    _painterManager.CommandLayer.Camera.ReferencePosition += 
                        _painterManager.CommandLayer.Camera.Right * _referencePositionXYDifference.X * 200 
                        + _painterManager.CommandLayer.Camera.Up * _referencePositionXYDifference.Y * 200;

                    //_painterManager.OrientationLayer.Camera.ReferencePosition = new Vector3(10f,0,0);
                    //_painterManager.OrientationLayer.Camera.CameraPosition = new Vector3(1f,1f,0);
                }
            } 
        }

        private float _aspectRatio = 1;
        public float AspectRatio 
        {
            get => _aspectRatio;
            set 
            {
                if (value != _aspectRatio)
                {
                    _aspectRatio = value;
                    _painterManager.CommandLayer.Camera.AspectRatio = _aspectRatio;
                    _painterManager.ToolPointLayer.Camera.AspectRatio = _aspectRatio;
                    _painterManager.OrientationLayer.Camera.AspectRatio = _aspectRatio;
                }
            } 
        }

        public float WindowWidth = 100f;
        public float WindowHeight = 100f;

        public ICommand CameraFitObjectCommand { get; set; }
        public ICommand CameraViewXYCommand { get; set; }
        public ICommand CameraViewXZCommand { get; set; }
        public ICommand CameraViewYZCommand { get; set; }

        public CameraViewModel(PainterManager painterManager, ControllerManager controllerManager)
        {
            _painterManager = painterManager;
            _controllerManager = controllerManager;

            IsTrackingTool = false;
            IsOrthographicView = false;
            CameraViewXYCommand = new RelayCommand(ExecuteCameraViewXYCommand);
            CameraViewXZCommand = new RelayCommand(ExecuteCameraViewXZCommand);
            CameraViewYZCommand = new RelayCommand(ExecuteCameraViewYZCommand);
            CameraFitObjectCommand = new RelayCommand(ExecuteFitCameraCommand);
        }

        private void ExecuteFitCameraCommand()
        {
            var verticesData = _painterManager.CommandLayer.GetCollectionsVerteces();
            var data = verticesData.Select(point => new System.Numerics.Vector3(point.X, point.Y, point.Z)).ToList();
            _painterManager.CommandLayer.Camera.FitObject(data);

            _distance = ScaleValueInverse(_painterManager.CommandLayer.Camera.Distance);
        }

        private void ExecuteCameraViewXYCommand()
        {
            _painterManager.CommandLayer.Camera.Pitch = 90;
            _painterManager.CommandLayer.Camera.Yaw = 90;

            _painterManager.OrientationLayer.Camera.Pitch = 90;
            _painterManager.OrientationLayer.Camera.Yaw = 90;

        }
        private void ExecuteCameraViewXZCommand()
        {
            _painterManager.CommandLayer.Camera.Pitch = 0;
            _painterManager.CommandLayer.Camera.Yaw = 90;

            _painterManager.OrientationLayer.Camera.Pitch = 0;
            _painterManager.OrientationLayer.Camera.Yaw = 90;

        }
        private void ExecuteCameraViewYZCommand()
        {
            _painterManager.CommandLayer.Camera.Pitch = 0;
            _painterManager.CommandLayer.Camera.Yaw = 0;

            _painterManager.OrientationLayer.Camera.Pitch = 0;
            _painterManager.OrientationLayer.Camera.Yaw = 0;

        }
        
        private float ScaleValue(int intValue)
        {
            var order = Math.Floor((float)intValue / 10);
            var power = Math.Pow(10, order);
            var mod = MathMod(intValue, 10);
            var mult = mod * power;
            var result = mult + power;

            return (float)result;
        }
        // Enhanced Inverse method with closest value logic
        public int ScaleValueInverse(float inputResult)
        {
            if (inputResult <= 0)
                throw new ArgumentException("Result must be a positive number.");

            // Find the closest valid result
            float closestResult = FindClosestValidResult(inputResult, out int n, out int k);

            // Determine the original intValue based on k and n
            int intValue = (n * 10) + (k == 10 ? 9 : k - 1);

            return intValue;
        }

        /// <summary>
        /// Finds the closest valid result generated by ScaleValueInverse.
        /// Outputs the corresponding order (n) and k values.
        /// </summary>
        private float FindClosestValidResult(float input, out int order, out int k)
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
                    float currentResult = (currentK * power) + power;

                    // Adjust currentResult based on the original ScaleValueInverse logic
                    // Note: In the original method, result = mult + power
                    // where mult = mod * power, and mod = k -1 (since intValue = n*10 + (k-1))
                    // Therefore, result = (k -1)*power + power = k*power

                    // Correct calculation based on original method
                    currentResult = (currentK - 1) * power + power; // Simplifies to k * power

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
        static int MathMod(int a, int b)
        {
            return (Math.Abs(a * b) + a) % b;
        }
    }
}
