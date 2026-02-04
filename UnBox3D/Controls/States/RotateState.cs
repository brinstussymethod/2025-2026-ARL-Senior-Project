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
            _accumulatedAngle = 0;

            Vector3 rayWorld = _rayCaster.GetRay();
            Vector3 rayOrigin = _camera.Position;

            if (_rayCaster.RayIntersectsMesh(_sceneManager.GetMeshes(), rayOrigin, rayWorld, out float _, out IAppMesh clickedMesh))
            {
                _selectedMesh = clickedMesh;
                _controlHost.Invalidate();
            }

            // Future implementation: Check for gizmo interaction
            // if (CheckGizmoInteraction(rayOrigin, rayWorld, out Vector3 axis))
            // {
            //     _rotationAxis = Quaternion.FromAxisAngle(axis.Normalized(), 1.0f);
            //     _isRotating = true;
            // }
        }

        public void OnMouseMove(MouseEventArgs e)
        {
            if (_isRotating && _selectedMesh != null)
            {
                Point currentMousePosition = Control.MousePosition;
                Vector2 delta = new Vector2(currentMousePosition.X - _lastMousePosition.X, currentMousePosition.Y - _lastMousePosition.Y);

                float angle = delta.Length * _rotationSensitivity;
                if (angle > 0)
                {
                    Vector3 axis = _rotationAxis.Xyz.Normalized();
                    _rotationAxis = Quaternion.FromAxisAngle(axis, angle);
                }

                _accumulatedAngle += angle;
                _lastMousePosition = currentMousePosition;

                _controlHost.Invalidate();
            }
        }

        public void OnMouseUp(MouseEventArgs e)
        {
            if (_isRotating && _selectedMesh != null && _accumulatedAngle != 0)
            {
                var rotateCommand = new RotateCommand(_selectedMesh, _rotationAxis, _accumulatedAngle);
                _commandHistory.PushCommand(rotateCommand);
                rotateCommand.Execute();
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
