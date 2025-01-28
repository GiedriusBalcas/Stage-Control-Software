using standa_controller_software.device_manager;
using standa_controller_software.device_manager.devices;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;
using System.Xml.Linq;

namespace standa_controller_software.custom_functions.helpers
{
    public static class CustomFunctionHelper
    {
        public static void CalculateKinParametersForMovementInfo(ref PositionerMovementInformation info)
        {

            float x0 = info.StartingMovementParameters.Position;
            float v0 = info.StartingMovementParameters.Speed;
            float vt = info.TargetMovementParameters.TargetSpeed;
            float a = info.TargetMovementParameters.Acceleration;
            float d = info.TargetMovementParameters.Deceleration;
            float x_target = info.TargetMovementParameters.Position;

            (float t1, float t2, float t3, float s1, float s2, float s3, float vMax) = CalculateMainParameters(x0, v0, vt, a, d, x_target);

            info.TargetMovementParameters.TargetSpeed = vMax;

            float totalTime = t1 + t2 + t3;
            int direction = Math.Sign(x_target - x0);

            info.KinematicParameters.TotalTime = totalTime;
            info.KinematicParameters.ConstantSpeedStartTime = t1;
            info.KinematicParameters.ConstantSpeedEndTime = totalTime - t3;
            info.KinematicParameters.ConstantSpeedStartPosition = info.StartingMovementParameters.Position + s1 * direction;
            info.KinematicParameters.ConstantSpeedEndPosition = info.StartingMovementParameters.Position + (s1 + s2) * direction;
        }

        public static (float t1, float t2, float t3, float s1, float s2, float s3, float vMax) CalculateMainParameters(float x0, float v0, float vt, float a, float d, float x_target)
        {

            float totalTime = 0f;
            // Calculate total movement direction
            float deltaX_total = x_target - x0;
            int direction = Math.Sign(deltaX_total); // +1 for positive, -1 for negative

            // Adjust initial speed to movement direction
            float v0_dir = v0 * direction;

            // Keep accelerations and speeds positive
            a = Math.Abs(a);
            d = Math.Abs(d);
            vt = Math.Abs(vt);
            float vMax = vt;

            // If initial speed is in the opposite direction, decelerate to zero first
            if (v0_dir < 0)
            {
                // Time to decelerate to zero speed
                float t_stop = Math.Abs(v0_dir) / d;
                // Distance covered during deceleration
                float s_stop = (v0_dir * v0_dir) / (2 * d);

                totalTime += t_stop;
                deltaX_total -= s_stop * direction; // Remaining distance after stopping

                v0_dir = 0f; // Reset initial speed after stopping
            }

            float deltaX_remaining = deltaX_total * direction; // Ensure positive

            // Determine whether we need to accelerate or decelerate to reach vt
            bool needsAcceleration = v0_dir < vt;


            // Calculate acceleration or deceleration distance and time to reach vt
            float s1;
            float t1;
            if (needsAcceleration)
            {
                // Acceleration phase
                s1 = (vt * vt - v0_dir * v0_dir) / (2 * a);
                t1 = (vt - v0_dir) / a;
            }
            else
            {
                // Deceleration phase
                s1 = (v0_dir * v0_dir - vt * vt) / (2 * d);
                t1 = (v0_dir - vt) / d;
            }

            // Deceleration to stop
            float s3 = (vt * vt) / (2 * d);
            float t3 = vt / d;

            // Total required distance
            float s_total_required = s1 + s3;

            float s2 = 0f;
            float t2 = 0f;

            if (s_total_required > deltaX_remaining)
            {
                // Need to adjust vt for triangular profile
                float numerator = 2 * a * d * deltaX_remaining + (needsAcceleration ? d : a) * v0_dir * v0_dir;
                float denominator = a + d;

                if (denominator == 0)
                {
                    throw new Exception("Denominator in speed calculation is zero.");
                }

                float vMaxSquaredCandidate = numerator / denominator;
                vMaxSquaredCandidate = Math.Max(0, vMaxSquaredCandidate);
                vMax = (float)Math.Sqrt(vMaxSquaredCandidate);

                if (needsAcceleration)
                {
                    s1 = (vMax * vMax - v0_dir * v0_dir) / (2 * a);
                    t1 = (vMax - v0_dir) / a;
                }
                else
                {
                    s1 = (v0_dir * v0_dir - vMax * vMax) / (2 * d);
                    t1 = (v0_dir - vMax) / d;
                }

                s3 = (vMax * vMax) / (2 * d);
                t3 = vMax / d;

                totalTime += t1 + t3;

            }
            else
            {
                // Trapezoidal profile
                s2 = deltaX_remaining - s_total_required;
                t2 = s2 / vt;

                totalTime += t1 + t2 + t3;

            }

            return (t1, t2, t3, s1, s2, s3, vMax);
        }


    }
}
