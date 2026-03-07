using OpenTK.Mathematics;
using UnBox3D.Models;
using UnBox3D.Commands;
using UnBox3D.Rendering;
using UnBox3D.Rendering.OpenGL;

namespace UnBox3D.Controls.States
{
    public class MoveState : IState
    {
        private readonly IGLControlHost  _controlHost;
        private readonly ISceneManager   _sceneManager;
        private readonly ICamera         _camera;
        private readonly IRayCaster      _rayCaster;
        private readonly ICommandHistory _commandHistory;

        private bool      _isDragging;
        private Point     _lastMousePosition;
        private Vector3   _totalMovement;      // accumulated world-space delta for undo recording
        private IAppMesh? _selectedMesh;
        private float     _worldScale;         // pixels → world-units conversion factor

        public MoveState(
            IGLControlHost  controlHost,
            ISceneManager   sceneManager,
            ICamera         camera,
            IRayCaster      rayCaster,
            ICommandHistory commandHistory)
        {
            _controlHost    = controlHost    ?? throw new ArgumentNullException(nameof(controlHost));
            _sceneManager   = sceneManager   ?? throw new ArgumentNullException(nameof(sceneManager));
            _camera         = camera         ?? throw new ArgumentNullException(nameof(camera));
            _rayCaster      = rayCaster      ?? throw new ArgumentNullException(nameof(rayCaster));
            _commandHistory = commandHistory ?? throw new ArgumentNullException(nameof(commandHistory));
        }

        public void OnMouseDown(MouseEventArgs e)
        {
            _lastMousePosition = Control.MousePosition;
            _totalMovement     = Vector3.Zero;

            Vector3 rayOrigin    = _camera.Position;
            Vector3 rayDirection = _rayCaster.GetRay();

            if (_rayCaster.RayIntersectsMesh(_sceneManager.GetMeshes(), rayOrigin, rayDirection,
                    out float _, out IAppMesh clickedMesh))
            {
                _selectedMesh = clickedMesh;
                _isDragging   = true;

                // Compute a world-scale factor once so movement feels 1:1 with the mouse.
                // Using camera distance from origin (meshes are centred at origin after import).
                // 0.002 gives ~1 world unit per 500 px at typical camera distance (~30 units).
                float camDist  = _camera.Position.Length;
                _worldScale    = Math.Max(camDist * 0.002f, 0.001f);
            }
        }

        public void OnMouseMove(MouseEventArgs e)
        {
            if (!_isDragging || _selectedMesh == null) return;

            Point current = Control.MousePosition;
            float pxX = current.X - _lastMousePosition.X;
            float pxY = current.Y - _lastMousePosition.Y;

            // Skip sub-pixel noise
            if (Math.Abs(pxX) < 0.5f && Math.Abs(pxY) < 0.5f)
            {
                _lastMousePosition = current;
                return;
            }

            // Convert pixel delta to world-space delta.
            // X mouse → world X,  Y mouse (screen-down) → world -Z (into screen)
            var frameDelta = new Vector3(pxX * _worldScale, 0f, pxY * _worldScale);

            // Apply immediately for live visual feedback (same pattern as RotateState)
            _selectedMesh.Translate(frameDelta);
            _totalMovement += frameDelta;

            _lastMousePosition = current;
            _controlHost.Invalidate();
        }

        public void OnMouseUp(MouseEventArgs e)
        {
            if (_isDragging && _selectedMesh != null && _totalMovement != Vector3.Zero)
            {
                // Movement already applied live in OnMouseMove.
                // Record with _firstExecute=true so Execute() is a no-op on first call
                // but works correctly on Redo.
                var moveCommand = new MoveCommand(_selectedMesh, _totalMovement);
                _commandHistory.PushCommand(moveCommand);
                // Do NOT call Execute() — the mesh is already in its new position.
            }

            _isDragging    = false;
            _totalMovement = Vector3.Zero;
            _selectedMesh  = null;
        }
    }
}
