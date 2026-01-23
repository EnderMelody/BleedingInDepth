using BleedingInDepth.config;
using BleedingInDepth.lib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using static BleedingInDepth.config.BID_ModConfig.Config_BleedReport;

namespace BleedingInDepth.config
{
    internal class BID_Config_Manager
    {
        private static ICoreAPI API = BleedingInDepthModSystem.API;



        internal static void Config_Conjure() //call to start config load
        {
            if (Config_Reference.Config_Loaded is not null) { API.Logger.Debug("[BleedingInDepth]: (Config_StartLoad) {0} config already loaded, skipping", [API.Side]); return; }
            if (API.Side == EnumAppSide.Server) { API.Logger.Debug("[BleedingInDepth]: (Config_StartLoad) Caught server config loading"); Config_LoadDisk(); Config_SaveWorld(); }
            else { API.Logger.Debug("[BleedingInDepth]: (Config_StartLoad) Caught non-server config loading, loading from World config"); Config_LoadWorld(); }
        }


        internal static void Config_LoadDisk()
        {
            try //create config for server
            {
                Config_Reference.Config_Loaded = API.LoadModConfig<Config_Reference>(Config_Reference.Config_FilePath);
                if (Config_Reference.Config_Loaded is null) { API.Logger.Debug("[BleedingInDepth]: (Config_LoadDisk) Found no '{0}' config, creating new", [API.Side]); } else { API.Logger.Debug("[BleedingInDepth]: (Config_LoadDisk) Found '{0}' config, loading", [API.Side]); } //TODO: replace with lang file calls and combine into one if statement using ?
                Config_Reference.Config_Loaded ??= new Config_Reference();
                Config_ValidateValue();
                Config_SaveDisk();
            }
            catch (Exception e) { BID_Lib_FunctionsGeneral.Log_Error("Caught exception: {0}", loggers: [e.Message]); Config_Reference.Config_Loaded = new Config_Reference(); }
        }


        internal static void Config_LoadWorld() //load config from world config for clients
        {
            var Config_LoadedBase64 = API.World.Config.GetString(Config_Reference.Config_FilePath);
            if (string.IsNullOrWhiteSpace(Config_LoadedBase64)) { Config_Reference.Config_Loaded = new Config_Reference(); Config_ValidateValue(); API.Logger.Debug("[BleedingInDepth]: (Config_LoadWorld) returned null or whitespace, loading default config"); return; }

            try
            {
                var Config_LoadedSerialized = Encoding.UTF8.GetString(Convert.FromBase64String(Config_LoadedBase64));
                var Config_Repopulated = new Config_Reference();
                JsonConvert.PopulateObject(Config_LoadedSerialized, Config_Repopulated, new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace});
                Config_Reference.Config_Loaded = Config_Repopulated;
                API.Logger.Debug("[BleedingInDepth]: (Config_LoadWorld) Complete");

            }
            catch (Exception e) { API.Logger.Debug("[BleedingInDepth]: (Config_LoadWorld) Caught exception when loading config, loading default: {0}", [e.Message]); Config_Reference.Config_Loaded = new Config_Reference(); } //fallback to default config
        }
        

        internal static void Config_SaveDisk()
        {
            API.StoreModConfig(Config_Reference.Config_Loaded, Config_Reference.Config_FilePath);
            API.Logger.Debug("[BleedingInDepth]: (Config_SaveDisk) Complete");
        }


        internal static void Config_SaveWorld()
        {
            API.World.Config.SetString(Config_Reference.Config_FilePath, Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(Config_Reference.Config_Loaded, Formatting.None))));
            API.Logger.Debug("[BleedingInDepth]: (Config_SaveWorld) Complete");
        }


        internal static void Config_Unload()
        {
            if (Config_Reference.Config_Loaded is not null) { Config_Reference.Config_Loaded = null; }
        }


        internal static void Config_ValidateValue() //various config validation checks; TODO: I can probably conbine these methods somewhat
        {
            if (Config_Reference.Config_Loaded is null) { API.Logger.Debug("[Bleedingindepth] (Config_ValidateValues) Config_Loaded was missing or invalid, skipping"); return; }
            Config_ValidateList();
            Config_ClampValue(Config_Reference.Config_Loaded);
            Config_PopulateList();
        }


        internal static void Config_ClampValue(object config_instance, HashSet<object>? instance_Visited = null) //iterate through config and clamp invalid values; TODO: add dictonary clamping?
        {
            try
            {
                instance_Visited ??= new HashSet<object>(ReferenceEqualityComparer.Instance); //prevent recursive checks on already verified members
                if (!instance_Visited.Add(config_instance)) { return; }
                foreach (var instance_Property in config_instance.GetType().GetProperties()) //iterate through all members in the config
                {
                    if (!instance_Property.CanRead || !instance_Property.CanWrite) { continue; } //check if property can be read/writen > has valid values > is class(recursively check nested) > has valid attribute metadata
                    var prop_type = instance_Property.PropertyType;
                    object? instance_raw = instance_Property.GetValue(config_instance);
                    if (instance_raw is null) continue;

                    if (prop_type.IsClass && prop_type != typeof(string) && !typeof(IEnumerable).IsAssignableFrom(prop_type) && prop_type.Namespace == "BleedingInDepth.config") //check if current property is a class which nests config values; run recursive Config_ClampValue inside class
                    {
                        Config_ClampValue(instance_raw, instance_Visited);
                        continue;
                    }

                    var range = instance_Property.GetCustomAttribute<RangeAttribute>();
                    if (range is null) continue;

                    if (prop_type == typeof(int) || prop_type == typeof(long) || prop_type == typeof(float) || prop_type == typeof(double)) //check if current property has valid type for clamping and apply clamp
                    {
                        double value = Convert.ToDouble(instance_raw);
                        double valMin = Convert.ToDouble(range.Minimum);
                        double valMax = Convert.ToDouble(range.Maximum);

                        value = Math.Clamp(value, valMin, valMax);
                        instance_Property.SetValue(config_instance, Convert.ChangeType(value, instance_Property.PropertyType));
                        continue;
                    }
                }
            }
            catch (Exception e) { API.Logger.Debug("[Bleedingindepth] (Config_ClampValues) Threw error: {0}", [e.Message]); }
        }


        internal static void Config_ValidateList() //TODO: freeze dictinary once values are checked?
        {
            if (Config_Reference.Config_Loaded.Config_Effect.sFX_Drip_Acc.Drip_Materials is null) { API.Logger.Debug("[BleedingInDepth]: (Config_FixList) Defaulted Bleed_Effect_SoundMaterials"); }
            Config_Reference.Config_Loaded.Config_Effect.sFX_Drip_Acc.Drip_Materials ??= [.. BID_ModConfig.Config_Effect.SFX_Drip.Default_Drip_Materials.Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];
            API.Logger.Debug("[Bleedingindepth]: (Config_ValidateList) Complete");
        }


        internal static void Config_PopulateList()
        {
            if (Config_Reference.Config_Loaded?.Config_BleedReport.List_Report_SeverityThreashold is null || Config_Reference.Config_Loaded.Config_BleedReport.List_Report_SeverityThreashold.Count == 0)
            {
                Config_Reference.Config_Loaded.Config_BleedReport.List_Report_SeverityThreashold.AddRange([
                new BleedSeverityThreashold { BleedLevel = BID_ModConfig.Config_BleedReport.Default_BleedLevel_Severe, Severity = "Severe" },
                new BleedSeverityThreashold { BleedLevel = BID_ModConfig.Config_BleedReport.Default_BleedLevel_Moderate, Severity = "Moderate" },
                new BleedSeverityThreashold { BleedLevel = BID_ModConfig.Config_BleedReport.Default_BleedLevel_Minor, Severity = "Minor" },
                new BleedSeverityThreashold { BleedLevel = BID_ModConfig.Config_BleedReport.Default_BleedLevel_Trivial, Severity = "Trivial" }
                ]);
            }
        }
    }
}
