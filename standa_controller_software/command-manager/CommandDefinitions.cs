﻿using standa_controller_software.command_manager.command_parameter_library;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.command_manager
{
    public enum CommandDefinitions
    {
        // global
        Initialize,
        UpdateState,
        ConnectDevice,
        Stop,
        UpdateDeviceProperty,
        // queue
        AwaitQueuedItems,
        // positioners
        MoveAbsolute,
        UpdateMoveSettings,
        AddSyncInAction,
        WaitForStop,
        Home,
        // shutters
        ChangeShutterStateOnInterval,
        ChangeShutterState,
        // sync
        StartQueueExecution,
        AddSyncControllerBufferItem,
        GetBufferCount,

    }
}
