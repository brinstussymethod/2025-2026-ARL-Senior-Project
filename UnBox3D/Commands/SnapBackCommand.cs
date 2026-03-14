using OpenTK.Mathematics;
using UnBox3D.Rendering;

namespace UnBox3D.Commands
{
    public class SnapBackCommand : ICommand
    {
        private readonly IAppMesh _mesh;

        private Vector3    _preSnapRenderCenter;
        private Quaternion _preSnapTransform;
        private bool       _executed = false;

        public SnapBackCommand(IAppMesh mesh)
        {
            _mesh = mesh ?? throw new ArgumentNullException(nameof(mesh));
        }

        public void Execute()
        {
            _preSnapRenderCenter = _mesh.GetRenderCenter();
            _preSnapTransform    = _mesh.GetTransform();
            _executed = true;
            _mesh.SnapToOrigin();
        }

        public void Undo()
        {
            if (!_executed) return;

            _mesh.SetTransform(_preSnapTransform);

            // Render-space delta back to world space (swap Y/Z).
            Vector3 renderDiff = _preSnapRenderCenter - _mesh.GetRenderCenter();
            Vector3 worldDelta = new Vector3(renderDiff.X, renderDiff.Z, renderDiff.Y);
            _mesh.Translate(worldDelta);
        }
    }
}
