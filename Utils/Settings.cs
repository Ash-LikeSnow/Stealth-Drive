using ProtoBuf;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StealthSystem
{
    internal class Settings
    {
        internal const string CONFIG_FILE = "StealthMod.cfg";
        internal const int CONFIG_VERSION = 7;

        internal StealthSettings Config;

        internal Settings()
        {
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

                StealthSession.UpdateEnforcement(Config);
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

            var fade = oldSettings.Version < 4;
            var five = oldSettings.Version < 5;
            var six = oldSettings.Version < 6;
            var seven = oldSettings.Version < 7;

            Config.FadeTime = fade ? 150 : oldSettings.FadeTime;
            Config.ShieldDelay = five ? 300 : oldSettings.ShieldDelay;
            Config.JumpPenalty = oldSettings.JumpPenalty;
            Config.Transparency = oldSettings.Transparency;
            Config.DisableShields = five ? true : oldSettings.DisableShields;
            Config.DamageThreshold = oldSettings.DamageThreshold;
            Config.DisableWeapons = six ? true : oldSettings.DisableWeapons;
            Config.HideThrusterFlames = seven ? true : oldSettings.HideThrusterFlames;

            if (Config.DriveConfig == null)
                Config.DriveConfig = new StealthSettings.DriveSettings();

            var oldDrive = oldSettings.DriveConfig;
            if (oldDrive != null)
            {
                Config.DriveConfig.Duration = oldDrive.Duration;
                Config.DriveConfig.PowerScale = oldDrive.PowerScale;
                Config.DriveConfig.SignalRangeScale = oldDrive.SignalRangeScale;
            }

            if (Config.SinkConfig == null)
                Config.SinkConfig = new StealthSettings.SinkSettings();

            var oldSink = oldSettings.SinkConfig;
            if (oldSink != null)
            {
                Config.SinkConfig.Duration = oldSink.Duration;
                Config.SinkConfig.Power = oldSink.Power;
                Config.SinkConfig.DoDamage = oldSink.DoDamage;
            }
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
            if (Config.DamageThreshold <= 0)
                Config.DamageThreshold = 1;

            if (Config.DriveConfig == null)
                Config.DriveConfig = new StealthSettings.DriveSettings();

            if (Config.DriveConfig.Duration <= 0)
                Config.DriveConfig.Duration = 1800;
            if (Config.DriveConfig.PowerScale <= 0)
                Config.DriveConfig.PowerScale = 0.02f;
            if (Config.DriveConfig.SignalRangeScale <= 0)
                Config.DriveConfig.SignalRangeScale = 20;

            if (Config.SinkConfig == null)
                Config.SinkConfig = new StealthSettings.SinkSettings();

            if (Config.SinkConfig.Duration <= 0)
                Config.SinkConfig.Duration = 900;
            if (Config.SinkConfig.Power <= 0)
                Config.SinkConfig.Power = 10f;
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
        [ProtoMember(5)] public float Blank1 = 1f; //deprecated
        [ProtoMember(6)] public float Blank2 = 1f; //deprecated
        [ProtoMember(7)] public float Transparency = 0.9f;
        [ProtoMember(8)] public bool DisableShields = true;
        [ProtoMember(9)] public int DamageThreshold = 1000;
        [ProtoMember(10)] public bool DisableWeapons = true;
        [ProtoMember(11)] public bool HideThrusterFlames = true;

        [ProtoMember(20)] public DriveSettings DriveConfig;
        [ProtoMember(21)] public SinkSettings SinkConfig;

        [ProtoContract]
        public class DriveSettings
        {
            [ProtoMember(1)] public int Duration = 1800;
            [ProtoMember(2)] public float PowerScale = 0.02f;
            [ProtoMember(3)] public int SignalRangeScale = 20;
        }

        [ProtoContract]
        public class SinkSettings
        {
            [ProtoMember(1)] public int Duration = 600;
            [ProtoMember(2)] public float Power = 10f;
            [ProtoMember(3)] public bool DoDamage = true;
        }

    }
}
