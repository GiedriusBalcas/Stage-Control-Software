using standa_controller_software.device_manager;
using standa_controller_software.device_manager.devices;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;
using System.Xml.Linq;

namespace standa_controller_software.custom_functions.helpers
{
    public static class CustomFunctionHelper
    {

        //public static void GetLineKinParameters(Dictionary<char, PositionerMovementInformation> positionerMovementInfo, float trajectorySpeed, ControllerManager controllerManager, out float allocatedTime)
        //{

        //    var devices = controllerManager.GetDevices<BasePositionerDevice>();
        //    char[] deviceNames = positionerMovementInfo.Keys.ToArray();


        //    foreach(char name in deviceNames)
        //    {
        //        if (controllerManager.TryGetDevice<BasePositionerDevice>(name, out var positioner))
        //        {
        //            positionerMovementInfo[name].CurrentPosition = positioner.CurrentPosition;
        //            positionerMovementInfo[name].MaxAcceleration = positioner.MaxAcceleration;
        //            positionerMovementInfo[name].MaxDeceleration = positioner.MaxDeceleration;
        //            positionerMovementInfo[name].MaxSpeed = positioner.MaxSpeed;

        //        }
        //        else
        //            throw new Exception($"Unable retrieve positioner device {name}.");
        //    }
                

        //    var startingPoint = controllerManager.ToolInformation.CalculateToolPositionUpdate
        //        ( 
        //            positionerMovementInfo.ToDictionary( positionerInfo => positionerInfo.Key, kvp => kvp.Value.CurrentPosition)
        //        );

        //    var endPoint = controllerManager.ToolInformation.CalculateToolPositionUpdate
        //        (
        //            positionerMovementInfo.ToDictionary(positionerInfo => positionerInfo.Key, kvp => kvp.Value.TargetPosition)
        //        );


        //    float trajectoryLength = (endPoint - startingPoint).Length();

        //    allocatedTime = trajectoryLength / trajectorySpeed;

        //    float[] speedfactors = new float[deviceNames.Length];
        //    float[] stopfactors = new float[deviceNames.Length];
        //    float[] speedValues = new float[deviceNames.Length];
        //    float[] accelValues = new float[deviceNames.Length];
        //    float[] decelValues = new float[deviceNames.Length];
        //    float[] maxAccelValues = new float[deviceNames.Length];
        //    float[] maxDecelValues = new float[deviceNames.Length];
        //    float[] intermediateValueAccel = new float[deviceNames.Length];
        //    float[] intermediateValueDecel = new float[deviceNames.Length];

        //    foreach (var name in deviceNames)
        //    {
        //        positionerMovementInfo[name].TargetSpeed = Math.Min
        //            ( 
        //                Math.Abs((positionerMovementInfo[name].TargetPosition - positionerMovementInfo[name].CurrentPosition) / allocatedTime)
        //                , positionerMovementInfo[name].MaxSpeed
        //            );
        //    }

        //    for (int i = 0; i < deviceNames.Length; i++)
        //    {
        //        var positioner = devices.FirstOrDefault(p => p.Name == deviceNames[i]) ?? throw new Exception($"Move command call on non-registered device: {deviceNames[i]}");
        //        var currentPosition = positioner.CurrentPosition;
        //        var targetPosition = endPositions[i];
        //        var direction = Math.Sign(targetPosition - currentPosition);
        //        //speedfactors[i] = Math.Abs(speedValues[i] * direction - positioner.CurrentSpeed);
        //        speedfactors[i] = Math.Abs(speedValues[i] * direction);

        //        stopfactors[i] = Math.Abs(speedValues[i]); ;

        //    }
        //    for (int i = 0; i < deviceNames.Length; i++)
        //    {
        //        speedfactors[i] = speedfactors[i] / speedfactors.Max();
        //        intermediateValueAccel[i] = (maxAccelValues[i] / speedfactors[i]);
        //        intermediateValueDecel[i] = (maxDecelValues[i] / speedfactors[i]);
        //    }
        //    for (int i = 0; i < deviceNames.Length; i++)
        //    {
        //        int minAccelIdx = Array.IndexOf(intermediateValueAccel, intermediateValueAccel.Min());
        //        int minDecelIdx = Array.IndexOf(intermediateValueAccel, intermediateValueAccel.Min());
        //        accelValues[i] = maxAccelValues[minAccelIdx] / speedfactors[minAccelIdx] * speedfactors[i];
        //        decelValues[i] = maxDecelValues[minDecelIdx] / stopfactors[minDecelIdx] * stopfactors[i];
        //        if (speedfactors[minAccelIdx] == 0 || speedfactors[minAccelIdx] is float.NaN)
        //        {
        //            accelValues[i] = maxAccelValues[minAccelIdx];
        //            decelValues[i] = maxDecelValues[minDecelIdx];
        //        }

        //        //accelValues[i] = speedfactors[i] == 0
        //        //    ? 10000
        //        //    : maxAccelValues[i] / speedfactors[i];
        //        //decelValues[i] = stopfactors[i] == 0
        //        //    ? 10000
        //        //    : maxDecelValues[i] / stopfactors[i];
        //    }

        //    speedValuesOut = speedValues;
        //    accelValuesOut = accelValues;
        //    decelValuesOut = decelValues;
        //}

        public static bool TryGetLineKinParameters(
            Dictionary<char, PositionerMovementInformation> positionerMovementInfo, 
            float trajectorySpeed, 
            ControllerManager controllerManager, 
            out float allocatedTime )
        {
           
            char[] deviceNames = positionerMovementInfo.Keys.ToArray();

            var accelTimes = new Dictionary<char, float>();
            var decelTimes = new Dictionary<char, float>();

            // Populate initial values
            foreach (char name in deviceNames)
            {
                if (controllerManager.TryGetDevice<BasePositionerDevice>(name, out var positioner))
                {
                    positionerMovementInfo[name].CurrentPosition = positioner.CurrentPosition;
                    positionerMovementInfo[name].CurrentSpeed = positioner.CurrentSpeed;
                    positionerMovementInfo[name].MaxAcceleration = positioner.MaxAcceleration;
                    positionerMovementInfo[name].MaxDeceleration = positioner.MaxDeceleration;
                    positionerMovementInfo[name].MaxSpeed = positioner.MaxSpeed;

                }
                else
                    throw new Exception($"Unable retrieve positioner device {name}.");
            }

            //Calculate the initial and final tool positions
            var startToolPoint = controllerManager.ToolInformation.CalculateToolPositionUpdate
                (
                    positionerMovementInfo.ToDictionary(positionerInfo => positionerInfo.Key, kvp => kvp.Value.CurrentPosition)
                );

            var endToolPoint = controllerManager.ToolInformation.CalculateToolPositionUpdate
                (
                    positionerMovementInfo.ToDictionary(positionerInfo => positionerInfo.Key, kvp => kvp.Value.TargetPosition)
                );

            if(startToolPoint == endToolPoint)
            {
                allocatedTime = 0f;
                foreach (char name in deviceNames)
                {
                    if (controllerManager.TryGetDevice<BasePositionerDevice>(name, out var positioner))
                    {
                        positionerMovementInfo[name].CurrentPosition = positioner.CurrentPosition;
                        positionerMovementInfo[name].CurrentSpeed = positioner.CurrentSpeed;
                        positionerMovementInfo[name].MaxAcceleration = positioner.MaxAcceleration;
                        positionerMovementInfo[name].MaxDeceleration = positioner.MaxDeceleration;
                        positionerMovementInfo[name].MaxSpeed = positioner.MaxSpeed;
                        positionerMovementInfo[name].TargetAcceleration = positioner.MaxAcceleration;
                        positionerMovementInfo[name].TargetDeceleration= positioner.MaxDeceleration;
                        positionerMovementInfo[name].TargetSpeed= positioner.MaxSpeed;

                    }
                    else
                        throw new Exception($"Unable retrieve positioner device {name}.");
                }
                return true;
            }

            // Calculate trajectory length and allocate time
            float trajectoryLength = (endToolPoint - startToolPoint).Length();
            allocatedTime = trajectoryLength / trajectorySpeed;

            if (allocatedTime <= 0 || float.IsNaN(allocatedTime))
                return false;

            // Calculate end speeds for each axis
            foreach (var name in deviceNames)
            {
                positionerMovementInfo[name].TargetSpeed = Math.Min
                    (
                        Math.Abs((positionerMovementInfo[name].TargetPosition - positionerMovementInfo[name].CurrentPosition) / allocatedTime)
                        , positionerMovementInfo[name].MaxSpeed
                    );
            }

            // Calculate acceleration and deceleration times
            foreach (var name in deviceNames)
            {
                accelTimes[name] = positionerMovementInfo[name].MaxAcceleration > 0 
                    ? Math.Abs(positionerMovementInfo[name].TargetSpeed - positionerMovementInfo[name].CurrentSpeed) / positionerMovementInfo[name].MaxAcceleration : 0;

                decelTimes[name] = positionerMovementInfo[name].MaxDeceleration > 0
                    ? Math.Abs(positionerMovementInfo[name].TargetSpeed - 0) / positionerMovementInfo[name].MaxDeceleration : 0;

            }

            // Normalize acceleration and deceleration values

            float maxAccelTimeValue = accelTimes.Values.Where(v => v > 0).Max();
            float maxDecelTimeValue = decelTimes.Values.Where(v => v > 0).Max();

            foreach (var name in deviceNames)
            {
                positionerMovementInfo[name].TargetAcceleration = accelTimes[name] > 0
                    ? positionerMovementInfo[name].MaxAcceleration * (accelTimes[name] / maxAccelTimeValue)
                    : positionerMovementInfo[name].MaxAcceleration;

                positionerMovementInfo[name].TargetDeceleration = decelTimes[name] > 0
                    ? positionerMovementInfo[name].MaxDeceleration * (decelTimes[name] / maxDecelTimeValue)
                    : positionerMovementInfo[name].MaxDeceleration;
            }


            // Calculate total time and adjust speed if necessary
            double maxAccel = positionerMovementInfo.Max(positionerInfo => positionerInfo.Value.TargetAcceleration);
            double maxDecel = positionerMovementInfo.Max(positionerInfo => positionerInfo.Value.TargetDeceleration);
            double calculatedTime = CalculateTotalTime(trajectoryLength, trajectorySpeed, maxAccel, maxDecel);

            // Final adjustment of allocated time after speed adjustment
            allocatedTime = (float)calculatedTime;

            return true;
        }

        private static float CalculateTime(float startSpeed, float endSpeed, float acceleration)
        {
            return acceleration > 0 ? Math.Abs(endSpeed - startSpeed) / acceleration : 0;
        }

        private static void NormalizeValues(
            Dictionary<char, float> timeValues,
            Dictionary<char, float> originalValues,
            out Dictionary<char, float> normalizedValues)
        {
            normalizedValues = new Dictionary<char, float>();
            float maxTimeValue = timeValues.Values.Where(v => v > 0).Max();

            foreach (var key in timeValues.Keys)
            {
                normalizedValues[key] = timeValues[key] > 0
                    ? originalValues[key] * (timeValues[key] / maxTimeValue)
                    : originalValues[key];
            }
        }

        private static void AdjustForLimitedTime(
            char[] devNames,
            ref Dictionary<char, float> endSpeeds,
            ref Dictionary<char, float> accelValues,
            ref Dictionary<char, float> decelValues,
            float allocatedTime)
        {
            // Adjust speed, acceleration, and deceleration based on the limited time
            foreach (var name in devNames)
            {
                float maxAccelTime = CalculateTime(0, endSpeeds[name], accelValues[name]);
                float maxDecelTime = CalculateTime(endSpeeds[name], 0, decelValues[name]);
                float totalRequiredTime = maxAccelTime + maxDecelTime;

                if (totalRequiredTime > allocatedTime)
                {
                    float reductionFactor = allocatedTime / totalRequiredTime;
                    endSpeeds[name] *= reductionFactor;
                    accelValues[name] *= reductionFactor;
                    decelValues[name] *= reductionFactor;
                }
            }
        }

        public static double CalculateTotalTime(double pathLength, double targetSpeed, double acceleration, double deceleration)
        {
            double tAcc = targetSpeed / acceleration;
            double dAcc = (targetSpeed * targetSpeed) / (2 * acceleration);
            double tDec = targetSpeed / deceleration;
            double dDec = (targetSpeed * targetSpeed) / (2 * deceleration);

            if (dAcc + dDec <= pathLength)
            {
                double dConst = pathLength - (dAcc + dDec);
                double tConst = dConst / targetSpeed;
                return tAcc + tConst + tDec;
            }
            else
            {
                double vMax = Math.Sqrt((2 * acceleration * deceleration * pathLength) / (acceleration + deceleration));
                return (vMax / acceleration) + (vMax / deceleration);
            }
        }

        //public static bool TryGetLineKinParameters(char[] deviceNames, float[] endPositions, float trajectorySpeed, ControllerManager controllerManager, out float[] speedValuesOut, out float[] accelValuesOut, out float[] decelValuesOut, out float allocatedTime)
        //{
        //    // Set-up values

        //    speedValuesOut = new float[deviceNames.Length];
        //    accelValuesOut = new float[deviceNames.Length];
        //    decelValuesOut = new float[deviceNames.Length];

        //    var devices = controllerManager.GetDevices<IPositionerDevice>();
        //    string[] devNames = deviceNames.Select(c => c.ToString()).ToArray();

        //    var startingSpeeds = new Dictionary<string, float>();
        //    var startingPositions = new Dictionary<string, float>();
        //    var endingPositions = new Dictionary<string, float>();
        //    var endingSpeeds = new Dictionary<string, float>();
        //    var accelTime = new Dictionary<string, float>();
        //    var decelTime = new Dictionary<string, float>();
        //    var accelValues = new Dictionary<string, float>();
        //    var decelValues = new Dictionary<string, float>();
        //    var accelValuesNorm = new Dictionary<string, float>();
        //    var decelValuesNorm = new Dictionary<string, float>();

        //    for (int i = 0; i < devNames.Length; i++)
        //    {
        //        var deviceName = devNames[i];
        //        var positioner = devices.FirstOrDefault(p => p.Name == deviceName) ?? throw new Exception($"Move command call on non-registered device: {deviceName}");
        //        startingPositions[devNames[i]] = positioner.CurrentPosition;
        //        startingSpeeds[devNames[i]] = positioner.CurrentSpeed;
        //        accelValues[devNames[i]] = positioner.MaxAcceleration;
        //        decelValues[devNames[i]] = positioner.MaxDeceleration;
        //    }
        //    var startingToolPoint = controllerManager.ToolInformation.CalculateToolPositionUpdate(startingPositions);

        //    for (int i = 0; i < devNames.Length; i++)
        //    {
        //        endingPositions[devNames[i]] = endPositions[i];
        //    }
        //    var endToolPoint = controllerManager.ToolInformation.CalculateToolPositionUpdate(endingPositions);

        //    float trajectoryLength = (endToolPoint - startingToolPoint).Length();
        //    allocatedTime = trajectoryLength / trajectorySpeed;

        //    if (allocatedTime == 0 || allocatedTime == float.NaN)
        //        return false;
        //    // We need to know the speedValues for each axis.

        //    foreach (var name in devNames)
        //    {
        //        endingSpeeds[name] = Math.Abs(endingPositions[name] - startingPositions[name]) / allocatedTime;
        //    }

        //    // Let's calculate the time needed for acceleration stage of movement.
        //    // Acelleration stage will either be finite or the final speed might be unreachable.
        //    // For now, lets ignore the case where accelTime + decelTime > allocatedTime.

        //    foreach (var name in devNames)
        //    {
        //        accelTime[name] = Math.Abs(endingSpeeds[name] - startingSpeeds[name]) / accelValues[name];
        //        accelTime[name] = float.IsNaN(accelTime[name]) ? 0 : accelTime[name];

        //        decelTime[name] = Math.Abs(endingSpeeds[name]) / decelValues[name];
        //        decelTime[name] = float.IsNaN(decelTime[name]) ? 0 : decelTime[name];
        //    }


        //    // Let's calculate the kin param values

        //    // Value normalization
        //    // Normalize all values by the smallest non-zero value

        //    float maxAccelTimeValue = accelTime.Values.Where(v => v > 0).Max();
        //    float maxDecelTimeValue = decelTime.Values.Where(v => v > 0).Max();

        //    foreach (var key in accelTime.Keys.ToList())
        //    {

        //        accelValuesNorm[key] = accelTime[key] != 0
        //            ? accelValues[key] * (accelTime[key] / maxAccelTimeValue)
        //            : accelValues[key];


        //        decelValuesNorm[key] = decelTime[key] != 0
        //            ? decelValues[key] * (decelTime[key] / maxDecelTimeValue)
        //            : decelValues[key];

        //    }


        //    for (int i = 0; i < devNames.Length; i++)
        //    {
        //        speedValuesOut[i] = endingSpeeds[devNames[i]];
        //        accelValuesOut[i] = accelValuesNorm[devNames[i]];
        //        decelValuesOut[i] = decelValuesNorm[devNames[i]];
        //    }

        //    allocatedTime = (float)CalculateTotalTime(trajectoryLength, trajectorySpeed, accelValues.Values.Where(v => v > 0).Max(), decelValues.Values.Where(v => v > 0).Max());

        //    return true;
        //}

        //public static double CalculateTotalTime(double pathLength, double targetSpeed, double acceleration, double deceleration)
        //{
        //    // Calculate time to reach target speed
        //    double t_acc = targetSpeed / acceleration;
        //    // Calculate distance covered during acceleration
        //    double d_acc = (targetSpeed * targetSpeed) / (2 * acceleration);

        //    // Calculate time to decelerate from target speed to 0
        //    double t_dec = targetSpeed / deceleration;
        //    // Calculate distance covered during deceleration
        //    double d_dec = (targetSpeed * targetSpeed) / (2 * deceleration);

        //    // Check if the path length allows reaching the target speed
        //    if (d_acc + d_dec <= pathLength)
        //    {
        //        // Case 1: Target speed is reached
        //        double d_const = pathLength - (d_acc + d_dec); // Distance at constant speed
        //        double t_const = d_const / targetSpeed; // Time spent at constant speed

        //        double totalTime = t_acc + t_const + t_dec;
        //        return totalTime;
        //    }
        //    else
        //    {
        //        // Case 2: Path is too short to reach target speed
        //        // Solve for the maximum speed that can be reached
        //        double v_max = Math.Sqrt((2 * acceleration * deceleration * pathLength) / (acceleration + deceleration));

        //        // Recalculate time and distance for the maximum achievable speed
        //        t_acc = v_max / acceleration;
        //        t_dec = v_max / deceleration;

        //        double totalTime = t_acc + t_dec;
        //        return totalTime;
        //    }
        //}

    }
}
