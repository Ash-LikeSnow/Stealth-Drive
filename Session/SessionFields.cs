using Sandbox.ModAPI;
using System;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;
using VRage.Utils;
using VRage.Game.Entity;
using System.Collections.Generic;
using VRage.Collections;
using Jakaria.API;

namespace StealthSystem
{
    public partial class StealthSession
    {
        internal const string STATUS_EMISSIVE = "Emissive";
        internal const string RADIANT_EMISSIVE = "Emissive0";

        internal const int FADE_INTERVAL = 5;

        internal readonly HashSet<string> STEALTH_BLOCKS = new HashSet<string>()
        {"StealthDrive", "StealthDriveSmall"};
        internal readonly HashSet<string> HEAT_BLOCKS = new HashSet<string>()
        {"StealthHeatSink", "StealthHeatSinkSmall"};

        internal readonly HashSet<string> SHIELD_BLOCKS = new HashSet<string>()
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

        internal string ModPath;
        internal readonly Guid CompDataGuid = new Guid("75BBB4F5-4FB9-4230-AAAA-BB79C9811507");
        internal static readonly MyStringId _square = MyStringId.GetOrCompute("Square");

        internal BoundingBoxD LargeBox;
        internal BoundingBoxD SmallBox;

        internal EntityFlags StealthFlag;

        internal int BaseDuration;
        internal int SinkDuration;
        internal int ShieldDelay;
        internal int JumpPenalty;
        internal int FadeTime;
        internal int FadeSteps;
        internal int DamageThreshold;
        internal float PowerScale;
        internal float SignalRangeScale;
        internal float SinkPower;
        internal float Transparency;
        internal bool DoDamage;
        internal bool DisableShields;
        internal bool DisableWeapons;

        internal readonly Dictionary<long, DriveComp> DriveMap = new Dictionary<long, DriveComp>();
        internal readonly Dictionary<IMyCubeGrid, GridComp> GridMap = new Dictionary<IMyCubeGrid, GridComp>();
        internal readonly Dictionary<IMyGridGroupData, GroupMap> GridGroupMap = new Dictionary<IMyGridGroupData, GroupMap>();
        internal readonly List<GridComp> GridList = new List<GridComp>();
        internal readonly HashSet<IMyCubeGrid> StealthedGrids = new HashSet<IMyCubeGrid>();

        internal Settings ConfigSettings;
        internal APIBackend API;
        internal APIServer APIServer;
        internal readonly WaterModAPI WaterAPI = new WaterModAPI();

        internal object InitObj = new object();
        internal bool Inited;
        internal bool PbApiInited;

        private readonly List<MyEntity> _entities = new List<MyEntity>();
        private readonly ConcurrentCachingList<IMyUpgradeModule> _startBlocks = new ConcurrentCachingList<IMyUpgradeModule>();
        private readonly ConcurrentCachingList<IMyCubeGrid> _startGrids = new ConcurrentCachingList<IMyCubeGrid>();
        private readonly Stack<GroupMap> _groupMapPool = new Stack<GroupMap>(64);
        private readonly Stack<GridComp> _gridCompPool = new Stack<GridComp>(128);

        private readonly Vector3D _large = new Vector3D(1.125, 6.25, 3.5);
        private readonly Vector3D _small = new Vector3D(1.125, 6.25, 1.125);

        public StealthSession()
        {
            API = new APIBackend(this);
            APIServer = new APIServer(this);
        }

        private void Clean()
        {
            STEALTH_BLOCKS.Clear();
            HEAT_BLOCKS.Clear();
            SHIELD_BLOCKS.Clear();

            DriveMap.Clear();
            GridMap.Clear();
            GridGroupMap.Clear();
            GridList.Clear();
            StealthedGrids.Clear();

            _entities.Clear();
            _startBlocks.ClearImmediate();
            _startGrids.ClearImmediate();
            _groupMapPool.Clear();
            _gridCompPool.Clear();

            _customControls.Clear();
            _customActions.Clear();
        }
    }
}
