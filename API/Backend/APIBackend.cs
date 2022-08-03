using Sandbox.Common.ObjectBuilders;
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
                ["GetStatus"] = new Func<IMyTerminalBlock, int>(GetStatus),
                ["GetDuration"] = new Func<IMyTerminalBlock, int>(GetDuration),
            };
        }


        internal void PbInit()
        {
            PbApiMethods = new Dictionary<string, Delegate>
            {
                ["ToggleStealth"] = new Func<IMyTerminalBlock, bool>(ToggleStealthPB),
                ["GetStatus"] = new Func<IMyTerminalBlock, int>(GetStatus),
                ["GetDuration"] = new Func<IMyTerminalBlock, int>(GetDuration),
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

        private int GetStatus(IMyTerminalBlock block)
        {
            DriveComp comp;
            if (!_session.DriveMap.TryGetValue(block.EntityId, out comp))
                return 4;

            var status = !comp.Online ? 4 : !comp.SufficientPower ? 3 : comp.CoolingDown ? 2 : comp.StealthActive ? 1 : 0;
            return status;
        }

        private int GetDuration(IMyTerminalBlock block)
        {
            DriveComp comp;
            if (!_session.DriveMap.TryGetValue(block.EntityId, out comp))
                return 0;

            var duration = comp.StealthActive ? comp.TotalTime - comp.TimeElapsed : comp.CoolingDown ? comp.TimeElapsed : comp.MaxDuration;
            return duration;

        }

    }
}
