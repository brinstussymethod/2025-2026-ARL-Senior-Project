using UnBox3D.Rendering.Rulers;

namespace UnBox3D.Commands.Rulers
{
    /// <summary>
    /// Undo/redo: change the HeightWorld of a single ruler (arrow-shaft drag).
    /// Only the ruler's visual height changes; the global scale (MmPerWorldUnit) is
    /// unaffected — that is the job of SetScaleCommand (triggered by label typing only).
    /// </summary>
    public class SetRulerHeightCommand : ICommand
    {
        private readonly RulerObject   _ruler;
        private readonly RulerRenderer _renderer;
        private readonly float         _oldHeight;
        private readonly float         _newHeight;

        public SetRulerHeightCommand(
            RulerObject   ruler,
            RulerRenderer renderer,
            float         oldHeight,
            float         newHeight)
        {
            _ruler     = ruler     ?? throw new ArgumentNullException(nameof(ruler));
            _renderer  = renderer  ?? throw new ArgumentNullException(nameof(renderer));
            _oldHeight = oldHeight;
            _newHeight = newHeight;
        }

        public void Execute() => Apply(_newHeight);
        public void Undo()    => Apply(_oldHeight);

        private void Apply(float height)
        {
            _ruler.HeightWorld = height;
            _renderer.BuildOrRebuild(_ruler);
        }
    }
}
