using standa_controller_software.device_manager;
using standa_controller_software.device_manager.devices;
using System.Numerics;

namespace standa_controller_software.custom_functions.helpers
{
    public static class CustomFunctionHelper
    {
        public static void GetLineKinParameters(char[] deviceNames, float[] endPositions, float trajectorySpeed, ControllerManager controllerManager, out float[] speedValuesOut, out float[] accelValuesOut, out float[] decelValuesOut, out float allocatedTime)
        {

            var devices = controllerManager.GetDevices<IPositionerDevice>();
            string[] devNames = deviceNames.Select(c => c.ToString()).ToArray();

            var newPositionDict = new Dictionary<string, float>();
            for (int i = 0; i < devNames.Length; i++)
            {
                var deviceName = devNames[i];
                var positioner = devices.FirstOrDefault(p => p.Name == deviceName) ?? throw new Exception($"Move command call on non-registered device: {deviceName}");
                newPositionDict[devNames[i]] = positioner.Position;
            }
            var startingPoint = controllerManager.ToolInformation.CalculateToolPositionUpdate(newPositionDict);

            newPositionDict = new Dictionary<string, float>();
            for (int i = 0; i < devNames.Length; i++)
            {
                newPositionDict[devNames[i]] = endPositions[i];
            }
            var endPoint = controllerManager.ToolInformation.CalculateToolPositionUpdate(newPositionDict);

            float trajectoryLength = (endPoint - startingPoint).Length();

            allocatedTime = trajectoryLength / trajectorySpeed;

            float[] speedfactors = new float[devNames.Length];
            float[] stopfactors = new float[devNames.Length];
            float[] speedValues = new float[devNames.Length];
            float[] accelValues = new float[devNames.Length];
            float[] decelValues = new float[devNames.Length];
            float[] maxAccelValues = new float[devNames.Length];
            float[] maxDecelValues = new float[devNames.Length];
            float[] intermediateValueAccel = new float[devNames.Length];
            float[] intermediateValueDecel = new float[devNames.Length];


            for (int i = 0; i < devNames.Length; i++)
            {
                var deviceName = devNames[i];
                var positioner = devices.FirstOrDefault(p => p.Name == deviceName) ?? throw new Exception($"Move command call on non-registered device: {deviceName}");
                var currentPosition = positioner.Position;
                var targetPosition = endPositions[i];
                var speed = Math.Abs((targetPosition - currentPosition) / allocatedTime);
                speed = Math.Min(speed, positioner.MaxSpeed);
                speedValues[i] = speed;
                maxAccelValues[i] = positioner.MaxAcceleration;
                maxDecelValues[i] = positioner.MaxDeceleration;
            }
            for (int i = 0; i < devNames.Length; i++)
            {
                var positioner = devices.FirstOrDefault(p => p.Name == devNames[i]) ?? throw new Exception($"Move command call on non-registered device: {devNames[i]}");
                var currentPosition = positioner.Position;
                var targetPosition = endPositions[i];
                var direction = Math.Sign(targetPosition - currentPosition);
                speedfactors[i] = Math.Abs(speedValues[i] * direction - positioner.Speed);
                stopfactors[i] = 1;

            }
            for (int i = 0; i < devNames.Length; i++)
            {
                speedfactors[i] = speedfactors[i] / speedfactors.Max();
                intermediateValueAccel[i] = (maxAccelValues[i] / speedfactors[i]);
                intermediateValueDecel[i] = (maxDecelValues[i] / speedfactors[i]);
            }
            for (int i = 0; i < devNames.Length; i++)
            {
                int minAccelIdx = Array.IndexOf(intermediateValueAccel, intermediateValueAccel.Min());
                int minDecelIdx = Array.IndexOf(intermediateValueAccel, intermediateValueAccel.Min());
                accelValues[i] = maxAccelValues[minAccelIdx] / speedfactors[minAccelIdx] * speedfactors[i];
                decelValues[i] = maxDecelValues[minDecelIdx] / stopfactors[minDecelIdx] * stopfactors[i];
                if (speedfactors[minAccelIdx] == 0)
                {
                    accelValues[i] = maxAccelValues[minAccelIdx];
                    decelValues[i] = maxDecelValues[minDecelIdx];
                }
            }

            speedValuesOut = speedValues;
            accelValuesOut = accelValues;
            decelValuesOut = decelValues;
        }
    }
}
