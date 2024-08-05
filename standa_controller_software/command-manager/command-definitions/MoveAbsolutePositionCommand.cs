using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.command_manager.command_definitions
{
    public class MoveAbsolutePositionCommand
    {
        private CommandManager _commandManager;

        public MoveAbsolutePositionCommand(CommandManager commandManager)
        {
            _commandManager = commandManager;
        }

        public void EnqueueCommand()
        {
            var commandLine = CreateCommandLine();
            _commandManager.EnqueueCommands(commandLine);

        }
        public Command[] CreateCommandLine()
        {
            return [new Command(), new Command()];
        }

        public void ExecuteCommand()
        {
            var commandLine = CreateCommandLine();
            Task.Run(() => _commandManager.ExecuteSingleCommandLine(commandLine));
        }

    }
}
