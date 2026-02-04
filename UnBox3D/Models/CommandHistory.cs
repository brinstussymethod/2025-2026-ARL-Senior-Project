using UnBox3D.Commands;

namespace UnBox3D.Models
{
    public interface ICommandHistory
    {
        void PushCommand(ICommand command);
        ICommand? PopCommand();
    }

    public class CommandHistory : ICommandHistory
    {
        private Stack<ICommand> _history = new Stack<ICommand>();

        // Last in...
        public void PushCommand(ICommand command)
        {
            _history.Push(command);
        }

        // ...first out
        public ICommand? PopCommand()
        {
            // If the list is not empty, get the most recent command from the history.
            return _history.Count > 0 ? _history.Pop() : null;
        }
    }
}
