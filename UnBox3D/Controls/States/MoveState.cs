using OpenTK.Mathematics;
using System.Windows.Forms;
using UnBox3D.Commands;
using UnBox3D.Models;
using UnBox3D.Rendering;
using UnBox3D.Rendering.OpenGL;

namespace UnBox3D.Controls.States
{
    /// <summary>
    /// Move mode.
    ///
    /// • Click an axis arrow  → translate along that axis only (axis-constrained drag)
    /// • Click mesh body      → free XZ translation
    /// • Click empty space    → deselect, hide gizmo
    ///
    /// Hit-testing is screen-space — the same technique used by GimbalState.
    /// </summary>
    public class MoveState : IState
    {
        private const float ArrowPx    = 28f;   // hit radius for arrow shafts (px)
        private const float RingLinePx = 14f;   // not used but reserved

        private readonly IGLControlHost   _controlHost;
        private readonly ISceneManager    _sceneManager;
        private readonly ICamera          _camera;
        private readonly IRayCaster       _rayCaster;
        private readonly ICommandHistory  _commandHistory;
        private readonly IRenderer        _renderer;
        private readonly Action<IAppMesh?>? _onSelectionChanged;

        private IAppMesh? _selectedMesh;

        // Which axis is being dragged right now
        private enum DragAxis { None, X, Y, Z, Free }
        private DragAxis _dragAxis  = DragAxis.None;

        private Point   _lastClientPos;
        private Vector3 _totalMovement;
        private float   _worldScale;
        private GizmoHoverElement  _lastHoveredElement = GizmoHoverElement.None;

        public MoveState(
            IGLControlHost   controlHost,
            ISceneManager    sceneManager,
            ICamera          camera,
            IRayCaster       rayCaster,
            ICommandHistory  commandHistory,
            IRenderer        renderer,
            Action<IAppMesh?>? onSelectionChanged = null)
        {
            _controlHost         = controlHost    ?? throw new ArgumentNullException(nameof(controlHost));
            _sceneManager        = sceneManager   ?? throw new ArgumentNullException(nameof(sceneManager));
            _camera              = camera         ?? throw new ArgumentNullException(nameof(camera));
            _rayCaster           = rayCaster      ?? throw new ArgumentNullException(nameof(rayCaster));
            _commandHistory      = commandHistory ?? throw new ArgumentNullException(nameof(commandHistory));
            _renderer            = renderer       ?? throw new ArgumentNullException(nameof(renderer));
            _onSelectionChanged  = onSelectionChanged;
        }

        /// <summary>
        /// Pre-selects a mesh so the gizmo appears immediately when entering Move mode
        /// without requiring an extra click.
        /// </summary>
        public void SetSelectedMesh(IAppMesh mesh)
        {
            _selectedMesh = mesh;
            _renderer.SetActiveGizmoMesh(mesh);
            _renderer.SetGizmoMode(GizmoMode.ArrowsOnly);
            _worldScale = WorldScaleFromCamera();
            _controlHost.Invalidate();
        }

        // ── Mouse Down ─────────────────────────────────────────────────────

        public void OnMouseDown(MouseEventArgs e)
        {
            _lastClientPos = new Point(e.X, e.Y);
            _totalMovement = Vector3.Zero;
            _dragAxis      = DragAxis.None;

            // 1. If a mesh is already selected, try arrow hit-test first.
            if (_selectedMesh != null)
            {
                var axis = HitTestArrows(e.X, e.Y);
                if (axis != DragAxis.None)
                {
                    _dragAxis   = axis;
                    _worldScale = WorldScaleFromCamera();
                    _controlHost.SetCursor(Cursors.SizeAll);
                    // Clear hover highlight while dragging.
                    if (_lastHoveredElement != GizmoHoverElement.None)
                    {
                        _lastHoveredElement = GizmoHoverElement.None;
                        _renderer.SetHoveredGizmoElement(GizmoHoverElement.None);
                    }
                    return;
                }
            }

            // 2. Ray-cast for mesh body click.
            Vector3 rayOrigin    = _camera.Position;
            Vector3 rayDirection = _rayCaster.GetRay();

            if (_rayCaster.RayIntersectsMesh(_sceneManager.GetMeshes(), rayOrigin, rayDirection,
                    out float _, out IAppMesh? clickedMesh))
            {
                // Guard: when a gizmo is active, never switch to a different mesh via
                // ray-cast.  The precise screen-space hit-test above (HitTestArrows)
                // may miss by a few pixels; falling through to a different mesh here
                // would cause the wrong mesh to be moved/rotated.  Free-drag the
                // already-selected mesh instead.  To select a different mesh the user
                // first clicks empty space to deselect, then clicks the target mesh.
                if (_selectedMesh != null && clickedMesh != _selectedMesh)
                {
                    _dragAxis   = DragAxis.Free;
                    _worldScale = WorldScaleFromCamera();
                    _controlHost.SetCursor(Cursors.SizeAll);
                    return;
                }

                _selectedMesh = clickedMesh;
                _onSelectionChanged?.Invoke(_selectedMesh);
                _renderer.SetActiveGizmoMesh(_selectedMesh);
                _renderer.SetGizmoMode(GizmoMode.ArrowsOnly);
                _dragAxis     = DragAxis.Free;
                _worldScale   = WorldScaleFromCamera();
                _controlHost.SetCursor(Cursors.SizeAll);
            }
            else
            {
                // Deselect only if click is clearly outside gizmo area.
                bool insideGizmo = false;
                if (_selectedMesh != null && _renderer.TryGetGizmoInfo(out Vector3 gc, out float gr))
                    insideGizmo = NearScreen(e.X, e.Y, gc, gr * 1.2f);

                if (!insideGizmo)
                {
                    _selectedMesh       = null;
                    _lastHoveredElement = GizmoHoverElement.None;
                    _onSelectionChanged?.Invoke(null);
                    _renderer.SetActiveGizmoMesh(null);   // also resets hover in SceneRenderer
                    _controlHost.SetCursor(Cursors.Default);
                    _controlHost.Invalidate();
                }
            }
        }

        // ── Mouse Move ─────────────────────────────────────────────────────

        public void OnMouseMove(MouseEventArgs e)
        {
            // Hover: update cursor and highlight the element under the cursor.
            if (_dragAxis == DragAxis.None)
            {
                if (_selectedMesh != null)
                {
                    var axis = HitTestArrows(e.X, e.Y);
                    _controlHost.SetCursor(axis != DragAxis.None ? Cursors.SizeAll : Cursors.Default);

                    var newHover = axis switch
                    {
                        DragAxis.X => GizmoHoverElement.MoveX,
                        DragAxis.Y => GizmoHoverElement.MoveY,
                        DragAxis.Z => GizmoHoverElement.MoveZ,
                        _          => GizmoHoverElement.None
                    };
                    if (newHover != _lastHoveredElement)
                    {
                        _lastHoveredElement = newHover;
                        _renderer.SetHoveredGizmoElement(newHover);
                        _controlHost.Render();
                    }
                }
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

            switch (_dragAxis)
            {
                // render X = world X, render Y = world Z, render Z = world Y  (Y/Z vertex swap)
                case DragAxis.X:    ApplyAxisMove(Vector3.UnitX, Vector3.UnitX, pxX, pxY); break;
                case DragAxis.Y:    ApplyAxisMove(Vector3.UnitY, Vector3.UnitZ, pxX, pxY); break;
                case DragAxis.Z:    ApplyAxisMove(Vector3.UnitZ, Vector3.UnitY, pxX, pxY); break;
                case DragAxis.Free: ApplyFreeMove(pxX, pxY);                               break;
            }

            // Keep arrows centred on the moving mesh.
            _renderer.SetActiveGizmoMesh(_selectedMesh);

            _lastClientPos = new Point(e.X, e.Y);
            _controlHost.Invalidate();
        }

        // ── Mouse Up ───────────────────────────────────────────────────────

        public void OnMouseUp(MouseEventArgs e)
        {
            if (_selectedMesh != null && _totalMovement != Vector3.Zero)
                _commandHistory.PushCommand(new MoveCommand(_selectedMesh, _totalMovement));

            _dragAxis      = DragAxis.None;
            _totalMovement = Vector3.Zero;
        }

        // ── Gizmo arrow hit-test (screen-space) ────────────────────────────

        private DragAxis HitTestArrows(int mx, int my)
        {
            if (!_renderer.TryGetGizmoInfo(out Vector3 center, out float radius))
                return DragAxis.None;

            if (NearScreenLine(mx, my, center, GizmoRenderer.ArrowTipX(center, radius), ArrowPx)) return DragAxis.X;
            if (NearScreenLine(mx, my, center, GizmoRenderer.ArrowTipY(center, radius), ArrowPx)) return DragAxis.Y;
            if (NearScreenLine(mx, my, center, GizmoRenderer.ArrowTipZ(center, radius), ArrowPx)) return DragAxis.Z;

            return DragAxis.None;
        }

        // ── Screen-space projection helpers ───────────────────────────────

        private bool NearScreen(int mx, int my, Vector3 worldPos, float threshPx)
        {
            var sp = ProjectToScreen(worldPos);
            if (sp == null) return false;
            float dx = mx - sp.Value.X, dy = my - sp.Value.Y;
            return dx * dx + dy * dy <= threshPx * threshPx;
        }

        private bool NearScreenLine(int mx, int my, Vector3 worldA, Vector3 worldB, float threshPx)
        {
            var sa = ProjectToScreen(worldA);
            var sb = ProjectToScreen(worldB);
            if (sa == null || sb == null) return false;

            Vector2 a  = sa.Value, b = sb.Value, ab = b - a;
            float lenSq = ab.LengthSquared;
            float t = lenSq > 0.001f
                ? Math.Clamp(Vector2.Dot(new Vector2(mx - a.X, my - a.Y), ab) / lenSq, 0f, 1f)
                : 0f;

            Vector2 closest = a + ab * t;
            float dx = mx - closest.X, dy = my - closest.Y;
            return dx * dx + dy * dy <= threshPx * threshPx;
        }

        private Vector2? ProjectToScreen(Vector3 worldPos)
        {
            Matrix4 vp   = _camera.GetViewMatrix() * _camera.GetProjectionMatrix();
            Vector4 clip = new Vector4(worldPos, 1f) * vp;
            if (clip.W <= 0f) return null;

            float invW = 1f / clip.W;
            return new Vector2(
                ( clip.X * invW + 1f) * 0.5f * _controlHost.GetWidth(),
                (1f - clip.Y * invW) * 0.5f * _controlHost.GetHeight()
            );
        }

        // ── Transform helpers ──────────────────────────────────────────────

        /// <summary>
        /// Translates along a single axis.
        /// renderAxis — direction in render/gizmo space (for screen projection).
        /// worldAxis  — direction passed to Translate() (accounts for Y/Z swap).
        /// </summary>
        private void ApplyAxisMove(Vector3 renderAxis, Vector3 worldAxis, float pxX, float pxY)
        {
            if (!_renderer.TryGetGizmoInfo(out Vector3 center, out float _)) return;

            var s0 = ProjectToScreen(center);
            var s1 = ProjectToScreen(center + renderAxis);
            if (s0 == null || s1 == null) return;

            Vector2 screenVec = s1.Value - s0.Value;
            float   screenLen = screenVec.Length;
            if (screenLen < 0.5f) return;

            Vector2 screenDir    = screenVec / screenLen;
            float   mouseAligned = pxX * screenDir.X + pxY * screenDir.Y;
            float   worldDist    = mouseAligned / screenLen;

            Vector3 delta = worldAxis * worldDist;
            _selectedMesh!.Translate(delta);
            _totalMovement += delta;
        }

        private void ApplyFreeMove(float pxX, float pxY)
        {
            var delta = new Vector3(pxX * _worldScale, 0f, pxY * _worldScale);
            _selectedMesh!.Translate(delta);
            _totalMovement += delta;
        }

        private float WorldScaleFromCamera()
        {
            float camDist = _camera.Position.Length;
            return Math.Max(camDist * 0.002f, 0.001f);
        }
    }
}
