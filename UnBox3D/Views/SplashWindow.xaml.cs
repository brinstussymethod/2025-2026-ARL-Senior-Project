using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace UnBox3D.Views
{
    public partial class SplashWindow : Window
    {
        public SplashWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(600))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var slideUp = new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(600))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            SplashContent.BeginAnimation(OpacityProperty, fadeIn);
            SplashContentSlide.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slideUp);

            // Slide loading bar across the track, looping
            var loadAnim = new DoubleAnimation(-60, 160, TimeSpan.FromMilliseconds(1100))
            {
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            LoadBarSlide.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, loadAnim);
        }
    }
}
