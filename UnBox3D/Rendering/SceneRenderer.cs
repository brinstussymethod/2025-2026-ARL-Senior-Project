using UnBox3D.Utils;
using OpenTK.Graphics.OpenGL4;
using System.Collections.ObjectModel;
using UnBox3D.Rendering.OpenGL;
using OpenTK.Mathematics;

namespace UnBox3D.Rendering
{
    /// <summary>Controls which parts of the transform gizmo are visible.</summary>
    public enum GizmoMode
    {
        /// <summary>No gizmo drawn (default, all other modes).</summary>
        None,
        /// <summary>Rings only — shown in Rotate mode.</summary>
        RingsOnly,
        /// <summary>Axis arrows only — shown in Move mode.</summary>
        ArrowsOnly,
        /// <summary>Rings + handles + arrows — shown in Gimbal mode.</summary>
        Full
    }

    public interface IRenderer
    {
        void RenderScene(ICamera camera, Shader shader);
        void SetActiveGizmoMesh(IAppMesh? mesh);
        /// <summary>Sets which parts of the gizmo are rendered. Call before or after SetActiveGizmoMesh.</summary>
        void SetGizmoMode(GizmoMode mode);
        /// <summary>
        /// Returns the render-space centre and ring radius that were used when the gizmo was last built.
        /// Returns false (and zeroed-out values) when no mesh is currently active.
        /// GimbalState must use THESE values for hit-testing so the numbers match what is actually drawn.
        /// </summary>
        bool TryGetGizmoInfo(out Vector3 center, out float radius);
    }

    public class SceneRenderer : IRenderer
    {
        private readonly ILogger _logger;
        private readonly ISettingsManager _settingsManager;
        private readonly ISceneManager _sceneManager;
        private readonly GizmoRenderer _gizmoRenderer = new();
        private IAppMesh?  _gizmoMesh;
        private Vector3    _gizmoCenter;
        private float      _gizmoRadius;
        private GizmoMode  _gizmoMode = GizmoMode.None;

        public void SetActiveGizmoMesh(IAppMesh? mesh)
        {
            _gizmoMesh = mesh;

            if (mesh != null)
            {
                _gizmoCenter = mesh.GetRenderCenter();
                _gizmoRadius = Math.Max(mesh.GetRenderRadius() * 1.5f, 0.5f);
                _gizmoRenderer.UpdateRings(_gizmoCenter, _gizmoRadius);
            }
            else
            {
                _gizmoMode = GizmoMode.None;
            }
        }

        public void SetGizmoMode(GizmoMode mode) => _gizmoMode = mode;

        public bool TryGetGizmoInfo(out Vector3 center, out float radius)
        {
            center = _gizmoCenter;
            radius = _gizmoRadius;
            return _gizmoMesh != null;
        }

        public SceneRenderer(ILogger logger, ISettingsManager settingsManager, ISceneManager sceneManager)
        {
            _logger          = logger          ?? throw new ArgumentNullException(nameof(logger));
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _sceneManager    = sceneManager    ?? throw new ArgumentNullException(nameof(sceneManager));

            _logger.Info("Initializing SceneRenderer");
        }

        public void RenderScene(ICamera camera, Shader shader)
        {
            var meshes = _sceneManager.GetMeshes();

            if (meshes == null || meshes.Count == 0)
            {
                _logger.Warn("No meshes available for rendering.");

                // Still draw gizmo rings if a mesh was selected before the scene was cleared
                if (_gizmoMesh != null)
                    _gizmoRenderer.Render(camera.GetViewMatrix(), camera.GetProjectionMatrix());
                return;
            }

            Vector3 lightPos = new(1.2f, 1.0f, 2.0f);
            shader.Use();

            shader.SetMatrix4("view",       camera.GetViewMatrix());
            shader.SetMatrix4("projection", camera.GetProjectionMatrix());
            shader.SetVector3("lightColor", new Vector3(1f, 1f, 1f));
            shader.SetVector3("lightPos",   lightPos);
            shader.SetVector3("viewPos",    camera.Position);

            foreach (var appMesh in meshes)
            {
                GL.BindVertexArray(appMesh.GetVAO());
                Matrix4 model = Matrix4.CreateFromQuaternion(appMesh.GetTransform());
                shader.SetMatrix4("model",       model);
                shader.SetVector3("objectColor", appMesh.GetColor());
                GL.DrawElements(PrimitiveType.Triangles, appMesh.GetIndices().Length, DrawElementsType.UnsignedInt, 0);
            }

            GL.BindVertexArray(0);

            // Draw gizmo on top of scene geometry
            if (_gizmoMesh != null && _gizmoMode != GizmoMode.None)
                _gizmoRenderer.Render(camera.GetViewMatrix(), camera.GetProjectionMatrix(), _gizmoMode);
        }
    }
}
