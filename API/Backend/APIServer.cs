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
    internal class APIServer
    {
        private const long CHANNEL = 2172757427;

        private readonly StealthSession _session;

        internal APIServer(StealthSession session)
        {
            _session = session;
        }

        /// <summary>
        /// Is the API ready to be serve
        /// </summary>
        public bool IsReady { get; private set; }

        private void HandleMessage(object o)
        {
            if ((o as string) == "ApiEndpointRequest")
                MyAPIGateway.Utilities.SendModMessage(CHANNEL, _session.API.ModApiMethods);
        }

        private bool _isRegistered;

        /// <summary>
        /// Prepares the client to receive API endpoints and requests an update.
        /// </summary>
        public void Load()
        {
            if (!_isRegistered)
            {
                _isRegistered = true;
                MyAPIGateway.Utilities.RegisterMessageHandler(CHANNEL, HandleMessage);
            }
            IsReady = true;
            try
            {
                MyAPIGateway.Utilities.SendModMessage(CHANNEL, _session.API.ModApiMethods);

            }
            catch (Exception ex) { Logs.WriteLine($"Exception in APIServer.Load() - {ex}"); }
        }


        /// <summary>
        /// Unloads all API endpoints and detaches events.
        /// </summary>
        public void Unload()
        {
            if (_isRegistered)
            {
                _isRegistered = false;
                MyAPIGateway.Utilities.UnregisterMessageHandler(CHANNEL, HandleMessage);
            }
            IsReady = false;
            MyAPIGateway.Utilities.SendModMessage(CHANNEL, new Dictionary<string, Delegate>());
        }
    }
}
