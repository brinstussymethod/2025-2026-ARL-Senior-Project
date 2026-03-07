using Moq;
using System.IO;
using UnBox3D.Commands;
using UnBox3D.Models;
using Xunit;

namespace UnBox3D.Tests.Models
{
    internal class DudCommand : ICommand
    {
        private readonly string _name;

        public DudCommand() { _name = "Dud"; }

        public void Execute() {}

        public void Undo() {}

        public override string ToString()
        {
            return _name;
        }
    }
    public class CommandHistoryTests
    {
        public CommandHistoryTests() {}

        [Fact]
        public void CommandHistory_ShouldGiveNull_WhenEmpty()
        {
            var history = new CommandHistory();

            var emptyHistoryResult = history.PopCommand();

            Assert.Null(emptyHistoryResult);
        }

        [Fact]
        public void CommandHistory_ShouldGiveCommand_WhenNotEmpty()
        {
            var history = new CommandHistory();
            var dudCommand = new DudCommand();
            history.PushCommand(dudCommand);

            var commandResult = history.PopCommand();

            Assert.NotNull(commandResult);
            Assert.Equal(commandResult.ToString(), dudCommand.ToString());
        }
    }
}
