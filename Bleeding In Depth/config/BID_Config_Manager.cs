using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace BleedingInDepth.config
{
    internal class BID_Config_Manager
    {
        private static ICoreAPI API = BID_VarRef.API;
        //private static ICoreClientAPI API_Client = BID_VarRef.API_Client;
        //private static ICoreServerAPI API_Server = BID_VarRef.API_Server;



        /// <summary>
        /// begins the config loading proccess
        /// </summary>
        internal static void Config_Conjure()
        {
            if (Config_Reference.Config_Loaded is not null) { API.Logger.Debug("[{0}]: (Config_Conjure) {1} config already loaded, skipping", [BID_VarRef.ModName, API.Side]); return; }
            if (API.Side == EnumAppSide.Server)
            {
                API.Logger.Debug("[{0}]: (Config_Conjure) Caught server config loading", [BID_VarRef.ModName]);
                Config_LoadDisk(); Config_SaveDisk(); Config_SaveWorld();
            }
            else { API.Logger.Debug("[{0}]: (Config_Conjure) Caught non-server config loading, loading from World config", [BID_VarRef.ModName]); Config_LoadWorld(); }
        }


        //config loading
        internal static void Config_LoadDisk()
        {
            Config_Reference.Config_Loaded = API.LoadModConfig<Config_Reference>(Config_Reference.Config_FilePath);
            API.Logger.Debug($"{(Config_Reference.Config_Loaded is null ? $"[{BID_VarRef.ModName}]: (Config_LoadDisk) Found no '{API.Side}' config, creating new" : $"[{BID_VarRef.ModName}]: (Config_LoadDisk) Found '{API.Side}' config, loading")}");
            Config_ResetIfError(); Config_Validate(); Config_ModCompat();

            API.Logger.Debug("[{0}]: (Config_LoadDisk) Complete", [BID_VarRef.ModName]);
        }


        internal static void Config_LoadWorld()
        {
            var Config_LoadedBase64 = API.World.Config.GetString(Config_Reference.Config_FilePath);
            if (string.IsNullOrWhiteSpace(Config_LoadedBase64)) { Config_Reference.Config_Loaded = new Config_Reference(); Config_Validate(); API.Logger.Debug("[{0}]: (Config_LoadWorld) Config loaded was null or whitespace, loading default config", [BID_VarRef.ModName]); return; }

            var Config_LoadedSerialized = Encoding.UTF8.GetString(Convert.FromBase64String(Config_LoadedBase64));
            var Config_Repopulated = new Config_Reference();
            JsonConvert.PopulateObject(Config_LoadedSerialized, Config_Repopulated, new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace });
            Config_Reference.Config_Loaded = Config_Repopulated;
        }


        //config saving
        internal static void Config_SaveDisk()
        {
            API.StoreModConfig(Config_Reference.Config_Loaded, Config_Reference.Config_FilePath);
        }


        internal static void Config_SaveWorld()
        {
            API.World.Config.SetString(Config_Reference.Config_FilePath, Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(Config_Reference.Config_Loaded, Formatting.None))));
        }


        //config validation
        internal static void Config_Validate()
        {
            Config_ResetIfError();
            Config_Validate_Clamp(Config_Reference.Config_Loaded);
            Config_Validate_List();
        }


        internal static void Config_Validate_Clamp(object config_Instance, HashSet<object>? instance_Visited = null)
        {
            instance_Visited ??= new HashSet<object>(ReferenceEqualityComparer.Instance); //prevent recursive checks on already verified members
            if (!instance_Visited.Add(config_Instance)) { return; }
            foreach (var instance_Property in config_Instance.GetType().GetProperties()) //iterate through all members in the config
            {
                if (!instance_Property.CanRead || !instance_Property.CanWrite) { continue; } //check if property can be read/writen -> has valid values -> is class(recursively check nested) -> has valid attribute metadata
                var prop_type = instance_Property.PropertyType;
                object? instance_raw = instance_Property.GetValue(config_Instance);
                if (instance_raw is null) continue;

                if (prop_type.IsClass && prop_type != typeof(string) && !typeof(IEnumerable).IsAssignableFrom(prop_type) && prop_type.Namespace == $"{BID_VarRef.ModName}.config") //check if current property is a class which nests config values; run recursive Config_ClampValue inside class
                {
                    Config_Validate_Clamp(instance_raw, instance_Visited);
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
                    instance_Property.SetValue(config_Instance, Convert.ChangeType(value, instance_Property.PropertyType));
                    continue;
                }
            }
        }


        internal static void Config_Validate_List()
        {
            //SFX: Drip Materials
            if (Config_Reference.Config_Loaded.Config_Effect.SFX_Drip_Acc.Drip_Materials is null)
            {
                API.Logger.Debug("[{0}]: (Config_Validate_List) Defaulted SFX_Drip.Bleed_Effect_SoundMaterials", [BID_VarRef.ModName]);
                Config_Reference.Config_Loaded.Config_Effect.SFX_Drip_Acc.Drip_Materials ??= [.. BID_Config_Main.Config_Effect.SFX_Drip.Default_Drip_Materials.Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];
            }

            //BleedReport: Severity Threashold
            if (Config_Reference.Config_Loaded.Config_BleedReport.List_BleedReport_DPS_SeverityThreashold is null || Config_Reference.Config_Loaded.Config_BleedReport.List_BleedReport_DPS_SeverityThreashold.Count == 0)
            {
                API.Logger.Debug("[{0}]: (Config_Validate_List) Defaulted List_BleedReport_DPS_SeverityThreashold", [BID_VarRef.ModName]);
                Config_Reference.Config_Loaded.Config_BleedReport.List_BleedReport_DPS_SeverityThreashold = [];
                Config_Reference.Config_Loaded.Config_BleedReport.List_BleedReport_DPS_SeverityThreashold.AddRange([
                    new BID_Config_Main.Config_BleedReport.BleedSeverityThreashold { BleedLevel = BID_Config_Main.Config_BleedReport.Default_BleedReport_DPS_Severe, Severity = "Severe" },
                    new BID_Config_Main.Config_BleedReport.BleedSeverityThreashold { BleedLevel = BID_Config_Main.Config_BleedReport.Default_BleedReport_DPS_Moderate, Severity = "Moderate" },
                    new BID_Config_Main.Config_BleedReport.BleedSeverityThreashold { BleedLevel = BID_Config_Main.Config_BleedReport.Default_BleedReport_DPS_Minor, Severity = "Minor" },
                    new BID_Config_Main.Config_BleedReport.BleedSeverityThreashold { BleedLevel = BID_Config_Main.Config_BleedReport.Default_BleedReport_DPS_Trivial, Severity = "Trivial" }
                ]);
            }
        }


        //config modifiers
        internal static void Config_Unload()
        {
            if (Config_Reference.Config_Loaded is not null) { Config_Reference.Config_Loaded = null; }
        }


        internal static void Config_ResetIfError(bool hardReset = false)
        {
            if (!string.IsNullOrWhiteSpace(Config_Reference.Config_Loaded?.ToString()) || hardReset) { return; }
            else
            {
                API.Logger.Error("[{0}]: (Config_LoadDefaultIfError) Config was missing, null or malformed; Loading default config", [BID_VarRef.ModName]);
                Config_Reference.Config_Loaded = new Config_Reference();
            }
        }


        internal static void Config_ModCompat() //TODO: create a list with any mod that changes damageTypes to not just blunt, then check if any of the mods are loaded and apply enable damagetypes
        {//TODO: find a way to recognize if damagetypes are used (through CO or other mods) and autoset compat instead of using a list of mods
            if (API.ModLoader.GetMod("combatoverhaul") is not null || API.ModLoader.GetMod("combatoverhaulfork") is not null)
            { Config_Reference.Config_Loaded.Config_System.System_Damage_Acc.UseDamageTypeCompat = true; }
        }
    }
}
