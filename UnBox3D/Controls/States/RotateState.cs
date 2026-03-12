using OpenTK.Mathematics;
using System.Windows.Forms;
using UnBox3D.Utils;
using UnBox3D.Models;
using UnBox3D.Commands;
using UnBox3D.Rendering;
using UnBox3D.Rendering.OpenGL;

namespace UnBox3D.Controls.States
{
    /// <summary>
    /// Rotate mode.
    ///
    /// • Click a colored ring  → rotate around that ring's axis (axis-constrained)
    /// • Click mesh body       → free Y-axis rotation (same as original behavior)
    /// • Click empty space     → deselect, hide gizmo
    ///
    /// Hit-testing is screen-space — the same technique used by GimbalState.
    /// </summary>
    public class RotateState : IState
    {
        private const int   RingSamples = 24;   // sample points around each ring
        private const float RingLinePx  = 16f;  // hit radius for ring lines (px)

        private readonly ISettingsManager _settingsManager;
        private readonly ISceneManager   _sceneManager;
        private readonly IGLControlHost  _controlHost;
        private readonly ICamera         _camera;
        private readonly IRayCaster      _rayCaster;
        private readonly ICommandHistory _commandHistory;
        private readonly IRenderer       _renderer;

        private IAppMesh? _selectedMesh;

        private enum RotateAxis { None, X, Y, Z, Free }
        private RotateAxis _rotateAxis = RotateAxis.None;

        private float  _rotationSensitivity;
        private Point  _lastClientPos;
        private float  _accumulatedAngle;

        public RotateState(
            ISettingsManager settingsManager,
            ISceneManager   sceneManager,
            IGLControlHost  controlHost,
            ICamera         camera,
            IRayCaster      rayCaster,
            ICommandHistory commandHistory,
            IRenderer       renderer)
        {
            _settingsManager     = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _sceneManager        = sceneManager    ?? throw new ArgumentNullException(nameof(sceneManager));
            _controlHost         = controlHost     ?? throw new ArgumentNullException(nameof(controlHost));
            _camera              = camera          ?? throw new ArgumentNullException(nameof(camera));
            _rayCaster           = rayCaster       ?? throw new ArgumentNullException(nameof(rayCaster));
            _commandHistory      = commandHistory  ?? throw new ArgumentNullException(nameof(commandHistory));
            _renderer            = renderer        ?? throw new ArgumentNullException(nameof(renderer));

            _rotationSensitivity = _settingsManager.GetSetting<float>(
                new UISettings().GetKey(), UISettings.MeshRotationSensitivity);
        }

        /// <summary>
        /// Pre-selects a mesh so rings appear immediately when entering Rotate mode.
        /// </summary>
        public void SetSelectedMesh(IAppMesh mesh)
        {
            _selectedMesh = mesh;
            _renderer.SetActiveGizmoMesh(mesh);
            _renderer.SetGizmoMode(GizmoMode.RingsOnly);
            _controlHost.Invalidate();
        }

        // ── Mouse Down ─────────────────────────────────────────────────────

        public void OnMouseDown(MouseEventArgs e)
        {
            _lastClientPos    = new Point(e.X, e.Y);
            _accumulatedAngle = 0f;
            _rotateAxis       = RotateAxis.None;

            // 1. If a mesh is already selected, try ring hit-test first.
            if (_selectedMesh != null)
            {
                var axis = HitTestRings(e.X, e.Y);
                if (axis != RotateAxis.None)
                {
                    _rotateAxis = axis;
                    _controlHost.SetCursor(Cursors.Hand);
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
                // ray-cast.  The precise screen-space hit-test above (HitTestRings)
                // may miss by a few pixels; falling through to a different mesh here
                // would cause the wrong mesh to be moved/rotated.  Free-drag the
                // already-selected mesh instead.  To select a different mesh the user
                // first clicks empty space to deselect, then clicks the target mesh.
                if (_selectedMesh != null && clickedMesh != _selectedMesh)
                {
                    _rotateAxis = RotateAxis.Free;
                    _controlHost.SetCursor(Cursors.Hand);
                    return;
                }

                _selectedMesh = clickedMesh;
                _renderer.SetActiveGizmoMesh(_selectedMesh);
                _renderer.SetGizmoMode(GizmoMode.RingsOnly);
                _rotateAxis   = RotateAxis.Free;   // default: drag = free Y-axis spin
                _controlHost.SetCursor(Cursors.Hand);
                _controlHost.Invalidate();
            }
            else
            {
                // Deselect only if clearly outside gizmo ring area.
                bool insideGizmo = false;
                if (_selectedMesh != null && _renderer.TryGetGizmoInfo(out Vector3 gc, out float gr))
                    insideGizmo = NearScreen(e.X, e.Y, gc, gr * 1.2f);

                if (!insideGizmo)
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
            // Hover cursor update.
            if (_rotateAxis == RotateAxis.None)
            {
                if (_selectedMesh != null)
                    _controlHost.SetCursor(HitTestRings(e.X, e.Y) != RotateAxis.None
                        ? Cursors.Hand : Cursors.Default);
                return;
            }

            if (_selectedMesh == null) return;

            float pxX = e.X - _lastClientPos.X;
            if (Math.Abs(pxX) < 0.5f)
            {
                _lastClientPos = new Point(e.X, e.Y);
                return;
            }

            float angle = pxX * _rotationSensitivity;
            if (Math.Abs(angle) > 0.001f)
            {
                Vector3 axis = _rotateAxis switch
                {
                    RotateAxis.X    => Vector3.UnitX,
                    RotateAxis.Y    => Vector3.UnitY,
                    RotateAxis.Z    => Vector3.UnitZ,
                    RotateAxis.Free => Vector3.UnitY,   // free drag: spin around world Y
                    _               => Vector3.UnitY
                };

                var q = Quaternion.FromAxisAngle(axis, MathHelper.DegreesToRadians(angle));
                _selectedMesh.Rotate(q);
                _accumulatedAngle += angle;
            }

            _lastClientPos = new Point(e.X, e.Y);
            _controlHost.Invalidate();
        }

        // ── Mouse Up ───────────────────────────────────────────────────────

        public void OnMouseUp(MouseEventArgs e)
        {
            if (_selectedMesh != null && Math.Abs(_accumulatedAngle) > 0.001f)
            {
                Vector3 axis = _rotateAxis switch
                {
                    RotateAxis.X    => Vector3.UnitX,
                    RotateAxis.Y    => Vector3.UnitY,
                    RotateAxis.Z    => Vector3.UnitZ,
                    RotateAxis.Free => Vector3.UnitY,
                    _               => Vector3.UnitY
                };

                float rad     = MathHelper.DegreesToRadians(_accumulatedAngle);
                var   doRot   = Quaternion.FromAxisAngle(axis,  rad);
                var   undoRot = Quaternion.FromAxisAngle(axis, -rad);
                _commandHistory.PushCommand(new RotateCommand(_selectedMesh, doRot, undoRot));
            }

            _rotateAxis       = RotateAxis.None;
            _accumulatedAngle = 0f;
        }

        // ── Ring hit-test (screen-space) ──────────────────────────────────

        private RotateAxis HitTestRings(int mx, int my)
        {
            if (!_renderer.TryGetGizmoInfo(out Vector3 center, out float radius))
                return RotateAxis.None;

            for (int i = 0; i < RingSamples; i++)
            {
                float a   = 2f * MathF.PI * i / RingSamples;
                float cos = MathF.Cos(a) * radius;
                float sin = MathF.Sin(a) * radius;

                // X ring: in render YZ plane
                if (NearScreen(mx, my, center + new Vector3(0f,  cos, sin), RingLinePx)) return RotateAxis.X;
                // Y ring: in render XZ plane
                if (NearScreen(mx, my, center + new Vector3(cos, 0f,  sin), RingLinePx)) return RotateAxis.Y;
                // Z ring: in render XY plane
                if (NearScreen(mx, my, center + new Vector3(cos, sin, 0f),  RingLinePx)) return RotateAxis.Z;
            }

            return RotateAxis.None;
        }

        // ── Screen-space projection helpers ───────────────────────────────

        private bool NearScreen(int mx, int my, Vector3 worldPos, float threshPx)
        {
            var sp = ProjectToScreen(worldPos);
            if (sp == null) return false;
            float dx = mx - sp.Value.X, dy = my - sp.Value.Y;
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
    }
}
