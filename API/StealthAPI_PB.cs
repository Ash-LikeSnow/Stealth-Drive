using System;
using System.Collections.Generic;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using VRage;
using VRage.Game;
using VRageMath;

namespace StealthSystem
{
    internal class StealthPbAPI
    {
        /// Returns true if drive status was toggled successfully.
        public bool ToggleStealth(Sandbox.ModAPI.Ingame.IMyTerminalBlock drive) => _toggleStealth?.Invoke(drive) ?? false;

        /// Returns status of drive. 0 = Ready, 1 = Active, 2 = Cooldown, 3 = Not enough power, 4 = Offline
        public uint GetStatus(Sandbox.ModAPI.Ingame.IMyTerminalBlock drive) => _getStatus?.Invoke(drive) ?? 4u;




        private Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, bool> _toggleStealth;
        private Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, uint> _getStatus;

        public bool Activate(Sandbox.ModAPI.Ingame.IMyTerminalBlock pbBlock)
        {
            var dict = pbBlock.GetProperty("StealthPbAPI")?.As<IReadOnlyDictionary<string, Delegate>>().GetValue(pbBlock);
            if (dict == null) throw new Exception("StealthPbAPI failed to activate");
            return ApiAssign(dict);
        }

        public bool ApiAssign(IReadOnlyDictionary<string, Delegate> delegates)
        {
            if (delegates == null)
                return false;

            AssignMethod(delegates, "ToggleStealth", ref _toggleStealth);
            AssignMethod(delegates, "GetStatus", ref _getStatus);
            return true;
        }

        private void AssignMethod<T>(IReadOnlyDictionary<string, Delegate> delegates, string name, ref T field) where T : class
        {
            if (delegates == null)
            {
                field = null;
                return;
            }

            Delegate del;
            if (!delegates.TryGetValue(name, out del))
                throw new Exception($"{GetType().Name} :: Couldn't find {name} delegate of type {typeof(T)}");

            field = del as T;
            if (field == null)
                throw new Exception(
                    $"{GetType().Name} :: Delegate {name} is not type {typeof(T)}, instead it's: {del.GetType()}");
        }

    }
    
}
