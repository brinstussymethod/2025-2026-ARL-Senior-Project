using OpenTK.Mathematics;
using System.Windows.Forms;
using UnBox3D.Commands;
using UnBox3D.Models;
using UnBox3D.Rendering;
using UnBox3D.Rendering.OpenGL;

namespace UnBox3D.Controls.States
{
    /// <summary>
    /// What the user is currently dragging in Gimbal mode.
    /// </summary>
    internal enum GimbalDragMode
    {
        None,
        RotateX, RotateY, RotateZ,   // drag a ring or its handle → rotate around that axis
        MoveX,   MoveY,   MoveZ,     // drag an axis arrow tip    → translate along that axis
        FreeDrag                      // drag the mesh body         → translate in XZ plane
    }

    /// <summary>
    /// Gimbal mode — selection, rotation and translation in one state.
    ///
    /// • Click mesh body             → select + show gizmo rings/arrows
    /// • Drag anywhere on a ring     → rotate around that ring's axis
    /// • Drag white handle on ring   → rotate (same, but clear grab point)
    /// • Drag coloured axis arrow    → translate along that axis
    /// • Drag mesh body              → free XZ translation
    /// • Click empty space           → deselect, hide gizmo
    ///
    /// Hit-testing is screen-space: every frame we project the known 3-D gizmo
    /// positions (handle centres, arrow tips, ring sample points) into pixel
    /// coordinates and compare against the mouse position.  This is robust to
    /// any camera angle or zoom level.
    ///
    /// COORDINATE NOTE
    /// AppMesh stores vertices with Y and Z swapped vs. world/g4 space:
    ///   render X = world X,  render Y = world Z,  render Z = world Y
    /// The gizmo is built and hit-tested in render space.
    /// Translate() takes a WORLD-space delta, so axis moves map as follows:
    ///   render X axis → Translate(UnitX)
    ///   render Y axis → Translate(UnitZ)   ← swapped!
    ///   render Z axis → Translate(UnitY)   ← swapped!
    /// </summary>
    public class GimbalState : IState
    {
        // ── Ring sampling for hit testing (every 15°, 24 points) ──────────
        private const int    RingSamples  = 24;
        private const float  HandlePx     = 36f;   // pixel radius — ring handle hit area
        private const float  ArrowPx      = 24f;   // pixel radius — arrow tip hit area
        private const float  RingLinePx   = 14f;   // pixel radius — ring line hit area

        private readonly IGLControlHost  _controlHost;
        private readonly ISceneManager   _sceneManager;
        private readonly ICamera         _camera;
        private readonly IRayCaster      _rayCaster;
        private readonly ICommandHistory _commandHistory;
        private readonly IRenderer       _renderer;

        private IAppMesh?      _selectedMesh;
        private GimbalDragMode _dragMode = GimbalDragMode.None;

        private Point   _lastClientPos;
        private float   _accumulatedAngle;
        private Vector3 _totalMovement;
        private float   _worldScale;

        // ── Constructor ────────────────────────────────────────────────────

        public GimbalState(
            IGLControlHost  controlHost,
            ISceneManager   sceneManager,
            ICamera         camera,
            IRayCaster      rayCaster,
            ICommandHistory commandHistory,
            IRenderer       renderer)
        {
            _controlHost    = controlHost    ?? throw new ArgumentNullException(nameof(controlHost));
            _sceneManager   = sceneManager   ?? throw new ArgumentNullException(nameof(sceneManager));
            _camera         = camera         ?? throw new ArgumentNullException(nameof(camera));
            _rayCaster      = rayCaster      ?? throw new ArgumentNullException(nameof(rayCaster));
            _commandHistory = commandHistory ?? throw new ArgumentNullException(nameof(commandHistory));
            _renderer       = renderer       ?? throw new ArgumentNullException(nameof(renderer));
        }

        /// <summary>
        /// Pre-selects a mesh when entering Gimbal mode so the gizmo rings appear
        /// immediately without requiring an extra click.
        /// </summary>
        public void SetSelectedMesh(IAppMesh mesh)
        {
            _selectedMesh = mesh;
            _renderer.SetActiveGizmoMesh(mesh);
            _worldScale   = WorldScaleFromCamera();
        }

        // ── Mouse Down ─────────────────────────────────────────────────────

        public void OnMouseDown(MouseEventArgs e)
        {
            _lastClientPos    = new Point(e.X, e.Y);
            _accumulatedAngle = 0f;
            _totalMovement    = Vector3.Zero;
            _dragMode         = GimbalDragMode.None;

            // 1. If a mesh is already selected, try to hit the gizmo first.
            if (_selectedMesh != null)
            {
                var hit = HitTestGizmo(e.X, e.Y);
                if (hit != GimbalDragMode.None)
                {
                    _dragMode  = hit;
                    _worldScale = WorldScaleFromCamera();
                    ApplyCursor(_dragMode);
                    return;
                }
            }

            // 2. Ray-cast for mesh body click.
            Vector3 rayOrigin    = _camera.Position;
            Vector3 rayDirection = _rayCaster.GetRay();

            if (_rayCaster.RayIntersectsMesh(_sceneManager.GetMeshes(), rayOrigin, rayDirection,
                    out float _, out IAppMesh? clickedMesh))
            {
                _selectedMesh = clickedMesh;
                _renderer.SetActiveGizmoMesh(_selectedMesh);
                _dragMode     = GimbalDragMode.FreeDrag;
                _worldScale   = WorldScaleFromCamera();
                ApplyCursor(_dragMode);
            }
            else
            {
                // Only deselect if the click is genuinely away from the gizmo.
                // If TryGetGizmoInfo succeeds and the click is inside the ring
                // boundary, treat it as a missed-arrow click rather than a deselect.
                bool clickedInsideGizmoArea = false;
                if (_selectedMesh != null && _renderer.TryGetGizmoInfo(out Vector3 gc, out float gr))
                {
                    // Reject if within 110% of the ring radius on screen
                    clickedInsideGizmoArea = NearScreen(e.X, e.Y, gc, gr * 1.1f * EstimateScreenScale(gc, gr));
                }

                if (!clickedInsideGizmoArea)
                {
                    _selectedMesh = null;
                    _renderer.SetActiveGizmoMesh(null);
                    _controlHost.SetCursor(Cursors.Default);
                    _controlHost.Invalidate();
                }
            }
        }

        // ── Mouse Move ─────────────────────────────────────────────────────

        public void OnMouseMove(MouseEventArgs e)
        {
            // Hover cursor update when not dragging.
            if (_dragMode == GimbalDragMode.None)
            {
                if (_selectedMesh != null)
                    ApplyCursor(HitTestGizmo(e.X, e.Y));
                else
                    _controlHost.SetCursor(Cursors.Default);
                return;
            }

            if (_selectedMesh == null) return;

            float pxX = e.X - _lastClientPos.X;
            float pxY = e.Y - _lastClientPos.Y;

            if (Math.Abs(pxX) < 0.5f && Math.Abs(pxY) < 0.5f)
            {
                _lastClientPos = new Point(e.X, e.Y);
                return;
            }

            switch (_dragMode)
            {
                // ── Rotation: rotate around the ring's own axis ──────────
                case GimbalDragMode.RotateX: ApplyRotation(Vector3.UnitX, pxX); break;
                case GimbalDragMode.RotateY: ApplyRotation(Vector3.UnitY, pxX); break;
                case GimbalDragMode.RotateZ: ApplyRotation(Vector3.UnitZ, pxX); break;

                // ── Axis translation: render→world axis mapping ──────────
                // render X = world X, render Y = world Z, render Z = world Y  (Y/Z vertex swap)
                case GimbalDragMode.MoveX:
                    ApplyAxisMove(renderAxis: Vector3.UnitX, worldAxis: Vector3.UnitX, pxX, pxY); break;
                case GimbalDragMode.MoveY:
                    ApplyAxisMove(renderAxis: Vector3.UnitY, worldAxis: Vector3.UnitZ, pxX, pxY); break;
                case GimbalDragMode.MoveZ:
                    ApplyAxisMove(renderAxis: Vector3.UnitZ, worldAxis: Vector3.UnitY, pxX, pxY); break;

                case GimbalDragMode.FreeDrag: ApplyFreeMove(pxX, pxY); break;
            }

            // Keep gizmo centred on the (now-moved) mesh.
            _renderer.SetActiveGizmoMesh(_selectedMesh);
            _lastClientPos = new Point(e.X, e.Y);
            _controlHost.Invalidate();
        }

        // ── Mouse Up ───────────────────────────────────────────────────────

        public void OnMouseUp(MouseEventArgs e)
        {
            if (_selectedMesh != null && _dragMode != GimbalDragMode.None)
            {
                switch (_dragMode)
                {
                    case GimbalDragMode.RotateX:
                    case GimbalDragMode.RotateY:
                    case GimbalDragMode.RotateZ:
                        if (Math.Abs(_accumulatedAngle) > 0.001f)
                        {
                            Vector3 axis = _dragMode == GimbalDragMode.RotateX ? Vector3.UnitX
                                         : _dragMode == GimbalDragMode.RotateY ? Vector3.UnitY
                                         : Vector3.UnitZ;
                            float rad     = MathHelper.DegreesToRadians(_accumulatedAngle);
                            var   doRot   = Quaternion.FromAxisAngle(axis,  rad);
                            var   undoRot = Quaternion.FromAxisAngle(axis, -rad);
                            _commandHistory.PushCommand(new RotateCommand(_selectedMesh, doRot, undoRot));
                        }
                        break;

                    case GimbalDragMode.MoveX:
                    case GimbalDragMode.MoveY:
                    case GimbalDragMode.MoveZ:
                    case GimbalDragMode.FreeDrag:
                        if (_totalMovement != Vector3.Zero)
                            _commandHistory.PushCommand(new MoveCommand(_selectedMesh, _totalMovement));
                        break;
                }
            }

            _dragMode         = GimbalDragMode.None;
            _accumulatedAngle = 0f;
            _totalMovement    = Vector3.Zero;

            // Restore hover cursor.
            if (_selectedMesh != null)
                ApplyCursor(HitTestGizmo(e.X, e.Y));
            else
                _controlHost.SetCursor(Cursors.Default);
        }

        // ── Gizmo hit testing (screen-space) ───────────────────────────────

        /// <summary>
        /// Priority order:
        ///   1. White handle discs (generous hit area)  → rotation
        ///   2. Full arrow shafts + cone tips            → axis translation
        ///   3. Ring lines (sampled every 15°)           → rotation fallback
        /// </summary>
        private GimbalDragMode HitTestGizmo(int mx, int my)
        {
            if (!_renderer.TryGetGizmoInfo(out Vector3 center, out float radius))
                return GimbalDragMode.None;

            // ── 1. White handle discs ──────────────────────────────────────
            if (NearScreen(mx, my, GizmoRenderer.HandlePosX(center, radius), HandlePx)) return GimbalDragMode.RotateX;
            if (NearScreen(mx, my, GizmoRenderer.HandlePosY(center, radius), HandlePx)) return GimbalDragMode.RotateY;
            if (NearScreen(mx, my, GizmoRenderer.HandlePosZ(center, radius), HandlePx)) return GimbalDragMode.RotateZ;

            // ── 2. Arrow shafts (full line from center → tip) + cone tips ──
            // Testing the whole shaft means the user doesn't have to hit the
            // tiny cone tip exactly — anywhere along the arrow registers.
            if (NearScreenLine(mx, my, center, GizmoRenderer.ArrowTipX(center, radius), ArrowPx)) return GimbalDragMode.MoveX;
            if (NearScreenLine(mx, my, center, GizmoRenderer.ArrowTipY(center, radius), ArrowPx)) return GimbalDragMode.MoveY;
            if (NearScreenLine(mx, my, center, GizmoRenderer.ArrowTipZ(center, radius), ArrowPx)) return GimbalDragMode.MoveZ;

            // ── 3. Ring lines (sample RingSamples points around each ring) ─
            for (int i = 0; i < RingSamples; i++)
            {
                float a   = 2f * MathF.PI * i / RingSamples;
                float cos = MathF.Cos(a) * radius;
                float sin = MathF.Sin(a) * radius;

                // X ring: in render YZ plane
                if (NearScreen(mx, my, center + new Vector3(0f,  cos, sin), RingLinePx)) return GimbalDragMode.RotateX;
                // Y ring: in render XZ plane
                if (NearScreen(mx, my, center + new Vector3(cos, 0f,  sin), RingLinePx)) return GimbalDragMode.RotateY;
                // Z ring: in render XY plane
                if (NearScreen(mx, my, center + new Vector3(cos, sin, 0f),  RingLinePx)) return GimbalDragMode.RotateZ;
            }

            return GimbalDragMode.None;
        }

        /// <summary>True when the projected screen position of <paramref name="worldPos"/>
        /// is within <paramref name="threshPx"/> pixels of (mx, my).</summary>
        private bool NearScreen(int mx, int my, Vector3 worldPos, float threshPx)
        {
            var sp = ProjectToScreen(worldPos);
            if (sp == null) return false;
            float dx = mx - sp.Value.X;
            float dy = my - sp.Value.Y;
            return dx * dx + dy * dy <= threshPx * threshPx;
        }

        /// <summary>
        /// True when (mx,my) is within <paramref name="threshPx"/> pixels of the
        /// screen-space line segment from <paramref name="worldA"/> to <paramref name="worldB"/>.
        /// Lets the user click anywhere along an arrow shaft, not just the cone tip.
        /// </summary>
        private bool NearScreenLine(int mx, int my, Vector3 worldA, Vector3 worldB, float threshPx)
        {
            var sa = ProjectToScreen(worldA);
            var sb = ProjectToScreen(worldB);
            if (sa == null || sb == null) return false;

            Vector2 a     = sa.Value;
            Vector2 b     = sb.Value;
            Vector2 ab    = b - a;
            float   lenSq = ab.LengthSquared;

            float t = 0f;
            if (lenSq > 0.001f)
            {
                // Project mouse onto segment, clamped to [0,1]
                t = Vector2.Dot(new Vector2(mx - a.X, my - a.Y), ab) / lenSq;
                t = Math.Clamp(t, 0f, 1f);
            }

            Vector2 closest = a + ab * t;
            float   dx      = mx - closest.X;
            float   dy      = my - closest.Y;
            return dx * dx + dy * dy <= threshPx * threshPx;
        }

        /// <summary>
        /// Projects a render-space world position to GL-control client pixel coordinates.
        /// Mirrors exactly what the gizmo shader does:
        ///   gl_Position = vec4(aPos, 1.0) * model * view * projection  (model = Identity)
        /// In C# row-vector convention:  clip = pos × (view × proj)
        /// </summary>
        private Vector2? ProjectToScreen(Vector3 worldPos)
        {
            Matrix4 view = _camera.GetViewMatrix();
            Matrix4 proj = _camera.GetProjectionMatrix();
            // Match the shader row-vector convention: pos * view * proj
            Matrix4 vp   = view * proj;
            Vector4 clip = new Vector4(worldPos, 1f) * vp;

            if (clip.W <= 0f) return null; // behind near plane

            float invW = 1f / clip.W;
            float ndcX =  clip.X * invW;
            float ndcY =  clip.Y * invW;

            int vpW = _controlHost.GetWidth();
            int vpH = _controlHost.GetHeight();

            // NDC (-1..1) → pixel coords (top-left origin, Y down)
            return new Vector2(
                (ndcX + 1f) * 0.5f * vpW,
                (1f - ndcY) * 0.5f * vpH
            );
        }

        // ── Transform helpers ──────────────────────────────────────────────

        private void ApplyRotation(Vector3 renderAxis, float pxX)
        {
            const float sensitivity = 0.5f;
            float angle = pxX * sensitivity;
            if (Math.Abs(angle) < 0.001f) return;

            var q = Quaternion.FromAxisAngle(renderAxis, MathHelper.DegreesToRadians(angle));
            _selectedMesh!.Rotate(q);
            _accumulatedAngle += angle;
        }

        /// <summary>
        /// Translates along a single axis.
        /// <paramref name="renderAxis"/> — direction in render space (used for screen projection).
        /// <paramref name="worldAxis"/>  — direction in world space passed to Translate()
        ///                                 (accounts for render Y = world Z, render Z = world Y swap).
        /// </summary>
        private void ApplyAxisMove(Vector3 renderAxis, Vector3 worldAxis, float pxX, float pxY)
        {
            if (!_renderer.TryGetGizmoInfo(out Vector3 center, out float _)) return;

            var s0 = ProjectToScreen(center);
            var s1 = ProjectToScreen(center + renderAxis);
            if (s0 == null || s1 == null) return;

            Vector2 screenVec = s1.Value - s0.Value;
            float   screenLen = screenVec.Length;
            if (screenLen < 0.5f) return; // axis nearly perpendicular to screen

            // Project mouse delta onto the screen direction of this axis.
            Vector2 screenDir    = screenVec / screenLen;
            float   mouseAligned = pxX * screenDir.X + pxY * screenDir.Y;
            float   worldDist    = mouseAligned / screenLen;

            Vector3 delta = worldAxis * worldDist;
            _selectedMesh!.Translate(delta);
            _totalMovement += delta;
        }

        private void ApplyFreeMove(float pxX, float pxY)
        {
            // Free drag: X mouse = world X, Y mouse (screen-down) = world Z
            var delta = new Vector3(pxX * _worldScale, 0f, pxY * _worldScale);
            _selectedMesh!.Translate(delta);
            _totalMovement += delta;
        }

        // ── Cursor helpers ─────────────────────────────────────────────────

        private void ApplyCursor(GimbalDragMode mode)
        {
            switch (mode)
            {
                case GimbalDragMode.RotateX:
                case GimbalDragMode.RotateY:
                case GimbalDragMode.RotateZ:
                    _controlHost.SetCursor(Cursors.Hand);    // ☝ rotate cue
                    break;
                case GimbalDragMode.MoveX:
                case GimbalDragMode.MoveY:
                case GimbalDragMode.MoveZ:
                case GimbalDragMode.FreeDrag:
                    _controlHost.SetCursor(Cursors.SizeAll); // ✛ move cue
                    break;
                default:
                    _controlHost.SetCursor(Cursors.Default);
                    break;
            }
        }

        // ── Utility ────────────────────────────────────────────────────────

        private float WorldScaleFromCamera()
        {
            float camDist = _camera.Position.Length;
            return Math.Max(camDist * 0.002f, 0.001f);
        }

        /// <summary>
        /// Returns pixels-per-world-unit at <paramref name="worldPos"/> so we can convert
        /// the gizmo's world-space ring radius into a pixel radius for the deselect guard.
        /// </summary>
        private float EstimateScreenScale(Vector3 worldPos, float worldRadius)
        {
            // Offset by a fraction of the radius so the sample point is always visible
            var sc = ProjectToScreen(worldPos);
            var se = ProjectToScreen(worldPos + new Vector3(worldRadius, 0f, 0f));
            if (sc == null || se == null || worldRadius < 0.0001f) return 1f;
            float pixels = (se.Value - sc.Value).Length;
            return pixels > 0.001f ? pixels / worldRadius : 1f;
        }
    }
}
