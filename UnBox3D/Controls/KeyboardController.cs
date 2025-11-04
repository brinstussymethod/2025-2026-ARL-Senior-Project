using System.Windows.Input;
using UnBox3D.Rendering;

namespace UnBox3D.Controls
{
    public class KeyboardController
    {
        private readonly ICamera _camera;

        public KeyboardController(ICamera camera)
        {
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));

            // Hook into the application's input events
            System.Windows.Application.Current.MainWindow.KeyDown += OnKeyDown;
            System.Windows.Application.Current.MainWindow.KeyUp += OnKeyUp;
        }

        private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            const float cameraSpeed = 1.5f;

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
