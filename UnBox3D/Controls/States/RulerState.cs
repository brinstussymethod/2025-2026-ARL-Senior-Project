using OpenTK.Mathematics;
using System.Windows.Forms;
using UnBox3D.Commands;
using UnBox3D.Commands.Rulers;
using UnBox3D.Models;
using UnBox3D.Rendering;
using UnBox3D.Rendering.OpenGL;
using UnBox3D.Rendering.Rulers;

namespace UnBox3D.Controls.States
{
    /// <summary>
    /// Ruler mode state machine.
    /// Left-click ground → place a new ruler. Left-click base disc → select and drag along ground.
    /// </summary>
    public class RulerState : IState
    {
        // ── Constants ──────────────────────────────────────────────────────
        private const float BaseHitPx = 20f;

        // ── Dependencies ───────────────────────────────────────────────────
        private readonly IGLControlHost  _controlHost;
        private readonly ICamera         _camera;
        private readonly IRayCaster      _rayCaster;
        private readonly ICommandHistory _commandHistory;
        private readonly IRulerManager   _rulerManager;
        private readonly RulerRenderer   _rulerRenderer;

        // ── Drag state ─────────────────────────────────────────────────────
        private enum Phase { Idle, MovingBase }
        private Phase        _phase         = Phase.Idle;
        private RulerObject? _activeRuler;
        private Point        _lastClientPos;
        private Vector3      _dragStartBase;
        private Vector3      _dragOffset;

        public RulerState(
            IGLControlHost  controlHost,
            ICamera         camera,
            IRayCaster      rayCaster,
            ICommandHistory commandHistory,
            IRulerManager   rulerManager,
            RulerRenderer   rulerRenderer)
        {
            _controlHost    = controlHost    ?? throw new ArgumentNullException(nameof(controlHost));
            _camera         = camera         ?? throw new ArgumentNullException(nameof(camera));
            _rayCaster      = rayCaster      ?? throw new ArgumentNullException(nameof(rayCaster));
            _commandHistory = commandHistory ?? throw new ArgumentNullException(nameof(commandHistory));
            _rulerManager   = rulerManager   ?? throw new ArgumentNullException(nameof(rulerManager));
            _rulerRenderer  = rulerRenderer  ?? throw new ArgumentNullException(nameof(rulerRenderer));
        }

        // ── IState ─────────────────────────────────────────────────────────

        public void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            _lastClientPos = new Point(e.X, e.Y);

            // Hit-test base discs
            foreach (var ruler in _rulerManager.GetRulers())
            {
                if (!_rulerRenderer.TryGetBaseCenter(ruler.Id, out var baseCenter)) continue;

                if (NearScreen(e.X, e.Y, baseCenter, BaseHitPx))
                {
                    Select(ruler);
                    _phase         = Phase.MovingBase;
                    _dragStartBase = ruler.BasePosition;
                    var grabHit    = RayHitsGround(_camera.Position, _rayCaster.GetRay());
                    _dragOffset    = grabHit.HasValue ? ruler.BasePosition - grabHit.Value : Vector3.Zero;
                    _controlHost.SetCursor(Cursors.SizeAll);
                    return;
                }
            }

            // Deselect the current ruler
            if (_activeRuler != null)
            {
                _activeRuler.IsSelected = false;
                _activeRuler = null;
            }

            // Place a new ruler at the ray–ground intersection
            Vector3 rayOrigin = _camera.Position;
            Vector3 rayDir    = _rayCaster.GetRay();
            var groundHit     = RayHitsGround(rayOrigin, rayDir);
            if (groundHit == null) return;

            var renderHit = groundHit.Value;

            var newRuler = new RulerObject
            {
                BasePosition = new Vector3(renderHit.X, 0f, renderHit.Z),
                HeightWorld  = 1.0f,
            };

            _rulerRenderer.BuildOrRebuild(newRuler);
            _rulerManager.AddRuler(newRuler);
            Select(newRuler);
            _controlHost.Invalidate();
        }

        public void OnMouseMove(MouseEventArgs e)
        {
            if (_phase == Phase.Idle || _activeRuler == null) { UpdateHoverCursor(e.X, e.Y); return; }

            _lastClientPos = new Point(e.X, e.Y);

            if (_phase == Phase.MovingBase) ApplyBaseMove();

            _controlHost.Invalidate();
        }

        public void OnMouseUp(MouseEventArgs e)
        {
            if (_phase == Phase.MovingBase && _activeRuler != null)
                if (_activeRuler.BasePosition != _dragStartBase)
                    _commandHistory.PushCommand(new MoveRulerCommand(
                        _activeRuler, _dragStartBase, _activeRuler.BasePosition));

            _phase = Phase.Idle;
            _controlHost.SetCursor(Cursors.Default);
        }

        // ── Base move ──────────────────────────────────────────────────────

        private void ApplyBaseMove()
        {
            if (_activeRuler == null) return;

            var groundHit = RayHitsGround(_camera.Position, _rayCaster.GetRay());
            if (groundHit == null) return;

            _activeRuler.BasePosition = new Vector3(
                groundHit.Value.X + _dragOffset.X,
                0f,
                groundHit.Value.Z + _dragOffset.Z);

            _rulerRenderer.BuildOrRebuild(_activeRuler);
        }

        // ── Hover cursor ───────────────────────────────────────────────────

        private void UpdateHoverCursor(int mx, int my)
        {
            foreach (var ruler in _rulerManager.GetRulers())
            {
                if (_rulerRenderer.TryGetBaseCenter(ruler.Id, out var bc)
                    && NearScreen(mx, my, bc, BaseHitPx))
                {
                    _controlHost.SetCursor(Cursors.SizeAll);
                    return;
                }
            }
            _controlHost.SetCursor(Cursors.Default);
        }

        // ── Selection ──────────────────────────────────────────────────────

        private void Select(RulerObject ruler)
        {
            if (_activeRuler != null && _activeRuler != ruler)
                _activeRuler.IsSelected = false;
            ruler.IsSelected = true;
            _activeRuler     = ruler;
        }

        // ── Ground ray-hit ─────────────────────────────────────────────────

        /// <summary>
        /// Solves ray–ground intersection. Ground is render Y = 0 (the visible grid plane).
        /// Returns the hit point in render space, or null if the ray is parallel to ground.
        /// </summary>
        private static Vector3? RayHitsGround(Vector3 rayOrigin, Vector3 rayDir)
        {
            if (MathF.Abs(rayDir.Y) < 1e-6f) return null;
            float t = -rayOrigin.Y / rayDir.Y;
            if (t < 0f) return null;
            return rayOrigin + t * rayDir;
        }

        // ── Screen-space helpers ───────────────────────────────────────────

        private bool NearScreen(int mx, int my, Vector3 renderPos, float threshPx)
        {
            var sp = ProjectToScreen(renderPos);
            if (sp == null) return false;
            float dx = mx - sp.Value.X, dy = my - sp.Value.Y;
            return dx * dx + dy * dy <= threshPx * threshPx;
        }

        private Vector2? ProjectToScreen(Vector3 renderPos)
        {
            Matrix4 vp   = _camera.GetViewMatrix() * _camera.GetProjectionMatrix();
            Vector4 clip = new Vector4(renderPos, 1f) * vp;
            if (clip.W <= 0f) return null;

            float invW = 1f / clip.W;
            return new Vector2(
                ( clip.X * invW + 1f) * 0.5f * _controlHost.GetWidth(),
                (1f - clip.Y * invW)  * 0.5f * _controlHost.GetHeight()
            );
        }
    }
}
