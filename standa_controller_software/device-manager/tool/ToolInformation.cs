using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager
{
    public class ToolInformation
    {
        private readonly Func<Dictionary<string, float>, Vector3> _positionCalcFunctions;
        private readonly IShutter _shutterDevice;
        private List<IPositionerDevice> _positionerDevices = new List<IPositionerDevice>();
        private Vector3 _position;

        public event Action<Vector3>? PositionChanged;
        public event Action? EngagedStateChanged;

        public Vector3 Position
        {
            get => _position;
            private set
            {
                _position = value;
                PositionChanged?.Invoke(_position);
            }
        }

        private bool _isOn;

        public bool IsOn
        {
            get { return _isOn; }
            private set
            {
                _isOn = value;
                EngagedStateChanged?.Invoke();
            }
        }

        public ToolInformation(IEnumerable<IPositionerDevice> positioners, IShutter shutterDevice, Func<Dictionary<string, float>, Vector3> positionCalculationFunctions)
        {
            _positionCalcFunctions = positionCalculationFunctions;
            foreach (var positioner in positioners)
            {
                _positionerDevices.Add(positioner);
                //positioner.PositionChanged += Positioner_PositionChanged;
            }

            _shutterDevice = shutterDevice;
            _shutterDevice.StateChanged += Shutter_StateChanged;
        }

        private void Shutter_StateChanged(object? sender, EventArgs e)
        {
            UpdateEngagedState();
        }

        private void UpdateEngagedState()
        {
            IsOn = _shutterDevice.IsOn;
        }

        private void Positioner_PositionChanged(object sender, EventArgs e)
        {
            RecalculateToolPosition();
        }

        public void RecalculateToolPosition()
        {
            var devicePositions = new Dictionary<string, float>();
            _positionerDevices.ForEach(device => devicePositions[device.Name] = device.Position);

            Position = _positionCalcFunctions(devicePositions);
        }

        public Vector3 CalculateToolPositionUpdate(Dictionary<string, float> newPositions)
        {
            var devicePositions = new Dictionary<string, float>();
            _positionerDevices.ForEach(device => devicePositions[device.Name] = device.Position);

            foreach (var entry in newPositions)
            {
                devicePositions[entry.Key] = entry.Value;
            }

            var positionResult = _positionCalcFunctions(devicePositions);

            return positionResult;
        }

        public Vector3 CalculateToolPosition(Dictionary<string, float> newPositions)
        {
            var devicePositions = new Dictionary<string, float>();
            foreach (var entry in newPositions)
            {
                devicePositions[entry.Key] = entry.Value;
            }

            var positionResult = _positionCalcFunctions(devicePositions);

            return positionResult;
        }

    }
}

