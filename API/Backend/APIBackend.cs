﻿using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using System;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;
using VRage.Utils;
using Sandbox.Game.Entities;
using ObjectBuilders.SafeZone;
using SpaceEngineers.Game.ModAPI;
using VRage.Game.Entity;
using Sandbox.Game.Entities.Cube;
using System.Collections.Generic;
using Sandbox.Game.EntityComponents;
using VRage.Collections;
using Sandbox.ModAPI.Interfaces.Terminal;
using Sandbox.Game.Entities.Blocks;

namespace StealthSystem
{
    internal class APIBackend
    {
        internal readonly Dictionary<string, Delegate> ModApiMethods;
        internal Dictionary<string, Delegate> PbApiMethods;

        private readonly StealthSession _session;

        internal APIBackend(StealthSession session)
        {
            _session = session;

            ModApiMethods = new Dictionary<string, Delegate>
            {
                ["ToggleStealth"] = new Func<IMyTerminalBlock, bool, bool>(ToggleStealth),
                ["GetStatus"] = new Func<IMyTerminalBlock, uint>(GetStatus),
            };
        }


        internal void PbInit()
        {
            PbApiMethods = new Dictionary<string, Delegate>
            {
                ["ToggleStealth"] = new Func<IMyTerminalBlock, bool>(ToggleStealthPB),
                ["GetStatus"] = new Func<IMyTerminalBlock, uint>(GetStatus),
            };
            var pb = MyAPIGateway.TerminalControls.CreateProperty<IReadOnlyDictionary<string, Delegate>, Sandbox.ModAPI.IMyTerminalBlock>("StealthPbAPI");
            pb.Getter = b => PbApiMethods;
            MyAPIGateway.TerminalControls.AddControl<IMyProgrammableBlock>(pb);
            _session.PbApiInited = true;
        }

        private bool ToggleStealth(IMyTerminalBlock block, bool force)
        {
            DriveComp comp;
            if (!_session.DriveMap.TryGetValue(block.EntityId, out comp))
                return false;

            return comp.ToggleStealth(force);
        }

        private bool ToggleStealthPB(IMyTerminalBlock block)
        {
            return ToggleStealth(block, false);
        }

        private uint GetStatus(IMyTerminalBlock block)
        {
            DriveComp comp;
            if (!_session.DriveMap.TryGetValue(block.EntityId, out comp))
                return 3u;

            var status = !comp.Online ? 4u : !comp.SufficientPower ? 3u : comp.CoolingDown ? 2u : comp.StealthActive ? 1u : 0u;
            return status;
        }

    }
}
