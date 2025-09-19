using System;
using System.Windows.Input;
using UnBox3D.Rendering;

// NOTE: we reference System.Windows.Forms.Keys in the WinForms bridge method.
// We don't need "using System.Windows.Forms;" at the top to avoid conflicts,
// we just fully-qualify it in the method signatures.
namespace UnBox3D.Controls
{
    public class KeyboardController
    {
        private readonly ICamera _camera;

        public KeyboardController(ICamera camera)
        {
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));

            // WPF side (works when MainWindow has focus)
            var win = System.Windows.Application.Current.MainWindow;
            win.PreviewKeyDown += OnKeyDown;   // catch arrows too
            win.KeyDown += OnKeyDown;
            // If you later want WPF KeyUp semantics, you can also hook: win.KeyUp += OnKeyUp;
        }

        // ===== WinForms bridge (GLControlHost forwards here) =====
        public void HandleWinFormsKeyDown(System.Windows.Forms.Keys key)
        {
            Key mapped = key switch
            {
                System.Windows.Forms.Keys.Left => Key.Left,
                System.Windows.Forms.Keys.Right => Key.Right,
                System.Windows.Forms.Keys.Up => Key.Up,
                System.Windows.Forms.Keys.Down => Key.Down,

                System.Windows.Forms.Keys.Oemplus => Key.OemPlus,
                System.Windows.Forms.Keys.Add => Key.Add,
                System.Windows.Forms.Keys.OemMinus => Key.OemMinus,
                System.Windows.Forms.Keys.Subtract => Key.Subtract,

                System.Windows.Forms.Keys.W => Key.W,
                System.Windows.Forms.Keys.A => Key.A,
                System.Windows.Forms.Keys.S => Key.S,
                System.Windows.Forms.Keys.D => Key.D,

                System.Windows.Forms.Keys.Space => Key.Space,
                System.Windows.Forms.Keys.ShiftKey => Key.LeftShift,
                System.Windows.Forms.Keys.LShiftKey => Key.LeftShift,
                System.Windows.Forms.Keys.RShiftKey => Key.LeftShift,

                System.Windows.Forms.Keys.Escape => Key.Escape,
                _ => Key.None
            };

            if (mapped != Key.None)
                HandleKey(mapped);
        }

        // Optional: WinForms KeyUp bridge (currently no stateful logic needed).
        // We include it so GLControlHost can call it without compile errors.
        public void HandleWinFormsKeyUp(System.Windows.Forms.Keys key)
        {
            // If you ever add stateful handling (e.g., continuous movement on key hold),
            // you can mirror the mapping above and clear flags here.
            // For now, it's intentionally a no-op.
        }

        // ===== WPF event handler -> shared logic =====
        private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e) => HandleKey(e.Key);

        // ===== Shared camera logic (Blender-like) =====
        private void HandleKey(Key key)
        {
            const float yawStep = 3.0f;   // arrow rotation (deg)
            const float pitchStep = 2.0f; // arrow rotation (deg)
            const float zoomStep = 0.3f;  // +/- changes radius
            const float panStep = 0.3f;   // WASD/Space/Shift pans target

            if (_camera is OrbitCamera orbit)
            {
                switch (key)
                {
                    // Orbit target
                    case Key.Left: orbit.Orbit(-yawStep, 0f); break;
                    case Key.Right: orbit.Orbit(+yawStep, 0f); break;
                    case Key.Up: orbit.Orbit(0f, +pitchStep); break;
                    case Key.Down: orbit.Orbit(0f, -pitchStep); break;

                    // Zoom
                    case Key.OemPlus:
                    case Key.Add:
                        orbit.Dolly(-zoomStep); break; // closer
                    case Key.OemMinus:
                    case Key.Subtract:
                        orbit.Dolly(+zoomStep); break; // farther

                    // Pan target (WASD + Space/Shift)
                    case Key.W: orbit.OffsetTarget(orbit.Front * panStep); break;
                    case Key.S: orbit.OffsetTarget(-orbit.Front * panStep); break;
                    case Key.A: orbit.OffsetTarget(-orbit.Right * panStep); break;
                    case Key.D: orbit.OffsetTarget(orbit.Right * panStep); break;
                    case Key.Space: orbit.OffsetTarget(orbit.Up * panStep); break;
                    case Key.LeftShift: orbit.OffsetTarget(-orbit.Up * panStep); break;
                }
            }

            if (key == Key.Escape)
                System.Windows.Application.Current.Shutdown();
        }
    }
}
