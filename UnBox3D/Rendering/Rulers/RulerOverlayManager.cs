using OpenTK.Mathematics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UnBox3D.Commands.Rulers;
using UnBox3D.Models;
using UnBox3D.Rendering.OpenGL;
using UnBox3D.Utils;
// Aliases to resolve WPF vs WinForms ambiguity (this file is pure WPF)
using WpfBrushes     = System.Windows.Media.Brushes;
using WpfColor       = System.Windows.Media.Color;
using WpfCursors     = System.Windows.Input.Cursors;
using WpfFontFamily  = System.Windows.Media.FontFamily;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfPoint       = System.Windows.Point;
using WpfTextBox      = System.Windows.Controls.TextBox;
using WpfComboBox     = System.Windows.Controls.ComboBox;
using WpfSize         = System.Windows.Size;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace UnBox3D.Rendering.Rulers
{
    /// <summary>
    /// Manages WPF label elements on a transparent Canvas that sits on top of the GL viewport.
    /// Each ruler gets a floating pill label showing its height value + unit.
    /// Clicking the label opens an inline edit (TextBox + ComboBox) to redefine the scale.
    /// </summary>
    public class RulerOverlayManager
    {
        // ── Fields ─────────────────────────────────────────────────────────
        private Canvas?         _canvas;
        private ICamera?        _camera;
        private IGLControlHost? _host;
        private IScaleSettings? _scaleSettings;
        private IRulerManager?  _rulerManager;
        private ICommandHistory? _commandHistory;

        private readonly Dictionary<Guid, Border>     _labels  = new();
        private readonly Dictionary<Guid, StackPanel> _editPanels = new();

        // ── Setup ──────────────────────────────────────────────────────────

        public void Attach(Canvas canvas, ICamera camera, IGLControlHost host,
                           IScaleSettings scaleSettings, IRulerManager rulerManager,
                           ICommandHistory commandHistory)
        {
            _canvas         = canvas;
            _camera         = camera;
            _host           = host;
            _scaleSettings  = scaleSettings;
            _rulerManager   = rulerManager;
            _commandHistory = commandHistory;

            // Keep labels in sync with collection changes
            rulerManager.GetRulers().CollectionChanged += (_, args) =>
            {
                if (args.NewItems != null)
                    foreach (RulerObject r in args.NewItems) CreateLabel(r);
                if (args.OldItems != null)
                    foreach (RulerObject r in args.OldItems) RemoveLabel(r.Id);
            };
        }

        // ── Per-frame update ───────────────────────────────────────────────

        /// <summary>
        /// Repositions all labels and refreshes their displayed values.
        /// Call this every frame from the render loop (on the UI thread).
        /// </summary>
        public void UpdateAll(IEnumerable<RulerObject> rulers)
        {
            if (_canvas == null || _camera == null || _host == null || _scaleSettings == null || _rulerManager == null)
                return;

            foreach (var ruler in rulers)
            {
                if (!_labels.TryGetValue(ruler.Id, out var label)) continue;

                // If in edit mode, skip repositioning (user is typing)
                if (_editPanels.ContainsKey(ruler.Id)) continue;

                // Refresh text
                if (label.Child is StackPanel sp)
                    RefreshLabelText(sp, ruler);

                // Project stem midpoint to screen.
                // BasePosition stores render-space (X, 0, Z); render Y is "up".
                float rx = ruler.BasePosition.X;
                float rz = ruler.BasePosition.Z;
                float h  = ruler.HeightWorld;
                var   midRender = new Vector3(rx, h * 0.5f, rz);
                var   screenPt  = ProjectToScreen(midRender);
                if (screenPt == null)
                {
                    label.Visibility = Visibility.Collapsed;
                    continue;
                }

                label.Visibility = Visibility.Visible;
                label.Measure(new WpfSize(double.PositiveInfinity, double.PositiveInfinity));
                double w = Math.Max(label.DesiredSize.Width,  80);
                double h2 = Math.Max(label.DesiredSize.Height, 24);

                Canvas.SetLeft(label, screenPt.Value.X - w  * 0.5);
                Canvas.SetTop( label, screenPt.Value.Y - h2 * 0.5);
            }
        }

        // ── Label lifecycle ────────────────────────────────────────────────

        private void CreateLabel(RulerObject ruler)
        {
            if (_canvas == null) return;

            var valueBlock = new TextBlock
            {
                FontSize   = 12,
                Foreground = WpfBrushes.White,
                FontFamily = new WpfFontFamily("Cascadia Code, Consolas, monospace"),
                FontWeight = FontWeights.SemiBold,
            };
            var unitBlock = new TextBlock
            {
                FontSize   = 12,
                Foreground = new SolidColorBrush(WpfColor.FromRgb(0x00, 0xB3, 0x94)),
            };

            var sp = new StackPanel { Orientation = WpfOrientation.Horizontal };
            sp.Children.Add(valueBlock);
            sp.Children.Add(unitBlock);

            var border = new Border
            {
                Background        = new SolidColorBrush(WpfColor.FromArgb(0xCC, 0x1A, 0x1A, 0x2E)),
                BorderBrush       = new SolidColorBrush(WpfColor.FromArgb(0x80, 0x00, 0xB3, 0x94)),
                BorderThickness   = new Thickness(1),
                CornerRadius      = new CornerRadius(4),
                Padding           = new Thickness(8, 4, 8, 4),
                IsHitTestVisible  = true,
                Cursor            = WpfCursors.IBeam,
                Child             = sp,
            };

            RefreshLabelText(sp, ruler);

            border.MouseLeftButtonDown += (_, e) =>
            {
                if (e.ClickCount == 1) OpenEditMode(ruler, border);
                e.Handled = true;
            };

            _labels[ruler.Id] = border;
            _canvas.Children.Add(border);
        }

        private void RemoveLabel(Guid id)
        {
            if (_canvas == null) return;
            if (_labels.Remove(id, out var b))  _canvas.Children.Remove(b);
            _editPanels.Remove(id);
        }

        /// <summary>
        /// Cancels any open inline edit panels immediately.
        /// Call this whenever a GL-viewport click steals Win32 focus (e.g. ruler placement),
        /// because WPF's IsKeyboardFocusWithinChanged does not fire across the WinForms HWND boundary.
        /// </summary>
        public void CancelAllEdits()
        {
            // Snapshot the pairs because CancelEdit mutates _editPanels
            foreach (var (id, _) in _editPanels.ToList())
            {
                if (_labels.TryGetValue(id, out var border) && _rulerManager != null)
                {
                    var ruler = _rulerManager.GetById(id);
                    if (ruler != null) CancelEdit(ruler, border);
                }
            }
        }

        // ── Inline edit ────────────────────────────────────────────────────

        private void OpenEditMode(RulerObject ruler, Border border)
        {
            if (_editPanels.ContainsKey(ruler.Id)) return; // already open

            double currentDisplay = ComputeDisplay(ruler);
            RulerUnit currentUnit  = _rulerManager!.GlobalUnit;

            var valueBox = new WpfTextBox
            {
                Text              = currentDisplay.ToString("F2", CultureInfo.InvariantCulture),
                Width             = 60,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize          = 12,
                BorderThickness   = new Thickness(0),
                Background        = WpfBrushes.Transparent,
                Foreground        = WpfBrushes.White,
                CaretBrush        = WpfBrushes.White,
                SelectionBrush    = new SolidColorBrush(WpfColor.FromArgb(0x80, 0x00, 0xB3, 0x94)),
            };
            var unitCombo = new WpfComboBox
            {
                Width             = 52,
                FontSize          = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Background        = new SolidColorBrush(WpfColor.FromArgb(0xCC, 0x1A, 0x1A, 0x2E)),
                Foreground        = WpfBrushes.White,
                BorderThickness   = new Thickness(0),
            };
            foreach (RulerUnit u in Enum.GetValues<RulerUnit>())
                unitCombo.Items.Add(RulerUnitConverter.UnitSymbol(u));
            unitCombo.SelectedItem = RulerUnitConverter.UnitSymbol(currentUnit);

            var editPanel = new StackPanel { Orientation = WpfOrientation.Horizontal };
            editPanel.Children.Add(valueBox);
            editPanel.Children.Add(unitCombo);

            border.Child = editPanel;
            _editPanels[ruler.Id] = editPanel;

            // Commit on Enter, cancel on Escape.
            // Only mark the event handled for keys we explicitly intercept — marking it handled
            // unconditionally suppresses WPF's TextInput event and blocks digit/letter input.
            valueBox.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Return)
                {
                    CommitEdit(ruler, border, valueBox, unitCombo);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    CancelEdit(ruler, border);
                    e.Handled = true;
                }
                // All other keys (digits, letters, arrows, etc.) fall through to TextBox normally
            };
            unitCombo.SelectionChanged += (_, _) =>
            {
                // Unit-only change: recompute the numeric display without committing
                string? sel = unitCombo.SelectedItem as string;
                if (sel == null) return;
                RulerUnit newUnit = ParseUnit(sel);
                double    newVal  = RulerUnitConverter.Convert(
                    double.TryParse(valueBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : currentDisplay,
                    currentUnit,
                    newUnit);
                currentUnit       = newUnit;
                currentDisplay    = newVal;
                valueBox.Text     = newVal.ToString("F2", CultureInfo.InvariantCulture);
            };

            // Cancel when focus leaves the entire edit panel (e.g. user clicks elsewhere).
            // IsKeyboardFocusWithinChanged fires on the panel rather than the TextBox so that
            // clicking the ComboBox (which briefly steals focus from the TextBox) does not
            // prematurely cancel.
            editPanel.IsKeyboardFocusWithinChanged += (_, e) =>
            {
                if (!(bool)e.NewValue && _editPanels.ContainsKey(ruler.Id))
                    CancelEdit(ruler, border);
            };

            // Focus first, then select-all at Input priority so WPF doesn't clear the selection
            // when the focus-change event lands. Calling SelectAll() before Focus() is unreliable —
            // it means the cursor position is unset, and the user's first keystroke appends to the
            // existing "0.00" text rather than replacing it (producing e.g. "0.001" = ~0 m).
            valueBox.Focus();
            valueBox.Dispatcher.BeginInvoke(
                new Action(valueBox.SelectAll),
                System.Windows.Threading.DispatcherPriority.Input);
        }

        private void CommitEdit(RulerObject ruler, Border border, WpfTextBox valueBox, WpfComboBox unitCombo)
        {
            if (_scaleSettings == null || _rulerManager == null || _commandHistory == null) return;

            if (!double.TryParse(valueBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double enteredValue) || enteredValue <= 0)
            {
                CancelEdit(ruler, border);
                return;
            }

            string?    selUnit = unitCombo.SelectedItem as string;
            RulerUnit  newUnit = selUnit != null ? ParseUnit(selUnit) : _rulerManager.GlobalUnit;

            double enteredMm  = RulerUnitConverter.ToMm(enteredValue, newUnit);
            double oldMm      = _scaleSettings.MmPerWorldUnit;
            double newMm      = ruler.HeightWorld > 0 ? enteredMm / ruler.HeightWorld : oldMm;

            _commandHistory.PushCommand(new SetScaleCommand(
                _scaleSettings, _rulerManager,
                oldMm, newMm,
                _rulerManager.GlobalUnit, newUnit));

            // Apply immediately (SetScaleCommand.Execute would run on redo, but we apply now too)
            _scaleSettings.SetScale(newMm);
            _rulerManager.GlobalUnit = newUnit;

            CloseEditMode(ruler, border);
        }

        private void CancelEdit(RulerObject ruler, Border border)
            => CloseEditMode(ruler, border);

        private void CloseEditMode(RulerObject ruler, Border border)
        {
            _editPanels.Remove(ruler.Id);

            // Restore normal label display
            var valueBlock = new TextBlock
            {
                FontSize   = 12,
                Foreground = WpfBrushes.White,
                FontFamily = new WpfFontFamily("Cascadia Code, Consolas, monospace"),
                FontWeight = FontWeights.SemiBold,
            };
            var unitBlock = new TextBlock
            {
                FontSize   = 12,
                Foreground = new SolidColorBrush(WpfColor.FromRgb(0x00, 0xB3, 0x94)),
            };
            var sp = new StackPanel { Orientation = WpfOrientation.Horizontal };
            sp.Children.Add(valueBlock);
            sp.Children.Add(unitBlock);
            RefreshLabelText(sp, ruler);
            border.Child = sp;
        }

        // ── Helpers ────────────────────────────────────────────────────────

        private void RefreshLabelText(StackPanel sp, RulerObject ruler)
        {
            if (_scaleSettings == null || _rulerManager == null) return;
            double display = ComputeDisplay(ruler);
            string sym     = RulerUnitConverter.UnitSymbol(_rulerManager.GlobalUnit);

            if (sp.Children.Count >= 2)
            {
                ((TextBlock)sp.Children[0]).Text = display.ToString("F2", CultureInfo.InvariantCulture) + " ";
                ((TextBlock)sp.Children[1]).Text = sym;
            }
        }

        private double ComputeDisplay(RulerObject ruler)
        {
            if (_scaleSettings == null || _rulerManager == null) return 0;
            double realMm = RulerUnitConverter.WorldUnitsToRealMm(ruler.HeightWorld, _scaleSettings.MmPerWorldUnit);
            return RulerUnitConverter.FromMm(realMm, _rulerManager.GlobalUnit);
        }

        private WpfPoint? ProjectToScreen(Vector3 renderPos)
        {
            if (_camera == null || _host == null) return null;

            Matrix4 vp   = _camera.GetViewMatrix() * _camera.GetProjectionMatrix();
            Vector4 clip = new Vector4(renderPos, 1f) * vp;
            if (clip.W <= 0f) return null;

            float invW = 1f / clip.W;
            double sx  = (clip.X * invW + 1f) * 0.5f * _host.GetWidth();
            double sy  = (1f - clip.Y * invW) * 0.5f * _host.GetHeight();
            return new WpfPoint(sx, sy);
        }

        private static RulerUnit ParseUnit(string sym) => sym switch
        {
            "mm" => RulerUnit.Mm,
            "cm" => RulerUnit.Cm,
            "m"  => RulerUnit.M,
            "in" => RulerUnit.In,
            "ft" => RulerUnit.Ft,
            _    => RulerUnit.M,
        };
    }
}
