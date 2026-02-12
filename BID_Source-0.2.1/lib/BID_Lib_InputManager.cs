using BleedingInDepth.config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using static BleedingInDepth.lib.BID_Lib_EntityManager;

namespace BleedingInDepth.lib
{
    internal class BID_Lib_InputManager
    {
        private static ICoreAPI API = BleedingInDepthModSystem.API;
        private static ICoreClientAPI clientAPI = BleedingInDepthModSystem.clientAPI;


        internal class Handle_Command
        {
            //called to report bleed level of targeted entity or self; if false/null get targeted entity, true get self -> report bleed level to caller through chat
            internal static TextCommandResult Command_ReportBleed(TextCommandCallingArgs args)
            {
                if (args.Caller?.Entity is not Entity caller) { return TextCommandResult.Error("Entity was missing or null"); }

                Handle_UserInputs.Handle_ReportBleed(caller, args[0] is true);
                return TextCommandResult.Success(); //handler handles error/success message
            }


            internal static TextCommandResult Command_MakeBleed(TextCommandCallingArgs args)//TODO: switch to lang
            {
                Entity? target = null;
                if (args.Caller?.Entity is not Entity caller) { return TextCommandResult.Error("caller was missing or null"); }

                if ((bool)args[2] && caller is EntityPlayer entityPlayer) { target = entityPlayer.EntitySelection?.Entity; }
                if (target is null || !target.HasBehavior<EntityBehavior_Bleed>()) { target = caller; }
                if (target.GetBehavior<EntityBehavior_Bleed>() is not EntityBehavior_Bleed entity_BleedBehavior) { return TextCommandResult.Error("entity was missing BehaviorBleed."); }

                if (args[1] is not true)
                { entity_BleedBehavior.Bleed_CurrentLevel_External += (float)args[0]; }
                else { entity_BleedBehavior.Bleed_CurrentLevel_Internal += (float)args[0]; }
                entity_BleedBehavior.State_BleedReductionFlag = BID_Lib_FunctionsGeneral.Calc_Flag_SetBit(entity_BleedBehavior.State_BleedReductionFlag, 3, false); entity_BleedBehavior.State_BleedReductionFlag = BID_Lib_FunctionsGeneral.Calc_Flag_SetBit(entity_BleedBehavior.State_BleedReductionFlag, 2, false);
                target.WatchedAttributes.MarkPathDirty("BID_SyncTree_State");

                return TextCommandResult.Success($"{target.GetPrefixAndCreatureName()} gained {args[0]} {((bool)args[1] ? "external" : "internal")} bleed.");
            }
        }


        internal static bool Handle_Hotkey(KeyCombination playerInput, string keyCaller) //called on all hotkeys to handle I/O
        {
            switch (keyCaller)
            {
                case ("BID:ReportBleed"): { return Handle_UserInputs.Handle_ReportBleed(clientAPI.World.Player.Entity, clientAPI.World.Player.Entity.Controls.Sneak ? false : true); }
                default: { BID_Lib_FunctionsGeneral.Log_Error("Hotkey key passed is invalid: {0}", loggers: [keyCaller]); return false; }
            }
        }


        private class Handle_UserInputs //called for shared output types between commands and hotkeys
        {
            //called to report bleed level of targeted entity or self; if false/null get targeted entity, true get self -> report bleed level to caller through chat
            internal static bool Handle_ReportBleed(Entity caller, bool? skipCheckEntity)
            {
                Entity? target = null;
                string reportOutput_External;
                string reportOutput_Internal;
                string reportOutput_BleedReduction = "";

                if (skipCheckEntity is not true && caller is EntityPlayer entityPlayer) { target = entityPlayer.EntitySelection?.Entity; } //get looked at entity; if entity is null fallback to self check
                if (target is null || !target.HasBehavior<EntityBehavior_Bleed>()) { target = caller; }

                if (target.GetBehavior<EntityBehavior_Bleed>() is not EntityBehavior_Bleed entity_BleedBehavior) { return false; }
                else
                {
                    if (Config_Reference.Config_Loaded.Config_BleedReport.BleedReport_Detailed) //output detailed or vague
                    {
                        reportOutput_External = Lang.Get("damageReport_BleedNotify_External", [entity_BleedBehavior.Bleed_CurrentLevel_External, target.GetName() ?? target.GetPrefixAndCreatureName()]);
                        reportOutput_Internal = (Config_Reference.Config_Loaded.Config_BleedReport.BleedReport_Internal) ? Lang.Get("damageReport_BleedNotify_Internal", [entity_BleedBehavior.Bleed_CurrentLevel_Internal]) : "";
                    }
                    else
                    {
                        string reportVague_External = "None";
                        string reportVague_Internal = "None";
                        float DPS_External = entity_BleedBehavior.Bleed_CurrentLevel_External * Config_Reference.Config_Loaded.Config_Rate.Rate_Bleed_Acc.Damage_External;
                        float DPS_Internal = entity_BleedBehavior.Bleed_CurrentLevel_Internal * Config_Reference.Config_Loaded.Config_Rate.Rate_Bleed_Acc.Damage_Internal * BID_Lib_FunctionsBleed.normalizer_InternalBleed;

                        foreach (var threashold in Config_Reference.Config_Loaded.Config_BleedReport.List_BleedReport_DPS_SeverityThreashold) { if (DPS_External >= threashold.BleedLevel) { reportVague_External = threashold.Severity; break; } }
                        foreach (var threashold in Config_Reference.Config_Loaded.Config_BleedReport.List_BleedReport_DPS_SeverityThreashold) { if (DPS_Internal >= threashold.BleedLevel) { reportVague_Internal = threashold.Severity; break; } }

                        reportOutput_External = Lang.Get("bleedingindepth:damageReport_BleedNotify_External", [reportVague_External, target.GetName() ?? target.GetPrefixAndCreatureName()]);
                        reportOutput_Internal = (Config_Reference.Config_Loaded.Config_BleedReport.BleedReport_Internal) ? Lang.Get("bleedingindepth:damageReport_BleedNotify_Internal", [reportVague_Internal]) : "";
                    }
                    reportOutput_BleedReduction = (BID_Lib_FunctionsGeneral.Calc_Flag_CheckBit(entity_BleedBehavior.State_BleedReductionFlag, 3) ? " and has bandaged" : (BID_Lib_FunctionsGeneral.Calc_Flag_CheckBit(entity_BleedBehavior.State_BleedReductionFlag, 2) ? " and has ragged" : ""));

                    clientAPI.Event.EnqueueMainThreadTask(() => { clientAPI.TriggerIngameError(caller, "BID:reportbleed", reportOutput_External + reportOutput_Internal + reportOutput_BleedReduction); }, "");
                    BID_Lib_FunctionsGeneral.Log_Debug("Player recieved bleed report for External: {0}; Internal: {1}; State_BleedReduction: {2}", loggers: [entity_BleedBehavior.Bleed_CurrentLevel_External, entity_BleedBehavior.Bleed_CurrentLevel_Internal, entity_BleedBehavior.State_BleedReductionFlag]);
                    return true;
                }
            }
        }
    }
}
