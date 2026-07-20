using Newtonsoft.Json;
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

namespace BleedingInDepth.config
{
    internal class BID_Config_Manager
    {
        private static ICoreAPI API = BID_VarRef.API;



        internal static void Config_Conjure() //call to start config load
        {
            try
            {
                if (Config_Reference.Config_Loaded is not null) { API.Logger.Debug("[{0}]: (Config_Conjure) {1} config already loaded, skipping", [BID_VarRef.ModName, API.Side]); return; }
                if (API.Side == EnumAppSide.Server) { API.Logger.Debug("[{0}]: (Config_Conjure) Caught server config loading", [BID_VarRef.ModName]); Config_LoadDisk(); Config_SaveWorld(); }
                else { API.Logger.Debug("[{0}]: (Config_Conjure) Caught non-server config loading, loading from World config", [BID_VarRef.ModName]); Config_LoadWorld(); }
            }
            catch (Exception e) { API.Logger.Error("[{0}]: (Config_Conjure) Exception caught: {1}", [BID_VarRef.ModName, e.Message]); }
        }


        internal static void Config_LoadDisk() //create config for server
        {
            try
            {
                Config_Reference.Config_Loaded = API.LoadModConfig<Config_Reference>(Config_Reference.Config_FilePath);
                API.Logger.Debug($"{(Config_Reference.Config_Loaded is null ? $"[{BID_VarRef.ModName}]: (Config_LoadDisk)Found no '{API.Side}' config, creating new" : $"[{BID_VarRef.ModName}]: (Config_LoadDisk) Found '{API.Side}' config, loading")}");
                Config_Reference.Config_Loaded ??= new Config_Reference();
                Config_ValidateValue();
                Config_SaveDisk();
                Config_CheckModCompat();
            }
            catch (Exception e) { API.Logger.Error("[{0}]: (Config_LoadDisk) Exception caught, loading default config: {1}", [BID_VarRef.ModName, e.Message]); Config_Reference.Config_Loaded = new Config_Reference(); }
            API.Logger.Debug("[{0}]: (Config_LoadDisk) Complete", [BID_VarRef.ModName]);
        }


        internal static void Config_LoadWorld() //load config from world config for clients
        {
            try
            {
                var Config_LoadedBase64 = API.World.Config.GetString(Config_Reference.Config_FilePath);
                if (string.IsNullOrWhiteSpace(Config_LoadedBase64)) { Config_Reference.Config_Loaded = new Config_Reference(); Config_ValidateValue(); API.Logger.Debug("[{0}]: (Config_LoadWorld) Config loaded was null or whitespace, loading default config", [BID_VarRef.ModName]); return; }

                var Config_LoadedSerialized = Encoding.UTF8.GetString(Convert.FromBase64String(Config_LoadedBase64));
                var Config_Repopulated = new Config_Reference();
                JsonConvert.PopulateObject(Config_LoadedSerialized, Config_Repopulated, new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace});
                Config_Reference.Config_Loaded = Config_Repopulated;
            }
            catch (Exception e) { API.Logger.Error("[{0}]: (Config_LoadWorld) Exception caught, loading default config: {1}", [BID_VarRef.ModName, e.Message]); Config_Reference.Config_Loaded = new Config_Reference(); }
            API.Logger.Debug("[{0}]: (Config_LoadWorld) Complete", [BID_VarRef.ModName]);
        }
        

        internal static void Config_SaveDisk()
        {
            try
            {
                API.StoreModConfig(Config_Reference.Config_Loaded, Config_Reference.Config_FilePath);
                API.Logger.Debug("[{0}]: (Config_SaveDisk) Complete", [BID_VarRef.ModName]);
            }
            catch (Exception e) { API.Logger.Error("[{0}]: (Config_SaveDisk) Exception caught: {1}", [BID_VarRef.ModName, e.Message]); }
        }


        internal static void Config_SaveWorld()
        {
            try
            {
                API.World.Config.SetString(Config_Reference.Config_FilePath, Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(Config_Reference.Config_Loaded, Formatting.None))));
                API.Logger.Debug("[{0}]: (Config_SaveWorld) Complete", [BID_VarRef.ModName]);
            }
            catch (Exception e) { API.Logger.Error("[{0}]: (Config_SaveWorld) Exception caught: {1}", [BID_VarRef.ModName, e.Message]); }
        }


        internal static void Config_Unload()
        {
            try
            {
                if (Config_Reference.Config_Loaded is not null) { Config_Reference.Config_Loaded = null; }
                API.Logger.Debug("[{0}]: (Config_Unload) Complete", [BID_VarRef.ModName]);
            }
            catch (Exception e) { API.Logger.Error("[{0}]: (Config_Unload) Exception caught: {1}", [BID_VarRef.ModName, e.Message]); }
        }


        internal static void Config_ValidateValue() //various config validation checks
        {
            if (Config_Reference.Config_Loaded is null) { API.Logger.Debug("[(0}]: (Config_ValidateValue) Config_Loaded was missing or invalid, skipping", [BID_VarRef.ModName]); return; }
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

                    if (prop_type.IsClass && prop_type != typeof(string) && !typeof(IEnumerable).IsAssignableFrom(prop_type) && prop_type.Namespace == $"{BID_VarRef.ModName}.config") //check if current property is a class which nests config values; run recursive Config_ClampValue inside class
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
            catch (Exception e) { API.Logger.Debug("[{0}]: (Config_ValidateClamp) Exception caught: {1}", [BID_VarRef.ModName, e.Message]); }
        }


        internal static void Config_ValidateList()
        {
            try
            {
                //SFX: Drip Materials
                if (Config_Reference.Config_Loaded.Config_Effect.SFX_Drip_Acc.Drip_Materials is null)
                {
                    API.Logger.Debug("[{0}]: (Config_ValidateList) Defaulted SFX_Drip.Bleed_Effect_SoundMaterials", [BID_VarRef.ModName]);
                    Config_Reference.Config_Loaded.Config_Effect.SFX_Drip_Acc.Drip_Materials ??= [.. BID_Config_Main.Config_Effect.SFX_Drip.Default_Drip_Materials.Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];
                }

                //BleedReport: Severity Threashold
                if (Config_Reference.Config_Loaded.Config_BleedReport.List_BleedReport_DPS_SeverityThreashold is null || Config_Reference.Config_Loaded.Config_BleedReport.List_BleedReport_DPS_SeverityThreashold.Count == 0)
                {
                    API.Logger.Debug("[{0}]: (Config_ValidateList) Defaulted List_BleedReport_DPS_SeverityThreashold", [BID_VarRef.ModName]);
                    Config_Reference.Config_Loaded.Config_BleedReport.List_BleedReport_DPS_SeverityThreashold = [];
                    Config_Reference.Config_Loaded.Config_BleedReport.List_BleedReport_DPS_SeverityThreashold.AddRange([
                    new BID_Config_Main.Config_BleedReport.BleedSeverityThreashold { BleedLevel = BID_Config_Main.Config_BleedReport.Default_BleedReport_DPS_Severe, Severity = "Severe" },
                    new BID_Config_Main.Config_BleedReport.BleedSeverityThreashold { BleedLevel = BID_Config_Main.Config_BleedReport.Default_BleedReport_DPS_Moderate, Severity = "Moderate" },
                    new BID_Config_Main.Config_BleedReport.BleedSeverityThreashold { BleedLevel = BID_Config_Main.Config_BleedReport.Default_BleedReport_DPS_Minor, Severity = "Minor" },
                    new BID_Config_Main.Config_BleedReport.BleedSeverityThreashold { BleedLevel = BID_Config_Main.Config_BleedReport.Default_BleedReport_DPS_Trivial, Severity = "Trivial" }
                    ]);
                }
            }
            catch (Exception e) { API.Logger.Error("[{0}]: (Config_ValidateList) Exception caught: {0}", [BID_VarRef.ModName, e.Message]); }
            API.Logger.Debug("[{0}]: (Config_ValidateList) Complete", [BID_VarRef.ModName]);
        }


        internal static void Config_CheckModCompat()
        {
            if (BID_VarRef.API.ModLoader.GetMod("combatoverhaul") is not null) //TODO: create a list with any mod that changes damageTypes to not just blunt, then check if any of the mods are loaded and apply enable damagetypes
            {//TODO: find a way to recognize if damagetypes are used (through CO or other mods) and autoset compat instead of using a list of mods
                Config_Reference.Config_Loaded.Config_System.System_Damage_Acc.UseDamageTypeCompat = true;
            }
        }


        internal static bool Config_LoadDefaultIfError(bool hardReset = false)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(Config_Reference.Config_Loaded?.ToString()) && !hardReset) { return false; }
                API.Logger.Error("[{0}]: (Config_LoadDefaultIfError) Config was missing, null or malformed; Loading default config", [BID_VarRef.ModName]);
                Config_Reference.Config_Loaded = new Config_Reference();
                return true;
            }
            catch (Exception e) { API.Logger.Error("[{0}]: (Config_LoadDefaultIfError) Exception caught: {1}", [BID_VarRef.ModName, e.Message]); return false; }
        }
    }
}
