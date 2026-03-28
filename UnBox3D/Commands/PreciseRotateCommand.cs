using OpenTK.Mathematics;
using UnBox3D.Rendering;

namespace UnBox3D.Commands
{
    public class PreciseRotateCommand : ICommand
    {
        private readonly IAppMesh   _mesh;
        private readonly Quaternion _rotation;
        private readonly Quaternion _inverse;

        public PreciseRotateCommand(IAppMesh mesh, Quaternion rotation)
        {
            _mesh     = mesh ?? throw new ArgumentNullException(nameof(mesh));
            _rotation = rotation;
            _inverse  = Quaternion.Invert(rotation);
        }

        public void Execute() => _mesh.Rotate(_rotation);
        public void Undo()    => _mesh.Rotate(_inverse);
    }
}
