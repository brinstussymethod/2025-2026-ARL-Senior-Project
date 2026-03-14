using OpenTK.Mathematics;
using System.Windows.Forms;
using UnBox3D.Commands;
using UnBox3D.Models;
using UnBox3D.Rendering;
using UnBox3D.Rendering.OpenGL;
using UnBox3D.Rendering.Rulers;

namespace UnBox3D.Controls.States
{
    /// <summary>
    /// Ruler mode state machine.
    /// Left-click on the ground places a new ruler at the ray-ground intersection.
    /// </summary>
    public class RulerState : IState
    {
        // ── Dependencies ───────────────────────────────────────────────────
        private readonly IGLControlHost  _controlHost;
        private readonly ICamera         _camera;
        private readonly IRayCaster      _rayCaster;
        private readonly ICommandHistory _commandHistory;
        private readonly IRulerManager   _rulerManager;
        private readonly RulerRenderer   _rulerRenderer;

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

            // Place a new ruler at the ray–ground intersection
            Vector3 rayOrigin = _camera.Position;
            Vector3 rayDir    = _rayCaster.GetRay();
            var groundHit     = RayHitsGround(rayOrigin, rayDir);
            if (groundHit == null) return;

            // groundHit.Y is guaranteed 0 (we hit the Y=0 plane).
            // Store render-space XZ as BasePosition; Y is always 0.
            var renderHit = groundHit.Value;

            var newRuler = new RulerObject
            {
                BasePosition = new Vector3(renderHit.X, 0f, renderHit.Z),
                HeightWorld  = 1.0f,
                IsSelected   = true,
            };

            _rulerRenderer.BuildOrRebuild(newRuler);
            _rulerManager.AddRuler(newRuler);
            _controlHost.Invalidate();
        }

        public void OnMouseMove(MouseEventArgs e) { }

        public void OnMouseUp(MouseEventArgs e) { }

        // ── Ground ray-hit ─────────────────────────────────────────────────

        /// <summary>
        /// Solves ray–ground intersection. Ground is render Y = 0 (the visible grid plane).
        /// The camera uses render Y as "up" so the XZ plane at Y=0 is the floor.
        /// Returns the hit point in render space, or null if the ray is parallel to ground.
        /// </summary>
        private static Vector3? RayHitsGround(Vector3 rayOrigin, Vector3 rayDir)
        {
            if (MathF.Abs(rayDir.Y) < 1e-6f) return null;
            float t = -rayOrigin.Y / rayDir.Y;
            if (t < 0f) return null;
            return rayOrigin + t * rayDir;
        }

    }
}
