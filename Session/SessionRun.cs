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
using VRage.Collections;
using Sandbox.Game;
using VRage.Input;

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
        internal static bool IsServer;
        internal static bool IsClient;
        internal static bool IsDedicated;
        internal static bool WcActive;

        public override void LoadData()
        {
            IsServer = MyAPIGateway.Multiplayer.MultiplayerActive && MyAPIGateway.Session.IsServer;
            IsClient = MyAPIGateway.Multiplayer.MultiplayerActive && !MyAPIGateway.Session.IsServer;
            IsDedicated = MyAPIGateway.Utilities.IsDedicated;

            DriveMap = new Dictionary<long, DriveComp>();
            GridMap = new Dictionary<IMyCubeGrid, GridComp>();
            GridGroupMap = new Dictionary<IMyGridGroupData, GroupMap>();
            GridList = new List<GridComp>();
            StealthedGrids = new HashSet<IMyCubeGrid>();
            LargeBox = new BoundingBoxD(-_large, _large);
            SmallBox = new BoundingBoxD(-_small, _small);

            _entities = new List<MyEntity>();
            _startBlocks = new ConcurrentCachingList<IMyUpgradeModule>();
            _startGrids = new ConcurrentCachingList<IMyCubeGrid>();
            Logs.InitLogs();

            ModPath = ModContext.ModPath;
            WcActive = ModCheck();

            RemoveEdges();
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
                MyVisualScriptLogicProvider.PlayerConnected += PlayerConnected;
            }

            if (!IsClient)
                MyAPIGateway.Session.DamageSystem.RegisterAfterDamageHandler(0, AfterDamageApplied);

            ConfigSettings = new Settings();

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
        }

        protected override void UnloadData()
        {
            if (IsClient)
                MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(ClientPacketId, ProcessPacket);
            else if (IsServer)
            {
                MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(ServerPacketId, ProcessPacket);
                MyVisualScriptLogicProvider.PlayerConnected -= PlayerConnected;
            }

            MyEntities.OnEntityCreate -= OnEntityCreate;
            //MyEntities.OnEntityDelete -= OnEntityDelete;
            MyEntities.OnCloseAll -= OnCloseAll;
            MyAPIGateway.TerminalControls.CustomControlGetter -= CustomControlGetter;
            MyAPIGateway.TerminalControls.CustomActionGetter -= CustomActionGetter;

            Logs.Close();
            APIServer.Unload();

            _groupMapPool.Clear();
            _gridCompPool.Clear();
            _customControls = null;
            _customActions = null;
            DriveMap = null;
            GridMap = null;
        }

    }
}
