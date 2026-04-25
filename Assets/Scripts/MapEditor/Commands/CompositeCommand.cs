using MapEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapEditor
{
    class CompositeCommand : BaseCommand
    {
        private List<BaseCommand> _commands;

        public CompositeCommand(List<BaseCommand> commands)
        {
            _commands = commands ?? new List<BaseCommand>();
        }

        public override void Execute()
        {
            foreach (var command in _commands)
                command.Execute();
        }

        public override void Unexecute()
        {
            for (int i = _commands.Count - 1; i >= 0; i--)
                _commands[i].Unexecute();
        }
    }
}
