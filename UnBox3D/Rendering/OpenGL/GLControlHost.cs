using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.GLControl;
using UnBox3D.Utils;
using UnBox3D.Controls.States;
using UnBox3D.Controls;
using System.Diagnostics;

namespace UnBox3D.Rendering.OpenGL
{
    // Interface for GLControl host
    public interface IGLControlHost : IDisposable
    {
        void Invalidate();
        int GetWidth();
        int GetHeight();
        void Render();
        void Cleanup();

    }

    // Enumeration for rendering modes
    public enum RenderMode
    {
        Wireframe,
        Solid,
        Point
    }

    public class GLControlHost : GLControl, IGLControlHost
    {
        // Here we now have added the normals of the vertices
        // Remember to define the layouts to the VAO's
        private readonly float[] _vertices =
        {
             // Position          Normal
            -0.5f, -0.5f, -0.5f,  0.0f,  0.0f, -1.0f, // Front face
             0.5f, -0.5f, -0.5f,  0.0f,  0.0f, -1.0f,
             0.5f,  0.5f, -0.5f,  0.0f,  0.0f, -1.0f,
             0.5f,  0.5f, -0.5f,  0.0f,  0.0f, -1.0f,
            -0.5f,  0.5f, -0.5f,  0.0f,  0.0f, -1.0f,
            -0.5f, -0.5f, -0.5f,  0.0f,  0.0f, -1.0f,

            -0.5f, -0.5f,  0.5f,  0.0f,  0.0f,  1.0f, // Back face
             0.5f, -0.5f,  0.5f,  0.0f,  0.0f,  1.0f,
             0.5f,  0.5f,  0.5f,  0.0f,  0.0f,  1.0f,
             0.5f,  0.5f,  0.5f,  0.0f,  0.0f,  1.0f,
            -0.5f,  0.5f,  0.5f,  0.0f,  0.0f,  1.0f,
            -0.5f, -0.5f,  0.5f,  0.0f,  0.0f,  1.0f,

            -0.5f,  0.5f,  0.5f, -1.0f,  0.0f,  0.0f, // Left face
            -0.5f,  0.5f, -0.5f, -1.0f,  0.0f,  0.0f,
            -0.5f, -0.5f, -0.5f, -1.0f,  0.0f,  0.0f,
            -0.5f, -0.5f, -0.5f, -1.0f,  0.0f,  0.0f,
            -0.5f, -0.5f,  0.5f, -1.0f,  0.0f,  0.0f,
            -0.5f,  0.5f,  0.5f, -1.0f,  0.0f,  0.0f,

             0.5f,  0.5f,  0.5f,  1.0f,  0.0f,  0.0f, // Right face
             0.5f,  0.5f, -0.5f,  1.0f,  0.0f,  0.0f,
             0.5f, -0.5f, -0.5f,  1.0f,  0.0f,  0.0f,
             0.5f, -0.5f, -0.5f,  1.0f,  0.0f,  0.0f,
             0.5f, -0.5f,  0.5f,  1.0f,  0.0f,  0.0f,
             0.5f,  0.5f,  0.5f,  1.0f,  0.0f,  0.0f,

            -0.5f, -0.5f, -0.5f,  0.0f, -1.0f,  0.0f, // Bottom face
             0.5f, -0.5f, -0.5f,  0.0f, -1.0f,  0.0f,
             0.5f, -0.5f,  0.5f,  0.0f, -1.0f,  0.0f,
             0.5f, -0.5f,  0.5f,  0.0f, -1.0f,  0.0f,
            -0.5f, -0.5f,  0.5f,  0.0f, -1.0f,  0.0f,
            -0.5f, -0.5f, -0.5f,  0.0f, -1.0f,  0.0f,

            -0.5f,  0.5f, -0.5f,  0.0f,  1.0f,  0.0f, // Top face
             0.5f,  0.5f, -0.5f,  0.0f,  1.0f,  0.0f,
             0.5f,  0.5f,  0.5f,  0.0f,  1.0f,  0.0f,
             0.5f,  0.5f,  0.5f,  0.0f,  1.0f,  0.0f,
            -0.5f,  0.5f,  0.5f,  0.0f,  1.0f,  0.0f,
            -0.5f,  0.5f, -0.5f,  0.0f,  1.0f,  0.0f
        };

        private readonly Vector3 _lightPos = new Vector3(1.2f, 1.0f, 2.0f);

        private int _vertexBufferObject;

        private int _vaoModel;

        private int _vaoLamp;

        private Shader _lampShader;

        private Shader _lightingShader;




        // Private Fields
        private readonly ISettingsManager _settingsManager;
        private readonly ISceneManager _sceneManager;
        private readonly IRenderer _sceneRenderer;
        private  GridPlaneRenderer _gridRenderer;
        private readonly string _vertShader = "Rendering/OpenGL/Shaders/shader.vert";
        private readonly string _fragShader = "Rendering/OpenGL/Shaders/lighting.frag";

        private ICamera _camera;

        private RenderMode currentRenderMode;
        private ShadingModel currentShadingModel;
        private Vector4 backgroundColor;

        // The GLControlHost now receives the single DI-managed Camera instance.
        // It no longer constructs its own Camera/RayCaster/Mouse/Keyboard controllers.
        public GLControlHost(ISceneManager sceneManager, IRenderer sceneRenderer, ISettingsManager settingsManager, ICamera camera)
        {
            Dock = DockStyle.Fill;

            _sceneManager       = sceneManager;
            _sceneRenderer      = sceneRenderer;
            _settingsManager    = settingsManager;
            _camera             = camera;

            // Attach event handlers
            // Only rendering lifecycle events are handled here.
            // Mouse/keyboard input is subscribed by the controllers (via DI), not by this host.
            Load    += GlControl_Load;
            Paint   += GlControl_Paint;
            Resize  += GlControl_Resize;
        }

        // Public Methods
        public void Render() 
        {
            Invalidate();
        }
        public void SetRenderMode(RenderMode mode)
        {
            currentRenderMode = mode;
        }

        public void SetShadingMode(ShadingModel shadingModel)
        {
            currentShadingModel = shadingModel;
        }

        public int GetWidth() => Width;
        public int GetHeight() => Height;

        public void Cleanup()
        {
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);
            GL.UseProgram(0);
        }

        // Private Methods
        private void LoadSettingsFromJson()
        {
            // Get settings
            string backgroundColorName = _settingsManager.GetSetting<string>(new RenderingSettings().GetKey(), RenderingSettings.BackgroundColor) ?? throw new InvalidOperationException("BackgroundColor setting is missing.");
            string renderMode = _settingsManager.GetSetting<string>(new RenderingSettings().GetKey(), RenderingSettings.RenderMode).ToLower() ?? throw new InvalidOperationException("Render mode setting is missing.");
            string shadingModel = _settingsManager.GetSetting<string>(new RenderingSettings().GetKey(), RenderingSettings.ShadingModel).ToLower() ?? throw new InvalidOperationException("Shading model setting is missing.");

            // Apply settings
            switch (renderMode)
            {
                case "wireframe":
                    SetRenderMode(RenderMode.Wireframe);
                    break;
                case "solid":
                    SetRenderMode(RenderMode.Solid);
                    break;
                case "point":
                    SetRenderMode(RenderMode.Point);
                    break;
                default:
                    Console.WriteLine("Error occurred when loading settings for render mode.");
                    SetRenderMode(RenderMode.Wireframe);
                    break;
            }

            SetBackgroundColor(backgroundColorName);
        }

        private void SetBackgroundColor(string color)
        {
            color = color.ToLower().Replace(" ", "");
            BackgroundColors.backgroundColorMap.TryGetValue(color, out backgroundColor);
        }

        // Event Handlers
        private void GlControl_Load(object? sender, EventArgs e)
        {
            LoadSettingsFromJson();
            GL.ClearColor(backgroundColor.X,backgroundColor.Y, backgroundColor.Z,  1.0f);

            GL.Enable(EnableCap.DepthTest);

            _vertexBufferObject = GL.GenBuffer(); // Generates a unique ID for a new OpenGL buffer object
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBufferObject);
            GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * sizeof(float), _vertices, BufferUsageHint.StaticDraw);

            _lightingShader = ShaderManager.LightingShader;
            _lampShader = ShaderManager.LampShader;

            {
                _vaoModel = GL.GenVertexArray();
                GL.BindVertexArray(_vaoModel);

                var positionLocation = _lightingShader.GetAttribLocation("aPos");
                GL.EnableVertexAttribArray(positionLocation);
                // Remember to change the stride as we now have 6 floats per vertex
                GL.VertexAttribPointer(positionLocation, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 0);

                // We now need to define the layout of the normal so the shader can use it
                var normalLocation = _lightingShader.GetAttribLocation("aNormal");
                GL.EnableVertexAttribArray(normalLocation);
                GL.VertexAttribPointer(normalLocation, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 3 * sizeof(float));
            }

            {
                _vaoLamp = GL.GenVertexArray();
                GL.BindVertexArray(_vaoLamp);

                var positionLocation = _lampShader.GetAttribLocation("aPos");
                GL.EnableVertexAttribArray(positionLocation);
                // Also change the stride here as we now have 6 floats per vertex. Now we don't define the normal for the lamp VAO
                // this is because it isn't used, it might seem like a waste to use the same VBO if they dont have the same data
                // The two cubes still use the same position, and since the position is already in the graphics memory it is actually
                // better to do it this way. Look through the web version for a much better understanding of this.
                GL.VertexAttribPointer(positionLocation, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 0);
            }
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Less);
            GL.Disable(EnableCap.CullFace);

            _gridRenderer = new GridPlaneRenderer(_vertShader, _fragShader);
        }

        private void GlControl_Paint(object? sender, PaintEventArgs e)
        {
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            _sceneRenderer.RenderScene(_camera, _lightingShader);

            _gridRenderer.DrawGrid(_camera.GetViewMatrix(), _camera.GetProjectionMatrix());


            GL.BindVertexArray(_vaoLamp);

            _lampShader.Use();

            Matrix4 lampMatrix = Matrix4.CreateScale(0.2f);
            lampMatrix = lampMatrix * Matrix4.CreateTranslation(_lightPos);

            _lampShader.SetMatrix4("model", lampMatrix);
            _lampShader.SetMatrix4("view", _camera.GetViewMatrix());
            _lampShader.SetMatrix4("projection", _camera.GetProjectionMatrix());

            GL.DrawArrays(PrimitiveType.Triangles, 0, 36);

            SwapBuffers();
        }


        private void GlControl_Resize(object? sender, EventArgs e)
        {
            GL.Viewport(0, 0, Width, Height);
            if (_camera != null)
                _camera.AspectRatio = (float)Width / Height;
        }

    }
}
