using UnBox3D.Commands;

namespace UnBox3D.Models
{
    public interface ICommandHistory
    {
        /// <summary>Fires whenever the undo or redo stack changes.</summary>
        event EventHandler? HistoryChanged;

        bool CanUndo { get; }
        bool CanRedo { get; }

        // Undo stack
        void   PushCommand(ICommand command);
        ICommand? PopCommand();

        // Redo stack
        void   PushRedoCommand(ICommand command);
        ICommand? PopRedoCommand();
        void   ClearRedo();
    }

    public class CommandHistory : ICommandHistory
    {
        private readonly Stack<ICommand> _history   = new();
        private readonly Stack<ICommand> _redoStack = new();

        public event EventHandler? HistoryChanged;

        public bool CanUndo => _history.Count   > 0;
        public bool CanRedo => _redoStack.Count > 0;

        // Pushing a brand-new action clears the redo branch automatically.
        public void PushCommand(ICommand command)
        {
            _redoStack.Clear();   // new action invalidates the redo chain
            _history.Push(command);
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        public ICommand? PopCommand()
        {
            var cmd = _history.Count > 0 ? _history.Pop() : null;
            HistoryChanged?.Invoke(this, EventArgs.Empty);
            return cmd;
        }

        public void PushRedoCommand(ICommand command)
        {
            _redoStack.Push(command);
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        public ICommand? PopRedoCommand()
        {
            var cmd = _redoStack.Count > 0 ? _redoStack.Pop() : null;
            HistoryChanged?.Invoke(this, EventArgs.Empty);
            return cmd;
        }

        public void ClearRedo()
        {
            _redoStack.Clear();
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
