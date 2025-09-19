using System;
using System.Windows.Forms;                 // WinForms key/mouse events
using System.Diagnostics;
using System.Drawing;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.GLControl;
using UnBox3D.Utils;
using UnBox3D.Controls.States;
using UnBox3D.Controls;
using UnBox3D.Rendering;                    // OrbitCamera, ICamera

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
        private GridPlaneRenderer _gridRenderer;
        private readonly string _vertShader = "Rendering/OpenGL/Shaders/shader.vert";
        private readonly string _fragShader = "Rendering/OpenGL/Shaders/lighting.frag";

        private ICamera _camera;
        private MouseController _mouseController;
        private RayCaster _rayCaster;
        private KeyboardController _keyboardController;

        private RenderMode currentRenderMode;
        private ShadingModel currentShadingModel;
        private Vector4 backgroundColor;
        private float angle = 0f;

        private readonly Stopwatch _timer = new Stopwatch();
        private double _lastTime;

        // Constructor
        public GLControlHost(ISceneManager sceneManager, IRenderer sceneRenderer, ISettingsManager settingsManager)
        {
            Dock = DockStyle.Fill;

            _sceneManager = sceneManager;
            _sceneRenderer = sceneRenderer;
            _settingsManager = settingsManager;

            // Attach event handlers
            Load += GlControl_Load;
            Paint += GlControl_Paint;
            Resize += GlControl_Resize;

            // Mouse
            MouseDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseUp += OnMouseUp;
            MouseWheel += OnMouseWheel;

            // Keep keyboard focus inside GL area when mouse enters
            MouseEnter += (s, e) => Focus();

            // Ensure control can receive focus
            TabStop = true;
        }

        // Public Methods
        public void Render() => Invalidate();
        public void SetRenderMode(RenderMode mode) => currentRenderMode = mode;
        public void SetShadingMode(ShadingModel shadingModel) => currentShadingModel = shadingModel;
        public int GetWidth() => Width;
        public int GetHeight() => Height;
        public void Invalidate() => base.Invalidate();

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
            string backgroundColorName = _settingsManager.GetSetting<string>(new RenderingSettings().GetKey(), RenderingSettings.BackgroundColor);
            string renderMode = _settingsManager.GetSetting<string>(new RenderingSettings().GetKey(), RenderingSettings.RenderMode).ToLower();
            string shadingModel = _settingsManager.GetSetting<string>(new RenderingSettings().GetKey(), RenderingSettings.ShadingModel).ToLower();

            // Apply settings
            switch (renderMode)
            {
                case "wireframe": SetRenderMode(RenderMode.Wireframe); break;
                case "solid": SetRenderMode(RenderMode.Solid); break;
                case "point": SetRenderMode(RenderMode.Point); break;
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

        // ---- INPUT UTILITIES ----
        private static bool IsArrowKey(Keys k) =>
            k == Keys.Left || k == Keys.Right || k == Keys.Up || k == Keys.Down;

        // Strongest signal to WinForms that arrows belong to this control.
        protected override bool IsInputKey(Keys keyData)
        {
            if (IsArrowKey(keyData) || keyData == Keys.Space)
                return true;
            return base.IsInputKey(keyData);
        }

        // Event Handlers
        private void GlControl_Load(object sender, EventArgs e)
        {
            LoadSettingsFromJson();
            GL.ClearColor(backgroundColor.X, backgroundColor.Y, backgroundColor.Z, 1.0f);

            GL.Enable(EnableCap.DepthTest);

            _vertexBufferObject = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBufferObject);
            GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * sizeof(float), _vertices, BufferUsageHint.StaticDraw);

            _lightingShader = ShaderManager.LightingShader;
            _lampShader = ShaderManager.LampShader;

            {
                _vaoModel = GL.GenVertexArray();
                GL.BindVertexArray(_vaoModel);

                var positionLocation = _lightingShader.GetAttribLocation("aPos");
                GL.EnableVertexAttribArray(positionLocation);
                GL.VertexAttribPointer(positionLocation, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 0);

                var normalLocation = _lightingShader.GetAttribLocation("aNormal");
                GL.EnableVertexAttribArray(normalLocation);
                GL.VertexAttribPointer(normalLocation, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 3 * sizeof(float));
            }

            {
                _vaoLamp = GL.GenVertexArray();
                GL.BindVertexArray(_vaoLamp);

                var positionLocation = _lampShader.GetAttribLocation("aPos");
                GL.EnableVertexAttribArray(positionLocation);
                GL.VertexAttribPointer(positionLocation, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 0);
            }

            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Less);
            GL.Disable(EnableCap.CullFace);

            // Use OrbitCamera (origin as target, radius = 5)
            _camera = new OrbitCamera(new Vector3(0, 0, 0), 5f, GetWidth() / (float)GetHeight());

            // Initialize RayCaster
            _rayCaster = new RayCaster(this, _camera);

            // Initialize MouseController with RayCaster and Default State
            _mouseController = new MouseController(
                _settingsManager,
                _camera,
                new DefaultState(_sceneManager, this, _camera, _rayCaster),
                _rayCaster,
                this
            );

            _keyboardController = new KeyboardController(_camera);
            _gridRenderer = new GridPlaneRenderer(_vertShader, _fragShader);

            // ---- Enable keyboard focus + make arrows "input keys" ----
            Focus();

            PreviewKeyDown += (s2, e2) =>
            {
                if (IsArrowKey(e2.KeyCode) || e2.KeyCode == Keys.Space)
                {
                    e2.IsInputKey = true; // prevents parent navigation (side panel)
                }
            };

            // Forward KeyDown/KeyUp to KeyboardController and suppress arrows so side UI doesn't react
            KeyDown += (s3, e3) =>
            {
                _keyboardController.HandleWinFormsKeyDown(e3.KeyCode);

                if (IsArrowKey(e3.KeyCode) || e3.KeyCode == Keys.Space)
                {
                    e3.Handled = true;
                    e3.SuppressKeyPress = true;
                }

                Invalidate();
            };

            KeyUp += (s4, e4) =>
            {
                _keyboardController.HandleWinFormsKeyUp(e4.KeyCode);

                if (IsArrowKey(e4.KeyCode) || e4.KeyCode == Keys.Space)
                {
                    e4.Handled = true;
                    e4.SuppressKeyPress = true;
                }
            };

            // Start timer for smooth camera updates
            _timer.Start();
            _lastTime = 0.0;
        }

        private void GlControl_Paint(object sender, PaintEventArgs e)
        {
            double t = _timer.Elapsed.TotalSeconds;
            float dt = (float)(t - _lastTime);
            _lastTime = t;

            // Smooth camera update
            if (_camera is OrbitCamera orbit)
                orbit.Update(dt);

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            _sceneRenderer.RenderScene(_camera, _lightingShader);
            _gridRenderer.DrawGrid(_camera.GetViewMatrix(), _camera.GetProjectionMatrix());

            GL.BindVertexArray(_vaoLamp);
            _lampShader.Use();

            Matrix4 lampMatrix = Matrix4.CreateScale(0.2f);
            lampMatrix *= Matrix4.CreateTranslation(_lightPos);

            _lampShader.SetMatrix4("model", lampMatrix);
            _lampShader.SetMatrix4("view", _camera.GetViewMatrix());
            _lampShader.SetMatrix4("projection", _camera.GetProjectionMatrix());

            GL.DrawArrays(PrimitiveType.Triangles, 0, 36);

            SwapBuffers();
        }

        private void GlControl_Resize(object sender, EventArgs e)
        {
            GL.Viewport(0, 0, Width, Height);
            if (_camera != null)
                _camera.AspectRatio = (float)Width / Height;
        }

        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            _mouseController?.OnMouseDown(sender, e);
            Focus(); // ensure subsequent key events go here
        }

        private void OnMouseMove(object sender, MouseEventArgs e) => _mouseController?.OnMouseMove(sender, e);
        private void OnMouseUp(object sender, MouseEventArgs e) => _mouseController?.OnMouseUp(sender, e);
        private void OnMouseWheel(object sender, MouseEventArgs e) => _mouseController?.OnMouseWheel(sender, e);
    }
}
