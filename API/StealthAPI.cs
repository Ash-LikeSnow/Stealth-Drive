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
    internal class StealthAPI
    {
        /// Returns true if drive status was toggled successfully.
        /// <param name="force">Ignore power requirements and overheat.</param>
        public bool ToggleStealth(IMyTerminalBlock drive, bool force) => _toggleStealth?.Invoke(drive, force) ?? false;


        /// Returns status of drive. 0 = Ready, 1 = Active, 2 = Cooldown, 3 = Not enough power, 4 = Offline
        public uint GetStatus(IMyTerminalBlock drive) => _getStatus?.Invoke(drive) ?? 4u;



        private const long CHANNEL = 2172757427;
        private bool _isRegistered;
        private bool _apiInit;
        private Action _readyCallback;
        private Func<IMyTerminalBlock, bool, bool> _toggleStealth;
        private Func<IMyTerminalBlock, uint> _getStatus;

        public bool IsReady { get; private set; }


        /// <summary>
        /// Ask CoreSystems to send the API methods.
        /// <para>Throws an exception if it gets called more than once per session without <see cref="Unload"/>.</para>
        /// </summary>
        /// <param name="readyCallback">Method to be called when CoreSystems replies.</param>
        public void Load(Action readyCallback = null)
        {
            if (_isRegistered)
                throw new Exception($"{GetType().Name}.Load() should not be called multiple times!");

            _readyCallback = readyCallback;
            _isRegistered = true;
            MyAPIGateway.Utilities.RegisterMessageHandler(CHANNEL, HandleMessage);
            MyAPIGateway.Utilities.SendModMessage(CHANNEL, "ApiEndpointRequest");
        }

        public void Unload()
        {
            MyAPIGateway.Utilities.UnregisterMessageHandler(CHANNEL, HandleMessage);

            ApiAssign(null);

            _isRegistered = false;
            _apiInit = false;
            IsReady = false;
        }

        private void HandleMessage(object obj)
        {
            if (_apiInit || obj is string
            ) // the sent "ApiEndpointRequest" will also be received here, explicitly ignoring that
                return;

            var dict = obj as IReadOnlyDictionary<string, Delegate>;

            if (dict == null)
                return;

            ApiAssign(dict);

            IsReady = true;
            _readyCallback?.Invoke();
        }

        public void ApiAssign(IReadOnlyDictionary<string, Delegate> delegates)
        {
            _apiInit = (delegates != null);
            /// base methods
            AssignMethod(delegates, "ToggleStealth", ref _toggleStealth);
        }

        private void AssignMethod<T>(IReadOnlyDictionary<string, Delegate> delegates, string name, ref T field)
            where T : class
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
