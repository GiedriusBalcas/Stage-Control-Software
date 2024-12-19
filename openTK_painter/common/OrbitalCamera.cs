using OpenTK.Audio.OpenAL;
using OpenTK.Mathematics;
using System.Reflection.Emit;

namespace opentk_painter_library.common
{
    public class OrbitalCamera
    {

        private Vector3 _referencePosition;
        public Vector3 ReferencePosition
        {
            get { return _referencePosition; }
            set
            {
                _referencePosition = value;
                UpdateVectors();
                UpdateCameraPosition();
            }
        }
        public Vector3 CameraPosition { get; set; }

        public Vector3 Front { get; private set; }
        public Vector3 Right { get; private set; }
        public Vector3 Up { get; private set; }

        private float _yaw;
        private float _pitch;
        private float _distance;
        private float _fovy;

        public float AspectRatio { get; set; }

        public float FovY
        {
            get => MathHelper.RadiansToDegrees(_fovy);
            set
            {
                var angle = MathHelper.Clamp(value, 1f, 90f);
                _fovy = MathHelper.DegreesToRadians(angle);
            }
        }

        public bool IsOrthographic { get; set; }

        public OrbitalCamera(float aspectRatio, float fOVY)
        {
            ReferencePosition = Vector3.Zero;
            CameraPosition = new Vector3(10f, 10f, 10f);
            AspectRatio = aspectRatio;

            // Initialize yaw, pitch, and distance based on initial positions
            _distance = (CameraPosition - ReferencePosition).Length;
            _yaw = MathF.Atan2(CameraPosition.Z - ReferencePosition.Z, CameraPosition.X - ReferencePosition.X);
            _pitch = MathF.Asin((CameraPosition.Y - ReferencePosition.Y) / _distance);
            FovY = fOVY;

            UpdateVectors();
            UpdateCameraPosition();
        }

        public float Yaw
        {
            get => MathHelper.RadiansToDegrees(_yaw);
            set
            {
                var angle = value;
                _yaw = MathHelper.DegreesToRadians(angle);
                UpdateVectors();
                UpdateCameraPosition();
            }
        }

        public float Pitch
        {
            get => MathHelper.RadiansToDegrees(_pitch);
            set
            {
                var angle = MathHelper.Clamp(value, -89.9999f, 89.9999f);
                _pitch = MathHelper.DegreesToRadians(angle);
                UpdateVectors();
                UpdateCameraPosition();
            }
        }

        public float Distance
        {
            get => _distance;
            set
            {
                _distance = Math.Max(value, 0.01f);
                UpdateCameraPosition();
            }
        }

        public bool IsTrackingTool { get; set; }

        private void UpdateVectors()
        {
            // First, the front matrix is calculated using some basic trigonometry.
            Front = new Vector3
            {
                X = MathF.Cos(_pitch) * MathF.Cos(_yaw),
                Y = MathF.Sin(_pitch),
                Z = MathF.Cos(_pitch) * MathF.Sin(_yaw)
            };

            // We need to make sure the vectors are all normalized, as otherwise we would get some funky results.
            Front = Vector3.Normalize(Front);

            // Calculate both the right and the up vector using cross product.
            // Note that we are calculating the right from the global up; this behaviour might
            // not be what you need for all cameras so keep this in mind if you do not want a FPS camera.
            Right = Vector3.Normalize(Vector3.Cross(Front, Vector3.UnitY));
            Up = Vector3.Normalize(Vector3.Cross(Right, Front));
        }

        private void UpdateCameraPosition()
        {
            // Convert spherical coordinates to Cartesian to update the camera position relative to the reference point
            Vector3 newPosition = new Vector3
            {
                X = ReferencePosition.X + _distance * MathF.Cos(_pitch) * MathF.Cos(_yaw),
                Y = ReferencePosition.Y + _distance * MathF.Sin(_pitch),
                Z = ReferencePosition.Z + _distance * MathF.Cos(_pitch) * MathF.Sin(_yaw)
            };

            // Update CameraPosition as a whole
            CameraPosition = newPosition;
        }

        public Matrix4 GetViewMatrix()
        {
            return Matrix4.LookAt(CameraPosition, ReferencePosition, Up);
        }
        public Matrix4 GetViewMatrix(Vector3 targetPosition)
        {
            // Step 1: Calculate the forward vector
            var forward = Vector3.Normalize(targetPosition - CameraPosition);

            // Step 3: Calculate the right vector
            var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));

            // Step 4: Calculate the up vector
            var up = Vector3.Cross(right, forward);

            return Matrix4.LookAt(CameraPosition, targetPosition, up);
        }

        public Matrix4 GetProjectionMatrix()
        {
            if (IsOrthographic)
                return Matrix4.CreateOrthographic(Distance * (float)Math.Tan(_fovy) * AspectRatio, Distance * (float)Math.Tan(_fovy), 0.001f, 10000000.0f);

            return Matrix4.CreatePerspectiveFieldOfView(_fovy, AspectRatio, 0.01f, 10000000.0f);
        }

        public void FitObject(List<System.Numerics.Vector3> positionsNumeric)
        {
            var vertices = positionsNumeric
                    .Select(vertex => new Vector3(vertex.X, vertex.Y, vertex.Z))
                    ;
            if (vertices is null || vertices.Count() < 2)
                return;

            Matrix4 rotationMatrix = Matrix4.CreateRotationY(_pitch) * Matrix4.CreateRotationX(_yaw);

            var minX = vertices.Min(vertex => vertex.X);
            var maxX = vertices.Max(vertex => vertex.X);
            var minY = vertices.Min(vertex => vertex.Y);
            var maxY = vertices.Max(vertex => vertex.Y);
            var minZ = vertices.Min(vertex => vertex.Z);
            var maxZ = vertices.Max(vertex => vertex.Z);

            ReferencePosition = new Vector3((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2);

            var rotatedVertices = vertices.Select(vertex =>
                Vector3.TransformPosition(vertex, GetViewMatrix())).ToList();

            minX = rotatedVertices.Min(vertex => vertex.X);
            maxX = rotatedVertices.Max(vertex => vertex.X);
            minY = rotatedVertices.Min(vertex => vertex.Y);
            maxY = rotatedVertices.Max(vertex => vertex.Y);
            minZ = rotatedVertices.Min(vertex => vertex.Z);
            maxZ = rotatedVertices.Max(vertex => vertex.Z);

            float width = maxX - minX;
            float height = maxY - minY;
            float depth = maxZ - minZ;

            width *= 2;
            height *= 2;
            depth   *= 2;

            float distanceForWidth = (float)(width / 2.0f / Math.Tan(FovY / 2 / 180 * Math.PI) / AspectRatio);
            float distanceForHeight = (float)(height / 2.0f / Math.Tan(FovY / 2 / 180 * Math.PI));
            float distance = Math.Max(distanceForWidth, distanceForHeight) + depth / 2;
            //distance = Math.Max(distance);

            Distance = distance;

            float midX = 1;
            float midY = 1;

            int index = 0;
            while (
                    (
                    Math.Abs(midX) > 0.05
                    || Math.Abs(midY) > 0.05
                    || Math.Abs(width - 2) > 0.1 || Math.Abs(height - 2) > 0.1
                    )
                && index < 50)
            {
                index++;
                rotatedVertices = vertices.Select(vertex =>
                Vector3.TransformPosition(vertex, GetViewMatrix())).ToList();

                var projectedVertices = rotatedVertices.Select(vertex =>
                    Vector3.TransformPerspective(vertex, GetProjectionMatrix())).ToList();


                minX = projectedVertices.Min(vertex => vertex.X);
                maxX = projectedVertices.Max(vertex => vertex.X);
                minY = projectedVertices.Min(vertex => vertex.Y);
                maxY = projectedVertices.Max(vertex => vertex.Y);
                minZ = projectedVertices.Min(vertex => vertex.Z);
                maxZ = projectedVertices.Max(vertex => vertex.Z);

                midX = (minX + maxX) / 2;
                midY = (minY + maxY) / 2;

                ReferencePosition = ReferencePosition - midX / 1 * Right + midY / 1 * Up;

                width = maxX - minX;
                height = maxY - minY;

                width *= 1.5f;
                height *= 1.5f;
                depth *= 1.5f;

                distanceForWidth = Distance - Distance * (1 - width / 2) / 2;
                distanceForHeight = Distance - Distance * (1 - height / 2) / 2;
                distance = Math.Max(distanceForWidth, distanceForHeight);

                if (float.IsFinite(distance))
                    Distance = distance;
            }


        }

    }
}
