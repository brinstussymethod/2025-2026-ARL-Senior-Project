using OpenTK.Mathematics;
using UnBox3D.Rendering;
using UnBox3D.Controls.States;
using UnBox3D.Utils;
using UnBox3D.Rendering.OpenGL;

namespace UnBox3D.Controls
{
    public class MouseController
    {
        private bool _isPanning;
        private bool _isYawingAndPitching;
        private Point _lastMousePosition;

        private GLControlHost _glControlHost;
        private readonly ISettingsManager _settingsManager;
        private readonly ICamera _camera;
        private readonly IRayCaster _rayCaster;
        private IState _currentState;

        private readonly float _cameraYawSensitivity;
        private readonly float _cameraPitchSensitivity;
        private readonly float _cameraPanSensitivity;
        private readonly float _zoomSensitivity;

        public MouseController(
            ISettingsManager settingsManager, 
            ICamera camera, 
            IState neutralState, 
            IRayCaster rayCaster,
            GLControlHost glControlHost)
        {
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));
            _currentState = neutralState ?? throw new ArgumentNullException(nameof(neutralState));
            _rayCaster = rayCaster ?? throw new ArgumentNullException(nameof(rayCaster));
            _glControlHost = glControlHost ?? throw new ArgumentNullException(nameof(glControlHost));

            // Apply settings
            _cameraYawSensitivity = _settingsManager.GetSetting<float>(new UISettings().GetKey(), UISettings.CameraYawSensitivity);
            _cameraPitchSensitivity = _settingsManager.GetSetting<float>(new UISettings().GetKey(), UISettings.CameraPitchSensitivity);
            _cameraPanSensitivity = _settingsManager.GetSetting<float>(new UISettings().GetKey(), UISettings.CameraPanSensitivity);
            _zoomSensitivity = _settingsManager.GetSetting<float>(new UISettings().GetKey(), UISettings.ZoomSensitivity);

            // Attach mouse event handlers to WinForms host
            glControlHost.MouseDown += OnMouseDown;
            glControlHost.MouseMove += OnMouseMove;
            glControlHost.MouseUp += OnMouseUp;
            glControlHost.MouseWheel += OnMouseWheel;
        }

        public void SetState(IState newState) => _currentState = newState;
        public IState GetState() => _currentState;

        public void OnMouseDown(object sender, MouseEventArgs e)
        {
            _lastMousePosition = e.Location;

            switch (e.Button)
            {
                case MouseButtons.Left:
                    _currentState?.OnMouseDown(e);
                    break;
                case MouseButtons.Right:
                    _isPanning = true;
                    break;
                case MouseButtons.Middle:
                    _isYawingAndPitching = true;
                    break;
            }
        }

        public void OnMouseMove(object sender, MouseEventArgs e)
        {
            var currentMousePosition = e.Location;
            Vector2 delta = CalculateMouseDelta(currentMousePosition);

            if (_isPanning)
            {
                PanCamera(delta);
            }
            else if (_isYawingAndPitching)
            {
                AdjustCameraYawAndPitch(delta);
            }
            else
            {
                _currentState?.OnMouseMove(e);
            }

            _lastMousePosition = currentMousePosition;
        }

        public void OnMouseUp(object sender, MouseEventArgs e)
        {
            switch (e.Button)
            {
                case MouseButtons.Left:
                    _currentState?.OnMouseUp(e);
                    break;
                case MouseButtons.Right:
                    _isPanning = false;
                    break;
                case MouseButtons.Middle:
                    _isYawingAndPitching = false;
                    break;
            }
        }

        public void OnMouseWheel(object sender, MouseEventArgs e)
        {
            AdjustCameraZoom(e.Delta / 120.0f);
        }

        private void PanCamera(Vector2 delta)
        {
            float deltaX = delta.X / _glControlHost.Width;
            float deltaY = delta.Y / _glControlHost.Height;

            _camera.Position -= _camera.Right * deltaX * _cameraPanSensitivity;
            _camera.Position += _camera.Up * deltaY * _cameraPanSensitivity;
        }

        private void AdjustCameraYawAndPitch(Vector2 delta)
        {
            _camera.Yaw += delta.X * _cameraYawSensitivity;
            _camera.Pitch -= delta.Y * _cameraPitchSensitivity;
        }

        private void AdjustCameraZoom(float delta)
        {
            _camera.Position += _camera.Front * delta * _zoomSensitivity;
        }

        private Vector2 CalculateMouseDelta(System.Drawing.Point currentMousePosition)
        {
            float deltaX = currentMousePosition.X - _lastMousePosition.X;
            float deltaY = currentMousePosition.Y - _lastMousePosition.Y;
            return new Vector2(deltaX, deltaY);
        }
    }
}
