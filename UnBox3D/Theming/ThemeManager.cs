using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Animation;
using UnBox3D.Utils; // ISettingsManager
using WpfApplication = System.Windows.Application; // Disambiguate from WinForms.Application

namespace UnBox3D.Theming
{
    public sealed class ThemeManager : IThemeManager
    {
        private readonly ISettingsManager _settings;
        private const string SettingsParent = "UISettings";
        private const string SettingsKey = "AppTheme";

        public AppTheme Current { get; private set; } = AppTheme.Light;

        public ThemeManager(ISettingsManager settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void ApplySavedTheme(bool animate = false)
        {
            EnsureBaseDictionaries();

            try
            {
                var saved = _settings.GetSetting<string>(SettingsParent, SettingsKey);
                if (Enum.TryParse<AppTheme>(saved, out var theme))
                    Apply(theme, animate);
                else
                    Apply(AppTheme.Light, false);
            }
            catch
            {
                Apply(AppTheme.Light, false);
            }
        }

        public void Toggle(bool animate = true)
        {
            var next = Current == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;
            Apply(next, animate);
        }

        public void Apply(AppTheme theme, bool animate = true)
        {
            var app = WpfApplication.Current;
            if (app == null) return;

            EnsureBaseDictionaries();

            var lightDict = app.TryFindResource("Theme.Light") as ResourceDictionary;
            var darkDict  = app.TryFindResource("Theme.Dark")  as ResourceDictionary;
            var newTheme  = (theme == AppTheme.Dark ? darkDict : lightDict);

            if (newTheme == null)
            {
                Debug.WriteLine("[ThemeManager] Theme dictionary not found (Theme.Light/Theme.Dark). Did Colors.xaml load?");
                return;
            }

            var md = app.Resources.MergedDictionaries;
            var oldRuntime = md.FirstOrDefault(d => d.Contains("__RuntimeTheme__"));
            if (oldRuntime != null) md.Remove(oldRuntime);

            var runtimeTheme = new ResourceDictionary();
            foreach (var key in newTheme.Keys)
                runtimeTheme[key] = newTheme[key];
            runtimeTheme["__RuntimeTheme__"] = true;

            if (animate && app.Windows != null && app.Windows.Count > 0)
            {
                var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(180)) { FillBehavior = FillBehavior.Stop };
                fadeOut.Completed += (_, __) =>
                {
                    md.Add(runtimeTheme);
                    var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(220)) { FillBehavior = FillBehavior.Stop };
                    foreach (Window w in app.Windows) w.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                };
                foreach (Window w in app.Windows) w.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            }
            else
            {
                md.Add(runtimeTheme);
            }

            Current = theme;
            _settings.SetSetting(SettingsParent, SettingsKey, theme.ToString());
            _settings.SaveSettings();
        }

        private static void EnsureBaseDictionaries()
        {
            var app = WpfApplication.Current;
            if (app == null) return;

            var md = app.Resources.MergedDictionaries;

            TryLoadOnce(md, "Colors.xaml");
            TryLoadOnce(md, "Typography.xaml");
            TryLoadOnce(md, "Controls.xaml");
        }

        private static void TryLoadOnce(System.Collections.ObjectModel.Collection<ResourceDictionary> md, string fileName)
        {
            bool already = md.Any(d => d.Source != null && d.Source.OriginalString.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                        || md.Any(d => d.Source != null && d.Source.OriginalString.EndsWith($"Themes/{fileName}", StringComparison.OrdinalIgnoreCase));
            if (already) return;

            string asmName = (WpfApplication.ResourceAssembly ?? Assembly.GetExecutingAssembly()).GetName().Name;

            var candidates = new[]
            {
                $"Themes/{fileName}",
                fileName,
                $"pack://application:,,,/Themes/{fileName}",
                $"pack://application:,,,/{fileName}",
                $"pack://application:,,,/{asmName};component/Themes/{fileName}",
                $"pack://application:,,,/{asmName};component/{fileName}"
            };

            foreach (var uri in candidates)
            {
                if (TryAdd(md, uri))
                {
                    Debug.WriteLine($"[ThemeManager] Loaded resources: {uri}");
                    return;
                }
            }

            Debug.WriteLine($"[ThemeManager] Failed to load {fileName} from any known location.");
        }

        private static bool TryAdd(System.Collections.ObjectModel.Collection<ResourceDictionary> md, string uriString)
        {
            try
            {
                var uri = uriString.StartsWith("pack://", StringComparison.OrdinalIgnoreCase)
                    ? new Uri(uriString, UriKind.Absolute)
                    : new Uri(uriString, UriKind.Relative);

                var dict = new ResourceDictionary { Source = uri };
                md.Add(dict);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
