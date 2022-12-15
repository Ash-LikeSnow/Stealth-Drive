using Sandbox.Definitions;
using Sandbox.ModAPI;
using System;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;
using VRage.Utils;
using Sandbox.Game.Entities;
using VRage.Game.Entity;
using System.Collections.Generic;
using Sandbox.ModAPI.Interfaces.Terminal;

namespace StealthSystem
{
    public partial class StealthSession
    {
        private void OnEntityCreate(MyEntity entity)
        {
            try
            {
                if (!Inited) lock (InitObj) Init();

                var grid = entity as IMyCubeGrid;
                if (grid != null)
                {
                    (grid as MyCubeGrid).AddedToScene += AddToStart => _startGrids.Add(grid);
                    return;
                }

                var upgrade = entity as IMyUpgradeModule;
                if (upgrade != null)
                {
                    var subtype = upgrade.BlockDefinition.SubtypeName;
                    if (!STEALTH_BLOCKS.Contains(subtype) && !HEAT_BLOCKS.Contains(subtype))
                        return;

                    (upgrade as MyCubeBlock).AddedToScene += AddToStart => _startBlocks.Add(upgrade);
                }

                if (!PbApiInited && IsServer && entity is IMyProgrammableBlock)
                    MyAPIGateway.Utilities.InvokeOnGameThread(() => API.PbInit());
            }
            catch (Exception ex)
            {
                Logs.WriteLine($"Exception in EntityCreate: {entity.GetType()} - {ex}");
            }

        }

        private void Init()
        {
            if (Inited) return;
            Inited = true;

            MyAPIGateway.GridGroups.OnGridGroupCreated += GridGroupsOnOnGridGroupCreated;
            MyAPIGateway.GridGroups.OnGridGroupDestroyed += GridGroupsOnOnGridGroupDestroyed;
        }

        private void OnGridClose(IMyEntity entity)
        {
            var grid = entity as IMyCubeGrid;

            if (GridMap.ContainsKey(grid))
            {
                var comp = GridMap[grid];
                GridMap.Remove(grid);
                GridList.Remove(comp);

                comp.Clean();
                _gridCompPool.Push(comp);
            }
            else Logs.WriteLine("OnGridClose() - grid not in map!!!");
        }

        private void OnCloseAll()
        {
            try
            {
                var list = new List<IMyGridGroupData>(GridGroupMap.Keys);
                foreach (var value in list)
                    GridGroupsOnOnGridGroupDestroyed(value);

                MyAPIGateway.GridGroups.OnGridGroupDestroyed -= GridGroupsOnOnGridGroupDestroyed;
                MyAPIGateway.GridGroups.OnGridGroupCreated -= GridGroupsOnOnGridGroupCreated;

                GridGroupMap.Clear();
            }
            catch (Exception ex)
            {
                Logs.WriteLine($"Exception in CloseAll: {ex}");
            }

        }

        private void GridGroupsOnOnGridGroupCreated(IMyGridGroupData groupData)
        {
            if (groupData.LinkType != GridLinkTypeEnum.Physical)
                return;

            var map = _groupMapPool.Count > 0 ? _groupMapPool.Pop() : new GroupMap();
            map.Init(groupData, this);

            //groupData.OnReleased += map.OnReleased;
            groupData.OnGridAdded += map.OnGridAdded;
            groupData.OnGridRemoved += map.OnGridRemoved;
            GridGroupMap[groupData] = map;
        }

        private void GridGroupsOnOnGridGroupDestroyed(IMyGridGroupData groupData)
        {
            if (groupData.LinkType != GridLinkTypeEnum.Physical)
                return;

            GroupMap map;
            if (GridGroupMap.TryGetValue(groupData, out map))
            {
                //groupData.OnReleased -= map.OnReleased;
                groupData.OnGridAdded -= map.OnGridAdded;
                groupData.OnGridRemoved -= map.OnGridRemoved;

                GridGroupMap.Remove(groupData);
                map.Clean();
                _groupMapPool.Push(map);
            }
            else
                Logs.WriteLine($"GridGroupsOnOnGridGroupDestroyed could not find map");
        }

        private void PlayerConnected(long id)
        {
            try
            {
                MyAPIGateway.Multiplayer.Players.GetPlayers(null, myPlayer => FindPlayer(myPlayer, id));
            }
            catch (Exception ex) { Logs.WriteLine($"Exception in PlayerConnected: {ex}"); }
        }

        private bool FindPlayer(IMyPlayer player, long id)
        {
            if (player.IdentityId == id)
            {
                var packet = new SettingsPacket { EntityId = 0, Settings = ConfigSettings.Config, Type = PacketType.Settings };
                SendPacketToClient(packet, player.SteamUserId);
            }
            return false;
        }

        private void InitPlayers()
        {
            List<IMyPlayer> players = new List<IMyPlayer>();
            MyAPIGateway.Multiplayer.Players.GetPlayers(players);

            for (int i = 0; i < players.Count; i++)
                PlayerConnected(players[i].IdentityId);
        }

        internal bool ModCheck()
        {
            foreach (var mod in Session.Mods)
            {
                if (mod.PublishedFileId == 1918681825 || mod.PublishedFileId == 2496225055 || mod.PublishedFileId == 2726343161)
                    return true;

                if (mod.Name == "WeaponCore" || mod.Name == "CoreSystems")
                    return true;
            }

            return false;
        }

        internal static void UpdateEnforcement(StealthSettings settings)
        {
            JumpPenalty = settings.JumpPenalty;
            Transparency = settings.Transparency;
            ShieldDelay = settings.ShieldDelay;
            FadeTime = settings.FadeTime;
            DamageThreshold = settings.DamageThreshold;
            BaseDuration = settings.DriveConfig.Duration;
            PowerScale = settings.DriveConfig.PowerScale;
            SignalRangeScale = settings.DriveConfig.SignalRangeScale;
            SinkDuration = settings.SinkConfig.Duration;
            SinkPower = settings.SinkConfig.Power;
            DoDamage = settings.SinkConfig.DoDamage;
            DisableShields = settings.DisableShields;
            DisableWeapons = settings.DisableWeapons;

            FadeSteps = FadeTime / FADE_INTERVAL + 1;

            StealthFlag = (EntityFlags)(DisableWeapons ? 0x1000004 : 0x1000000);
        }

        internal void RemoveEdges()
        {
            var defs = MyDefinitionManager.Static.GetAllDefinitions();
            foreach (var def in defs)
            {
                if (def is MyCubeBlockDefinition && def.Id.SubtypeName.Contains("Armor"))
                {
                    var armorDef = (MyCubeBlockDefinition)def;
                    if (armorDef.CubeDefinition == null)
                        continue;

                    armorDef.CubeDefinition.ShowEdges = false;
                }
            }
        }

        internal void RefreshTerminal(IMyFunctionalBlock block, IMyTerminalControlOnOffSwitch control)
        {
            block.RefreshCustomInfo();

            if (control != null)
            {
                var originalSetting = control.Getter(block);
                control.Setter(block, !originalSetting);
                control.Setter(block, originalSetting);
            }
        }

        internal void AfterDamageApplied(object target, MyDamageInformation info)
        {
            if (!DisableWeapons) //Reveal grid on dealing damage
            {
                var ent = MyEntities.GetEntityById(info.AttackerId);
                if (!(ent is MyCubeBlock)) return;

                var attackingGrid = (ent as IMyCubeBlock).CubeGrid;
                if (attackingGrid == null) return;

                if (!StealthedGrids.Contains(attackingGrid))
                    return;

                GridComp gridCompA;
                if (!GridMap.TryGetValue(attackingGrid, out gridCompA))
                {
                    Logs.WriteLine("Attacking grid not mapped in damage handler");
                    return;
                }

                gridCompA.Revealed = true;
            }


            if (info.AttackerId == 0 || !(target is IMySlimBlock))
                return;

            var targetGrid = (target as IMySlimBlock).CubeGrid;

            if (targetGrid == null || !StealthedGrids.Contains(targetGrid)) return;

            GridComp gridComp;
            if (!GridMap.TryGetValue(targetGrid, out gridComp))
            {
                Logs.WriteLine("Grid not mapped in damage handler");
                return;
            }

            gridComp.DamageTaken += (int)info.Amount;
        }

        internal static void DrawBox(MyOrientedBoundingBoxD obb, Color color)
        {
            var box = new BoundingBoxD(-obb.HalfExtent, obb.HalfExtent);
            var wm = MatrixD.CreateFromTransformScale(obb.Orientation, obb.Center, Vector3D.One);
            MySimpleObjectDraw.DrawTransparentBox(ref wm, ref box, ref color, MySimpleObjectRasterizer.Solid, 1);
        }

        internal static void DrawScaledPoint(Vector3D pos, double radius, Color color, int divideRatio = 20, bool solid = true, float lineWidth = 0.5f)
        {
            var posMatCenterScaled = MatrixD.CreateTranslation(pos);
            var posMatScaler = MatrixD.Rescale(posMatCenterScaled, radius);
            var material = MyStringId.GetOrCompute("square");
            MySimpleObjectDraw.DrawTransparentSphere(ref posMatScaler, 1f, ref color, solid ? MySimpleObjectRasterizer.Solid : MySimpleObjectRasterizer.Wireframe, divideRatio, null, material, lineWidth);
        }

        internal static void DrawLine(Vector3D start, Vector3D end, Vector4 color, float width)
        {
            var c = color;
            MySimpleObjectDraw.DrawLine(start, end, _square, ref c, width);
        }

        internal static void DrawLine(Vector3D start, Vector3D dir, Vector4 color, float width, float length)
        {
            var c = color;
            MySimpleObjectDraw.DrawLine(start, start + (dir * length), _square, ref c, width);
        }
    }
}
