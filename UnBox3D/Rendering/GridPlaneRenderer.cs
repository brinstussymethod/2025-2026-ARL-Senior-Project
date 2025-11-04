using System;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using UnBox3D.Rendering.OpenGL;

namespace UnBox3D.Rendering
{
    public sealed class GridPlaneRenderer : IDisposable
    {
        private int _vao, _vbo;
        private Shader _shader;
        private int _gridLines;
        private float _gridSize;
        private float _transparency;

        public GridPlaneRenderer(string vertexShaderPath, string fragmentShaderPath)
        {
            _gridSize = 1000.0f;
            _gridLines = 1000;   // grid lines in ONE quadrant at one axis | Ex. 10 _gridLines = total 20 lines in 1 quadrant (10 x-axis & 10 z-axis)
            _transparency = 0.3f;

            // Load Shader using your Shader class
            _shader = new Shader(vertexShaderPath, fragmentShaderPath);

            InitializeGrid();
        }

        private void InitializeGrid()
        {
            float[] vertices = GenerateGridVertices(_gridSize, _gridLines);

            // Generate VAO & VBO
            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();

            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

            // Define vertex attributes (position only)
            int positionLocation = _shader.GetAttribLocation("aPos");
            GL.VertexAttribPointer(positionLocation, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            GL.EnableVertexAttribArray(positionLocation);

            GL.BindVertexArray(0);
        }

        private float[] GenerateGridVertices(float gridSize, int gridLines)
        {
            int numLines = (gridLines * 4) + 2; // 4 quadrants,  +2 main axes (X, Z)
            float[] vertices = new float[numLines * 2 * 3]; // each line = 2 vertices (points) | each vertex = 3 floats (X, Y, Z)

            int index = 0;
            float step = gridSize / gridLines;

            // Vertical lines (X direction)
            for (float x = -gridSize; x <= gridSize; x += step)
            {
                vertices[index++] = x; vertices[index++] = 0; vertices[index++] = -gridSize;
                vertices[index++] = x; vertices[index++] = 0; vertices[index++] = gridSize;
            }

            // Horizontal lines (Z direction)
            for (float z = -gridSize; z <= gridSize; z += step)
            {
                vertices[index++] = -gridSize; vertices[index++] = 0; vertices[index++] = z;
                vertices[index++] =  gridSize; vertices[index++] = 0; vertices[index++] = z;
            }

            return vertices;
        }

        public void DrawGrid(Matrix4 view, Matrix4 projection)
        {
            //GL.Enable(EnableCap.Blend);
            //GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthMask(false);

            _shader.Use();

            _shader.SetMatrix4("view", view);
            _shader.SetMatrix4("projection", projection);
            _shader.SetMatrix4("model", Matrix4.Identity);

            // Bind VAO and draw
            GL.BindVertexArray(_vao);
            GL.DrawArrays(PrimitiveType.Lines, 0, (_gridLines * 4 + 2) * 2) ; // numLines * 2
            GL.BindVertexArray(0);

            GL.UseProgram(0);

            GL.DepthMask(true);
            GL.Disable(EnableCap.Blend);
        }

        public void Dispose()
        {
            GL.DeleteVertexArray(_vao);
            GL.DeleteBuffer(_vbo);
            GL.DeleteProgram(_shader.Handle);
        }
    }
}
