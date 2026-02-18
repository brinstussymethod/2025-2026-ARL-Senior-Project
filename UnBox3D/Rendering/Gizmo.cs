using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;
using UnBox3D.Utils;
using System;
using System.Collections.Generic;
using Assimp;
using System.Diagnostics;
using UnBox3D.Rendering.OpenGL;

namespace UnBox3D.Rendering
{
    public class Gizmo
    {
        private int _vao;
        private int _vbo;
        private int _ebo;
        private int _vertexCount;
        private Shader _shader;
        private bool _isInitialized = false;

        public void Initialize(string modelPath)
        {
            if (_isInitialized)
                return;

            // Load the FBX model using Assimp
            List<float> vertices = new List<float>();
            List<uint> indices = new List<uint>();

            try
            {
                using (var importer = new AssimpContext())
                {
                    var scene = importer.ImportFile(modelPath,
                        PostProcessSteps.Triangulate |
                        PostProcessSteps.GenerateNormals);

                    if (scene == null || scene.MeshCount == 0)
                    {
                        Debug.WriteLine("Failed to load gizmo model");
                        return;
                    }

                    uint indexOffset = 0;
                    foreach (var mesh in scene.Meshes)
                    {
                        // Add vertices (position + normal)
                        for (int i = 0; i < mesh.VertexCount; i++)
                        {
                            var pos = mesh.Vertices[i];
                            var norm = mesh.HasNormals ? mesh.Normals[i] : new Vector3D(0, 1, 0);

                            vertices.Add(pos.X);
                            vertices.Add(pos.Y);
                            vertices.Add(pos.Z);
                            vertices.Add(norm.X);
                            vertices.Add(norm.Y);
                            vertices.Add(norm.Z);
                        }

                        // Add indices
                        foreach (var face in mesh.Faces)
                        {
                            if (face.IndexCount == 3)
                            {
                                indices.Add((uint)(face.Indices[0] + indexOffset));
                                indices.Add((uint)(face.Indices[1] + indexOffset));
                                indices.Add((uint)(face.Indices[2] + indexOffset));
                            }
                        }

                        indexOffset += (uint)mesh.VertexCount;
                    }
                }

                _vertexCount = indices.Count;

                // Create OpenGL buffers
                _vao = GL.GenVertexArray();
                _vbo = GL.GenBuffer();
                _ebo = GL.GenBuffer();

                GL.BindVertexArray(_vao);

                GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
                GL.BufferData(BufferTarget.ArrayBuffer, vertices.Count * sizeof(float),
                    vertices.ToArray(), BufferUsageHint.StaticDraw);

                GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
                GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Count * sizeof(uint),
                    indices.ToArray(), BufferUsageHint.StaticDraw);

                // Position attribute
                GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 0);
                GL.EnableVertexAttribArray(0);

                // Normal attribute
                GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 3 * sizeof(float));
                GL.EnableVertexAttribArray(1);

                GL.BindVertexArray(0);

                // Use lighting shader for the gizmo
                _shader = ShaderManager.LightingShader;

                _isInitialized = true;
                Debug.WriteLine("Gizmo initialized successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading gizmo model: {ex.Message}");
            }
        }

        public void Render(ICamera camera, int screenWidth, int screenHeight)
        {
            if (!_isInitialized)
                return;

            // Save current viewport
            int[] viewport = new int[4];
            GL.GetInteger(GetPName.Viewport, viewport);

            // Define gizmo viewport (top right corner, 180x180 pixels for more space)
            int gizmoSize = 180;
            int gizmoPosX = screenWidth - gizmoSize - 10;
            int gizmoPosY = screenHeight - gizmoSize - 10;
            GL.Viewport(gizmoPosX, gizmoPosY, gizmoSize, gizmoSize);

            // Clear depth buffer for gizmo rendering
            GL.Clear(ClearBufferMask.DepthBufferBit);

            // Create rotation matrix from camera orientation (without translation)
            Matrix4 gizmoRotation = CreateRotationFromCamera(camera);

            // Apply correction: rotate to match initial camera orientation and flip upside down
            // Z forward, Y up, X right when camera starts at default position
            Matrix4 correctionRotation = Matrix4.CreateRotationX(MathHelper.DegreesToRadians(-90));

            // Scale to a balanced size - big enough to see clearly but not too big
            Matrix4 gizmoModel = Matrix4.CreateScale(0.55f) * correctionRotation * gizmoRotation;

            // Create a simple view matrix (camera looking at origin from close distance)
            Matrix4 gizmoView = Matrix4.LookAt(new Vector3(0, 0, 3), Vector3.Zero, Vector3.UnitY);

            // Create orthographic projection with larger bounds to prevent cropping
            Matrix4 gizmoProjection = Matrix4.CreateOrthographic(3.5f, 3.5f, 0.1f, 100.0f);

            // Render the gizmo
            _shader.Use();
            _shader.SetMatrix4("model", gizmoModel);
            _shader.SetMatrix4("view", gizmoView);
            _shader.SetMatrix4("projection", gizmoProjection);
            _shader.SetVector3("objectColor", new Vector3(0.8f, 0.8f, 0.8f));
            _shader.SetVector3("lightColor", new Vector3(1.0f, 1.0f, 1.0f));
            _shader.SetVector3("lightPos", new Vector3(5.0f, 5.0f, 5.0f));
            _shader.SetVector3("viewPos", new Vector3(0, 0, 3));

            GL.BindVertexArray(_vao);
            GL.DrawElements(BeginMode.Triangles, _vertexCount, DrawElementsType.UnsignedInt, 0);
            GL.BindVertexArray(0);

            // Restore original viewport
            GL.Viewport(viewport[0], viewport[1], viewport[2], viewport[3]);
        }

        public string GetCurrentLabel(ICamera camera)
        {
            // Determine which direction is most prominently visible based on camera orientation
            Vector3 forward = -camera.Front;

            string label = "";

            // Front/Back (Z-axis) - highest priority
            if (Math.Abs(forward.Z) > 0.5f)
            {
                if (forward.Z > 0)
                    label = "FRONT";
                else
                    label = "BACK";
            }
            // Top/Bottom (Y-axis)
            else if (Math.Abs(forward.Y) > 0.5f)
            {
                if (forward.Y > 0)
                    label = "TOP";
                else
                    label = "BOTTOM";
            }
            // Left/Right (X-axis)
            else if (Math.Abs(forward.X) > 0.5f)
            {
                if (forward.X > 0)
                    label = "RIGHT";
                else
                    label = "LEFT";
            }

            return label;
        }

        private Matrix4 CreateRotationFromCamera(ICamera camera)
        {
            // Create rotation matrix matching camera's pitch, yaw, and roll
            // using the camera's Right, Up, and Front vectors
            Matrix4 rotation = new Matrix4(
                new Vector4(camera.Right, 0),
                new Vector4(camera.Up, 0),
                new Vector4(-camera.Front, 0),  // Negate front to match camera direction
                new Vector4(0, 0, 0, 1)
            );

            return rotation;
        }

        public void Dispose()
        {
            if (_isInitialized)
            {
                GL.DeleteVertexArray(_vao);
                GL.DeleteBuffer(_vbo);
                GL.DeleteBuffer(_ebo);

                _isInitialized = false;
            }
        }
    }
}
