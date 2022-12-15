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

namespace StealthSystem
{
    public partial class StealthSession
    {
        internal const string STATUS_EMISSIVE = "Emissive";
        internal const string RADIANT_EMISSIVE = "Emissive0";

        internal const int FADE_INTERVAL = 5;

        internal static readonly HashSet<string> STEALTH_BLOCKS = new HashSet<string>()
        {"StealthDrive", "StealthDriveSmall"};
        internal static readonly HashSet<string> HEAT_BLOCKS = new HashSet<string>()
        {"StealthHeatSink", "StealthHeatSinkSmall"};

        internal static readonly HashSet<string> SHIELD_BLOCKS = new HashSet<string>()
        {
            "EmitterL",
            "EmitterS",
            "EmitterST",
            "EmitterLA",
            "EmitterSA",
            "LargeShipSmallShieldGeneratorBase",
            "LargeShipLargeShieldGeneratorBase",
            "SmallShipSmallShieldGeneratorBase",
            "SmallShipMicroShieldGeneratorBase",
            "LargeGridLargeShield",
            "LargeGridSmallShield",
            "SmallGridLargeShield",
            "SmallGridSmallShield",
        };

        internal static readonly Guid CompDataGuid = new Guid("75BBB4F5-4FB9-4230-AAAA-BB79C9811507");
        internal static readonly MyStringId _square = MyStringId.GetOrCompute("Square");
        internal static string ModPath;

        internal static BoundingBoxD LargeBox;
        internal static BoundingBoxD SmallBox;

        internal static EntityFlags StealthFlag;

        internal static int BaseDuration;
        internal static int SinkDuration;
        internal static int ShieldDelay;
        internal static int JumpPenalty;
        internal static int FadeTime;
        internal static int FadeSteps;
        internal static int DamageThreshold;
        internal static float PowerScale;
        internal static float SignalRangeScale;
        internal static float SinkPower;
        internal static float Transparency;
        internal static bool DoDamage;
        internal static bool DisableShields;
        internal static bool DisableWeapons;

        internal readonly Dictionary<long, DriveComp> DriveMap = new Dictionary<long, DriveComp>();
        internal readonly Dictionary<IMyCubeGrid, GridComp> GridMap = new Dictionary<IMyCubeGrid, GridComp>();
        internal static Dictionary<IMyGridGroupData, GroupMap> GridGroupMap;
        internal static List<GridComp> GridList;
        internal static HashSet<IMyCubeGrid> StealthedGrids;

        internal Settings ConfigSettings;
        internal APIBackend API;
        internal APIServer APIServer;

        internal object InitObj = new object();
        internal bool Inited;
        internal bool PbApiInited;

        private List<MyEntity> _entities;
        private ConcurrentCachingList<IMyUpgradeModule> _startBlocks;
        private ConcurrentCachingList<IMyCubeGrid> _startGrids;
        private readonly Stack<GroupMap> _groupMapPool = new Stack<GroupMap>(64);
        private readonly Stack<GridComp> _gridCompPool = new Stack<GridComp>(128);

        private readonly Vector3D _large = new Vector3D(1.125, 6.25, 3.5);
        private readonly Vector3D _small = new Vector3D(1.125, 6.25, 1.125);

        public StealthSession()
        {
            API = new APIBackend(this);
            APIServer = new APIServer(this);
        }
    }
}
