using UnBox3D.Utils;
using OpenTK.Graphics.OpenGL;
using System.Collections.ObjectModel;
using UnBox3D.Rendering.OpenGL;
using OpenTK.Mathematics;

namespace UnBox3D.Rendering
{
    public interface IRenderer
    {
        void RenderScene(ICamera camera, Shader shader);
    }

    public class SceneRenderer : IRenderer
    {
        private readonly ILogger _logger;
        private readonly ISettingsManager _settingsManager;
        private readonly ISceneManager _sceneManager;

        public SceneRenderer(ILogger logger, ISettingsManager settingsManager, ISceneManager sceneManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _sceneManager = sceneManager ?? throw new ArgumentNullException(nameof(sceneManager));

            _logger.Info("Initializing SceneRenderer");
        }

        public void RenderScene(ICamera camera, Shader shader)
        {
            var meshes = _sceneManager.GetMeshes();

            if (meshes == null || meshes.Count == 0)
            {
                _logger.Warn("No meshes available for rendering.");
                return;
            }
            else
            {
                Vector3 lightPos = new Vector3(1.2f, 1.0f, 2.0f);
                shader.Use();

                // Set uniform values common for the entire scene.
                shader.SetMatrix4("view", camera.GetViewMatrix());
                shader.SetMatrix4("projection", camera.GetProjectionMatrix());
                shader.SetVector3("lightColor", new Vector3(1.0f, 1.0f, 1.0f));
                shader.SetVector3("lightPos", lightPos);
                shader.SetVector3("viewPos", camera.Position);

                foreach (var appMesh in meshes)
                {
                    // Bind the mesh VAO and set specific uniforms.
                    GL.BindVertexArray(appMesh.GetVAO());
                    Matrix4 model = Matrix4.CreateFromQuaternion(appMesh.GetTransform());
                    shader.SetMatrix4("model", model);
                    shader.SetVector3("objectColor", appMesh.GetColor());

                    // Draw the mesh based on its index buffer.
                    GL.DrawElements(PrimitiveType.Triangles, appMesh.GetIndices().Length, DrawElementsType.UnsignedInt, 0);
                }

                // Unbind for safety.
                GL.BindVertexArray(0);
            }
        }
    }
}
