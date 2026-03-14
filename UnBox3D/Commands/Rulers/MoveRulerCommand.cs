using OpenTK.Mathematics;
using UnBox3D.Rendering.Rulers;

namespace UnBox3D.Commands.Rulers
{
    /// <summary>Undo/redo: move a ruler's base position along the ground plane.</summary>
    public class MoveRulerCommand : ICommand
    {
        private readonly RulerObject _ruler;
        private readonly Vector3     _oldBase;
        private readonly Vector3     _newBase;

        public MoveRulerCommand(RulerObject ruler, Vector3 oldBase, Vector3 newBase)
        {
            _ruler   = ruler   ?? throw new ArgumentNullException(nameof(ruler));
            _oldBase = oldBase;
            _newBase = newBase;
        }

        public void Execute() => _ruler.BasePosition = _newBase;
        public void Undo()    => _ruler.BasePosition = _oldBase;
    }
}
