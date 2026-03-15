using UnBox3D.Rendering.Rulers;

namespace UnBox3D.Commands.Rulers
{
    /// <summary>Undo/redo: place a new ruler in the scene.</summary>
    public class PlaceRulerCommand : ICommand
    {
        private readonly IRulerManager _manager;
        private readonly RulerObject   _ruler;

        public PlaceRulerCommand(IRulerManager manager, RulerObject ruler)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _ruler   = ruler   ?? throw new ArgumentNullException(nameof(ruler));
        }

        public void Execute() => _manager.AddRuler(_ruler);
        public void Undo()    => _manager.RemoveRuler(_ruler);
    }
}
