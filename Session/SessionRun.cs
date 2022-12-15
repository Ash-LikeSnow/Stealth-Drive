using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRageMath;
using Sandbox.Game.Entities;
using VRage.Game.Entity;
using System.Collections.Generic;
using VRage.Collections;
using Sandbox.Game;

namespace StealthSystem
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public partial class StealthSession : MySessionComponentBase
    {
        internal static int Tick;
        internal int TickMod15;
        internal int TickMod20;
        internal int TickMod60;
        internal bool Tick10;
        internal bool Tick20;
        internal bool Tick60;
        internal bool Tick120;
        internal bool Tick600;
        internal bool IsServer;
        internal bool IsClient;
        internal bool IsDedicated;
        internal bool WcActive;

        private bool FirstRun = true;

        public override void LoadData()
        {
            IsServer = MyAPIGateway.Multiplayer.MultiplayerActive && MyAPIGateway.Session.IsServer;
            IsClient = MyAPIGateway.Multiplayer.MultiplayerActive && !MyAPIGateway.Session.IsServer;
            IsDedicated = MyAPIGateway.Utilities.IsDedicated;

            LargeBox = new BoundingBoxD(-_large, _large);
            SmallBox = new BoundingBoxD(-_small, _small);

            Logs.InitLogs();

            ModPath = ModContext.ModPath;
            WcActive = ModCheck();

            //RemoveEdges();
            CreateTerminalControls<IMyUpgradeModule>();

            MyEntities.OnEntityCreate += OnEntityCreate;
            //MyEntities.OnEntityDelete += OnEntityDelete;
            MyEntities.OnCloseAll += OnCloseAll;
            MyAPIGateway.TerminalControls.CustomControlGetter += CustomControlGetter;
            MyAPIGateway.TerminalControls.CustomActionGetter += CustomActionGetter;
        }

        public override void BeforeStart()
        {
            if (IsClient)
                MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(ClientPacketId, ProcessPacket);
            else if (IsServer)
            {
                MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(ServerPacketId, ProcessPacket);
                MyVisualScriptLogicProvider.PlayerRespawnRequest += PlayerConnected;
            }

            if (!IsClient)
                MyAPIGateway.Session.DamageSystem.RegisterAfterDamageHandler(0, AfterDamageApplied);

            ConfigSettings = new Settings(this);

            APIServer.Load();
        }

        public override void UpdateAfterSimulation()
        {
            Tick++;

            TickMod15 = Tick % 15;
            TickMod20 = Tick % 20;
            TickMod60 = Tick % 60;

            Tick10 = Tick % 10 == 0;
            Tick20 = TickMod20 == 0;
            Tick60 = TickMod60 == 0;
            Tick120 = Tick % 120 == 0;
            Tick600 = Tick % 600 == 0;

            CompLoop();

            if (!_startBlocks.IsEmpty || !_startGrids.IsEmpty)
                StartComps();

            //if (MyAPIGateway.Input.IsNewKeyPressed(MyKeys.J))
            //    Logs.CheckGrid();

            if (FirstRun)
            {
                if (IsServer && !IsDedicated)
                    InitPlayers();
                FirstRun = false;
            }
        }

        protected override void UnloadData()
        {
            if (IsClient)
                MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(ClientPacketId, ProcessPacket);
            else if (IsServer)
            {
                MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(ServerPacketId, ProcessPacket);
                MyVisualScriptLogicProvider.PlayerRespawnRequest -= PlayerConnected;
            }

            MyEntities.OnEntityCreate -= OnEntityCreate;
            MyEntities.OnCloseAll -= OnCloseAll;
            MyAPIGateway.TerminalControls.CustomControlGetter -= CustomControlGetter;
            MyAPIGateway.TerminalControls.CustomActionGetter -= CustomActionGetter;

            Logs.Close();
            APIServer.Unload();

            Clean();
        }

    }
}
