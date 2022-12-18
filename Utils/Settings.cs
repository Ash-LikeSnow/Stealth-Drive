using ProtoBuf;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;

namespace StealthSystem
{
    internal class Settings
    {
        private readonly StealthSession _session;

        internal const string CONFIG_FILE = "StealthMod.cfg";
        internal const int CONFIG_VERSION = 8;

        internal StealthSettings Config;

        internal Settings(StealthSession session)
        {
            _session = session;

            LoadConfig();
        }

        private void LoadConfig()
        {
            try
            {
                if (MyAPIGateway.Utilities.FileExistsInWorldStorage(CONFIG_FILE, typeof(StealthSettings)))
                {

                    var writer = MyAPIGateway.Utilities.ReadFileInWorldStorage(CONFIG_FILE, typeof(StealthSettings));

                    StealthSettings xmlData = null;

                    try { xmlData = MyAPIGateway.Utilities.SerializeFromXML<StealthSettings>(writer.ReadToEnd()); }
                    catch (Exception e) { writer.Dispose(); }

                    writer.Dispose();

                    if (xmlData?.Version == CONFIG_VERSION)
                    {
                        Config = xmlData;
                        CorruptionCheck();
                        SaveConfig();
                    }
                    else
                        GenerateConfig(xmlData);
                }
                else GenerateConfig();

                _session.UpdateEnforcement(Config);
            }
            catch (Exception ex)
            {
                Logs.WriteLine($"Exception in LoadConfig: {ex}");
            }
        }

        private void GenerateConfig(StealthSettings oldSettings = null)
        {

            if (oldSettings != null) RebuildConfig(oldSettings);
            else
                Config = new StealthSettings { Version = CONFIG_VERSION };

            CorruptionCheck();
            SaveConfig();
        }

        private void RebuildConfig(StealthSettings oldSettings)
        {
            Config = new StealthSettings { Version = CONFIG_VERSION };

            if (oldSettings == null)
                return;

            var fade = oldSettings.Version < 4;
            var five = oldSettings.Version < 5;
            var six = oldSettings.Version < 6;
            var seven = oldSettings.Version < 7;
            var eight = oldSettings.Version < 8;

            Config.FadeTime = fade ? 150 : oldSettings.FadeTime;
            Config.ShieldDelay = five ? 300 : oldSettings.ShieldDelay;
            Config.JumpPenalty = oldSettings.JumpPenalty;
            Config.Transparency = oldSettings.Transparency;
            Config.DisableShields = five ? true : oldSettings.DisableShields;
            Config.DamageThreshold = oldSettings.DamageThreshold;
            Config.DisableWeapons = six ? true : oldSettings.DisableWeapons;
            Config.HideThrusterFlames = seven ? true : oldSettings.HideThrusterFlames;
            Config.WorkInWater = eight ? true : oldSettings.WorkInWater;
            Config.WorkOutOfWater = eight ? true : oldSettings.WorkOutOfWater;
            Config.WaterTransitionDepth = eight ? 0f : oldSettings.WaterTransitionDepth;

            Config.DriveConfig = oldSettings.DriveConfig;
            Config.SinkConfig = oldSettings.SinkConfig;

            Config.DriveConfigs = oldSettings.DriveConfigs;
            Config.SinkConfigs = oldSettings.SinkConfigs;


        }

        private void CorruptionCheck()
        {
            if (Config.FadeTime < 0)
                Config.FadeTime = 210;
            if (Config.ShieldDelay < 0)
                Config.ShieldDelay = 300;
            if (Config.JumpPenalty < 0)
                Config.JumpPenalty = 180;
            if (Config.Transparency <= 0)
                Config.Transparency = 0.9f;

            if (Config.WorkInWater == false && Config.WorkOutOfWater == false)
                Config.WorkInWater = Config.WorkOutOfWater = true;

            if (Config.DriveConfigs == null || Config.DriveConfigs.Length == 0)
            {
                var oldDrive = Config.DriveConfig;
                Config.DriveConfigs = new StealthSettings.DriveSettings[3]
                {
                    new StealthSettings.DriveSettings(oldDrive)
                    {
                        Subtype = "StealthDrive",
                    },
                    new StealthSettings.DriveSettings(oldDrive)
                    {
                        Subtype = "StealthDriveSmall",
                    },
                    new StealthSettings.DriveSettings(oldDrive)
                    {
                        Subtype = "StealthDrive1x1",
                        Duration = 600,
                    },
                };
            }
            else
            {
                var drives = new List<StealthSettings.DriveSettings>();
                for (int i = 0; i < Config.DriveConfigs.Length; i++)
                {
                    var drive = Config.DriveConfigs[i];
                    if (string.IsNullOrEmpty(drive.Subtype))
                        continue;

                    if (drive.Duration <= 0)
                        drive.Duration = 1800;
                    if (drive.PowerScale <= 0f)
                        drive.PowerScale = 0.02f;
                    if (drive.SignalRangeScale <= 0f)
                        drive.SignalRangeScale = 20f;

                    drives.Add(drive);                        
                }
                if (drives.Count > 0)
                    Config.DriveConfigs = drives.ToArray();
            }

            if (Config.SinkConfigs == null || Config.SinkConfigs.Length == 0)
            {
                var oldSink = Config.SinkConfig;
                Config.SinkConfigs = new StealthSettings.SinkSettings[2]
                {
                    new StealthSettings.SinkSettings(oldSink)
                    {
                        Subtype = "StealthHeatSink",
                    },
                    new StealthSettings.SinkSettings(oldSink)
                    {
                        Subtype = "StealthHeatSinkSmall",
                    },
                };
            }
            else
            {
                var sinks = new List<StealthSettings.SinkSettings>();
                for (int i = 0; i < Config.SinkConfigs.Length; i++)
                {
                    var sink = Config.SinkConfigs[i];
                    if (string.IsNullOrEmpty(sink.Subtype))
                        continue;

                    if (sink.Duration <= 0)
                        sink.Duration = 900;
                    if (sink.Power <= 0f)
                        sink.Power = 10f;

                    sinks.Add(sink);
                }
                if (sinks.Count > 0)
                    Config.SinkConfigs = sinks.ToArray();
            }

            Config.DriveConfig = null;
            Config.SinkConfig = null;

        }

        private void SaveConfig()
        {
            MyAPIGateway.Utilities.DeleteFileInWorldStorage(CONFIG_FILE, typeof(StealthSettings));
            var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(CONFIG_FILE, typeof(StealthSettings));
            var data = MyAPIGateway.Utilities.SerializeToXML(Config);
            Write(writer, data);
        }

        private static void Write(TextWriter writer, string data)
        {
            writer.Write(data);
            writer.Flush();
            writer.Dispose();
        }
    }

    [ProtoContract]
    public class StealthSettings
    {
        [ProtoMember(1)] public int Version = -1;
        [ProtoMember(2)] public int FadeTime = 210;
        [ProtoMember(3)] public int ShieldDelay = 300;
        [ProtoMember(4)] public int JumpPenalty = 180;

        [ProtoMember(7)] public float Transparency = 0.9f;
        [ProtoMember(8)] public bool DisableShields = true;
        [ProtoMember(9)] public int DamageThreshold = 1000;
        [ProtoMember(10)] public bool DisableWeapons = true;
        [ProtoMember(11)] public bool HideThrusterFlames = true;
        [ProtoMember(12)] public bool WorkInWater = true;
        [ProtoMember(13)] public bool WorkOutOfWater = true;
        [ProtoMember(14)] public float WaterTransitionDepth = 0f;
        [ProtoMember(15)] public bool RevealOnDamage = true;

        [ProtoMember(20)] public DriveSettings DriveConfig;
        [ProtoMember(21)] public SinkSettings SinkConfig;

        [ProtoMember(30)] public DriveSettings[] DriveConfigs;
        [ProtoMember(31)] public SinkSettings[] SinkConfigs;

        [ProtoContract]
        public class DriveSettings
        {
            [ProtoMember(1)] public string Subtype;
            [ProtoMember(2)] public int Duration = 1800;
            [ProtoMember(3)] public float PowerScale = 0.02f;
            [ProtoMember(4)] public float SignalRangeScale = 20f;

            public DriveSettings()
            {

            }

            public DriveSettings(DriveSettings old)
            {
                if (old == null) return;

                Duration = old.Duration;
                PowerScale = old.PowerScale;
                SignalRangeScale = old.SignalRangeScale;
            }
        }

        [ProtoContract]
        public class SinkSettings
        {
            [ProtoMember(1)] public string Subtype;
            [ProtoMember(2)] public int Duration = 600;
            [ProtoMember(3)] public float Power = 10f;
            [ProtoMember(4)] public bool DoDamage = true;

            public SinkSettings()
            {

            }

            public SinkSettings(SinkSettings old)
            {
                if (old == null) return;

                Duration = old.Duration;
                Power = old.Power;
                DoDamage = old.DoDamage;
            }
        }

    }
}
