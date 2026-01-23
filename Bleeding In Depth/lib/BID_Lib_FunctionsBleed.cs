using BleedingInDepth.config;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.Server;
using static BleedingInDepth.lib.BID_Lib_EntityManager;

namespace BleedingInDepth.lib
{
    internal class BID_Lib_FunctionsBleed
    {
        private static ICoreAPI API = BleedingInDepthModSystem.API;

        internal static FrozenDictionary<EnumDamageType, Dictionary<string, float>>? DamageType_Dict_ConfigCache;
        private static Dictionary<string, float> Dict_Outer_DamageType = []; //cache last damage type to avoid rebuilding the dict every hit; TODO: remove cache and just call loaded config dictionary; TODO: dont remove cache as this will read from disk every call otherwise; TOOD: nevermind config_reference.Config_Loaded is stored in ram
        internal static float CancelAndReturnValue = 1024.4201f; //Arbitrary number returned to caller function to tell it that bleed conversion failed and to fall back to vanilla damage handler



        //OLD FUNCTION NO LONGER USED; called for converting damage into bleed on hit AND how much of the Bleed_CurrentLevel_External applied from that hit is diverted into Bleed_CurrentLevel_Internal, then applies bleed and returns remaining damage to vanilla handler
        internal static float OLDDONOTUSEBleedDamage_Conversion(Entity entity, float appliedDamage, DamageSource damageSource)
        {
            //TODO: entity validity for bleeding checks; Check if entity has bleed component and if entity is alive; per entity config overrides should go here too once added
            if (entity.GetBehavior<BID_Lib_EntityManager.EntityBehavior_Bleed>() is not BID_Lib_EntityManager.EntityBehavior_Bleed Entity_BleedBehavior) { BID_Lib_FunctionsGeneral.Log_Debug("Entity had null or missing EntityBehavior_Bleed: {0}", loggers: [entity.GetPrefixAndCreatureName()]); return CancelAndReturnValue; }
            if (false) { BID_Lib_FunctionsGeneral.Log_Debug("AAAAAAAAAa"); return CancelAndReturnValue; }

            //specific damageType checks; TODO: switch to case statement?
            if (appliedDamage <= 0) { BID_Lib_FunctionsGeneral.Log_Debug("appliedDamage was not positive! {0}", loggers: [appliedDamage]); return CancelAndReturnValue; }
            if (damageSource.Type == EnumDamageType.Heal) { appliedDamage = BleedDamage_HealAndReduce(entity, appliedDamage); return appliedDamage; }
            if (damageSource.Type == EnumDamageType.Fire) { BleedDamage_Cauterize(entity, appliedDamage); return CancelAndReturnValue; } //fire damage reduces Bleed_CurrentLevel_External then returns to vanilla damage handling; TODO: see if i can inject in entity.ApplyFireDamage or similar method instead

            if (Config_Reference.Config_Loaded.Config_System.System_UseDamageTypeCompat is false && Config_Reference.Config_Loaded.Config_DamageType?.Dict_DamageType?.ContainsKey(damageSource.Type) == true)
            { damageSource.Type = EnumDamageType.SlashingAttack; } //if vanilla updates attacks to use DamageTypes, this toggle will allow the system to use per DamageType values without requiring me to update the mod immediately

            //damageType validity for bleeding checks; check config exists and DamageType's dictionary is valid for bleeding
            if (Config_Reference.Config_Loaded.Config_DamageType.Dict_DamageType is null) { BID_Lib_FunctionsGeneral.Log_Debug("Dict_DamageType is missing or invalid"); return CancelAndReturnValue; }
            if (Config_Reference.Config_Loaded.Config_DamageType?.Dict_DamageType?.TryGetValue(damageSource.Type, out var DamageType_OuterDict) is not true) { BID_Lib_FunctionsGeneral.Log_Debug("Dict_DamageType does not contain damageType and is invalid for bleed: {0}", loggers: [damageSource.Type]); return CancelAndReturnValue; }//TODO: move innerdicts to a cache? frozen dict?
            if (DamageType_OuterDict?.TryGetValue("Direct_Multi", out var _) is not true || DamageType_OuterDict?.TryGetValue("Bleed_Multi_External", out var _) is not true) { BID_Lib_FunctionsGeneral.Log_Debug("Dict_DamageType does not contain innerdict value(s) within Dict_DamageType: {0}", loggers: [damageSource.Type]); return CancelAndReturnValue; } //do not check for internal bleed modifiers as DamageTypes can not have internal bleeding. Instead Skip application of internal if missing
            

            float baseAppliedDamage = appliedDamage; //TODO: better way?
            float bleedToApply_External;
            float bleedToApply_Internal = 0;


            //calc bleed and internal bleed to apply and reduce the direct damage to apply
            try 
            {
                //external bleed
                if (Config_Reference.Config_Loaded.Config_System.System_Bleed_External)
                {
                    bleedToApply_External = MathF.Round(MathF.Max(baseAppliedDamage * DamageType_OuterDict["Bleed_Multi_External"], 0.0f), 4);
                    appliedDamage = MathF.Round(MathF.Max(baseAppliedDamage * DamageType_OuterDict["Direct_Multi"], 0.0f), 4);
                }
                else { BID_Lib_FunctionsGeneral.Log_Debug("Bleed_External is disabled in config"); return CancelAndReturnValue; }

                //internal bleed
                if (Config_Reference.Config_Loaded.Config_System.System_Bleed_Internal)
                {
                    if (DamageType_OuterDict?.TryGetValue("Bleed_Multi_Internal", out var _) is true && DamageType_OuterDict?.TryGetValue("Bleed_ConversionThreshold_Internal", out var _) is true)
                    {
                        if (bleedToApply_External > DamageType_OuterDict["Bleed_ConversionThreshold_Internal"] && DamageType_OuterDict["Bleed_ConversionThreshold_Internal"] >= 0) //TODO: clamp config values and remove last check
                        {
                            if (DamageType_OuterDict["Bleed_Multi_Internal"] != 0)
                            {
                                float curve_Output = BleedDamage_InternalBleedCurve(bleedToApply_External - DamageType_OuterDict["Bleed_ConversionThreshold_Internal"]);
                                bleedToApply_Internal = bleedToApply_External * curve_Output;
                                bleedToApply_External -= bleedToApply_Internal;
                            }
                            else
                            {
                                bleedToApply_Internal = bleedToApply_External;
                                bleedToApply_External = 0;
                            }
                        }
                        else { BID_Lib_FunctionsGeneral.Log_Debug("Damage did not surpass [Bleed_ConversionThreshold_Internal]"); }
                    }
                    else { BID_Lib_FunctionsGeneral.Log_Debug("Dict_DamageType does not contain internal bleed values and is invalid for DamageType: {0}", loggers: [damageSource.Type]); }
                }
                else { BID_Lib_FunctionsGeneral.Log_Debug("Bleed_Internal is disabled in config"); return CancelAndReturnValue; }

                if (entity is EntityPlayer player && Config_Reference.Config_Loaded.Config_BleedReport.Report_BleedCheck_Notify is true) { BleedDamage_NotifyOnHit(player, bleedToApply_External, bleedToApply_Internal, damageSource); }
                BID_Lib_FunctionsGeneral.Log_Debug("Damage modifiers successfully pulled from dictionary and applied");
            }
            catch (Exception e)
            {
                BID_Lib_FunctionsGeneral.Log_Debug("DamageType is not valid for bleeding: {0}; error:");
                BID_Lib_FunctionsGeneral.Log_Error(": {0}", loggers: [e.Message]);
                return CancelAndReturnValue;
            }


            //apply damages
            try
            {
                Entity_BleedBehavior.State_IsRagged = false; Entity_BleedBehavior.State_IsBandaged = false;
                Entity_BleedBehavior.Bleed_CurrentLevel_External += bleedToApply_External;//TODO: does this write to disk? check for nonsaved method to reduce disk writes and only save when unloading entity
                Entity_BleedBehavior.Bleed_CurrentLevel_Internal += bleedToApply_Internal;
                Entity_BleedBehavior.lastBleedSource = damageSource;
            }
            catch (Exception e) { BID_Lib_FunctionsGeneral.Log_Error("Exception while applying bleed: {0}", loggers: [e.Message]); return CancelAndReturnValue; }

            BID_Lib_FunctionsGeneral.Log_Debug("Applied {0} damage, {1} Bleed_External and {2} Bleed_Internal to {3} from {4} which dealt {5} base damage of {6} DamageType", loggers: [appliedDamage, bleedToApply_External, bleedToApply_Internal, entity.GetPrefixAndCreatureName(), damageSource.CauseEntity?.GetPrefixAndCreatureName() ?? damageSource.SourceEntity?.GetPrefixAndCreatureName() ?? "null", baseAppliedDamage, damageSource.Type]);
            return appliedDamage;
        }


        //called on entity healthbehavior.ondamaged; performs damagetype checks -> stores values to be used in post armor bleed conversion -> reduces direct damage applied and resumes vanilla logic
        internal static float BleedDamage_Conversion_Store(Entity entity, float appliedDamage, ref DamageSource damageSource)
        {
            //damagetype verification; check config exists and DamageType's dictionary is valid for bleeding
            if (!Config_Reference.Config_Loaded.Config_System.System_Bleed_External && !Config_Reference.Config_Loaded.Config_System.System_Bleed_Internal) { BID_Lib_FunctionsGeneral.Log_Debug("Bleeding is disabled in config"); return CancelAndReturnValue; }
            if (appliedDamage <= 0f) { BID_Lib_FunctionsGeneral.Log_Debug("appliedDamage was not positive! {0}", loggers: [appliedDamage]); return CancelAndReturnValue; }
            if (entity.GetBehavior<EntityBehaviorHealth>() is not EntityBehaviorHealth entity_HealthBehavior || entity.GetBehavior<BID_Lib_EntityManager.EntityBehavior_Bleed>() is not BID_Lib_EntityManager.EntityBehavior_Bleed entity_BleedBehavior) { BID_Lib_FunctionsGeneral.Log_Debug("Entity was missing BehaviorHealth or BehaviorBleed: {0}", loggers: [entity.GetPrefixAndCreatureName() ?? "null entity"]); return CancelAndReturnValue; }
            
            if (damageSource.Type == EnumDamageType.Heal) { appliedDamage = BleedDamage_HealAndReduce(entity, appliedDamage); return appliedDamage; } //TODO: find way to hook into initial bandage application instead
            if (damageSource.Type == EnumDamageType.Fire) { BleedDamage_Cauterize(entity, appliedDamage); return CancelAndReturnValue; } //fire damage reduces Bleed_CurrentLevel_External then returns to vanilla damage handling; TODO: see if i can inject in entity.ApplyFireDamage or similar method instead

            if (Config_Reference.Config_Loaded.Config_System.System_UseDamageTypeCompat is false && Config_Reference.Config_Loaded.Config_DamageType?.Dict_DamageType?.ContainsKey(damageSource.Type) == true)//TODO: remove... eventually
            { damageSource.Type = EnumDamageType.SlashingAttack; } //if vanilla updates attacks to use DamageTypes, this toggle will allow the system to use per DamageType values without requiring me to update the mod immediately

            if (DamageType_Dict_ConfigCache?.TryGetValue(damageSource.Type, out var DamageType_OuterDict) is not true) { BID_Lib_FunctionsGeneral.Log_Debug("Dict_DamageType  was null or missing damageType: {0}", loggers: [damageSource.Type]); return CancelAndReturnValue; }
            if (DamageType_OuterDict?.TryGetValue("Direct_Multi", out var _) is not true || DamageType_OuterDict?.TryGetValue("Bleed_Multi_External", out var _) is not true) { BID_Lib_FunctionsGeneral.Log_Debug("Dict_DamageType does not contain innerdict value(s) within Dict_DamageType: {0}", loggers: [damageSource.Type]); return CancelAndReturnValue; } //dont check for internal bleed mods as DamageTypes can not have internal bleeding. Instead Skip applying internal if missing

            //convert and store values
            entity_BleedBehavior.health_PreDamage = entity_HealthBehavior.Health;
            entity_BleedBehavior.appliedDamage_Base = appliedDamage;
            entity_BleedBehavior.lastBleedSource = damageSource;
            appliedDamage = MathF.Round(MathF.Max(appliedDamage * DamageType_OuterDict["Direct_Multi"], 0.0f), 4);

            return appliedDamage;
        }


        //called on entity.OnGameTick; compares health before and after armor damage reduction, calculate the actual damage taken then calculates and applies the Bleed_CurrentLevel_External and Internal increases
        internal static void BleedDamage_Conversion_Apply(Entity entity, DamageSource lastBleedSource)
        {
            //verification//TODO: add return check if damage was even taken
            if (entity.GetBehavior<EntityBehaviorHealth>() is not EntityBehaviorHealth entity_HealthBehavior || entity.GetBehavior<BID_Lib_EntityManager.EntityBehavior_Bleed>() is not BID_Lib_EntityManager.EntityBehavior_Bleed entity_BleedBehavior) { BID_Lib_FunctionsGeneral.Log_Debug("Entity was missing BehaviorHealth or BehaviorBleed: {0}", loggers: [entity.GetPrefixAndCreatureName() ?? "null entity"]); return; }
            if (Config_Reference.Config_Loaded.Config_DamageType?.Dict_DamageType?.TryGetValue(lastBleedSource.Type, out var DamageType_OuterDict) is not true) { BID_Lib_FunctionsGeneral.Log_Debug("Dict_DamageType was null or missing damageType: {0}", loggers: [lastBleedSource.Type]); return; }//redeclare dict in case another entity calls bleed conversion in the same tick
            if (DamageType_OuterDict?.TryGetValue("Direct_Multi", out var _) is not true || DamageType_OuterDict?.TryGetValue("Bleed_Multi_External", out var _) is not true) { BID_Lib_FunctionsGeneral.Log_Debug("Dict_DamageType does not contain innerdict value(s) within Dict_DamageType: {0}", loggers: [lastBleedSource.Type]); return; } //do not check for internal bleed modifiers as DamageTypes can not have internal bleeding. Instead Skip application of internal if missing


            float bleedToApply_External;
            float bleedToApply_Internal = 0;

            float damageReduction_PostArmor = (entity_BleedBehavior.health_PreDamage - entity_HealthBehavior.Health) / (entity_BleedBehavior.appliedDamage_Base * DamageType_OuterDict["Direct_Multi"]);
            float damage_ToConvert = entity_BleedBehavior.appliedDamage_Base * damageReduction_PostArmor;


            //calc bleed and internal bleed to apply
            try
            {
                //external bleed
                if (Config_Reference.Config_Loaded.Config_System.System_Bleed_External)
                {
                    //TODO: ease out curve applied to armour damage reduction to reduce how much bleed is reduced by armor at higher levels of protection
                    bleedToApply_External = MathF.Round(MathF.Max(damage_ToConvert * DamageType_OuterDict["Bleed_Multi_External"], 0.0f), 4);
                }
                else { BID_Lib_FunctionsGeneral.Log_Debug("Bleed_External is disabled in config"); return; }

                //internal bleed
                if (Config_Reference.Config_Loaded.Config_System.System_Bleed_Internal)
                {
                    if (DamageType_OuterDict?.TryGetValue("Bleed_Multi_Internal", out var _) is true && DamageType_OuterDict?.TryGetValue("Bleed_ConversionThreshold_Internal", out var _) is true)
                    {
                        if (bleedToApply_External > DamageType_OuterDict["Bleed_ConversionThreshold_Internal"] && DamageType_OuterDict["Bleed_ConversionThreshold_Internal"] >= 0) //TODO: clamp config values and remove last check
                        {
                            if (DamageType_OuterDict["Bleed_Multi_Internal"] != 0)
                            {
                                float curve_Output = BleedDamage_InternalBleedCurve(bleedToApply_External - DamageType_OuterDict["Bleed_ConversionThreshold_Internal"]);
                                bleedToApply_Internal = bleedToApply_External * curve_Output;
                                bleedToApply_External -= bleedToApply_Internal;
                            }
                            else
                            {
                                bleedToApply_Internal = MathF.Round(MathF.Max(damage_ToConvert * DamageType_OuterDict["Bleed_Multi_External"], 0.0f), 4);
                                bleedToApply_External = 0;
                            }
                        }
                        else { BID_Lib_FunctionsGeneral.Log_Debug("Damage did not surpass [Bleed_ConversionThreshold_Internal]"); }
                    }
                    else { BID_Lib_FunctionsGeneral.Log_Debug("Dict_DamageType does not contain internal bleed values and is invalid for DamageType: {0}", loggers: [lastBleedSource.Type]); }
                }
                else { BID_Lib_FunctionsGeneral.Log_Debug("Bleed_Internal is disabled in config"); return; }
            }
            catch(Exception e) { BID_Lib_FunctionsGeneral.Log_Debug("DamageType is not valid for bleeding: {0}; Error: {1}", loggers: [lastBleedSource.Type, e.Message]); return; }

            //apply bleed
            entity_BleedBehavior.State_IsRagged = false; entity_BleedBehavior.State_IsBandaged = false;
            entity_BleedBehavior.Bleed_CurrentLevel_External += bleedToApply_External;
            entity_BleedBehavior.Bleed_CurrentLevel_Internal += bleedToApply_Internal;
            BID_Lib_FunctionsGeneral.Log_Debug("Applied {0} damage, {1} Bleed_External and {2} Bleed_Internal to {3} from {4} which dealt {5} base damage of {6} DamageType", loggers: [entity_BleedBehavior.health_PreDamage - entity_HealthBehavior.Health, bleedToApply_External, bleedToApply_Internal, entity.GetPrefixAndCreatureName(), lastBleedSource.CauseEntity?.GetPrefixAndCreatureName() ?? lastBleedSource.SourceEntity?.GetPrefixAndCreatureName() ?? "null", entity_BleedBehavior.appliedDamage_Base, lastBleedSource.Type]);

            //reset values
            entity_BleedBehavior.health_PreDamage = 0f;
            entity_BleedBehavior.appliedDamage_Base = 0f;
        }


        //called for applying damage from external bleed on tick
        internal static void BleedDamage_Tick(Entity entity, float deltaTimeSum) 
        {
            if (entity.GetBehavior<BID_Lib_EntityManager.EntityBehavior_Bleed>() is not BID_Lib_EntityManager.EntityBehavior_Bleed entity_BleedBehavior) { return; }
            if (entity.GetBehavior<EntityBehaviorHealth>() is not EntityBehaviorHealth entity_HealthBehavior) { return; }
            if (entity is IServerPlayer) { if (((IServerPlayer)entity).WorldData.CurrentGameMode == EnumGameMode.Creative || ((IServerPlayer)entity).WorldData.CurrentGameMode == EnumGameMode.Spectator) { entity_BleedBehavior.Bleed_CurrentLevel_External = 0; entity_BleedBehavior.Bleed_CurrentLevel_Internal = 0 ; return; } }

            float damageToApply = 0;
            deltaTimeSum *= (entity.World.Calendar.CalendarSpeedMul * entity.World.Calendar.SpeedOfTime); deltaTimeSum /= (30f / Config_Reference.Config_Loaded.Config_TimeScale.TimeScale_BleedRate);
            float state_BleedReduction_RaggedOrBandaged = entity_BleedBehavior.State_IsBandaged ? Config_Reference.Config_Loaded.Config_HealBonus.HealBonus_BleedReduction_Bandage : (entity_BleedBehavior.State_IsRagged ? Config_Reference.Config_Loaded.Config_HealBonus.HealBonus_BleedReduction_Rag : 0f);
            float state_BleedReduction_PressureOrCare = entity is EntityPlayer ? (((EntityPlayer)entity).Controls.Sneak ? Config_Reference.Config_Loaded.Config_HealBonus.HealBonus_BleedReduction_Pressure : 0f) : (false /*TODO: add State_IsCare without watchedattributes*/ ? Config_Reference.Config_Loaded.Config_HealBonus.HealBonus_BleedReduction_Care : 0f);
            float state_HealBonus_Resting = entity is EntityPlayer ? (((EntityPlayer)entity).Controls.FloorSitting ? Config_Reference.Config_Loaded.Config_HealBonus.HealBonus_BleedHeal_Ground : (((EntityPlayer)entity).MountedOn is BlockEntityBed ? Config_Reference.Config_Loaded.Config_HealBonus.HealBonus_BleedHeal_Bed : (((EntityPlayer)entity).MountedOn is BlockEntityPie ? Config_Reference.Config_Loaded.Config_HealBonus.HealBonus_BleedHeal_Comfort : 0f))) : 0f; //TODO: add confort object check
            entity_BleedBehavior.State_BleedReduction = state_BleedReduction_PressureOrCare + state_BleedReduction_RaggedOrBandaged; //stored for bleed effect to access


            //calc external bleed damage
            if (entity_BleedBehavior.Bleed_CurrentLevel_External > 0f)
            {
                float damageToAdd = entity_BleedBehavior.Bleed_CurrentLevel_External * (deltaTimeSum * Config_Reference.Config_Loaded.Config_Rate.Rate_BleedDamage_External / MathF.Max((state_BleedReduction_PressureOrCare + state_BleedReduction_RaggedOrBandaged), 1f));
                damageToApply += Config_Reference.Config_Loaded.Config_System.System_BleedCanKill_External ? damageToAdd : MathF.Min(damageToAdd, MathF.Max((entity_HealthBehavior.Health - 0.01f), 0f));
                entity_BleedBehavior.Bleed_CurrentLevel_External -= (deltaTimeSum * Config_Reference.Config_Loaded.Config_Rate.Rate_BleedHeal_External * MathF.Max(state_HealBonus_Resting, 1f)) + (deltaTimeSum * entity_BleedBehavior.Bleed_CurrentLevel_External * Config_Reference.Config_Loaded.Config_Rate.Rate_ScaledtHeal_External);
            }

            //calc internal bleed damage
            if (entity_BleedBehavior.Bleed_CurrentLevel_Internal > 0f)
            {
                float damageToAdd = entity_BleedBehavior.Bleed_CurrentLevel_Internal * (deltaTimeSum * Config_Reference.Config_Loaded.Config_Rate.Rate_BleedDamage_Internal / MathF.Max( 0f/*TODO: add limited stemming check*/, 1f));
                damageToApply += Config_Reference.Config_Loaded.Config_System.System_BleedCanKill_Internal ? damageToAdd : MathF.Min(damageToAdd, MathF.Max((entity_HealthBehavior.Health - 0.01f), 0f));
                entity_BleedBehavior.Bleed_CurrentLevel_Internal -= (deltaTimeSum * Config_Reference.Config_Loaded.Config_Rate.Rate_BleedHeal_Internal * MathF.Max(state_HealBonus_Resting, 1f)) + (deltaTimeSum * entity_BleedBehavior.Bleed_CurrentLevel_Internal * Config_Reference.Config_Loaded.Config_Rate.Rate_ScaledHeal_Internal);
            }

            //apply all bleed damage
            if (!entity.Alive) { return; } //dead entitys can still have bleed effects so bleeding still needs to occur
            if (entity_HealthBehavior.Health > damageToApply) { entity_HealthBehavior.Health -= damageToApply; }
            else { entity.Die(EnumDespawnReason.Death, entity_BleedBehavior.lastBleedSource); }
            return;
        }


        //called to cauterize Bleed_CurrentLevel_External when damageType is fire; TODO:move to inside vanilla fire damage call? or just vanilla damage application?
        internal static void BleedDamage_Cauterize(Entity entity, float appliedDamage)
        {
            if (entity.GetBehavior<BID_Lib_EntityManager.EntityBehavior_Bleed>() is not BID_Lib_EntityManager.EntityBehavior_Bleed entity_BleedBehavior) { return; }

            if (entity_BleedBehavior.Bleed_CurrentLevel_External > 0)
            {
                entity_BleedBehavior.Bleed_CurrentLevel_External = MathF.Max(entity_BleedBehavior.Bleed_CurrentLevel_External - (appliedDamage * Config_Reference.Config_Loaded.Config_HealBonus.HealBonus_BleedHeal_Cauterize), 0f);
                BID_Lib_FunctionsGeneral.Log_Debug("Cauterize healed BleedLevel_External: {0}");
            }
        }


        //called to reduce healing from items and heal some BleedLevel
        internal static float BleedDamage_HealAndReduce(Entity entity, float appliedDamage)//TODO: detect initial bandage aplication and modify the damageSource itself; damageSource.DamageOverTimeType ???; TODO: if hit remove remaining HOT
        {
            if (entity.GetBehavior<BID_Lib_EntityManager.EntityBehavior_Bleed>() is not BID_Lib_EntityManager.EntityBehavior_Bleed entity_BleedBehavior) { return appliedDamage; }
            float bleed_ToRemove = MathF.Max(appliedDamage * Config_Reference.Config_Loaded.Config_HealBonus.HealBonus_BleedReduction_Flat_Bandage, 0f);
            float heal_Reduction_Bandage = MathF.Max(appliedDamage * Config_Reference.Config_Loaded.Config_HealBonus.HealBonus_HealReduction_Bandage, 0f);

            entity_BleedBehavior.Bleed_CurrentLevel_External -= bleed_ToRemove;
            appliedDamage = heal_Reduction_Bandage;
            entity_BleedBehavior.State_IsBandaged = true; //TODO: only apply this if the item used is certain healing items (ie. bandage)

            BID_Lib_FunctionsGeneral.Log_Debug_Verbose("Healing Item healed BleedLevel_External by: {0}; heal amount was reduced by: {1}", loggers: [bleed_ToRemove, heal_Reduction_Bandage]);
            return appliedDamage;
        }


        internal static void BleedDamage_NotifyOnHit(EntityPlayer player, float bleedToApply_External, float bleedToApply_Internal, DamageSource damageSource)
        {
            {
                if (Config_Reference.Config_Loaded.Config_BleedReport.Report_BleedCheck_Detailed)
                {
                    ((IServerPlayer)player.Player).SendMessage(GlobalConstants.DamageLogChatGroup, Lang.Get("damagelog_BleedReport_Detailed", [damageSource.GetType(), bleedToApply_External]) + (Config_Reference.Config_Loaded.Config_BleedReport.Report_BleedCheck_Internal ? Lang.Get("damagelog_BleedReport_Detailed_Internal", [bleedToApply_Internal]) : ""), EnumChatType.Notification);
                }
                else
                {
                    string vagueBleed_External = "None";
                    string vagueBleed_Internal = "None";

                    foreach (var threashold in Config_Reference.Config_Loaded.Config_BleedReport.List_Report_SeverityThreashold) { if (bleedToApply_External >= threashold.BleedLevel) { vagueBleed_External = threashold.Severity; break; } }
                    foreach (var threashold in Config_Reference.Config_Loaded.Config_BleedReport.List_Report_SeverityThreashold) { if (bleedToApply_Internal * 15f >= threashold.BleedLevel) { vagueBleed_Internal = threashold.Severity; break; } } //multiply by arbitrary value to get internal within a similar range; TODO: tweak
                    ((IServerPlayer)player.Player).SendMessage(GlobalConstants.DamageLogChatGroup, Lang.Get("damagelog_BleedReport_Vague", [damageSource.GetType(), bleedToApply_External]) + ((Config_Reference.Config_Loaded.Config_BleedReport.Report_BleedCheck_Internal && bleedToApply_Internal > 0) ? Lang.Get("damagelog_BleedReport_Vague_Internal") : ""), EnumChatType.Notification);
                }
            }
        }


        //called for converting to internal bleed; big hits that deal bleed past a specific # will be diverted somewhat (more bleed above threshold means more convert %) into an internal wound that is much harder to heal manually. internal bleeding is slower but heals slower effectively doing more damage; not effected by pressure application
        internal static float BleedDamage_InternalBleedCurve(float x0) 
        {
            //variables; y0 = -0.5f; a1 = 8.0f; k1 = 1.0f; x1 = 3.0f; a2 = 12.5f; k2 = 0.3f; x2 = 11.5f;
            float y0 = Config_Reference.Config_Loaded?.Config_Curve.Curve_Bleed_InternalConversionY0 ?? -0.5f;
            float a1 = Config_Reference.Config_Loaded?.Config_Curve.Curve_Bleed_InternalConversionA1 ?? 8.0f;
            float k1 = Config_Reference.Config_Loaded?.Config_Curve.Curve_Bleed_InternalConversionK1 ?? 1.0f;
            float x1 = Config_Reference.Config_Loaded?.Config_Curve.Curve_Bleed_InternalConversionX1 ?? 3.0f;
            float a2 = Config_Reference.Config_Loaded?.Config_Curve.Curve_Bleed_InternalConversionA2 ?? 12.5f;
            float k2 = Config_Reference.Config_Loaded?.Config_Curve.Curve_Bleed_InternalConversionK2 ?? 0.3f;
            float x2 = Config_Reference.Config_Loaded?.Config_Curve.Curve_Bleed_InternalConversionX2 ?? 11.5f;

            return BID_Lib_FunctionsGeneral.Calc_Curve_DoubleSigmoid(x0, y0, a1, k1, x1, a2, k2, x2);
        }
    }
}
