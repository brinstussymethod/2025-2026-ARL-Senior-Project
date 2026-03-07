using g4;
using OpenTK.Mathematics;
using UnBox3D.Utils;
using UnBox3D.Models;
using UnBox3D.Commands;
using UnBox3D.Controls.States;
using UnBox3D.Rendering;
using UnBox3D.Rendering.OpenGL;
using System;

namespace UnBox3D.Controls.States
{
    public class RotateState : IState
    {
        private readonly ISettingsManager _settingsManager;
        private readonly ISceneManager _sceneManager;
        private readonly IGLControlHost _controlHost;
        private readonly ICamera _camera;
        private readonly IRayCaster _rayCaster;
        private readonly ICommandHistory _commandHistory;

        private IAppMesh? _selectedMesh;
        private bool _isRotating = false;
        private float _rotationSensitivity;
        private Quaternion _rotationAxis;
        private Point _lastMousePosition;
        private float _accumulatedAngle;

        public RotateState(
            ISettingsManager settingsManager,
            ISceneManager sceneManager,
            IGLControlHost controlHost,
            ICamera camera,
            IRayCaster rayCaster,
            ICommandHistory commandHistory)
        {
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _sceneManager = sceneManager ?? throw new ArgumentNullException(nameof(sceneManager));
            _controlHost = controlHost ?? throw new ArgumentNullException(nameof(controlHost));
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));
            _rayCaster = rayCaster ?? throw new ArgumentNullException(nameof(rayCaster));
            _commandHistory = commandHistory ?? throw new ArgumentNullException(nameof(commandHistory));

            _rotationSensitivity = _settingsManager.GetSetting<float>(new UISettings().GetKey(), UISettings.MeshRotationSensitivity);
        }

        public void OnMouseDown(MouseEventArgs e)
        {
            _lastMousePosition = Control.MousePosition;
            _accumulatedAngle  = 0;

            // Default axis: world Y (vertical spin).  Dragging left/right rotates around Y.
            _rotationAxis = new Quaternion(0f, 1f, 0f, 0f); // .Xyz = (0,1,0)

            Vector3 rayWorld = _rayCaster.GetRay();
            Vector3 rayOrigin = _camera.Position;

            if (_rayCaster.RayIntersectsMesh(_sceneManager.GetMeshes(), rayOrigin, rayWorld, out float _, out IAppMesh clickedMesh))
            {
                _selectedMesh = clickedMesh;
                _isRotating   = true;   // FIX: was never set — rotation never fired
                _controlHost.Invalidate();
            }
        }

        public void OnMouseMove(MouseEventArgs e)
        {
            if (!_isRotating || _selectedMesh == null) return;

            Point   currentMousePosition = Control.MousePosition;
            Vector2 delta = new Vector2(
                currentMousePosition.X - _lastMousePosition.X,
                currentMousePosition.Y - _lastMousePosition.Y);

            // Use signed X-delta so dragging left/right has the correct direction.
            float angle = delta.X * _rotationSensitivity;
            if (Math.Abs(angle) > 0.001f)
            {
                Vector3   axis   = _rotationAxis.Xyz.Normalized();
                Quaternion q     = Quaternion.FromAxisAngle(axis, MathHelper.DegreesToRadians(angle));
                _selectedMesh.Rotate(q);     // live visual feedback
                _accumulatedAngle += angle;
            }

            _lastMousePosition = currentMousePosition;
            _controlHost.Invalidate();
        }

        public void OnMouseUp(MouseEventArgs e)
        {
            if (_isRotating && _selectedMesh != null && Math.Abs(_accumulatedAngle) > 0.001f)
            {
                // Rotation was already applied live in OnMouseMove.
                // Store BOTH directions so Undo reverses and Redo re-applies correctly.
                Vector3 axis = _rotationAxis.Xyz.Normalized();
                float   rad  = MathHelper.DegreesToRadians(_accumulatedAngle);

                var doRotation   = Quaternion.FromAxisAngle(axis,  rad);
                var undoRotation = Quaternion.FromAxisAngle(axis, -rad);

                var rotateCommand = new RotateCommand(_selectedMesh, doRotation, undoRotation);
                _commandHistory.PushCommand(rotateCommand);
                // Do NOT call Execute() here — rotation is already on the mesh from OnMouseMove.
            }

            ResetRotationState();
        }

        private void ResetRotationState()
        {
            _isRotating = false;
            _rotationAxis = Quaternion.Identity;
            _accumulatedAngle = 0;
        }
    }
}
