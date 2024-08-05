using standa_controller_software.command_manager;
using standa_controller_software.command_manager.command_definitions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using text_parser_library;

namespace standa_controller_software
{
    internal class MoveAbsolutePositionFunction : CustomFunction
    {
        public string Message { get; set; } = "";
        private CommandManager _commandManager;

        public override object? Execute(params object[] args)
        {
            _commandManager.EnqueueCommands([
                new Command() {
                Action = "MovementParameters",
                Await = true,
                // Acceleration | Decceleration | Speed
                Parameters = [1000,2000,100],
                TargetController = "FirstController",
                TargetDevice = "X"
                },
                new Command() {
                Action = "MovementParameters",
                Await = true,
                // Acceleration | Decceleration | Speed
                Parameters = [1000,2000,100],
                TargetController = "FirstController",
                TargetDevice = "Y"
                },
                new Command() {
                Action = "MoveA",
                Await = true,
                Parameters = [30],
                TargetController = "FirstController",
                TargetDevice = "X"
                }
            ]) ;

            return null;
        }
    }
}
