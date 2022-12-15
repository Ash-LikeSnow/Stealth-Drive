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
using Sandbox.ModAPI.Interfaces.Terminal;
using SpaceEngineers.Game.Entities.Blocks;
using System.Text;

namespace StealthSystem
{
    public partial class StealthSession
    {
        private static List<IMyTerminalControl> _customControls = new List<IMyTerminalControl>();
        private static List<IMyTerminalAction> _customActions = new List<IMyTerminalAction>();

        internal IMyTerminalBlock LastTerminal;

        private void CustomControlGetter(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            if (block is IMyUpgradeModule && STEALTH_BLOCKS.Contains(block.BlockDefinition.SubtypeName))
            {
                foreach (var control in _customControls)
                    controls.Add(control);
            }

            LastTerminal = block;
        }

        private void CustomActionGetter(IMyTerminalBlock block, List<IMyTerminalAction> actions)
        {
            if (block is IMyUpgradeModule && STEALTH_BLOCKS.Contains(block.BlockDefinition.SubtypeName))
            {
                foreach (var action in _customActions)
                    actions.Add(action);
            }
        }

        internal void CreateTerminalControls<T>() where T : IMyUpgradeModule
        {
            _customControls.Add(Separator<T>());
            _customControls.Add(CreateEnterStealth<T>());
            _customControls.Add(CreateExitStealth<T>());

            _customActions.Add(CreateEnterAction<T>());
            _customActions.Add(CreateExitAction<T>());
            _customActions.Add(CreateSwitchAction<T>());
        }

        internal IMyTerminalControlSeparator Separator<T>() where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, T>("Stealth_Separator");

            c.Enabled = IsTrue;
            c.Visible = IsTrue;

            return c;
        }

        internal IMyTerminalControlButton CreateEnterStealth<T>() where T : IMyUpgradeModule
        {
            var control = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, T>($"Stealth_Enter");

            control.Title = MyStringId.GetOrCompute("Enter Stealth");
            control.Tooltip = MyStringId.GetOrCompute("Engage Stealth Drive to become virtually undetectable.");
            control.Action = EnterStealth;
            control.Visible = IsTrue;
            control.Enabled = CanEnterStealth;

            return control;
        }

        internal IMyTerminalControlButton CreateExitStealth<T>() where T : IMyUpgradeModule
        {
            var control = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, T>($"Stealth_Exit");

            control.Title = MyStringId.GetOrCompute("Leave Stealth");
            control.Tooltip = MyStringId.GetOrCompute("Disengage Stealth Drive.");
            control.Action = ExitStealth;
            control.Visible = IsTrue;
            control.Enabled = CanExitStealth;

            return control;
        }

        internal IMyTerminalAction CreateEnterAction<T>() where T : IMyUpgradeModule
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>("Stealth_Enter_Action");
            action.Icon = ModPath + @"\Textures\GUI\Icons\Actions\StealthSwitchOn.dds";
            action.Name = new StringBuilder("Enter Stealth");
            action.Action = EnterStealth;
            action.Writer = EnterStealthWriter;
            action.Enabled = IsTrue;

            return action;
        }

        internal IMyTerminalAction CreateExitAction<T>() where T : IMyUpgradeModule
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>("Stealth_Exit_Action");
            action.Icon = ModPath + @"\Textures\GUI\Icons\Actions\StealthSwitchOff.dds";
            action.Name = new StringBuilder("Leave Stealth");
            action.Action = ExitStealth;
            action.Writer = ExitStealthWriter;
            action.Enabled = IsTrue;

            return action;
        }

        internal IMyTerminalAction CreateSwitchAction<T>() where T : IMyUpgradeModule
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>("Stealth_Switch_Action");
            action.Icon = ModPath + @"\Textures\GUI\Icons\Actions\StealthSwitchToggle.dds";
            action.Name = new StringBuilder("Switch Stealth");
            action.Action = SwitchStealth;
            action.Writer = SwitchStealthWriter;
            action.Enabled = IsTrue;

            return action;
        }

        internal bool IsTrue(IMyTerminalBlock block)
        {
            return true;
        }

        internal bool CanEnterStealth(IMyTerminalBlock block)
        {
            DriveComp comp;
            if (!DriveMap.TryGetValue(block.EntityId, out comp))
            {
                Logs.WriteLine("CanEnterStealth() - Comp not found!");
                return false;
            }

            return comp.Online && comp.SufficientPower && !comp.CoolingDown && !comp.StealthActive;
        }

        internal bool CanExitStealth(IMyTerminalBlock block)
        {
            DriveComp comp;
            if (!DriveMap.TryGetValue(block.EntityId, out comp))
            {
                Logs.WriteLine("CanExitStealth() - Comp not found!");
                return false;
            }

            return comp.Online && comp.StealthActive;
        }

        internal void EnterStealthWriter(IMyTerminalBlock block, StringBuilder builder)
        {
            DriveComp comp;
            if (!DriveMap.TryGetValue(block.EntityId, out comp))
            {
                Logs.WriteLine("EnterStealthWriter() - Comp not found!");
                return;
            }

            if (comp.StealthActive)
                builder.Append("Cloaked");
            else
                builder.Append("Cloak");
        }

        internal void ExitStealthWriter(IMyTerminalBlock block, StringBuilder builder)
        {
            DriveComp comp;
            if (!DriveMap.TryGetValue(block.EntityId, out comp))
            {
                Logs.WriteLine("ExitStealthWriter() - Comp not found!");
                return;
            }

            if (comp.StealthActive)
                builder.Append("Uncloak");
            else
                builder.Append("Uncloaked");
        }

        internal void SwitchStealthWriter(IMyTerminalBlock block, StringBuilder builder)
        {
            DriveComp comp;
            if (!DriveMap.TryGetValue(block.EntityId, out comp))
            {
                Logs.WriteLine("ExitStealthWriter() - Comp not found!");
                return;
            }

            if (comp.StealthActive)
                builder.Append("Uncloak");
            else
                builder.Append("Cloak");
        }

        internal void EnterStealth(IMyTerminalBlock block)
        {
            DriveComp comp;
            if (!DriveMap.TryGetValue(block.EntityId, out comp))
            {
                Logs.WriteLine("EnterStealth() - Comp not found!");
                return;
            }

            if (!comp.Online || !comp.SufficientPower || comp.CoolingDown || comp.StealthActive)
                return;

            comp.EnterStealth = true;

            foreach (var control in _customControls)
                control.UpdateVisual();
        }

        internal void ExitStealth(IMyTerminalBlock block)
        {
            DriveComp comp;
            if (!DriveMap.TryGetValue(block.EntityId, out comp))
            {
                Logs.WriteLine("ExitStealth() - Comp not found!");
                return;
            }

            if (!comp.Online || !comp.StealthActive) return;

            comp.ExitStealth = true;

            foreach (var control in _customControls)
                control.UpdateVisual();
        }

        internal void SwitchStealth(IMyTerminalBlock block)
        {
            DriveComp comp;
            if (!DriveMap.TryGetValue(block.EntityId, out comp))
            {
                Logs.WriteLine("SwitchStealth() - Comp not found!");
                return;
            }

            comp.ToggleStealth();

            foreach (var control in _customControls)
                control.UpdateVisual();
        }

    }
}
