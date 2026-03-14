using OpenTK.Mathematics;
using UnBox3D.Rendering;

namespace UnBox3D.Commands
{
    public class ScaleCommand : ICommand
    {
        private readonly IAppMesh _mesh;
        private readonly Vector3  _scaleFactors;
        private readonly Vector3  _inverseFactors;

        public ScaleCommand(IAppMesh mesh, Vector3 scaleFactors)
        {
            _mesh = mesh ?? throw new ArgumentNullException(nameof(mesh));

            _scaleFactors = new Vector3(
                scaleFactors.X == 0f ? 1f : scaleFactors.X,
                scaleFactors.Y == 0f ? 1f : scaleFactors.Y,
                scaleFactors.Z == 0f ? 1f : scaleFactors.Z);

            _inverseFactors = new Vector3(
                1f / _scaleFactors.X,
                1f / _scaleFactors.Y,
                1f / _scaleFactors.Z);
        }

        public void Execute() => _mesh.Scale(_scaleFactors);
        public void Undo()    => _mesh.Scale(_inverseFactors);
    }
}
