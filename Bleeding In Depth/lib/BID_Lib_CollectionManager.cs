using BleedingInDepth.config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Frozen;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using BleedingInDepth.lib;
using BleedingInDepth;

namespace BleedingInDepth.lib
{
    internal class BID_Lib_CollectionManager 
    {
        internal required ICoreAPI API;

        internal static void Dicitionary_Freeze()
        {
            BID_Lib_FunctionsBleed.DamageType_Dict_ConfigCache = Config_Reference.Config_Loaded.Config_DamageType.Dict_DamageType.ToFrozenDictionary();
        }



        internal class HandleDictionary() //this is probably unneeded now as the config itself is building the dictionary; leaving here in case i can use it again
        {
            //cached dictionarys
            internal static Dictionary<string, Dictionary<string, float>>? Dict_DamageTypeValues;

            internal static List<string> Dict_ToBuildQueue = ["Dict_DamageTypeValues"]; //place all dictionarys here. World start will build and remove from list. List can be repopulated with dictionarys that need to be rebuilt?.




            internal static void Dict_BuildQueue()//called on world start. itterates through all dictionarys and attempts to build them
            {
                foreach (var kvp in Dict_ToBuildQueue)
                {
                    Dict_BuildReferred(kvp);
                }
            }


            internal static bool Dict_BuildVerify(string RequestedDictName) //invalid check -> attempt to rebuild -> return build success (t/f)
            {
                try
                {
                    switch (RequestedDictName)
                    {
                        case null: { BID_Lib_FunctionsGeneral.Log_Error("Recieved null RequestedDictName: fallback to vanilla damage system"); return false; }
                        case "Dict_DamageTypeValues": { Dict_DamageTypeValues ??= Dict_BuildReferred(RequestedDictName); return Dict_DamageTypeValues != null && Dict_DamageTypeValues.Count > 0; }
                        default: { BID_Lib_FunctionsGeneral.Log_Debug("Dict verificaiton failed, Dict type does not exist: {0}", loggers: [RequestedDictName]); return false; }
                    }
                }
                catch { BID_Lib_FunctionsGeneral.Log_Error("Dict verificaiton failed: {0}", loggers: [RequestedDictName]); return false; }
            }


            private static Dictionary<string, Dictionary<string, float>>? Dict_BuildReferred(string RequestedDictName) //attempt to build a specific  dictionary
            {
                if (Config_Reference.Config_Loaded == null) { BID_Lib_FunctionsGeneral.Log_Error("Config file is missing. Falling back to vanilla damage system."); return []; }
                BID_Lib_FunctionsGeneral.Log_Debug("Building dict: {0}", loggers: [RequestedDictName]);
                switch (RequestedDictName) //TODO: convert to for loops
                {
                    case null: { BID_Lib_FunctionsGeneral.Log_Error("Recived null RequestedDictName, fallback to vanilla damage system"); return []; }
                    case "Dict_DamageTypeValues":
                        {
                            var Dict_Built = new Dictionary<string, Dictionary<string, float>> //set usable DamageTypes and their associated multipliers from the loaded config; DamageType[Modifier]
                        {
                            {
                                "Slash", new Dictionary<string, float>
                                {
                                    { "Direct",  1f}, //Config_Reference.Config_Loaded.Config_DamageType.DamageType_Slash_Direct
                                    { "Bleed",  1f}, //Config_Reference.Config_Loaded.Config_DamageType.DamageType_Slash_Bleed
                                    { "InternalBleed", 1f }, //Config_Reference.Config_Loaded.Config_DamageType.DamageType_Slash_InternalBleed
                                    { "InternalStartValue", 1f } //Config_Reference.Config_Loaded.Config_DamageType.DamageType_Slash_InternalStartValue
                                }
                            },
                            {
                                "Blunt", new Dictionary<string, float>
                                {
                                    { "Direct", 1f }, //Config_Reference.Config_Loaded.Config_DamageType.DamageType_Blunt_Direct
                                    { "Bleed", 1f }, //Config_Reference.Config_Loaded.Config_DamageType.DamageType_Blunt_Bleed
                                    { "InternalBleed", 1f }, //Config_Reference.Config_Loaded.Config_DamageType.DamageType_Blunt_InternalBleed
                                    { "InternalStartValue", 1f } //Config_Reference.Config_Loaded.Config_DamageType.DamageType_Blunt_InternalStartValue
                                }
                            },
                            {
                                "Pierce", new Dictionary<string, float>
                                {
                                    { "Direct", 1f }, //Config_Reference.Config_Loaded.Config_DamageType.DamageType_Pierce_Direct
                                    { "Bleed", 1f }, //Config_Reference.Config_Loaded.Config_DamageType.DamageType_Pierce_Bleed
                                    { "InternalBleed", 1f }, //Config_Reference.Config_Loaded.Config_DamageType.DamageType_Pierce_InternalBleed
                                    { "InternalStartValue", 1f } //Config_Reference.Config_Loaded.Config_DamageType.DamageType_Pierce_InternalStartValue
                                }
                                },
                            {
                                "Poison", new Dictionary<string, float>
                                {
                                    { "Direct", 1f }, //Config_Reference.Config_Loaded.Config_DamageType.DamageType_Poison_Direct
                                    { "Bleed", 1f }, //Config_Reference.Config_Loaded.Config_DamageType.DamageType_Poison_Bleed
                                    { "InternalBleed", 1f }, //Config_Reference.Config_Loaded.Config_DamageType.DamageType_Poison_InternalBleed
                                    { "InternalStartValue", 1f } //Config_Reference.Config_Loaded.Config_DamageType.DamageType_Poison_InternalStartValue
                                }
                            }
                        };
                            return Dict_Built;
                        }
                    default: { BID_Lib_FunctionsGeneral.Log_Debug("Failed to build dicitonary: ({0})", loggers: [RequestedDictName]); Dict_DamageTypeValues = []; return []; }
                }
            }
        }


        internal class HandleList()
        {

        }
    }
}
