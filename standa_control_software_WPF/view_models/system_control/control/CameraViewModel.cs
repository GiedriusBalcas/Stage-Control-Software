using OpenTK.Mathematics;
using opentk_painter_library.common;
using standa_control_software_WPF.view_models.commands;
using standa_controller_software.device_manager;
using System.Windows.Input;

namespace standa_control_software_WPF.view_models.system_control.control
{
    /// <summary>
    /// ViewModel for managing and controlling the camera within the application.
    /// Handles camera transformations, tracking of tools, and view adjustments.
    /// </summary>
    public class CameraViewModel : ViewModelBase
    {
        private readonly OrbitalCamera _camera;
        private readonly PainterManagerViewModel _painterManager;
        private readonly ControllerManager _controllerManager;
        private float _yaw;
        private float _pitch;
        private float _distance;
        private float _fovy;
        private bool _isTrackingTool = false;
        private bool _isOrthographicView = false;
        private Vector2 _referencePositionXYDifference = new(0, 0);
        private float _aspectRatio = 1;

        public bool IsTrackingTool
        {
            get => _isTrackingTool;
            set
            {
                if(value != _isTrackingTool)
                {
                    _isTrackingTool = value;
                    _camera.IsTrackingTool = _isTrackingTool;

                    if (_isTrackingTool)
                    {
                        _controllerManager.ToolInformation!.PositionChanged += ToolInformation_PositionChanged;
                    }
                    else
                    {
                        _controllerManager.ToolInformation!.PositionChanged -= ToolInformation_PositionChanged;
                    }
                    OnPropertyChanged(nameof(IsTrackingTool));
                }
            }
        }
        public bool IsOrthographicView
        {
            get => _isOrthographicView;
            set
            {
                if(value != _isOrthographicView)
                {
                    _isOrthographicView = value;
                    _camera.IsOrthographic = _isOrthographicView;
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
                    _camera.Yaw = _yaw;
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
                    _camera.Pitch = _pitch;
                }
            }
        }
        public float Distance 
        { 
            get => _distance
;
            set
            {
                if(-15 < value && value <= 55)
                {
                    _distance = value;
                    var distanceCalc = ScaleValue((int)_distance);
                    _camera.Distance = distanceCalc;
                }
            }
        }
        public float Fovy { get => _fovy; set => _fovy = value; }
        public Vector2 ReferencePositionXY 
        {
            get => _referencePositionXYDifference;
            set 
            {
                if(value != _referencePositionXYDifference)
                {
                    _referencePositionXYDifference = value;
                    _camera.ReferencePosition +=
                        _camera.Right * _referencePositionXYDifference.X * Math.Abs(_camera.Distance)
                        + _camera.Up * _referencePositionXYDifference.Y * Math.Abs(_camera.Distance);

                    //_painterManager.OrientationLayer.Camera.ReferencePosition = new Vector3(10f,0,0);
                    //_painterManager.OrientationLayer.Camera.CameraPosition = new Vector3(1f,1f,0);
                }
            } 
        }
        public float AspectRatio 
        {
            get => _aspectRatio;
            set 
            {
                if (value != _aspectRatio)
                {
                    _aspectRatio = value;
                    _camera.AspectRatio = _aspectRatio;
                }
            } 
        }
        public float WindowWidth = 100f;
        public float WindowHeight = 100f;

        public ICommand CameraFitObjectCommand { get; set; }
        public ICommand CameraViewXYCommand { get; set; }
        public ICommand CameraViewXZCommand { get; set; }
        public ICommand CameraViewYZCommand { get; set; }

        public CameraViewModel(PainterManagerViewModel painterManager, ControllerManager controllerManager, OrbitalCamera camera)
        {
            _camera = camera;
            _painterManager = painterManager;
            _controllerManager = controllerManager;
            _distance = ScaleValueInverse(_camera.Distance);

            IsTrackingTool = false;
            IsOrthographicView = false;
            CameraViewXYCommand = new RelayCommand(ExecuteCameraViewXYCommand);
            CameraViewXZCommand = new RelayCommand(ExecuteCameraViewXZCommand);
            CameraViewYZCommand = new RelayCommand(ExecuteCameraViewYZCommand);
            CameraFitObjectCommand = new RelayCommand(ExecuteFitCameraCommand);
        }

        private void ToolInformation_PositionChanged(System.Numerics.Vector3 obj)
        {
            var toolPos = obj;
            var pos = new OpenTK.Mathematics.Vector3(toolPos.X, toolPos.Z, toolPos.Y);
            _camera.ReferencePosition = pos;
        }
        /// <summary>
        /// Executes the command to fit the camera view to encompass all objects.
        /// Adjusts the camera's distance based on the fitted view.
        /// </summary>
        private void ExecuteFitCameraCommand()
        {
            var verticesData = _painterManager.CommandLayer.GetCollectionsVerteces();
            var data = verticesData.Select(point => new System.Numerics.Vector3(point.X, point.Y, point.Z)).ToList();
            _camera.FitObject(data);

            _distance = ScaleValueInverse(_camera.Distance);
        }
        /// <summary>
        /// Executes the command to set the camera view to the XY plane.
        /// Adjusts the pitch and yaw angles accordingly.
        /// </summary>
        private void ExecuteCameraViewXYCommand()
        {
            Pitch = 90;
            Yaw = 90;
        }
        /// <summary>
        /// Executes the command to set the camera view to the XZ plane.
        /// Adjusts the pitch and yaw angles accordingly.
        /// </summary>
        private void ExecuteCameraViewXZCommand()
        {
            Pitch = 0;
            Yaw = 90;
        }
        /// <summary>
        /// Executes the command to set the camera view to the YZ plane.
        /// Adjusts the pitch and yaw angles accordingly.
        /// </summary>
        private void ExecuteCameraViewYZCommand()
        {
            Pitch = 0;
            Yaw = 0;
        }
        /// <summary>
        /// Scales an integer value to a float based on its order of magnitude.
        /// This method is used to calculate the camera's distance.
        /// </summary>
        /// <param name="intValue">The integer value to scale.</param>
        /// <returns>The scaled float value.</returns>
        private static float ScaleValue(int intValue)
        {
            var order = Math.Floor((float)intValue / 10);
            var power = Math.Pow(10, order);
            var mod = MathMod(intValue, 10);
            var mult = mod * power;
            var result = mult + power;

            return (float)result;
        }
        /// <summary>
        /// Inversely scales a float value back to an integer based on predefined scaling logic.
        /// </summary>
        /// <param name="inputResult">The scaled float value.</param>
        /// <returns>The original integer value before scaling.</returns>
        /// <exception cref="ArgumentException">Thrown when the input result is not a positive number.</exception>
        public static int ScaleValueInverse(float inputResult)
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
        private static float FindClosestValidResult(float input, out int order, out int k)
        {
            // Initialize variables
            order = 0;
            k = 1;
            float closest = 0;
            float minDifference = float.MaxValue;

            // To prevent infinite loops, set a reasonable maximum order
            int maxOrder = 20;

            for (int currentOrder = 0; currentOrder <= maxOrder; currentOrder++)
            {
                var power = (float)Math.Pow(10, currentOrder);

                // Iterate k from 1 to 10
                for (int currentK = 1; currentK <= 10; currentK++)
                {
                    var currentResult = (currentK - 1) * power + power; // Simplifies to k * power

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
        /// <summary>
        /// Computes the mathematical modulo of two integers.
        /// </summary>
        static int MathMod(int a, int b)
        {
            return (Math.Abs(a * b) + a) % b;
        }
    }
}
