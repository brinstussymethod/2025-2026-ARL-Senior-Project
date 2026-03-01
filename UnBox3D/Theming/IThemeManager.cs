namespace UnBox3D.Theming
{
    // Defines the app's two themes and the theme manager contract.
    public enum AppTheme { Light, Dark }

    public interface IThemeManager
    {
        AppTheme Current { get; }
        void Apply(AppTheme theme, bool animate = true);
        void Toggle(bool animate = true);
        void ApplySavedTheme(bool animate = false);
    }
}
