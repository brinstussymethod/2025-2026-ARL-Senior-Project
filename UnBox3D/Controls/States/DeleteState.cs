using UnBox3D.Models;
using UnBox3D.Commands;
using UnBox3D.Rendering;
using UnBox3D.Rendering.OpenGL;

namespace UnBox3D.Controls.States
{
    public class DeleteState : IState
    {
        private readonly ISceneManager  _sceneManager;
        private readonly IGLControlHost _glControlHost;
        private readonly IRayCaster     _rayCaster;
        private readonly ICamera        _camera;
        private readonly ICommandHistory _commandHistory;

        public DeleteState(IGLControlHost glControlHost, ISceneManager sceneManager,
                           ICamera camera, IRayCaster rayCaster, ICommandHistory commandHistory)
        {
            _glControlHost   = glControlHost;
            _sceneManager    = sceneManager;
            _camera          = camera;
            _rayCaster       = rayCaster;
            _commandHistory  = commandHistory;
        }

        public void OnMouseDown(MouseEventArgs e)
        {
            // FIX: Raycast HERE in the state, then pass the specific mesh to the command.
            // DeleteCommand.Execute() previously did its own raycast, which fails during
            // redo (no mouse click → ray hits nothing → silent no-op).
            OpenTK.Mathematics.Vector3 origin    = _camera.Position;
            OpenTK.Mathematics.Vector3 direction = _rayCaster.GetRay();

            if (_rayCaster.RayIntersectsMesh(
                    _sceneManager.GetMeshes(), origin, direction,
                    out float _, out IAppMesh hitMesh))
            {
                var deleteCommand = new DeleteCommand(_glControlHost, _sceneManager, hitMesh);
                _commandHistory.PushCommand(deleteCommand);
                deleteCommand.Execute();
            }
        }

        public void OnMouseMove(MouseEventArgs e) { }
        public void OnMouseUp(MouseEventArgs e)   { }
    }
}
