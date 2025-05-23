﻿

using Microsoft.Extensions.Logging;
using standa_control_software_WPF.view_models.commands;
using standa_control_software_WPF.view_models.system_control.information;
using standa_controller_software.device_manager;
using standa_controller_software.device_manager.devices;
using System.Collections.ObjectModel;
using System.Numerics;
using System.Windows.Input;

namespace standa_control_software_WPF.view_models.system_control
{
    /// <summary>
    /// View model responsible for managing system information, including device acquisitions and tool position tracking.
    /// </summary>
    public class SystemInformationViewModel : ViewModelBase
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ControllerManager _controllerManager;
        private readonly standa_controller_software.command_manager.CommandManager _commandManager;
        private double _acquisitionDuration;
        private bool _isContiniousAcquisition;

        public Vector3 ToolPos
        {
            get => _controllerManager.ToolInformation!.Position;
        }
        public double AcquisitionDuration
        {
            get => _acquisitionDuration;
            set
            {
                _acquisitionDuration = value;
                OnPropertyChanged(nameof(AcquisitionDuration));
            }
        }
        public bool IsContiniousAcquisition
        {
            get { return _isContiniousAcquisition; }
            set
            {
                if (value == true && _isContiniousAcquisition == false)
                {
                    StartContiniousAcquisition();
                    _isContiniousAcquisition = value;
                    OnPropertyChanged(nameof(IsContiniousAcquisition));
                }
                else if (value == false && _isContiniousAcquisition == true)
                {
                    StopContiniousAcquisition();
                    _isContiniousAcquisition = value;
                    OnPropertyChanged(nameof(IsContiniousAcquisition));
                }
            }
        }
        public ObservableCollection<DeviceViewModel> Devices { get; set; }
        public ToolViewModel ToolViewModel { get; set; }

        public ICommand AcquireCommand => new RelayCommand(StartAcquisition);

        public SystemInformationViewModel(ControllerManager controllerManager, standa_controller_software.command_manager.CommandManager commandManager, ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _controllerManager = controllerManager;
            _commandManager = commandManager;
            Devices = [];

            foreach (BaseDevice device in _controllerManager.GetDevices<BaseDevice>())
            {
                var deviceViewModel = CreateViewModelForDevice(device);
                if(deviceViewModel is not null)
                    Devices.Add(deviceViewModel);
            }
            if (_controllerManager.ToolInformation is null)
                throw new Exception("Configuration lacks tool information.");
            
            ToolViewModel = new ToolViewModel(_controllerManager.ToolInformation);
        }
        /// <summary>
        /// Stops continuous data acquisition for all devices and the tool.
        /// </summary>
        private void StopContiniousAcquisition()
        {
            foreach (var deviceViewModel in Devices)
            {
                deviceViewModel.StopAcquisition();
            }
    
            ToolViewModel.StopAcquisition();
        }
        /// <summary>
        /// Starts continuous data acquisition for all devices that require tracking and the tool.
        /// </summary>
        private void StartContiniousAcquisition()
        {
            foreach (var deviceViewModel in Devices)
            {
                if (deviceViewModel.NeedsToBeTracked)
                {
                    deviceViewModel.StartAcquisition();
                }
            }
            if (ToolViewModel.NeedsToBeTracked)
                ToolViewModel.StartAcquisition();
        }
        /// <summary>
        /// Starts a single data acquisition session for all devices that require tracking and the tool.
        /// The acquisition stops automatically after the specified duration.
        /// </summary>
        private void StartAcquisition()
        {
            foreach (var deviceViewModel in Devices.OfType<PositionerDeviceViewModel>())
            {
                if (deviceViewModel.NeedsToBeTracked)
                {
                    deviceViewModel.StartAcquisition();
                }
            }
            if (ToolViewModel.NeedsToBeTracked)
                ToolViewModel.StartAcquisition();

            // Stop acquisition after the specified duration
            Task.Delay(TimeSpan.FromSeconds(AcquisitionDuration)).ContinueWith(_ =>
            {
                foreach (var deviceViewModel in Devices.OfType<PositionerDeviceViewModel>())
                {
                    deviceViewModel.StopAcquisition();
                }
                if (ToolViewModel.NeedsToBeTracked)
                    ToolViewModel.StopAcquisition();
            });
        }
        /// <summary>
        /// Factory method to create a ViewModel based on the device type
        /// </summary>
        /// <param name="device">The base device instance.</param>
        /// <returns>A specialized <see cref="DeviceViewModel"/> if applicable; otherwise, <c>null</c>.</returns>
        private DeviceViewModel? CreateViewModelForDevice(BaseDevice device)
        {
            if (device is BasePositionerDevice positionerDevice)
            {
                return new PositionerDeviceViewModel(positionerDevice, _commandManager,_controllerManager, _loggerFactory);
            }
            else if (device is BaseShutterDevice shutterDevice)
            {
                return new ShutterDeviceViewModel(shutterDevice, _commandManager, _controllerManager);
            }

            return null;
        }
    }


}
