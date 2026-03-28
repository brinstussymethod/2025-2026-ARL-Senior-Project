using UnBox3D.Rendering.Rulers;
using UnBox3D.Utils;

namespace UnBox3D.Commands.Rulers
{
    /// <summary>
    /// Undo/redo: change the global scene scale (mmPerWorldUnit) and/or display unit.
    /// Covers both arrow-drag resize and label value/unit edits — any change that
    /// redefines what a world unit means in real life.
    /// </summary>
    public class SetScaleCommand : ICommand
    {
        private readonly IScaleSettings _scaleSettings;
        private readonly IRulerManager  _rulerManager;
        private readonly double         _oldMm;
        private readonly double         _newMm;
        private readonly RulerUnit      _oldUnit;
        private readonly RulerUnit      _newUnit;

        public SetScaleCommand(
            IScaleSettings scaleSettings,
            IRulerManager  rulerManager,
            double         oldMm,
            double         newMm,
            RulerUnit      oldUnit,
            RulerUnit      newUnit)
        {
            _scaleSettings = scaleSettings ?? throw new ArgumentNullException(nameof(scaleSettings));
            _rulerManager  = rulerManager  ?? throw new ArgumentNullException(nameof(rulerManager));
            _oldMm   = oldMm;
            _newMm   = newMm;
            _oldUnit = oldUnit;
            _newUnit = newUnit;
        }

        public void Execute() => Apply(_newMm, _newUnit);
        public void Undo()    => Apply(_oldMm, _oldUnit);

        private void Apply(double mm, RulerUnit unit)
        {
            _scaleSettings.SetScale(mm);
            _rulerManager.GlobalUnit = unit;
        }
    }
}
