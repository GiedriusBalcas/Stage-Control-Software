using standa_controller_software.device_manager;
using standa_controller_software.device_manager.devices;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;

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
                newPositionDict[devNames[i]] = positioner.CurrentPosition;
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
                var currentPosition = positioner.CurrentPosition;
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
                var currentPosition = positioner.CurrentPosition;
                var targetPosition = endPositions[i];
                var direction = Math.Sign(targetPosition - currentPosition);
                //speedfactors[i] = Math.Abs(speedValues[i] * direction - positioner.CurrentSpeed);
                speedfactors[i] = Math.Abs(speedValues[i] * direction);

                stopfactors[i] = Math.Abs(speedValues[i]); ;

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
                if (speedfactors[minAccelIdx] == 0 || speedfactors[minAccelIdx] is float.NaN)
                {
                    accelValues[i] = maxAccelValues[minAccelIdx];
                    decelValues[i] = maxDecelValues[minDecelIdx];
                }

                //accelValues[i] = speedfactors[i] == 0
                //    ? 10000
                //    : maxAccelValues[i] / speedfactors[i];
                //decelValues[i] = stopfactors[i] == 0
                //    ? 10000
                //    : maxDecelValues[i] / stopfactors[i];
            }

            speedValuesOut = speedValues;
            accelValuesOut = accelValues;
            decelValuesOut = decelValues;
        }


        public static bool TryGetLineKinParameters(char[] deviceNames, float[] endPositions, float trajectorySpeed, ControllerManager controllerManager, out float[] speedValuesOut, out float[] accelValuesOut, out float[] decelValuesOut, out float allocatedTime)
        {
            // Set-up values

            speedValuesOut = new float[deviceNames.Length];
            accelValuesOut = new float[deviceNames.Length];
            decelValuesOut = new float[deviceNames.Length];

            var devices = controllerManager.GetDevices<IPositionerDevice>();
            string[] devNames = deviceNames.Select(c => c.ToString()).ToArray();
            
            var startingSpeeds = new Dictionary<string, float>();
            var startingPositions = new Dictionary<string, float>();
            var endingPositions = new Dictionary<string, float>();
            var endingSpeeds = new Dictionary<string, float>();
            var accelTime = new Dictionary<string, float>();
            var decelTime = new Dictionary<string, float>();
            var accelValues = new Dictionary<string, float>();
            var decelValues = new Dictionary<string, float>();

            for (int i = 0; i < devNames.Length; i++)
            {
                var deviceName = devNames[i];
                var positioner = devices.FirstOrDefault(p => p.Name == deviceName) ?? throw new Exception($"Move command call on non-registered device: {deviceName}");
                startingPositions[devNames[i]] = positioner.CurrentPosition;
                startingSpeeds[devNames[i]] = positioner.CurrentSpeed;
                accelValues[devNames[i]] = positioner.MaxAcceleration;
                decelValues[devNames[i]] = positioner.MaxDeceleration;
            }
            var startingToolPoint = controllerManager.ToolInformation.CalculateToolPositionUpdate(startingPositions);
            
            for (int i = 0; i < devNames.Length; i++)
            {
                endingPositions[devNames[i]] = endPositions[i];
            }
            var endToolPoint = controllerManager.ToolInformation.CalculateToolPositionUpdate(endingPositions);

            float trajectoryLength = (endToolPoint - startingToolPoint).Length();
            allocatedTime = trajectoryLength / trajectorySpeed;

            if (allocatedTime == 0 || allocatedTime == float.NaN)
                return false;
            // We need to know the speedValues for each axis.
            
            foreach (var name in devNames)
            {
                endingSpeeds[name] = Math.Abs(endingPositions[name] - startingPositions[name]) / allocatedTime;
            }

            // Let's calculate the time needed for acceleration stage of movement.
            // Acelleration stage will either be finite or the final speed might be unreachable.
            // For now, lets ignore the case where accelTime + decelTime > allocatedTime.

            foreach (var name in devNames)
            {
                accelTime[name] = Math.Abs(endingSpeeds[name] - startingSpeeds[name]) / accelValues[name];
                accelTime[name] = float.IsNaN(accelTime[name]) ? 0 : accelTime[name];
            }

            foreach (var name in devNames)
            {
                decelTime[name] = Math.Abs(endingSpeeds[name]) / decelValues[name];
                decelTime[name] = float.IsNaN(decelTime[name]) ? 0 : decelTime[name];
            }

            // Let's calculate the kin param values

            // Value normalization
            // Normalize all values by the smallest non-zero value

            float maxAccelTimeValue = accelTime.Values.Where(v => v > 0).Max();
            float maxDecelTimeValue = decelTime.Values.Where(v => v > 0).Max();

            foreach (var key in accelTime.Keys.ToList())
            {
                if (accelTime[key] != 0)
                {
                    accelValues[key] = accelValues[key] * (accelTime[key] / maxAccelTimeValue);
                }

                if (decelTime[key] != 0)
                {
                    decelValues[key] = decelValues[key] * (decelTime[key] / maxDecelTimeValue);
                }
            }


            for (int i = 0; i < devNames.Length; i++)
            {
                speedValuesOut[i] = endingSpeeds[devNames[i]];
                accelValuesOut[i] = accelValues[devNames[i]];
                decelValuesOut[i] = decelValues[devNames[i]];
            }

            return true;
        }

    }
}
