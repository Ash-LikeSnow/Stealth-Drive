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
using System.Text;
using Sandbox.ModAPI.Interfaces.Terminal;
using System.Collections.Concurrent;

namespace StealthSystem
{
    public class DriveComp : MyEntityComponentBase
    {
        internal IMyFunctionalBlock Block;
        internal IMyCubeGrid Grid;
        internal MyResourceSinkComponent Sink;
        internal MyResourceDistributorComponent Source;
        internal IMyTerminalControlOnOffSwitch ShowInToolbarSwitch;
        internal IMyGps HeatSignature;

        internal DriveRepo Repo;
        internal GridComp GridComp;

        //internal List<IMyCubeGrid> ConnectedGrids = new List<IMyCubeGrid>();
        internal List<IMySlimBlock> SlimBlocks = new List<IMySlimBlock>();
        internal List<IMyEntity> FadeEntities = new List<IMyEntity>();
        internal List<IMySlimBlock> FadeSlims = new List<IMySlimBlock>();
        internal HashSet<IMyEntity> Children = new HashSet<IMyEntity>();
        internal HashSet<IMyCubeGrid> StealthedExternalGrids = new HashSet<IMyCubeGrid>();
        internal Dictionary<IMyJumpDrive, float> JumpDrives = new Dictionary<IMyJumpDrive, float>();
        internal ConcurrentDictionary<IMyFunctionalBlock, bool> DisabledBlocks = new ConcurrentDictionary<IMyFunctionalBlock, bool>();
        internal List<ulong> ReplicatedClients = new List<ulong>();
        internal HashSet<MyEntity> PreviousEntities = new HashSet<MyEntity>();
        internal HashSet<MyEntity> CurrentEntities = new HashSet<MyEntity>();

        internal List<IMyLargeTurretBase> NearbyTurrets;
        internal MyOrientedBoundingBoxD ExpandedOBB;
        internal Color OldColour;

        internal bool IsPrimary;
        internal bool Inited;
        internal bool Online;
        internal bool CoolingDown;
        internal bool SufficientPower;
        internal bool StealthActive;
        internal bool EnterStealth;
        internal bool ExitStealth;
        internal bool VisibleToClient;
        internal bool Fading;
        internal bool GridUpdated;
        internal bool PowerDirty;
        internal bool TransferFailed;
        internal bool StealthOnInit;
        internal bool CdOnInit;
        internal bool Transfer;
        internal bool ShieldWaiting;
        internal bool IgnorePower;

        internal int Fade;
        internal int ShieldWait;
        internal int SurfaceArea;
        internal int MaxDuration;
        internal int RemainingDuration;

        internal float RequiredPower;
        internal long CompTick15;
        internal long CompTick60;
        internal long SignalDistance;
        internal long SignalDistanceSquared;

        private readonly StealthSession _session;

        private List<MyEntity> _entities;
        private BoundingSphereD _sphere;
        private readonly Vector3D[] _obbCorners = new Vector3D[8];

        internal DriveComp(IMyFunctionalBlock stealthBlock, StealthSession session)
        {
            _session = session;

            Block = stealthBlock;

            if (!StealthSession.WcActive)
            {
                NearbyTurrets = new List<IMyLargeTurretBase>();
                _entities = new List<MyEntity>();
                _sphere = new BoundingSphereD(Vector3D.Zero, 1200);
            }
        }

        public override void OnAddedToContainer()
        {
            base.OnAddedToContainer();
        }

        public override void OnAddedToScene()
        {
            base.OnAddedToScene();

            if (!MyAPIGateway.Session.IsServer) StealthSession.SendPacketToServer(new ReplicationPacket { EntityId = Block.EntityId, Fresh = true, Type = PacketType.Replicate });
        }

        public override void OnBeforeRemovedFromContainer()
        {
            base.OnBeforeRemovedFromContainer();

            Close();
        }

        public override bool IsSerialized()
        {
            if (Block.Storage == null || Repo == null) return false;

            Repo.Sync(this);

            Block.Storage[StealthSession.CompDataGuid] = Convert.ToBase64String(MyAPIGateway.Utilities.SerializeToBinary(Repo));

            return false;
        }

        internal void Init()
        {
            Grid = Block.CubeGrid;
            if (Grid == null)
            {
                Logs.WriteLine("DriveComp.Init() - Grid null");
                return;
            }

            var gridData = StealthSession.GridMap[Grid];
            if (gridData.StealthComps.Count == 1)
            {
                IsPrimary = true;
                GridComp = gridData;
                gridData.MasterComp = this;
            }

            Block.Components.Add(this);
            CompTick15 = Block.EntityId % 15;
            CompTick60 = Block.EntityId % 60;

            SinkInit();
            StorageInit();

            Inited = true;
            GridUpdated = true;
            VisibleToClient = true;

            Grid.OnGridSplit += GridSplit;
            Grid.OnBlockAdded += BlockAdded;
            Grid.OnBlockRemoved += BlockRemoved;

            Block.EnabledChanged += EnabledChanged;
            Source.SystemChanged += SourceChanged;

            if (!StealthSession.IsDedicated)
            {
                GetShowInToolbarSwitch();
                Block.AppendingCustomInfo += AppendingCustomData;
            }

            if (!MyAPIGateway.Session.IsServer)
                StealthSession.SendPacketToServer(new ReplicationPacket { EntityId = Block.EntityId, Fresh = true, Type = PacketType.Replicate });
        }

        internal void Close()
        {
            if (Transfer) return;

            if (IsPrimary)
                TransferPrimary(true);
            
            _session.DriveMap.Remove(Block.EntityId);

            var gridData = StealthSession.GridMap[Grid];
            gridData.StealthComps.Remove(this);

            Grid.OnGridSplit -= GridSplit;
            Grid.OnBlockAdded -= BlockAdded;
            Grid.OnBlockRemoved -= BlockRemoved;

            Block.EnabledChanged -= EnabledChanged;

            if (Source != null)
                Source.SystemChanged -= SourceChanged;
            else Logs.WriteLine("Source null on close");

            if (StealthActive && !VisibleToClient)
            {
                SwitchStealth(false);

                foreach (var entity in PreviousEntities)
                {
                    if (entity == null)
                    {
                        Logs.WriteLine($"Previous entity null on close");
                        continue;
                    }

                    if (!StealthSession.IsDedicated)
                    {
                        if (entity is IMyCubeGrid)
                            StealthExternalGrid(false, entity as IMyCubeGrid);
                        else
                            entity.Render.Visible = true;
                    }

                    entity.Flags ^= StealthSession.StealthFlag;
                }
            }

            if (HeatSignature != null)
                MyAPIGateway.Session.GPS.RemoveLocalGps(HeatSignature);

            if (!StealthSession.IsDedicated)
                Block.AppendingCustomInfo -= AppendingCustomData;

            if (!MyAPIGateway.Session.IsServer)
                StealthSession.SendPacketToServer(new ReplicationPacket { EntityId = Block.EntityId, Fresh = false, Type = PacketType.Replicate });

            Clean();
        }

        internal void Clean()
        {
            Block = null;
            Grid = null;
            Sink = null;
            Source = null;
            ShowInToolbarSwitch = null;
            HeatSignature = null;

            Repo = null;
            GridComp = null;

            //ConnectedGrids = null;
            SlimBlocks = null;
            FadeEntities = null;
            FadeSlims = null;
            StealthedExternalGrids = null;
            JumpDrives = null;
            DisabledBlocks = null;
            ReplicatedClients = null;
            PreviousEntities = null;
            CurrentEntities = null;
            NearbyTurrets = null;
    }

        internal void GridChange()
        {
            Grid.OnGridSplit -= GridSplit;
            Grid.OnBlockAdded -= BlockAdded;
            Grid.OnBlockRemoved -= BlockRemoved;

            if (StealthActive)
                SwitchStealth(false, true);

            var gridData = StealthSession.GridMap[Grid];
            if (TransferPrimary(true))
            {
                var newPrimary = gridData.MasterComp;
                newPrimary.IsPrimary = true;
                newPrimary.RemainingDuration = StealthActive ? MaxDuration - RemainingDuration : RemainingDuration;
                newPrimary.CoolingDown = RemainingDuration > 0;
            }

            gridData.StealthComps.Remove(this);

            Grid = Block.CubeGrid;

            //if (StealthActive)
            //    Grid.Visible = false;

            var newGridData = StealthSession.GridMap[Block.CubeGrid];
            GridComp = newGridData;
            newGridData.StealthComps.Add(this);
            if (newGridData.MasterComp == null)
            {
                newGridData.MasterComp = this;
                IsPrimary = true;
            }
            else
            {
                IsPrimary = false;
            }

            Grid.OnGridSplit += GridSplit;
            Grid.OnBlockAdded += BlockAdded;
            Grid.OnBlockRemoved += BlockRemoved;

            Source = Grid.ResourceDistributor as MyResourceDistributorComponent;
            CalculatePowerRequirements();
            UpdateStatus(true);

            Transfer = false;
        }

        internal bool TransferPrimary(bool force)
        {
            var gridData = StealthSession.GridMap[Grid];

            if (gridData.StealthComps.Count <= 1)
                return false;

            DriveComp newPrimary = null;
            for (int i = 0; i < gridData.StealthComps.Count; i++)
            {
                var comp = gridData.StealthComps[i];

                if (comp == this || comp.Block.CubeGrid != Grid)
                    continue;

                if (comp.Block.IsFunctional)
                {
                    newPrimary = comp;
                    break;
                }

                if (force && newPrimary == null)
                    newPrimary = comp;
            }

            if (newPrimary == null)
                return false;

            IsPrimary = false;
            newPrimary.IsPrimary = true;
            gridData.MasterComp = newPrimary;
            return true;
        }

        private void SourceChanged()
        {
            PowerDirty = true;
            GridUpdated = true;
        }

        private void EnabledChanged(IMyTerminalBlock block)
        {
            UpdateStatus();
            block.RefreshCustomInfo();
        }

        private void GridSplit(IMyCubeGrid grid1, IMyCubeGrid grid2)
        {
            GridUpdated = true;
        }

        private void BlockAdded(IMySlimBlock slim)
        {
            GridUpdated = true;

            if (StealthActive)
                DitherBlock(true, slim);
        }

        private void BlockRemoved(IMySlimBlock slim)
        {
            GridUpdated = true;

            if (StealthActive)
                DitherBlock(false, slim);
        }

        private void AppendingCustomData(IMyTerminalBlock block, StringBuilder builder)
        {
            var status = !IsPrimary ? "Standby" : !Online ? "Offline" : !SufficientPower ? "Insufficient Power" : CoolingDown ? "Cooling Down" : StealthActive ? "Stealth Engaged" : "Ready";

            builder.Append("Drive Status: ")
                .Append(status)
                .Append("\n");

            if (!IsPrimary) return;

            if (Online)
            {
                if (!StealthActive && !CoolingDown)
                    builder.Append($"Stealth Duration: {MaxDuration / 60}s \n");

                builder.Append($"Surface Area: {SurfaceArea} blocks square \n")
                    .Append($"Required Power: {RequiredPower.ToString("F1")}MW \n")
                    .Append($"Detection Radius: {SignalDistance}m \n");
            }

            if (StealthActive)
            {
                int timeLeft = (RemainingDuration) / 60;
                int seconds = timeLeft % 60;
                int minutes = (timeLeft - seconds) / 60;
                builder.Append("Time Remaining: ")
                    .Append($"{minutes.ToString("00")}:{seconds.ToString("00")} \n");
            }

            if (CoolingDown)
            {
                int timeLeft = (RemainingDuration) / 60;
                int seconds = timeLeft % 60;
                int minutes = (timeLeft - seconds) / 60;
                builder.Append("Time Remaining: ")
                    .Append($"{minutes.ToString("00")}:{seconds.ToString("00")}\n");
            }
        }

        internal void UpdateStatus(bool gridChange = false)
        {
            if (PowerDirty || Source == null)
            {
                Source = Grid.ResourceDistributor as MyResourceDistributorComponent;
                PowerDirty = false;
            }
            var available = Source.MaxAvailableResourceByType(MyResourceDistributorComponent.ElectricityId, (MyCubeGrid)Grid) - Source.TotalRequiredInputByType(MyResourceDistributorComponent.ElectricityId, (MyCubeGrid)Grid);
            SufficientPower = StealthActive ? available >= 0 : available >= RequiredPower;
            Online = Block.IsFunctional && Block.Enabled && available > 0;

            if (!StealthSession.IsDedicated)
                SetEmissiveColor(gridChange);
        }

        internal void SetEmissiveColor(bool force)
        {
            var emissiveColor = !Block.IsFunctional ? Color.Black : !Online ? EmissiveValues.RED : StealthActive ? Color.Cyan : CoolingDown ? Color.OrangeRed : EmissiveValues.GREEN;
            if (!force && emissiveColor == OldColour)
                return;

            OldColour = emissiveColor;
            Block.SetEmissiveParts(StealthSession.STATUS_EMISSIVE, emissiveColor, 1f);
        }

        internal void RefreshTerminal()
        {
            Block.RefreshCustomInfo();

            if (ShowInToolbarSwitch != null)
            {
                var originalSetting = ShowInToolbarSwitch.Getter(Block);
                ShowInToolbarSwitch.Setter(Block, !originalSetting);
                ShowInToolbarSwitch.Setter(Block, originalSetting);
            }
        }

        internal void CalculatePowerRequirements()
        {
            //ConnectedGrids.Clear();
            //MyAPIGateway.GridGroups.GetGroup(Grid, GridLinkTypeEnum.Physical, ConnectedGrids);

            CalculateExpandedOBB();
            var scale = Grid.GridSizeEnum == MyCubeSize.Large ? 6.25 : 0.25;
            var areaMetres = (int)OBBSurfaceArea(ExpandedOBB);
            SurfaceArea = (int)(areaMetres / scale);

            RequiredPower = areaMetres * StealthSession.PowerScale;
            SignalDistance = (int)RequiredPower * StealthSession.SignalRangeScale;
            SignalDistanceSquared = SignalDistance * SignalDistance;

        }

        internal void CalculateExpandedOBB()
        {
            var worldMat = Grid.PositionComp.WorldMatrixRef;
            var halfExtents = (Vector3D)Grid.PositionComp.LocalAABB.HalfExtents;
            var newCentre = Grid.PositionComp.WorldAABB.Center;

            var left = worldMat.Left;
            var up = worldMat.Up;
            var back = worldMat.Backward;

            var grids = GridComp.GroupMap.ConnectedGrids;
            for (int i = 0; i < grids.Count; i++)
            {
                var cGrid = grids[i];
                if (cGrid == Grid) continue;

                var obb = new MyOrientedBoundingBoxD(cGrid.PositionComp.LocalAABB, cGrid.PositionComp.WorldMatrixRef);
                obb.GetCorners(_obbCorners, 0);
                for (int j = 0; j < 8; j++)
                {
                    var point = _obbCorners[j];
                    var offset = point - newCentre;
                    if (offset.LengthSquared() < Math.Pow(halfExtents.Min(), 2))
                        continue;

                    var xDot = Vector3D.Dot(offset, left);
                    var xAbs = Math.Abs(xDot);
                    if (xAbs > halfExtents.X)
                    {
                        var dist = (xAbs - halfExtents.X) / 2;
                        halfExtents.X += dist;
                        newCentre += left * dist * Math.Sign(xDot);
                    }

                    var yDot = Vector3D.Dot(offset, up);
                    var yAbs = Math.Abs(yDot);
                    if (yAbs > halfExtents.Y)
                    {
                        var dist = (yAbs - halfExtents.Y) / 2;
                        halfExtents.Y += dist;
                        newCentre += up * dist * Math.Sign(yDot);
                    }

                    var zDot = Vector3D.Dot(offset, back);
                    var zAbs = Math.Abs(zDot);
                    if (zAbs > halfExtents.Z)
                    {
                        var dist = (zAbs - halfExtents.Z) / 2;
                        halfExtents.Z += dist;
                        newCentre += back * dist * Math.Sign(zDot);
                    }
                }
            }

            var orientation = Quaternion.CreateFromRotationMatrix(worldMat);
            ExpandedOBB = new MyOrientedBoundingBoxD(newCentre, halfExtents, orientation);

        }

        internal double OBBSurfaceArea(MyOrientedBoundingBoxD obb)
        {
            var halfExtent = obb.HalfExtent;

            return 8 * (halfExtent.X * halfExtent.Y + halfExtent.X * halfExtent.Z + halfExtent.Y * halfExtent.Z);
        }

        internal void PrepGrids(bool set)
        {
            //ConnectedGrids.Clear();
            //MyAPIGateway.GridGroups.GetGroup(Grid, GridLinkTypeEnum.Physical, ConnectedGrids);

            var grids = GridComp.GroupMap.ConnectedGrids;
            for (int i = 0; i < grids.Count; i++)
            {
                var grid = grids[i];
                var comp = StealthSession.GridMap[grid];

                if (set)
                {
                    grid.Flags |= StealthSession.StealthFlag;
                    StealthSession.StealthedGrids.Add(grid);

                    if (GridComp.DisableShields)
                        DisableShields(comp);

                    if (GridComp.DisableWeapons)
                        DisableTurrets(comp);
                }
                else
                {
                    grid.Flags ^= StealthSession.StealthFlag;
                    StealthSession.StealthedGrids.Remove(grid);

                    if (GridComp.DisableWeapons)
                        ReEnableTurrets(comp);
                }
            }

            if (!set && StealthSession.DisableShields)
            {
                ShieldWait = StealthSession.ShieldDelay;
                ShieldWaiting = true;
            }

        }

        internal void DisableShields(GridComp comp)
        {
            for (int j = 0; j < comp.ShieldBlocks.Count; j++)
            {
                var block = comp.ShieldBlocks[j];

                DisabledBlocks[block] = block.Enabled;

                block.Enabled = false;
                block.EnabledChanged += OnEnabledChanged;
            }
        }

        internal void DisableTurrets(GridComp comp)
        {
            for (int j = 0; j < comp.Turrets.Count; j++)
            {
                var block = comp.Turrets[j];

                DisabledBlocks[block] = block.Enabled;

                block.Enabled = false;
                block.EnabledChanged += OnEnabledChanged;
            }
        }

        internal void ReEnableShields(GridComp comp)
        {
            for (int j = 0; j < comp.ShieldBlocks.Count; j++)
            {
                var block = comp.ShieldBlocks[j];

                bool wasEnabled;
                if (!DisabledBlocks.TryGetValue(block, out wasEnabled))
                    continue;

                block.EnabledChanged -= OnEnabledChanged;
                block.Enabled = wasEnabled;

                DisabledBlocks.Remove(block);
            }
        }

        internal void ReEnableTurrets(GridComp comp)
        {
            for (int j = 0; j < comp.Turrets.Count; j++)
            {
                var block = comp.Turrets[j];

                bool wasEnabled;
                if (!DisabledBlocks.TryGetValue(block, out wasEnabled))
                    continue;

                block.EnabledChanged -= OnEnabledChanged;
                block.Enabled = wasEnabled;

                DisabledBlocks.Remove(block);
            }
        }

        internal void OnEnabledChanged(IMyTerminalBlock block)
        {
            (block as IMyFunctionalBlock).Enabled = false;
        }

        internal bool ToggleStealth(bool force = false)
        {
            if (!Online || !StealthActive && !force && (!SufficientPower || CoolingDown)) return false;

            EnterStealth = !StealthActive;
            ExitStealth = StealthActive;

            IgnorePower = force && EnterStealth;

            return true;
        }

        internal void SwitchStealth(bool stealth, bool fade = false)
        {
            var dither = stealth ? StealthSession.Transparency : 0f;

            if (fade)
            {
                var steps = StealthSession.FadeSteps;
                float fraction = (stealth ? 1 : steps - 1) / (float)steps;
                dither = fraction * StealthSession.Transparency;

                FadeEntities.Clear();
                FadeSlims.Clear();
            }
            if (stealth) JumpDrives.Clear();

            foreach (var grid in GridComp.GroupMap.ConnectedGrids)//test
            {
                grid.GetBlocks(SlimBlocks);

                foreach (var slim in SlimBlocks)
                {
                    var fatBlock = slim.FatBlock;
                    if (fatBlock == null)
                    {
                        slim.Dithering = dither;
                        if (fade) FadeSlims.Add(slim);
                        continue;
                    }

                    if (fade) FadeEntities.Add(fatBlock);

                    fatBlock.Render.Transparency = dither;
                    fatBlock.Render.UpdateTransparency();

                    fatBlock.Hierarchy.GetChildrenRecursive(Children);
                    foreach (var child in Children)
                    {
                        if (fade) FadeEntities.Add(child);

                        child.Render.Transparency = dither;
                        child.Render.UpdateTransparency();
                    }
                    Children.Clear();

                    if (stealth)
                    {
                        var jump = fatBlock as IMyJumpDrive;
                        if (jump != null)
                            JumpDrives.Add(jump, jump.CurrentStoredPower);
                    }
                }
                SlimBlocks.Clear();

                //(grid as MyCubeGrid).UpdateDirty(null, true);
            }

            if (fade)
            {
                Fade = Fading ? StealthSession.FadeTime - Fade : StealthSession.FadeTime;
                Fading = true;
            }

            VisibleToClient = !stealth;
        }

        internal void ReCacheBlocks()
        {
            FadeSlims.Clear();
            FadeEntities.Clear();

            var grids = GridComp.GroupMap.ConnectedGrids;
            for (int i = 0; i < grids.Count; i++)
            {
                var grid = grids[i];
                grid.GetBlocks(SlimBlocks);

                for (int j = 0; j < SlimBlocks.Count; j++)
                {
                    var slim = SlimBlocks[j];

                    var fatBlock = slim.FatBlock;
                    if (fatBlock == null)
                    {
                        FadeSlims.Add(slim);
                        continue;
                    }

                    FadeEntities.Add(fatBlock);

                    fatBlock.Hierarchy.GetChildrenRecursive(Children);
                    foreach (var child in Children)
                        FadeEntities.Add(child);

                    Children.Clear();
                }
                SlimBlocks.Clear();
            }
        }

        internal void FadeBlocks(bool fadeOut, int step)
        {
            var steps = StealthSession.FadeSteps;
            var fraction = (fadeOut ? steps - step : step) / (float)steps;
            var dither = fraction * StealthSession.Transparency;

            for (int i = 0; i < FadeSlims.Count; i++)
                FadeSlims[i].Dithering = dither;

            //for (int i = 0; i < ConnectedGrids.Count; i++)
            //    (ConnectedGrids[i] as MyCubeGrid).UpdateDirty(null, true);

            for (int i = 0; i < FadeEntities.Count; i++)
            {
                var entity = FadeEntities[i];
                entity.Render.Transparency = dither;
                entity.Render.UpdateTransparency();
            }

            Fading = step != 0;
        }

        internal void StealthExternalGrid(bool stealth, IMyCubeGrid grid)
        {
            if (stealth) StealthedExternalGrids.Add(grid);
            else StealthedExternalGrids.Remove(grid);

            var dither = stealth ? StealthSession.Transparency : 0f;

            grid.GetBlocks(SlimBlocks);
            foreach (var slim in SlimBlocks)
            {
                var block = slim.FatBlock;
                if (block == null)
                {
                    slim.Dithering = dither;
                    continue;
                }

                block.Render.Transparency = dither;
                block.Render.UpdateTransparency();

                block.Hierarchy.GetChildrenRecursive(Children);
                foreach (var child in Children)
                {
                    child.Render.Transparency = dither;
                    child.Render.UpdateTransparency();
                }
            }
            SlimBlocks.Clear();

            (grid as MyCubeGrid).UpdateDirty(null, true);
        }

        internal void DitherBlock(bool stealth, IMySlimBlock slim)
        {
            var dither = stealth ? StealthSession.Transparency : 0f;

            if (slim.FatBlock == null)
            {
                slim.Dithering = dither;
                return;
            }

            var fat = slim.FatBlock;

            fat.Render.Transparency = dither;
            fat.Render.UpdateTransparency();

            fat.Hierarchy.GetChildrenRecursive(Children);
            foreach (var child in Children)
            {
                child.Render.Transparency = dither;
                child.Render.UpdateTransparency();
            }
            Children.Clear();
        }

        internal void CreateHeatSignature()
        {
            var gps = MyAPIGateway.Session.GPS.Create("Heat Signature", "Heat signature from a cooling down stealth drive.", Block.PositionComp.WorldAABB.Center, true, true);
            gps.GPSColor = Color.OrangeRed;
            HeatSignature = gps;
            MyAPIGateway.Session.GPS.AddLocalGps(gps);
        }

        internal void SinkInit()
        {
            var sinkInfo = new MyResourceSinkInfo()
            {
                MaxRequiredInput = 0,
                RequiredInputFunc = PowerFunc,
                ResourceTypeId = MyResourceDistributorComponent.ElectricityId
            };

            Sink = Block.Components?.Get<MyResourceSinkComponent>();
            if (Sink != null)
            {
                Sink.RemoveType(ref sinkInfo.ResourceTypeId);
                Sink.AddType(ref sinkInfo);
            }
            else
            {
                Sink = new MyResourceSinkComponent();
                Sink.Init(MyStringHash.GetOrCompute("Utility"), sinkInfo);
                (Block as MyCubeBlock).Components.Add(Sink);
            }

            Source = Grid.ResourceDistributor as MyResourceDistributorComponent;
            if (Source != null)
                Source.AddSink(Sink);
            else
                Logs.WriteLine($"DriveComp.SinkInit() - Distributor null");

            Sink.Update();
        }

        private void GetShowInToolbarSwitch()
        {
            List<IMyTerminalControl> items;
            MyAPIGateway.TerminalControls.GetControls<IMyUpgradeModule>(out items);

            foreach (var item in items)
            {

                if (item.Id == "ShowInToolbarConfig")
                {
                    ShowInToolbarSwitch = (IMyTerminalControlOnOffSwitch)item;
                    break;
                }
            }
        }

        private void StorageInit()
        {
            string rawData;
            DriveRepo loadRepo = null;
            if (Block.Storage == null)
            {
                Block.Storage = new MyModStorageComponent();
            }
            else if (Block.Storage.TryGetValue(StealthSession.CompDataGuid, out rawData))
            {
                try
                {
                    var base64 = Convert.FromBase64String(rawData);
                    loadRepo = MyAPIGateway.Utilities.SerializeFromBinary<DriveRepo>(base64);
                }
                catch (Exception ex)
                {
                    Logs.WriteLine($"DriveComp - Exception at StorageInit() - {ex}");
                }
            }

            if (loadRepo != null)
            {
                Sync(loadRepo);
            }
            else
            {
                Repo = new DriveRepo();
            }
        }

        private float PowerFunc()
        {
            if (!Online)
                return 0f;
            if (StealthActive)
                return RequiredPower;
            return 0.001f;
        }

        private void Sync(DriveRepo repo)
        {
            Repo = repo;

            StealthActive = repo.StealthActive;
            CoolingDown = repo.CoolingDown;
            RemainingDuration = repo.RemainingDuration;

            StealthOnInit = repo.StealthActive;
            CdOnInit = repo.CoolingDown;
        }

        //
        // Vanilla Cope
        //

        internal void GetNearbyTurrets()
        {
            _sphere.Center = Block.PositionComp.WorldAABB.Center;

            MyGamePruningStructure.GetAllEntitiesInSphere(ref _sphere, _entities);

            NearbyTurrets.Clear();
            for (int i = 0; i < _entities.Count; i++)
            {
                var entity = _entities[i];
                if (!(entity is IMyLargeTurretBase)) continue;

                var turret = entity as IMyLargeTurretBase;
                if (turret.CubeGrid == Grid) continue;

                NearbyTurrets.Add(turret);
            }
            _entities.Clear();

        }

        public override string ComponentTypeDebugString => "StealthMod";

    }
}
