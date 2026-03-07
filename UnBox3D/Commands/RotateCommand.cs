using OpenTK.Mathematics;
using UnBox3D.Rendering;

namespace UnBox3D.Commands
{
    /// <summary>
    /// Records a rotation gesture.
    ///
    /// Because RotateState applies the rotation live (incrementally during the drag),
    /// the very first call to Execute() must be a no-op — the rotation is already on the mesh.
    /// Every subsequent call (i.e. Redo after Undo) re-applies the forward rotation.
    /// </summary>
    public class RotateCommand : ICommand
    {
        private readonly IAppMesh   _mesh;
        private readonly Quaternion _doRotation;    // total forward rotation (stored for Redo)
        private readonly Quaternion _undoRotation;  // inverse (stored for Undo)
        private bool _firstExecute = true;          // first call is a no-op (already applied live)

        /// <param name="mesh">The mesh that was rotated.</param>
        /// <param name="doRotation">The total forward rotation applied during the drag.</param>
        /// <param name="undoRotation">The exact inverse of doRotation.</param>
        public RotateCommand(IAppMesh mesh, Quaternion doRotation, Quaternion undoRotation)
        {
            _mesh         = mesh         ?? throw new ArgumentNullException(nameof(mesh));
            _doRotation   = doRotation;
            _undoRotation = undoRotation;
        }

        public void Execute()
        {
            if (_firstExecute)
            {
                // Rotation was already applied live during the drag — skip.
                _firstExecute = false;
                return;
            }
            // Redo path: re-apply the forward rotation.
            _mesh.Rotate(_doRotation);
        }

        public void Undo()
        {
            _firstExecute = false;   // after an undo, next Execute() is always a redo
            _mesh.Rotate(_undoRotation);
        }
    }
}
