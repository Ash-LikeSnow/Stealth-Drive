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
using VRage.Game.ModAPI.Interfaces;

namespace StealthSystem
{
    public partial class StealthSession
    {

        internal void CompLoop()
        {
            //var position = MyAPIGateway.Session.LocalHumanPlayer?.Character?.PositionComp.WorldAABB.Center ?? MyAPIGateway.Session?.Camera?.Position;
            //var controlledGrid = (MyAPIGateway.Session.ControlledObject as MyCubeBlock)?.GetTopMostParent();

            if (GridList.Count == 0) return;

            for (int i = 0; i < GridList.Count; i++)
            {
                var gridComp = GridList[i];
                var master = gridComp.MasterComp;

                if (gridComp.GroupMap == null)
                {
                    var group = MyAPIGateway.GridGroups.GetGridGroup(GridLinkTypeEnum.Physical, gridComp.Grid);
                    if (group != null)
                    {
                        GroupMap map;
                        if (GridGroupMap.TryGetValue(group, out map))
                            gridComp.GroupMap = map;
                    }
                }

                bool enter = false;
                bool exit = false;
                bool cold = false;

                try
                {
                    for (int j = 0; j < gridComp.StealthComps.Count; j++)
                    {
                        var comp = gridComp.StealthComps[j];

                        //if (comp.Age < 2)
                        //{
                        //    comp.Age++;
                        //    continue;
                        //}

                        //if (!comp.Inited)
                        //{
                        //    MyLog.Default.WriteLine("Try Init on loop");
                        //    comp.Init2();
                        //}

                        if (comp.Grid != comp.Block.CubeGrid)
                        {
                            if (!GridMap.ContainsKey(comp.Block.CubeGrid))
                            {
                                comp.Transfer = true;
                                continue;
                            }

                            comp.GridChange();
                        }

                        if (!IsDedicated && comp.Fading)
                        {
                            if (comp.StealthActive && (gridComp.GroupsDirty || comp.GridUpdated))
                                comp.ReCacheBlocks();

                            if (comp.Fade-- % FADE_INTERVAL == 0)
                                comp.FadeBlocks(comp.StealthActive, comp.Fade / FADE_INTERVAL);
                        }

                        if (comp.ShieldWaiting)
                        {
                            if (comp.ShieldWait-- <= 0)
                            {
                                comp.ShieldWaiting = false;

                                foreach (var block in comp.DisabledBlocks.Keys)
                                {
                                    block.EnabledChanged -= comp.OnEnabledChanged;
                                    block.Enabled = comp.DisabledBlocks[block];
                                }
                                comp.DisabledBlocks.Clear();
                            }
                        }

                        //Hide/unhide main grid after delay to match slimblock transparency update
                        //if (comp.DelayedRender)
                        //{
                        //    if (comp.Delay-- == 0)
                        //    {
                        //        foreach (var grid in comp.ConnectedGrids)
                        //            comp.DitherFatBlocks(!comp.VisibleToClient);
                                    
                        //        comp.DelayedRender = false;
                        //    }
                        //}

                        if (!comp.IsPrimary)
                            continue;

                        if (comp != master && (master == null || !master.Online))
                        {
                            Logs.WriteLine($"[StealthMod] Primary != master - master null: {master == null}");
                            master = comp;
                        }

                        if (!comp.Block.IsFunctional && (!comp.TransferFailed || Tick120))
                            comp.TransferFailed = !comp.TransferPrimary(false);

                        //Calculate grid surface area and drive power
                        if (gridComp.GroupsDirty || Tick60 && comp.GridUpdated)
                        {
                            comp.CalculatePowerRequirements();
                            gridComp.GroupsDirty = false;
                            comp.GridUpdated = false;
                        }

                        //Decrease remaining stealth duration after jump
                        if (Tick120 && comp.StealthActive)
                        {
                            var jumpList = new List<IMyJumpDrive>(comp.JumpDrives.Keys);
                            foreach (var jump in jumpList)
                            {
                                if (jump.CurrentStoredPower < comp.JumpDrives[jump])
                                    comp.RemainingDuration -= JumpPenalty;

                                comp.JumpDrives[jump] = jump.CurrentStoredPower;
                            }
                        }

                        //Update comp state and refresh custom info
                        if (Tick20 || comp.PowerDirty)
                        {
                            comp.UpdateStatus();
                            if (!IsDedicated && LastTerminal == comp.Block && MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel)
                                comp.RefreshTerminal();
                        }

                        //Exit stealth conditions
                        if (comp.StealthActive && (!comp.IsPrimary || !comp.Online || !comp.SufficientPower || comp.RemainingDuration-- <= 0 ||
                            gridComp.Revealed || gridComp.DamageTaken > DamageThreshold))
                        {
                            comp.ExitStealth = true;
                        }

                        //Update cooldown and heat signal
                        if (comp.CoolingDown)
                        {
                            if (comp.RemainingDuration-- <= 0)
                            {
                                if (!IsDedicated && comp.HeatSignature != null)
                                {
                                    MyAPIGateway.Session.GPS.RemoveLocalGps(comp.HeatSignature);
                                    comp.HeatSignature = null;
                                }
                                comp.CoolingDown = false;
                                cold = true;
                            }
                            else if (!IsDedicated && comp.HeatSignature != null)
                            {
                                comp.HeatSignature.Coords = comp.Block.PositionComp.WorldAABB.Center;
                            }

                            if (!IsDedicated && comp.CdOnInit)
                            {
                                var position = MyAPIGateway.Session?.Camera?.Position ?? Vector3D.Zero;
                                if (Vector3D.DistanceSquared(position, comp.Block.PositionComp.WorldAABB.Center) < comp.SignalDistanceSquared)
                                    comp.CreateHeatSignature();

                                comp.CdOnInit = false;
                            }
                        }

                        //Vanilla fuckery
                        if (!WcActive && comp.StealthActive)
                        {
                            if (TickMod60 == comp.CompTick60)
                                comp.GetNearbyTurrets();

                            for (int k = 0; k < comp.NearbyTurrets.Count; k++)
                            {
                                var turret = comp.NearbyTurrets[k];

                                if (!turret.HasTarget) continue;

                                var target = turret.Target;

                                var block = target as IMyCubeBlock;
                                if (block != null && ((uint)block.CubeGrid.Flags & 0x1000000) > 0)
                                {
                                    turret.ResetTargetingToDefault();
                                    continue;
                                }

                                if (((uint)target.Flags & 0x1000000) > 0)
                                    turret.ResetTargetingToDefault();
                            }
                        }

                        //if (comp.ExpandedOBB != null) DrawBox(comp.ExpandedOBB, Color.AliceBlue);

                        if (comp.EnterStealth || comp.ExitStealth || comp.StealthOnInit || comp.StealthActive && TickMod15 == comp.CompTick15)
                        {
                            comp.CalculateExpandedOBB();

                            Vector3D position = Vector3D.Zero;
                            bool inside = false;
                            if (!IsDedicated)
                            {
                                position = MyAPIGateway.Session?.Camera?.Position ?? Vector3D.Zero;
                                inside = comp.ExpandedOBB.Contains(ref position);
                            }

                            if (comp.EnterStealth)
                            {
                                comp.EnterStealth = false;
                                comp.UpdateStatus();
                                if (!comp.Online || !comp.SufficientPower)
                                    continue;

                                enter = true;

                                gridComp.DamageTaken = 0;
                                gridComp.Revealed = false;
                                comp.StealthActive = true;
                                comp.RemainingDuration = comp.MaxDuration;

                                //comp.Grid.Flags |= (EntityFlags)0x1000000;

                                comp.Sink.Update();

                                var packet = new UpdateStatePacket { EntityId = comp.Block.EntityId, EnterStealth = true, Type = PacketType.UpdateState };
                                if (IsServer)
                                    SendPacketToClients(packet, comp.ReplicatedClients);

                                comp.PrepGrids(true);

                                if (!WcActive) comp.GetNearbyTurrets();

                                if (!IsDedicated)
                                {
                                    if (IsClient)
                                        SendPacketToServer(packet);

                                    if (!inside)
                                        comp.SwitchStealth(true, true);

                                    comp.RefreshTerminal();

                                }

                            }
                            else if (comp.ExitStealth)
                            {
                                exit = true;

                                comp.ExitStealth = false;
                                comp.StealthActive = false;

                                comp.CoolingDown = true;
                                comp.RemainingDuration = comp.MaxDuration - comp.RemainingDuration;

                                //comp.Grid.Flags ^= (EntityFlags)0x1000000;

                                comp.Sink.Update();

                                var packet = new UpdateStatePacket { EntityId = comp.Block.EntityId, ExitStealth = true, Type = PacketType.UpdateState };
                                if (IsServer)
                                    SendPacketToClients(packet, comp.ReplicatedClients);

                                comp.PrepGrids(false);

                                foreach (var entity in comp.PreviousEntities)
                                {
                                    if (!IsDedicated)
                                    {
                                        if (entity is IMyCubeGrid)
                                            comp.StealthExternalGrid(false, entity as IMyCubeGrid);
                                        else
                                            entity.Render.Visible = true;
                                    }

                                    entity.Flags ^= StealthFlag;
                                }

                                if (!IsDedicated)
                                {
                                    if (IsClient)
                                        SendPacketToServer(packet);

                                    if (!comp.VisibleToClient)
                                        comp.SwitchStealth(false, true);

                                    comp.RefreshTerminal();

                                    if (Vector3D.DistanceSquared(position, comp.Block.PositionComp.WorldAABB.Center) < comp.SignalDistanceSquared)
                                        comp.CreateHeatSignature();
                                }

                            }
                            else
                            {
                                if (comp.StealthOnInit)
                                {
                                    if (!WcActive) comp.GetNearbyTurrets();
                                    comp.PrepGrids(true);
                                    comp.StealthOnInit = false;
                                }

                                if (!IsDedicated && (comp.StealthOnInit || inside != comp.VisibleToClient))
                                {
                                    comp.SwitchStealth(!inside);
                                    comp.Fading = false;
                                }

                                MyGamePruningStructure.GetAllEntitiesInOBB(ref comp.ExpandedOBB, _entities);

                                for (int k = 0; k < _entities.Count; k++)
                                {
                                    MyEntity entity = _entities[k];
                                    if (!(entity is IMyDestroyableObject || entity is IMyCubeGrid))
                                        continue;

                                    if (entity is IMyCubeGrid)
                                    {
                                        var grid = (IMyCubeGrid)entity;
                                        if (StealthedGrids.Contains(grid)) continue;

                                        var obb = new MyOrientedBoundingBoxD(grid.PositionComp.LocalAABB, grid.PositionComp.WorldMatrixRef);
                                        if (comp.ExpandedOBB.Contains(ref obb) != ContainmentType.Contains) continue;


                                        if (!IsDedicated && inside == comp.StealthedExternalGrids.Contains(grid))
                                            comp.StealthExternalGrid(!inside, grid);
                                    }
                                    else if (!IsDedicated) entity.Render.Visible = inside;

                                    comp.CurrentEntities.Add(entity);

                                    if (comp.PreviousEntities.Remove(entity))
                                        continue;

                                    entity.Flags |= StealthFlag;
                                }
                                _entities.Clear();

                                foreach (var entity in comp.PreviousEntities)
                                {
                                    var grid = entity as IMyCubeGrid;
                                    if (grid != null && gridComp.GroupMap.ConnectedGrids.Contains(grid))
                                    {
                                        comp.StealthedExternalGrids.Remove(grid);
                                        continue;
                                    }

                                    if (!IsDedicated)
                                    {
                                        if (grid != null)
                                            comp.StealthExternalGrid(false, grid);
                                        else
                                            entity.Render.Visible = true;
                                    }
                                    entity.Flags ^= StealthFlag;
                                }

                                comp.PreviousEntities = new HashSet<MyEntity>(comp.CurrentEntities);
                                comp.CurrentEntities.Clear();
                            }
                        }
                    }

                }
                catch (Exception ex)
                {
                    Logs.WriteLine($"Exception in stealth comp loop: {ex}");
                }


                if (Tick20 && master != null)
                {
                    _duration = master.MaxDuration;
                    master.MaxDuration = BaseDuration;
                }

                if (gridComp.HeatComps.Count == 0) continue;

                try
                {
                    for (int j = 0; j < gridComp.HeatComps.Count; j++)
                    {
                        var comp = gridComp.HeatComps[j];

                        //if (comp.Age < 2)
                        //{
                        //    comp.Age++;
                        //    continue;
                        //}

                        //if (!comp.Inited)
                        //{
                        //    MyLog.Default.WriteLine("Try Init on loop");
                        //    comp.Init();
                        //}

                        if (comp.Grid != comp.Block.CubeGrid)
                        {
                            comp.GridChange();
                        }

                        if (enter)
                        {
                            comp.Accumulating = true;
                        }
                        else if (exit)
                        {
                            comp.Accumulating = false;
                            comp.Radiating = true;
                            comp.Block.SetEmissiveParts(RADIANT_EMISSIVE, Color.DarkRed, 0.5f);
                        }
                        else if (cold)
                        {
                            comp.Radiating = false;
                            comp.Block.SetEmissiveParts(RADIANT_EMISSIVE, Color.DarkSlateGray, 0.1f);
                        }

                        //if (comp.Radiating)
                        //{
                        //    DrawBox(comp.DamageBox, Color.Blue);
                        //    MyAPIGateway.Utilities.ShowNotification($"Drawing {comp.BlockBoxes.Count} block boxes", 16);
                        //    foreach (var box in comp.BlockBoxes)
                        //        DrawBox(box, Color.Red);
                        //}

                        if (TickMod20 != comp.CompTick)
                            continue;

                        comp.UpdateStatus();
                        if (!IsDedicated && LastTerminal == comp.Block && MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel)
                            RefreshTerminal(comp.Block, comp.ShowInToolbarSwitch);

                        if (comp.Working)
                        {
                            if (comp.Accumulating)
                                comp.HeatPercent = (byte)(100 * (1 - (master.RemainingDuration / (float)_duration)));
                            else if (comp.Radiating)
                            {
                                comp.HeatPercent = (byte)(100 * (master.RemainingDuration / (float)_duration));
                                if (!IsClient) comp.DamageBlocks();
                            }
                            else
                            {
                                comp.HeatPercent = 0;
                            }

                            if (master != null)
                                master.MaxDuration += SinkDuration;

                        }
                        else
                        {
                            if (comp.Accumulating)
                            {
                                comp.Accumulating = false;
                                comp.WasAccumulating = true;
                                master.RemainingDuration -= SinkDuration * (100 - comp.HeatPercent) / 100;
                            }

                            if (comp.HeatPercent > 0)
                            {
                                comp.HeatPercent -= (byte)(100 * (20 / (float)SinkDuration));
                                if (!IsClient) comp.DamageBlocks();
                                //do heat signature
                            }
                        }
                    }

                }
                catch (Exception ex)
                {
                    Logs.WriteLine($"Exception in heat comp loop: {ex}");
                }

            }

        }

        internal void StartComps()
        {
            try
            {
                _startGrids.ApplyAdditions();
                if (_startGrids.Count > 0)
                {
                    for (int i = 0; i < _startGrids.Count; i++)
                    {
                        var grid = _startGrids[i];

                        if ((grid as MyCubeGrid).IsPreview)
                            continue;

                        var gridComp = _gridCompPool.Count > 0 ? _gridCompPool.Pop() : new GridComp();
                        gridComp.Init(grid);

                        GridList.Add(gridComp);
                        GridMap[grid] = gridComp;
                        grid.OnClose += OnGridClose;
                    }
                    _startGrids.ClearImmediate();
                }

                _startBlocks.ApplyAdditions();
                for (int i = 0; i < _startBlocks.Count; i++)
                {
                    var module = _startBlocks[i];

                    if (module?.CubeGrid == null || !GridMap.ContainsKey(module.CubeGrid))
                        continue;

                    if (module.CubeGrid.Physics == null || (module.CubeGrid as MyCubeGrid).IsPreview)
                    {
                        Logs.WriteLine($"invalid grid in startblocks - IsPreview {(module.CubeGrid as MyCubeGrid).IsPreview} - physics null {module.CubeGrid.Physics == null}");
                        continue;
                    }

                    var gridData = GridMap[module.CubeGrid];

                    if (STEALTH_BLOCKS.Contains(module.BlockDefinition.SubtypeName))
                    {
                        if (DriveMap.ContainsKey(module.EntityId)) continue;

                        var comp = new DriveComp(module);
                        DriveMap[module.EntityId] = comp;
                        gridData.StealthComps.Add(comp);
                        comp.Init();
                    }
                    else
                    {
                        var comp = new SinkComp(module);
                        gridData.HeatComps.Add(comp);
                        comp.Init();
                    }
                }
                _startBlocks.ClearImmediate();
            }
            catch (Exception ex)
            {
                Logs.WriteLine($"Exception in StartComps: {ex}");
            }

        }
    }
}
