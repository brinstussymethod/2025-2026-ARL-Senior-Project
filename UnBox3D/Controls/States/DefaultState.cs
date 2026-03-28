using g4;
using Assimp;
using OpenTK.Mathematics;
using UnBox3D.Utils;
using UnBox3D.Models;
using UnBox3D.Controls;
using UnBox3D.Controls.States;
using UnBox3D.Rendering;
using UnBox3D.Rendering.OpenGL;

namespace UnBox3D.Controls.States
{
    // InteractionHandler to handle model interaction logic
    public class DefaultState : IState
    {
        private readonly ISceneManager _sceneManager;
        private readonly IGLControlHost _glControlHost;
        private readonly ICamera _camera;
        private readonly IRayCaster _rayCaster;
        private readonly IRenderer? _renderer;
        private readonly Action<IAppMesh?>? _onSelectionChanged;

        public DefaultState(ISceneManager sceneManager, IGLControlHost glControlHost, ICamera camera, IRayCaster rayCaster,
            IRenderer? renderer = null, Action<IAppMesh?>? onSelectionChanged = null)
        {
            _sceneManager = sceneManager;
            _glControlHost = glControlHost;
            _rayCaster = rayCaster;
            _camera = camera;
            _renderer = renderer;
            _onSelectionChanged = onSelectionChanged;
        }

        public void OnMouseDown(MouseEventArgs e)
        {
            // Get the world space ray from the MousePicker
            Vector3 rayWorld = _rayCaster.GetRay();
            Vector3 rayOrigin = _camera.Position;

            // Check for intersection with the model
            if (_rayCaster.RayIntersectsMesh(_sceneManager.GetMeshes(), rayOrigin, rayWorld, out float distance, out IAppMesh selectedMesh))
            {
                _glControlHost.Invalidate(); // Re-render
            }
            else
            {
                // Click missed all meshes — clear selection.
                _onSelectionChanged?.Invoke(null);
                _renderer?.SetActiveGizmoMesh(null);
                _glControlHost.Invalidate();
            }
        }

        public void OnMouseMove(MouseEventArgs e)
        {

        }

        public void OnMouseUp(MouseEventArgs e) 
        {

        }
    }
}
