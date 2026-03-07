using UnBox3D.Rendering;
using UnBox3D.Rendering.OpenGL;

namespace UnBox3D.Commands
{
    public class ReplaceMeshCommand : ICommand
    {
        private readonly ISceneManager  _sceneManager;
        private readonly IGLControlHost _glControlHost;
        private readonly IAppMesh       _oldMesh;
        private readonly IAppMesh       _newMesh;

        public ReplaceMeshCommand(
            ISceneManager  sceneManager,
            IGLControlHost glControlHost,
            IAppMesh       oldMesh,
            IAppMesh       newMesh)
        {
            _sceneManager  = sceneManager  ?? throw new ArgumentNullException(nameof(sceneManager));
            _glControlHost = glControlHost ?? throw new ArgumentNullException(nameof(glControlHost));
            _oldMesh       = oldMesh       ?? throw new ArgumentNullException(nameof(oldMesh));
            _newMesh       = newMesh       ?? throw new ArgumentNullException(nameof(newMesh));
        }

        public void Execute()
        {
            _sceneManager.ReplaceMesh(_oldMesh, _newMesh);
            _glControlHost.Invalidate();
        }

        public void Undo()
        {
            _sceneManager.ReplaceMesh(_newMesh, _oldMesh);
            _glControlHost.Invalidate();
        }
    }
}
