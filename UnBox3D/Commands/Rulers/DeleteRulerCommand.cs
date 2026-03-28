using UnBox3D.Rendering.Rulers;

namespace UnBox3D.Commands.Rulers
{
    /// <summary>Undo/redo: delete a ruler from the scene.</summary>
    public class DeleteRulerCommand : ICommand
    {
        private readonly IRulerManager _manager;
        private readonly RulerObject   _ruler;
        private readonly RulerRenderer _renderer;

        public DeleteRulerCommand(IRulerManager manager, RulerObject ruler, RulerRenderer renderer)
        {
            _manager  = manager  ?? throw new ArgumentNullException(nameof(manager));
            _ruler    = ruler    ?? throw new ArgumentNullException(nameof(ruler));
            _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        }

        public void Execute()
        {
            _manager.RemoveRuler(_ruler);
            _renderer.Remove(_ruler.Id);
        }

        public void Undo()
        {
            _renderer.BuildOrRebuild(_ruler); // rebuild GPU geometry before adding to collection
            _manager.AddRuler(_ruler);
        }
    }
}
