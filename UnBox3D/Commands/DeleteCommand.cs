using OpenTK.Mathematics;
using UnBox3D.Rendering;
using UnBox3D.Rendering.OpenGL;

namespace UnBox3D.Commands
{
    /// <summary>
    /// Deletes a specific, pre-identified mesh from the scene.
    /// The mesh is determined by the caller (DeleteState) via raycasting — NOT inside Execute(),
    /// so Redo works correctly without needing a mouse click.
    /// </summary>
    public class DeleteCommand : ICommand
    {
        private readonly IGLControlHost _glControlHost;
        private readonly ISceneManager  _sceneManager;
        private readonly IAppMesh       _mesh;   // the exact mesh to delete/restore

        public DeleteCommand(IGLControlHost glControlHost, ISceneManager sceneManager, IAppMesh mesh)
        {
            _glControlHost = glControlHost  ?? throw new ArgumentNullException(nameof(glControlHost));
            _sceneManager  = sceneManager   ?? throw new ArgumentNullException(nameof(sceneManager));
            _mesh          = mesh           ?? throw new ArgumentNullException(nameof(mesh));
        }

        /// <summary>Delete the stored mesh. Safe to call on Redo — no raycasting needed.</summary>
        public void Execute()
        {
            _sceneManager.DeleteMesh(_mesh);
            _glControlHost.Invalidate();
        }

        /// <summary>Restore the mesh that was deleted.</summary>
        public void Undo()
        {
            _sceneManager.AddMesh(_mesh);
            _glControlHost.Invalidate();
        }
    }
}
