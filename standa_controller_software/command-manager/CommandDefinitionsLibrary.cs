using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.command_manager
{
    public enum CommandDefinitionsLibrary
    {
        MoveAbsolute,
        ChangeShutterState,
        WaitUntilStop,
        UpdateMoveSettings,
        ChangeShutterStateOnInterval
    }
}
