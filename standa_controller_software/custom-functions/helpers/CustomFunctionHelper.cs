using standa_controller_software.device_manager;
using standa_controller_software.device_manager.devices;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;
using System.Xml.Linq;

namespace standa_controller_software.custom_functions.helpers
{
    public static class CustomFunctionHelper
    {

        

        public static bool TryGetLineKinParameters(
            ControllerManager controllerManager, 
            float? trajectorySpeed, 
            ref Dictionary<char, PositionerMovementInformation> positionerMovementInfo, 
            out float allocatedTime )
        {

            // TODO: Refactor acceleration phase. 
            /// The acceleration values should not be normilized by the 
            /// time it takes to achieve target speed
            /// the distance traveled (on the line trajectory) until the target speeds are reached has to be the same.

            char[] deviceNames = positionerMovementInfo.Keys.ToArray();
            Dictionary<char, float> movementRatio = new Dictionary<char, float>();

            //Calculate the initial and final tool positions
            var startToolPoint = controllerManager.ToolInformation.CalculateToolPositionUpdate
                (
                    positionerMovementInfo.ToDictionary(positionerInfo => positionerInfo.Key, kvp => kvp.Value.StartingPosition)
                );

            var endToolPoint = controllerManager.ToolInformation.CalculateToolPositionUpdate
                (
                    positionerMovementInfo.ToDictionary(positionerInfo => positionerInfo.Key, kvp => kvp.Value.TargetPosition)
                );

            if(startToolPoint == endToolPoint)
            {
                throw new Exception("Error encountered, when trying to get kinematic parameters. Starting point and end point are the same.");
            }

            // TODO: calculate the speed according to DefaultSpeed of positioners used.

            float trajectorySpeedCalculated = (float)((trajectorySpeed is null) ? 100f : trajectorySpeed);

            // Calculate trajectory length
            float trajectoryLength = (endToolPoint - startToolPoint).Length();


            // Calculate the target kinematic parameters
            var projectedMaxAccelerations = new Dictionary<char, float>();
            var projectedMaxDecelerations = new Dictionary<char, float>();
            var projectedMaxSpeeds = new Dictionary<char, float>();

            foreach (char name in deviceNames)
            {
                movementRatio[name] = trajectoryLength / positionerMovementInfo[name].TargetDistance;
                projectedMaxAccelerations[name] = positionerMovementInfo[name].MaxAcceleration * movementRatio[name];
                projectedMaxDecelerations[name] = positionerMovementInfo[name].MaxDeceleration * movementRatio[name];
                projectedMaxSpeeds[name] = positionerMovementInfo[name].MaxSpeed * movementRatio[name];
            }
            var projectedMaxAcceleration = projectedMaxAccelerations.Min(kvp => kvp.Value);
            var projectedMaxDeceleration = projectedMaxDecelerations.Min(kvp => kvp.Value);
            var projectedMaxSpeed = Math.Min(trajectorySpeedCalculated, projectedMaxSpeeds.Min(kvp => kvp.Value));

            var timesToAccel = new Dictionary<char, float>();
            var timesToDecel = new Dictionary<char, float>();

            foreach (char name in deviceNames)
            {
                positionerMovementInfo[name].MaxAcceleration = projectedMaxAcceleration / movementRatio[name];
                positionerMovementInfo[name].MaxDeceleration = projectedMaxDeceleration / movementRatio[name];
                positionerMovementInfo[name].TargetSpeed = projectedMaxSpeed / movementRatio[name];

                // TODO: check if direction is needed here. Also include addiotional deceleration when changing directions.

                int direction = positionerMovementInfo[name].TargetDirection ? 1 : -1;
                timesToAccel[name] = Math.Abs(positionerMovementInfo[name].TargetSpeed * direction - positionerMovementInfo[name].StartingSpeed) / positionerMovementInfo[name].MaxAcceleration;
                timesToDecel[name] = Math.Abs(positionerMovementInfo[name].TargetSpeed * direction - 0) / positionerMovementInfo[name].MaxDeceleration;
            }

            var maxTimeToAccel = timesToAccel.Max(kvp => kvp.Value);
            var maxTimeToDecel = timesToAccel.Max(kvp => kvp.Value);

            foreach (char name in deviceNames)
            {
                positionerMovementInfo[name].TargetAcceleration = positionerMovementInfo[name].MaxAcceleration * (timesToAccel[name] / maxTimeToAccel);
                positionerMovementInfo[name].TargetDeceleration = positionerMovementInfo[name].MaxDeceleration * (timesToDecel[name] / maxTimeToDecel);
            }

            var selectedPosInfo = positionerMovementInfo.Where(kvp => kvp.Value.TargetAcceleration > 0).First();
            var projectedAccel = positionerMovementInfo[selectedPosInfo.Key].TargetAcceleration * movementRatio[selectedPosInfo.Key];
            var projectedDecel = positionerMovementInfo[selectedPosInfo.Key].TargetDeceleration * movementRatio[selectedPosInfo.Key];
            var projectedTargetSpeed = positionerMovementInfo[selectedPosInfo.Key].TargetSpeed * movementRatio[selectedPosInfo.Key];

            allocatedTime = (float)CalculateTotalTime(trajectoryLength, projectedTargetSpeed, projectedAccel, projectedDecel);

            return true;
        }

        public static bool TryGetMaxKinParameters(
            ControllerManager controllerManager,
            float? trajectorySpeed,
            ref Dictionary<char, PositionerMovementInformation> positionerMovementInfo,
            out float allocatedTime)
        {

            // TODO: Refactor acceleration phase. 
            /// The acceleration values should not be normilized by the 
            /// time it takes to achieve target speed
            /// the distance traveled (on the line trajectory) until the target speeds are reached has to be the same.

            char[] deviceNames = positionerMovementInfo.Keys.ToArray();
            Dictionary<char, float> movementRatio = new Dictionary<char, float>();

            // Populate initial values
            foreach (char name in deviceNames)
            {
                if (controllerManager.TryGetDevice<BasePositionerDevice>(name, out var positioner))
                {
                    positionerMovementInfo[name].TargetAcceleration = positionerMovementInfo[name].MaxAcceleration;
                    positionerMovementInfo[name].TargetDeceleration = positionerMovementInfo[name].MaxDeceleration;
                    positionerMovementInfo[name].TargetSpeed = positionerMovementInfo[name].MaxSpeed;
                }
                else
                    throw new Exception($"Unable retrieve positioner device {name}.");
            }

            //Calculate the initial and final tool positions
            var startToolPoint = controllerManager.ToolInformation.CalculateToolPositionUpdate
                (
                    positionerMovementInfo.ToDictionary(positionerInfo => positionerInfo.Key, kvp => kvp.Value.StartingPosition)
                );

            var endToolPoint = controllerManager.ToolInformation.CalculateToolPositionUpdate
                (
                    positionerMovementInfo.ToDictionary(positionerInfo => positionerInfo.Key, kvp => kvp.Value.TargetPosition)
                );

            if (startToolPoint == endToolPoint)
            {
                throw new Exception("Error encountered, when trying to get kinematic parameters. Starting point and end point are the same.");
            }

            // TODO: calculate the speed according to DefaultSpeed of positioners used.

            float trajectorySpeedCalculated = (float)((trajectorySpeed is null) ? 100f : trajectorySpeed);

            // Calculate trajectory length
            float trajectoryLength = (endToolPoint - startToolPoint).Length();


            // Calculate the target kinematic parameters
            var projectedMaxAccelerations = new Dictionary<char, float>();
            var projectedMaxDecelerations = new Dictionary<char, float>();
            var projectedMaxSpeeds = new Dictionary<char, float>();

            foreach (char name in deviceNames)
            {
                movementRatio[name] = trajectoryLength / positionerMovementInfo[name].TargetDistance;
                projectedMaxAccelerations[name] = positionerMovementInfo[name].MaxAcceleration * movementRatio[name];
                projectedMaxDecelerations[name] = positionerMovementInfo[name].MaxDeceleration * movementRatio[name];
                projectedMaxSpeeds[name] = positionerMovementInfo[name].MaxSpeed * movementRatio[name];
            }
            var projectedMaxAcceleration = projectedMaxAccelerations.Min(kvp => kvp.Value);
            var projectedMaxDeceleration = projectedMaxDecelerations.Min(kvp => kvp.Value);
            var projectedMaxSpeed = Math.Min(trajectorySpeedCalculated, projectedMaxSpeeds.Min(kvp => kvp.Value));

            var targetSpeed = new Dictionary<char, float>();

            foreach (char name in deviceNames)
            {
                targetSpeed[name] = projectedMaxSpeed / movementRatio[name];
            }

                // Calculate the time for completion for each axis
                // allocatedTime will equal the slowest one.
                // parameters will alays equal to maxAccel, maxDecell, maxSpeed? yes.

                var timesToComplete = new Dictionary<char, float>();

            foreach (char name in deviceNames)
            {
                timesToComplete[name] = (float)CalculateTotalTime(positionerMovementInfo[name].TargetDistance, targetSpeed[name], positionerMovementInfo[name].MaxAcceleration, positionerMovementInfo[name].MaxDeceleration);
            }
            
            allocatedTime = timesToComplete.Max(kvp=> kvp.Value);

            return true;
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

        public static double CalculateTotalTime(double pathLength, double targetSpeed, double acceleration, double deceleration, double initialSpeed)
        {
            double tAcc = (targetSpeed - initialSpeed) / acceleration;
            double dAcc = (targetSpeed * targetSpeed - initialSpeed * initialSpeed) / (2 * acceleration);
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
                // Compute the maximum speed achievable
                double numerator = 2 * acceleration * deceleration * pathLength + deceleration * initialSpeed * initialSpeed;
                double denominator = acceleration + deceleration;
                double vMax = Math.Sqrt(numerator / denominator);

                double tAccNew = (vMax - initialSpeed) / acceleration;
                double tDecNew = vMax / deceleration;

                return tAccNew + tDecNew;
            }
        }



    }
}
