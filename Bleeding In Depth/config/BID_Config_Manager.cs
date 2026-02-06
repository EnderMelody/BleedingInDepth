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
using static System.Net.Mime.MediaTypeNames;

namespace BleedingInDepth.config
{
    internal class BID_Config_Manager
    {
        private static ICoreAPI API = BleedingInDepthModSystem.API;



        internal static void Config_Conjure() //call to start config load
        {
            try
            {
                if (Config_Reference.Config_Loaded is not null) { API.Logger.Debug("[BleedingInDepth]: (Config_Conjure) {0} config already loaded, skipping", [API.Side]); return; }
                if (API.Side == EnumAppSide.Server) { API.Logger.Debug("[BleedingInDepth]: (Config_Conjure) Caught server config loading"); Config_LoadDisk(); Config_SaveWorld(); }
                else { API.Logger.Debug("[BleedingInDepth]: (Config_Conjure) Caught non-server config loading, loading from World config"); Config_LoadWorld(); }
            }
            catch (Exception e) { API.Logger.Error("[BleedingInDepth]: (Config_Conjure) Exception caught: {0}", [e.Message]); }
        }


        internal static void Config_LoadDisk() //create config for server
        {
            try
            {
                Config_Reference.Config_Loaded = API.LoadModConfig<Config_Reference>(Config_Reference.Config_FilePath);
                API.Logger.Debug($"{(Config_Reference.Config_Loaded is null ? $"[BleedingInDepth]: (Config_LoadDisk)Found no '{API.Side}' config, creating new" : $"[BleedingInDepth]: (Config_LoadDisk) Found '{API.Side}' config, loading")}"); //TODO: replace with lang file
                Config_Reference.Config_Loaded ??= new Config_Reference();
                Config_ValidateValue();
                Config_SaveDisk();
            }
            catch (Exception e) { API.Logger.Error("[BleedingInDepth]: (Config_LoadDisk) Exception caught, loading default config: {0}", [e.Message]); Config_Reference.Config_Loaded = new Config_Reference(); }
            API.Logger.Debug("[BleedingInDepth]: (Config_LoadDisk) Complete");
        }


        internal static void Config_LoadWorld() //load config from world config for clients
        {
            try
            {
                var Config_LoadedBase64 = API.World.Config.GetString(Config_Reference.Config_FilePath);
                if (string.IsNullOrWhiteSpace(Config_LoadedBase64)) { Config_Reference.Config_Loaded = new Config_Reference(); Config_ValidateValue(); API.Logger.Debug("[BleedingInDepth]: (Config_LoadWorld) Config loaded was null or whitespace, loading default config"); return; }

                var Config_LoadedSerialized = Encoding.UTF8.GetString(Convert.FromBase64String(Config_LoadedBase64));
                var Config_Repopulated = new Config_Reference();
                JsonConvert.PopulateObject(Config_LoadedSerialized, Config_Repopulated, new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace});
                Config_Reference.Config_Loaded = Config_Repopulated;
            }
            catch (Exception e) { API.Logger.Error("[BleedingInDepth]: (Config_LoadWorld) Exception caught, loading default config: {0}", [e.Message]); Config_Reference.Config_Loaded = new Config_Reference(); }
            API.Logger.Debug("[BleedingInDepth]: (Config_LoadWorld) Complete");
        }
        

        internal static void Config_SaveDisk()
        {
            try
            {
                API.StoreModConfig(Config_Reference.Config_Loaded, Config_Reference.Config_FilePath);
                API.Logger.Debug("[BleedingInDepth]: (Config_SaveDisk) Complete");
            }
            catch (Exception e) { API.Logger.Error("[BleedingInDepth]: (Config_SaveDisk) Exception caught: {0}", [e.Message]); }
        }


        internal static void Config_SaveWorld()
        {
            try
            {
                API.World.Config.SetString(Config_Reference.Config_FilePath, Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(Config_Reference.Config_Loaded, Formatting.None))));
                API.Logger.Debug("[BleedingInDepth]: (Config_SaveWorld) Complete");
            }
            catch (Exception e) { API.Logger.Error("[BleedingInDepth]: (Config_SaveWorld) Exception caught: {0}", [e.Message]); }
        }


        internal static void Config_Unload()
        {
            try
            {
                if (Config_Reference.Config_Loaded is not null) { Config_Reference.Config_Loaded = null; }
                API.Logger.Debug("[BleedingInDepth]: (Config_Unload) Complete");
            }
            catch (Exception e) { API.Logger.Error("[BleedingInDepth]: (Config_Unload) Exception caught: {0}", [e.Message]); }
        }


        internal static void Config_ValidateValue() //various config validation checks
        {
            if (Config_Reference.Config_Loaded is null) { API.Logger.Debug("[Bleedingindepth]: (Config_ValidateValue) Config_Loaded was missing or invalid, skipping"); return; }
            Config_ValidateList();
            Config_ValidateClamp(Config_Reference.Config_Loaded);
        }


        //iterate through config and clamp invalid values; TODO: add dictonary clamping?
        internal static void Config_ValidateClamp(object config_instance, HashSet<object>? instance_Visited = null)//TODO: find way to include complete debug message only on last loop
        {
            try
            {
                instance_Visited ??= new HashSet<object>(ReferenceEqualityComparer.Instance); //prevent recursive checks on already verified members
                if (!instance_Visited.Add(config_instance)) { return; }
                foreach (var instance_Property in config_instance.GetType().GetProperties()) //iterate through all members in the config
                {
                    if (!instance_Property.CanRead || !instance_Property.CanWrite) { continue; } //check if property can be read/writen -> has valid values -> is class(recursively check nested) -> has valid attribute metadata
                    var prop_type = instance_Property.PropertyType;
                    object? instance_raw = instance_Property.GetValue(config_instance);
                    if (instance_raw is null) continue;

                    if (prop_type.IsClass && prop_type != typeof(string) && !typeof(IEnumerable).IsAssignableFrom(prop_type) && prop_type.Namespace == "BleedingInDepth.config") //check if current property is a class which nests config values; run recursive Config_ClampValue inside class
                    {
                        Config_ValidateClamp(instance_raw, instance_Visited);
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
            catch (Exception e) { API.Logger.Debug("[Bleedingindepth]: (Config_ValidateClamp) Exception caught: {0}", [e.Message]); }
        }


        internal static void Config_ValidateList()
        {
            try
            {
                //SFX: Drip Materials
                if (Config_Reference.Config_Loaded.Config_Effect.SFX_Drip_Acc.Drip_Materials is null)
                {
                    API.Logger.Debug("[BleedingInDepth]: (Config_ValidateList) Defaulted SFX_Drip.Bleed_Effect_SoundMaterials");
                    Config_Reference.Config_Loaded.Config_Effect.SFX_Drip_Acc.Drip_Materials ??= [.. BID_ModConfig.Config_Effect.SFX_Drip.Default_Drip_Materials.Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];
                }

                //BleedReport: Severity Threashold
                if (Config_Reference.Config_Loaded.Config_BleedReport.List_BleedReport_DPS_SeverityThreashold is null || Config_Reference.Config_Loaded.Config_BleedReport.List_BleedReport_DPS_SeverityThreashold.Count == 0)
                {
                    API.Logger.Debug("[BleedingInDepth]: (Config_ValidateList) Defaulted List_BleedReport_DPS_SeverityThreashold");
                    Config_Reference.Config_Loaded.Config_BleedReport.List_BleedReport_DPS_SeverityThreashold = [];
                    Config_Reference.Config_Loaded.Config_BleedReport.List_BleedReport_DPS_SeverityThreashold.AddRange([
                    new BleedSeverityThreashold { BleedLevel = BID_ModConfig.Config_BleedReport.Default_BleedReport_DPS_Severe, Severity = "Severe" },
                    new BleedSeverityThreashold { BleedLevel = BID_ModConfig.Config_BleedReport.Default_BleedReport_DPS_Moderate, Severity = "Moderate" },
                    new BleedSeverityThreashold { BleedLevel = BID_ModConfig.Config_BleedReport.Default_BleedReport_DPS_Minor, Severity = "Minor" },
                    new BleedSeverityThreashold { BleedLevel = BID_ModConfig.Config_BleedReport.Default_BleedReport_DPS_Trivial, Severity = "Trivial" }
                    ]);
                }
                if (Config_Reference.Config_Loaded.Config_BleedReport.List_BleedReport_GainedBleed_SeverityThreashold is null || Config_Reference.Config_Loaded.Config_BleedReport.List_BleedReport_GainedBleed_SeverityThreashold.Count == 0)
                {
                    API.Logger.Debug("[BleedingInDepth]: (Config_ValidateList) Defaulted List_BleedReport_GainedBleed_SeverityThreashold");
                    Config_Reference.Config_Loaded.Config_BleedReport.List_BleedReport_GainedBleed_SeverityThreashold = [];
                    Config_Reference.Config_Loaded.Config_BleedReport.List_BleedReport_GainedBleed_SeverityThreashold.AddRange([
                    new BleedSeverityThreashold { BleedLevel = BID_ModConfig.Config_BleedReport.Default_BleedReport_GainedBleed_Severe, Severity = "Severe" },
                    new BleedSeverityThreashold { BleedLevel = BID_ModConfig.Config_BleedReport.Default_BleedReport_GainedBleed_Moderate, Severity = "Moderate" },
                    new BleedSeverityThreashold { BleedLevel = BID_ModConfig.Config_BleedReport.Default_BleedReport_GainedBleed_Minor, Severity = "Minor" },
                    new BleedSeverityThreashold { BleedLevel = BID_ModConfig.Config_BleedReport.Default_BleedReport_GainedBleed_Trivial, Severity = "Trivial" }
                    ]);
                }
            }
            catch (Exception e) { API.Logger.Error("[BleedingInDepth]: (Config_ValidateList) Exception caught: {0}", [e.Message]); }
            API.Logger.Debug("[BleedingInDepth]: (Config_ValidateList) Complete");
        }


        internal static bool Config_LoadDefaultIfError(bool hardReset = false)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(Config_Reference.Config_Loaded?.ToString()) && !hardReset) { return false; }
                API.Logger.Error("[BleedingInDepth]: (Config_LoadDefaultIfError) Config was missing, null or malformed; Loading default config");
                Config_Reference.Config_Loaded = new Config_Reference();
                return true;
            }
            catch (Exception e) { API.Logger.Error("[BleedingInDepth]: (Config_LoadDefaultIfError) Exception caught: {0}", [e.Message]); return false; }
        }
    }
}
