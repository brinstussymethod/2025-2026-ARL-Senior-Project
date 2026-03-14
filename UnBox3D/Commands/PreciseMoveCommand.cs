using OpenTK.Mathematics;
using UnBox3D.Rendering;

namespace UnBox3D.Commands
{
    public class PreciseMoveCommand : ICommand
    {
        private readonly IAppMesh _mesh;
        private readonly Vector3  _delta;

        public PreciseMoveCommand(IAppMesh mesh, Vector3 delta)
        {
            _mesh  = mesh ?? throw new ArgumentNullException(nameof(mesh));
            _delta = delta;
        }

        public void Execute() => _mesh.Translate(_delta);
        public void Undo()    => _mesh.Translate(-_delta);
    }
}
