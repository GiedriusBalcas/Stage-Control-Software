using standa_controller_software.command_manager.command_parameter_library;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.command_manager
{
    public enum CommandDefinitions
    {
        MoveAbsolute,
        ChangeShutterState,
        WaitUntilStop,
        UpdateMoveSettings,
        ChangeShutterStateOnInterval,
        WaitUntilStopPolar,
        AddSyncInAction,
        OnSyncIn
    }

    public static class CommandLibrary
    {
        private static readonly Dictionary<CommandDefinitions, Func<object>> parameterMap =
            new Dictionary<CommandDefinitions, Func<object>>
            {
                {
                    CommandDefinitions.MoveAbsolute, () => new MoveAbsoluteParameters()
                }
            };

        public static object GetDefaultParameters(CommandDefinitions command)
        {
            if (parameterMap.TryGetValue(command, out var getParametersFunc))
            {
                return getParametersFunc();
            }

            throw new ArgumentException("Unsupported command");
        }
    }
}
