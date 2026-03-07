using OpenTK.Mathematics;
using UnBox3D.Rendering;

namespace UnBox3D.Commands
{
    /// <summary>
    /// Records a translation gesture.
    ///
    /// Because MoveState now applies the translation live (incrementally during drag),
    /// the very first call to Execute() must be a no-op — the mesh is already in its
    /// new position. Every subsequent call (i.e. Redo after Undo) re-applies the delta.
    /// </summary>
    public class MoveCommand : ICommand
    {
        private readonly IAppMesh _mesh;
        private readonly Vector3  _movement;
        private bool _firstExecute = true;   // first call is no-op (already applied live)

        public MoveCommand(IAppMesh mesh, Vector3 movement)
        {
            _mesh     = mesh     ?? throw new ArgumentNullException(nameof(mesh));
            _movement = movement;
        }

        public void Execute()
        {
            if (_firstExecute)
            {
                // Translation was already applied live during the drag — skip.
                _firstExecute = false;
                return;
            }
            // Redo path: re-apply the same delta.
            _mesh.Translate(_movement);
        }

        public void Undo()
        {
            _firstExecute = false;   // after an undo, next Execute() is always a redo
            _mesh.Translate(-_movement);
        }
    }
}
