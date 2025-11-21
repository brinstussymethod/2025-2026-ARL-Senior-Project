using OpenTK.Mathematics;

// Borrowed from https://github.com/opentk/LearnOpenTK/blob/master/Common/Camera.cs
namespace UnBox3D.Rendering
{
    public interface ICamera
    {
        Vector3 Position { get; set; }
        float AspectRatio { get; set; }
        float Pitch { get; set; }
        float Yaw { get; set; }
        float Fov { get; set; }
        float Roll { get; set; }
        Vector3 Front { get; }
        Vector3 Up { get; }
        Vector3 Right { get; }
        Matrix4 GetViewMatrix();
        Matrix4 GetProjectionMatrix();
        void AddRoll(float deltaDegrees); 
    }
    public class Camera: ICamera
    {
        // Those vectors are directions pointing outwards from the camera to define how it rotated.
        private Vector3 _right = Vector3.UnitX;
        private Vector3 _up = Vector3.UnitY;
        private Vector3 _front = -Vector3.UnitZ;
        private float _pitch = 0; // Rotation around the X axis (radians)
        private float _yaw = -MathHelper.PiOver2; // Rotaion around the Y axis (radians)
        private float _fov;  // The field of view of the camera (radians)
        private float _roll = 0f; 

        // Constructor that loads the FOV from settings
        public Camera(Vector3 position, float aspectRatio)
        {
            Position = position;
            AspectRatio = aspectRatio;
            _fov = MathHelper.DegreesToRadians(45);
        }

        public Vector3 Position { get; set; }
        public float AspectRatio { get; set; }
        public Vector3 Front => _front;
        public Vector3 Up => _up;
        public Vector3 Right => _right;

        // We convert from degrees to radians as soon as the property is set to improve performance.
        public float Pitch
        {
            get => MathHelper.RadiansToDegrees(_pitch);
            set
            {
                // Allow full rotation: do not clamp pitch here. Store the angle in radians.
                _pitch = MathHelper.DegreesToRadians(value);
                UpdateVectors();
            }
        }

        // We convert from degrees to radians as soon as the property is set to improve performance.
        public float Yaw
        {
            get => MathHelper.RadiansToDegrees(_yaw);
            set
            {
                _yaw = MathHelper.DegreesToRadians(value);
                UpdateVectors();
            }
        }


        
        public float Roll
        {
            get => MathHelper.RadiansToDegrees(_roll);
            set 
            {
                _roll = MathHelper.DegreesToRadians(value);
                UpdateVectors(); 
            }
        }

        // The field of view (FOV) is the vertical angle of the camera view.
        // We convert from degrees to radians as soon as the property is set to improve performance.
        public float Fov
        {
            get => MathHelper.RadiansToDegrees(_fov);
            set
            {
                var angle = MathHelper.Clamp(value, 1f, 90f);
                _fov = MathHelper.DegreesToRadians(angle);
            }
        }
        public void AddRoll(float deltaDegrees)
        {
            _roll += MathHelper.DegreesToRadians(deltaDegrees);
            UpdateVectors();
        }

        public Matrix4 GetViewMatrix()
        {
            UpdateVectors();
            return Matrix4.LookAt(Position, Position + _front, _up);
        }

        public Matrix4 GetProjectionMatrix()
        {
            return Matrix4.CreatePerspectiveFieldOfView(_fov, AspectRatio, 0.1f, 1000000.0f);
        }

        private void UpdateVectors()
        {
            // Compute orientation as quaternion from yaw (Y), pitch (X) and roll (Z).
            // _yaw, _pitch and _roll are stored in radians.
            var qYaw = Quaternion.FromAxisAngle(Vector3.UnitY, _yaw);
            var qPitch = Quaternion.FromAxisAngle(Vector3.UnitX, _pitch);
            var qRoll = Quaternion.FromAxisAngle(Vector3.UnitZ, _roll);

            // Apply yaw then pitch then roll. The multiplication order below applies
            // q = qYaw * qPitch * qRoll.
            var q = qYaw * qPitch * qRoll;

            // Create rotation matrix from quaternion and transform the basis vectors.
            var rot = Matrix4.CreateFromQuaternion(q);

            // Camera's default forward is -Z in our conventions.
            _front = Vector3.Normalize(Vector3.TransformVector(-Vector3.UnitZ, rot));
            _right = Vector3.Normalize(Vector3.TransformVector(Vector3.UnitX, rot));
            _up = Vector3.Normalize(Vector3.TransformVector(Vector3.UnitY, rot));
        }
    }
}