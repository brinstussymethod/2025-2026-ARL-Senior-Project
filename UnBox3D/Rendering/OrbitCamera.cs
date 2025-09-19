using OpenTK.Mathematics;
using System;

namespace UnBox3D.Rendering
{
    public class OrbitCamera : ICamera
    {
        private readonly Camera _inner;
        private Vector3 _target;

        // current
        private float _yaw, _pitch, _radius;

        // desired
        private float _targetYaw, _targetPitch, _targetRadius;

        // smoothing
        private const float YawSmooth = 12f;
        private const float PitchSmooth = 12f;
        private const float RadiusSmooth = 10f;

        // limits
        private const float MinPitch = -89f;
        private const float MaxPitch = 89f;
        private const float MinRadius = 0.5f;
        private const float MaxRadius = 100f;

        public OrbitCamera(Vector3 target, float initialRadius, float aspectRatio)
        {
            _inner = new Camera(new Vector3(0, 0, initialRadius), aspectRatio);
            _target = target;

            _yaw = _targetYaw = _inner.Yaw;
            _pitch = _targetPitch = _inner.Pitch;
            _radius = _targetRadius = Math.Clamp(initialRadius, MinRadius, MaxRadius);

            RecomputePosition();
        }

        public void Update(float dt)
        {
            _yaw = Smooth(_yaw, _targetYaw, YawSmooth, dt);
            _pitch = Smooth(_pitch, _targetPitch, PitchSmooth, dt);
            _radius = Smooth(_radius, _targetRadius, RadiusSmooth, dt);

            _pitch = Math.Clamp(_pitch, MinPitch, MaxPitch);
            _radius = Math.Clamp(_radius, MinRadius, MaxRadius);

            _inner.Yaw = _yaw;
            _inner.Pitch = _pitch;
            RecomputePosition();
        }

        private static float Smooth(float current, float target, float k, float dt)
            => current + (target - current) * (1f - MathF.Exp(-k * dt));

        private void RecomputePosition()
        {
            var front = _inner.Front;              // derived from yaw/pitch
            _inner.Position = _target - front * _radius;
        }

        // ----- Public controls -----
        public void Orbit(float deltaYawDeg, float deltaPitchDeg)
        {
            _targetYaw += deltaYawDeg;
            _targetPitch = Math.Clamp(_targetPitch + deltaPitchDeg, MinPitch, MaxPitch);
        }

        public void Dolly(float deltaRadius)
        {
            _targetRadius = Math.Clamp(_targetRadius + deltaRadius, MinRadius, MaxRadius);
        }

        public void SetTarget(Vector3 target) => _target = target;

        public void OffsetTarget(Vector3 delta)
        {
            _target += delta;
        }

        // ICamera passthroughs
        public Vector3 Position { get => _inner.Position; set { _inner.Position = value; } }
        public float AspectRatio { get => _inner.AspectRatio; set => _inner.AspectRatio = value; }
        public float Pitch { get => _pitch; set { _pitch = _targetPitch = Math.Clamp(value, MinPitch, MaxPitch); } }
        public float Yaw { get => _yaw; set { _yaw = _targetYaw = value; } }
        public float Fov { get => _inner.Fov; set => _inner.Fov = value; }
        public Vector3 Front => _inner.Front;
        public Vector3 Up => _inner.Up;
        public Vector3 Right => _inner.Right;
        public Matrix4 GetViewMatrix() => _inner.GetViewMatrix();
        public Matrix4 GetProjectionMatrix() => _inner.GetProjectionMatrix();
    }
}
