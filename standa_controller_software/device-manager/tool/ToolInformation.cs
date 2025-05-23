﻿using Microsoft.Extensions.Logging;
using standa_controller_software.command_manager;
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
        private readonly ILogger<ToolInformation> _logger;
        private readonly ControllerManager _controllerManager;
        private readonly BaseShutterDevice _shutterDevice;
        private List<BasePositionerDevice> _positionerDevices = new List<BasePositionerDevice>();
        private Vector3 _position;
        private bool _isOutOfBounds = false;

        public bool IsOutOfBounds { get => _isOutOfBounds; private set 
            {
                if (value != _isOutOfBounds || value is true)
                {
                    _logger.LogError($"New position {value} is outside allowed bounds (Min: {MinimumCoordinates}, Max: {MaximumCoordinates}).");
                    _isOutOfBounds = value;
                    OutOfBoundsChanged?.Invoke(value);
                }
            }
        }
        public Func<Dictionary<char, float>, Vector3> PositionCalcFunctions {  get; private set; }
        public event Action<Vector3>? PositionChanged;
        public event Action? EngagedStateChanged;
        public char Name { get => _shutterDevice.Name;}
        private Vector3 _minimumCoordinates;
        public Vector3 MinimumCoordinates
        {
            get { return _minimumCoordinates; }
            set 
            { 
                _minimumCoordinates = value; 
            }
        }
        private Vector3 _maximumCoordinates;
        public Vector3 MaximumCoordinates
        {
            get { return _maximumCoordinates; }
            set
            {
                _maximumCoordinates = value;
            }
        }
        public Vector3 Position
        {
            get => _position;
            private set
            {
                _position = value;

                if (value.X < MinimumCoordinates.X || value.X > MaximumCoordinates.X ||
                value.Y < MinimumCoordinates.Y || value.Y > MaximumCoordinates.Y ||
                value.Z < MinimumCoordinates.Z || value.Z > MaximumCoordinates.Z)
                {
                    IsOutOfBounds = true;
                }
                else
                {
                    IsOutOfBounds = false;
                }
                
                PositionChanged?.Invoke(_position);
            }
        }
        public bool IsOn
        {
            get 
            { 
                return _shutterDevice.IsOn; 
            }
            private set
            {
                EngagedStateChanged?.Invoke();
            }
        }
        public event Action<bool>? OutOfBoundsChanged;

        public ToolInformation(ControllerManager controllerManager, BaseShutterDevice shutterDevice, Func<Dictionary<char, float>, Vector3> positionCalculationFunctions, ILogger<ToolInformation> logger)
        {
            _logger = logger;
            _controllerManager = controllerManager;
            var positioners = _controllerManager.GetDevices<BasePositionerDevice>();
            PositionCalcFunctions = positionCalculationFunctions;
            foreach (var positioner in positioners)
            {
                _positionerDevices.Add(positioner);
                positioner.PositionChanged += Positioner_PositionChanged; ;
            }

            _shutterDevice = shutterDevice;
            _shutterDevice.StateChanged += OnShutterStateChanged; ;
        }

        private void Positioner_PositionChanged(object? sender, EventArgs e)
        {
            RecalculateToolPosition();
        }
        private void OnShutterStateChanged(object? sender, EventArgs e)
        {
            IsOn = _shutterDevice.IsOn;

        }
        public void RecalculateToolPosition()
        {
            var devicePositions = new Dictionary<char, float>();
            _positionerDevices.ForEach(device => devicePositions[device.Name] = device.CurrentPosition);

            Position = PositionCalcFunctions(devicePositions);
        }
        public Vector3 CalculateToolPositionUpdate(Dictionary<char, float>? newPositions = null)
        {
            var devicePositions = new Dictionary<char, float>();
            _positionerDevices.ForEach(device => devicePositions[device.Name] = device.CurrentPosition);

            if(newPositions is not null) { 
                foreach (var entry in newPositions)
                {
                    devicePositions[entry.Key] = entry.Value;
                }
            }
            var positionResult = PositionCalcFunctions(devicePositions);

            return positionResult;
        }
        public Vector3 CalculateToolPosition(Dictionary<char, float> newPositions)
        {
            var devicePositions = new Dictionary<char, float>();
            foreach (var entry in newPositions)
            {
                devicePositions[entry.Key] = entry.Value;
            }

            var positionResult = PositionCalcFunctions(devicePositions);

            return positionResult;
        }

    }
}

