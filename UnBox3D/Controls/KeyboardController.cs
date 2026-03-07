using System.Windows.Input;
using UnBox3D.Rendering;

namespace UnBox3D.Controls
{
    public class KeyboardController
    {
        private readonly ICamera _camera;
        private System.Windows.Window? _subscribedWindow;

        public KeyboardController(ICamera camera)
        {
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));

            // Hook into the application's input events on the current MainWindow.
            // Call ReAttach() whenever a new MainWindow instance takes over.
            ReAttach(System.Windows.Application.Current.MainWindow);
        }

        /// <summary>
        /// Unsubscribes from the old window and re-subscribes to the new one.
        /// Call this every time a new MainWindow is shown (e.g. after Back to Menu).
        /// </summary>
        public void ReAttach(System.Windows.Window? newWindow)
        {
            // Detach from old window if any
            if (_subscribedWindow != null)
            {
                _subscribedWindow.KeyDown -= OnKeyDown;
                _subscribedWindow.KeyUp   -= OnKeyUp;
            }

            _subscribedWindow = newWindow;

            if (_subscribedWindow != null)
            {
                _subscribedWindow.KeyDown += OnKeyDown;
                _subscribedWindow.KeyUp   += OnKeyUp;
            }
        }
        /* 
         * OnKeyDoown(Object sender, System.Windows.Input.KeyEventArgs e) 
         * Date 10/1/2025 Modified by Brian Andrade
         * Summary of Modefication: Added Q and E keys to roll the camera appropriatly 
         */
        private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            const float cameraSpeed = 1.5f;
            const float rollStep = 2.5f; 

            switch (e.Key)
            {
                case Key.W:
                    _camera.Position += _camera.Front * cameraSpeed; // Move Forward
                    break;
                case Key.S:
                    _camera.Position -= _camera.Front* cameraSpeed; // Move Backward
                    break;
                case Key.A:
                    _camera.Position -= _camera.Right * cameraSpeed; // Move Left
                    break;
                case Key.D:
                    _camera.Position += _camera.Right * cameraSpeed; // Move Right
                    break;
                case Key.Space:
                    _camera.Position += _camera.Up * cameraSpeed; // Move Up
                    break;
                case Key.LeftShift:
                    _camera.Position -= _camera.Up * cameraSpeed; // Move Down
                    break;

                case Key.Q: _camera.AddRoll(-rollStep); break; // Roll left
                case Key.E: _camera.AddRoll(+rollStep); break; // Roll right 

                case Key.Escape:
                    System.Windows.Application.Current.Shutdown(); // Close the application
                    break;
            }
        }

        private void OnKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Handle key release logic if needed
        }
    }
}
