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

namespace BleedingInDepth.lib
{
    internal class BID_Lib_FunctionsBleed
    {
        private static ICoreAPI API = BleedingInDepthModSystem.API;

        internal static FrozenDictionary<EnumDamageType, Dictionary<string, float>>? DamageType_Dict_ConfigCache;
        internal static float CancelAndReturnValue = 1024.4201f; //Arbitrary number returned to caller function to tell it that bleed conversion failed and to fall back to vanilla damage handler
        internal static float normalizer_InternalBleed = 8f;//(Config_Reference.Config_Loaded.Config_Rate.Rate_BleedDamage_Internal / Config_Reference.Config_Loaded.Config_Rate.Rate_BleedDamage_External);//TODO: desire around 8f from dynamic



        //called on entity healthbehavior.ondamaged; performs damagetype checks -> stores values to be used in post armor bleed conversion -> reduces direct damage applied and resumes vanilla logic
        internal static float BleedDamage_Conversion_Store(Entity entity, float appliedDamage, ref DamageSource damageSource)
        {
            //damagetype verification; check config exists and DamageType's dictionary is valid for bleeding
            if (appliedDamage <= 0f) { BID_Lib_FunctionsGeneral.Log_Debug("appliedDamage was not positive! {0}", loggers: [appliedDamage]); return CancelAndReturnValue; }
            if (entity.GetBehavior<EntityBehaviorHealth>() is not EntityBehaviorHealth entity_HealthBehavior || entity.GetBehavior<BID_Lib_EntityManager.EntityBehavior_Bleed>() is not BID_Lib_EntityManager.EntityBehavior_Bleed entity_BleedBehavior) { BID_Lib_FunctionsGeneral.Log_Debug("Entity was missing BehaviorHealth or BehaviorBleed: {0}", loggers: [entity.GetName() ?? entity.GetPrefixAndCreatureName() ?? "null entity"]); return CancelAndReturnValue; }
            
            switch (damageSource.Type)
            {
                case EnumDamageType.Heal: { appliedDamage = BleedDamage_HealAndReduce(entity, appliedDamage); return appliedDamage; } //TODO: find way to hook into initial bandage application instead
                case EnumDamageType.Fire: { BleedDamage_Cauterize(entity, appliedDamage); return CancelAndReturnValue; } //TODO: see if i can inject in entity.ApplyFireDamage or similar method instead
            }

            if (Config_Reference.Config_Loaded.Config_System.System_BleedDamage_Acc.UseDamageTypeCompat is false && Config_Reference.Config_Loaded.Config_DamageType?.Dict_DamageType?.ContainsKey(damageSource.Type) == true)//TODO: remove... eventually
            { damageSource.Type = EnumDamageType.SlashingAttack; } //if vanilla updates attacks to use DamageTypes, this toggle will allow the system to use per DamageType values without requiring me to update the mod immediately

            if (DamageType_Dict_ConfigCache?.TryGetValue(damageSource.Type, out var DamageType_OuterDict) is not true) { BID_Lib_FunctionsGeneral.Log_Debug("Dict_DamageType  was null or missing damageType: {0}", loggers: [damageSource.Type]); return CancelAndReturnValue; }
            if (DamageType_OuterDict?.TryGetValue("Direct_Multi", out var _) is not true || DamageType_OuterDict?.TryGetValue("Bleed_Multi_External", out var _) is not true) { BID_Lib_FunctionsGeneral.Log_Debug("Dict_DamageType does not contain innerdict value(s) within Dict_DamageType: {0}", loggers: [damageSource.Type]); return CancelAndReturnValue; } //dont check for internal bleed mods as DamageTypes can not have internal bleeding. Instead Skip applying internal if missing

            //convert and store values
            entity_BleedBehavior.health_PreDamage = entity_HealthBehavior.Health;
            entity_BleedBehavior.appliedDamage_Base = appliedDamage;
            entity_BleedBehavior.lastBleedSource = damageSource;
            float returnedDamage = MathF.Round(MathF.Max(appliedDamage * DamageType_OuterDict["Direct_Multi"], 0.0f), 4);
            BID_Lib_FunctionsGeneral.Log_Debug("Stored [health_PreDamage: {0}; appliedDamage_Base: {1} lastBleedSource.Type: {2}]", loggers: [entity_BleedBehavior.health_PreDamage, entity_BleedBehavior.appliedDamage_Base, entity_BleedBehavior.lastBleedSource.Type]);

            return returnedDamage;
        }


        //called on entity.OnGameTick; compares health before and after armor damage reduction -> calculate the actual damage taken -> calculates and applies the Bleed_CurrentLevel_External and Internal increases
        internal static void BleedDamage_Conversion_Apply(Entity entity, DamageSource lastBleedSource)
        {
            //verification//TODO: add return check if damage was even taken
            if (entity.GetBehavior<EntityBehaviorHealth>() is not EntityBehaviorHealth entity_HealthBehavior || entity.GetBehavior<BID_Lib_EntityManager.EntityBehavior_Bleed>() is not BID_Lib_EntityManager.EntityBehavior_Bleed entity_BleedBehavior) { BID_Lib_FunctionsGeneral.Log_Debug("Entity was missing BehaviorHealth or BehaviorBleed: {0}", loggers: [entity.GetName() ?? entity.GetPrefixAndCreatureName() ?? "null entity"]); return; }
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
                if (Config_Reference.Config_Loaded.Config_System.System_BleedDamage_Acc.Bleed_External)
                {
                    //TODO: ease out curve applied to armour damage reduction to reduce how much bleed is reduced by armor at higher levels of protection
                    bleedToApply_External = MathF.Round(MathF.Max(damage_ToConvert * DamageType_OuterDict["Bleed_Multi_External"], 0.0f), 4);
                }
                else { BID_Lib_FunctionsGeneral.Log_Debug("Bleed_External is disabled in config"); return; }

                //internal bleed
                if (Config_Reference.Config_Loaded.Config_System.System_BleedDamage_Acc.Bleed_Internal)
                {
                    if (DamageType_OuterDict?.TryGetValue("Bleed_Multi_Internal", out var _) is true && DamageType_OuterDict?.TryGetValue("Bleed_ConversionThreshold_Internal", out var _) is true)
                    {
                        if (damage_ToConvert > DamageType_OuterDict["Bleed_ConversionThreshold_Internal"] && DamageType_OuterDict["Bleed_Multi_Internal"] != 0)
                        {
                            float curve_Output = BleedDamage_InternalBleedCurve((damage_ToConvert - DamageType_OuterDict["Bleed_ConversionThreshold_Internal"]) * DamageType_OuterDict["Bleed_Multi_External"]) * 0.01f;
                            bleedToApply_Internal = bleedToApply_External * curve_Output;
                            bleedToApply_External -= bleedToApply_Internal;
                        }
                        if (DamageType_OuterDict["Bleed_Multi_Internal"] == 0) //if [Bleed_Multi_Internal] is 0 all external bleed applied is converted directly into internal 
                        {
                            bleedToApply_Internal = MathF.Round(MathF.Max(damage_ToConvert * DamageType_OuterDict["Bleed_Multi_External"] - DamageType_OuterDict["Bleed_ConversionThreshold_Internal"], 0.0f), 4);
                            bleedToApply_External = 0;
                        }
                        else { BID_Lib_FunctionsGeneral.Log_Debug("Damage did not surpass [Bleed_ConversionThreshold_Internal]"); }
                    }
                    else { BID_Lib_FunctionsGeneral.Log_Debug("Dict_DamageType does not contain internal bleed values and is invalid for DamageType: {0}", loggers: [lastBleedSource.Type]); }
                }
                else { BID_Lib_FunctionsGeneral.Log_Debug("Bleed_Internal is disabled in config"); return; }
            }
            catch(Exception e) { BID_Lib_FunctionsGeneral.Log_Debug("Exception caught: {0}", loggers: [e.Message]); return; }

            //apply bleed
            entity_BleedBehavior.State_IsRagged = false; entity_BleedBehavior.State_IsBandaged = false;
            entity_BleedBehavior.Bleed_CurrentLevel_External += bleedToApply_External;
            entity_BleedBehavior.Bleed_CurrentLevel_Internal += bleedToApply_Internal;
            if (Config_Reference.Config_Loaded.Config_BleedReport.BleedReport_NotifyOnHit && entity is EntityPlayer entityPlayer) { BleedDamage_NotifyOnHit(entityPlayer, bleedToApply_External, bleedToApply_Internal); }
            BID_Lib_FunctionsGeneral.Log_Debug("Applied {0} damage, {1} Bleed_External and {2} Bleed_Internal to {3} from {4} which dealt {5} base damage of {6} DamageType",
                loggers: [entity_BleedBehavior.health_PreDamage - entity_HealthBehavior.Health, bleedToApply_External, bleedToApply_Internal, entity.GetName() ?? entity.GetPrefixAndCreatureName(), lastBleedSource.CauseEntity?.GetPrefixAndCreatureName() ?? lastBleedSource.SourceEntity?.GetPrefixAndCreatureName() ?? "null", entity_BleedBehavior.appliedDamage_Base, lastBleedSource.Type]);

            //reset values
            entity_BleedBehavior.health_PreDamage = 0f;
            entity_BleedBehavior.appliedDamage_Base = 0f;
        }


        //called on entity.OnGameTick; applies damage from bleed
        internal static void BleedDamage_Tick(Entity entity, float deltaTimeSum) 
        {
            if (entity.GetBehavior<BID_Lib_EntityManager.EntityBehavior_Bleed>() is not BID_Lib_EntityManager.EntityBehavior_Bleed entity_BleedBehavior) { return; }
            if (entity.GetBehavior<EntityBehaviorHealth>() is not EntityBehaviorHealth entity_HealthBehavior) { return; }
            if (entity is EntityPlayer entityPlayer)
            {
                if (entityPlayer.Player.WorldData.CurrentGameMode == EnumGameMode.Creative || entityPlayer.Player.WorldData.CurrentGameMode == EnumGameMode.Spectator) { entity_BleedBehavior.Bleed_CurrentLevel_External = 0; entity_BleedBehavior.Bleed_CurrentLevel_Internal = 0 ; return; }
                if (Config_Reference.Config_Loaded.Config_System.System_BleedDamage_Acc.Bleed_SlowAtLowHealth) { deltaTimeSum *= BID_Lib_FunctionsGeneral.Calc_Curve_ExpoEaseOut(entity_HealthBehavior.Health, 0.8f, 0.8f, 0f, 0.2f); } //TODO: expose these in config
            }

            float damageToApply = 0;
            deltaTimeSum *= (entity.World.Calendar.CalendarSpeedMul * entity.World.Calendar.SpeedOfTime); deltaTimeSum /= (30f / Config_Reference.Config_Loaded.Config_TimeScale.TimeScale_BleedRate);
            float state_BleedReduction_RaggedOrBandaged = entity_BleedBehavior.State_IsBandaged ?
                Config_Reference.Config_Loaded.Config_HealBonus.HealBonus_BleedReduction_Bandage : (entity_BleedBehavior.State_IsRagged ? Config_Reference.Config_Loaded.Config_HealBonus.HealBonus_BleedReduction_Rag : 0f);
            float state_BleedReduction_PressureOrCare = entity is EntityPlayer ?
                (((EntityPlayer)entity).Controls.Sneak || ((EntityPlayer)entity).MountedOn is BlockEntityBed ? Config_Reference.Config_Loaded.Config_HealBonus.HealBonus_BleedReduction_Pressure : 0f) : (false /*TODO: add State_IsCare without watchedattributes*/ ? Config_Reference.Config_Loaded.Config_HealBonus.HealBonus_BleedReduction_Care : 0f);
            float state_HealBonus_Resting = entity is EntityPlayer ?
                (((EntityPlayer)entity).Controls.FloorSitting ? Config_Reference.Config_Loaded.Config_HealBonus.HealBonus_BleedHeal_Ground : (((EntityPlayer)entity).MountedOn is BlockEntityBed ?Config_Reference.Config_Loaded.Config_HealBonus.HealBonus_BleedHeal_Bed : (((EntityPlayer)entity).MountedOn is BlockEntityPie ? Config_Reference.Config_Loaded.Config_HealBonus.HealBonus_BleedHeal_Comfort : 0f))) : 0f; //TODO: add confort object check
            entity_BleedBehavior.State_BleedReduction = state_BleedReduction_PressureOrCare + state_BleedReduction_RaggedOrBandaged; //stored for bleed effect to access


            //calc external bleed damage
            if (entity_BleedBehavior.Bleed_CurrentLevel_External > 0f)
            {
                float damageToAdd = entity_BleedBehavior.Bleed_CurrentLevel_External * (deltaTimeSum * Config_Reference.Config_Loaded.Config_Rate.Rate_BleedDamage_External / MathF.Max((state_BleedReduction_PressureOrCare + state_BleedReduction_RaggedOrBandaged), 1f));
                if (Config_Reference.Config_Loaded.Config_System.System_BleedDamage_Acc.BleedCanDamage_External) { damageToApply += Config_Reference.Config_Loaded.Config_System.System_BleedDamage_Acc.BleedCanKill_External ? damageToAdd : MathF.Min(damageToAdd, MathF.Max((entity_HealthBehavior.Health - 0.01f), 0f)); }
                entity_BleedBehavior.Bleed_CurrentLevel_External -= (deltaTimeSum * Config_Reference.Config_Loaded.Config_Rate.Rate_BleedHeal_External * MathF.Max(state_HealBonus_Resting, 1f)) + (deltaTimeSum * entity_BleedBehavior.Bleed_CurrentLevel_External * Config_Reference.Config_Loaded.Config_Rate.Rate_ScaledtHeal_External);
            }

            //calc internal bleed damage
            if (entity_BleedBehavior.Bleed_CurrentLevel_Internal > 0f)
            {
                float damageToAdd = entity_BleedBehavior.Bleed_CurrentLevel_Internal * (deltaTimeSum * Config_Reference.Config_Loaded.Config_Rate.Rate_BleedDamage_Internal / MathF.Max( 0f/*TODO: add limited stemming check*/, 1f));
                if (Config_Reference.Config_Loaded.Config_System.System_BleedDamage_Acc.BleedCanDamage_Internal) { damageToApply += Config_Reference.Config_Loaded.Config_System.System_BleedDamage_Acc.BleedCanKill_Internal ? damageToAdd : MathF.Min(damageToAdd, MathF.Max((entity_HealthBehavior.Health - 0.01f), 0f)); }
                entity_BleedBehavior.Bleed_CurrentLevel_Internal -= (deltaTimeSum * Config_Reference.Config_Loaded.Config_Rate.Rate_BleedHeal_Internal * MathF.Max(state_HealBonus_Resting, 1f)) + (deltaTimeSum * entity_BleedBehavior.Bleed_CurrentLevel_Internal * Config_Reference.Config_Loaded.Config_Rate.Rate_ScaledHeal_Internal);
            }

            //apply all bleed damage
            if (!entity.Alive) { entity_BleedBehavior.Bleed_CurrentLevel_External *= 0.9f; entity_BleedBehavior.Bleed_CurrentLevel_Internal = 0f; return; } //dead entitys can still have bleed effects so bleeding still needs to occur. reduce bleed level more to prevent dead entitys from creating excess particles
            if (entity_HealthBehavior.Health > damageToApply) { entity_HealthBehavior.Health -= damageToApply; }
            else { entity.Die(EnumDespawnReason.Death, entity_BleedBehavior.lastBleedSource); }
            return;
        }


        //called to cauterize Bleed_CurrentLevel_External when damageType is fire; TODO: move to inside vanilla fire damage call? or just vanilla damage application?
        internal static void BleedDamage_Cauterize(Entity entity, float appliedDamage)
        {
            if (entity.GetBehavior<BID_Lib_EntityManager.EntityBehavior_Bleed>() is not BID_Lib_EntityManager.EntityBehavior_Bleed entity_BleedBehavior) { return; }

            if (entity_BleedBehavior.Bleed_CurrentLevel_External > 0)
            {
                float cauterizeAmount = appliedDamage * Config_Reference.Config_Loaded.Config_HealBonus.HealBonus_BleedHeal_Cauterize;
                entity_BleedBehavior.Bleed_CurrentLevel_External = MathF.Max(entity_BleedBehavior.Bleed_CurrentLevel_External - cauterizeAmount, 0f);
                if (entity is EntityPlayer entityPlayer && Config_Reference.Config_Loaded.Config_BleedReport.BleedReport_NotifyOnHit) { ((IServerPlayer)entityPlayer.Player).SendMessage(GlobalConstants.DamageLogChatGroup, Lang.Get("bleedingindepth:damageLog_BleedNotify_Cauterize", [cauterizeAmount]), EnumChatType.Notification); }
                BID_Lib_FunctionsGeneral.Log_Debug("Cauterized [BleedLevel_External] by: {0}", loggers: [cauterizeAmount]);
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

            BID_Lib_FunctionsGeneral.Log_Debug_Verbose("Healing Item healed BleedLevel_External by: {0}; heal amount was reduced down to: {1}", loggers: [bleed_ToRemove, heal_Reduction_Bandage]);
            return appliedDamage;
        }


        internal static void BleedDamage_NotifyOnHit(EntityPlayer entityPlayer, float bleedToApply_External, float bleedToApply_Internal)
        {
            {
                if (Config_Reference.Config_Loaded.Config_BleedReport.BleedReport_Detailed)
                {
                    ((IServerPlayer)entityPlayer.Player).SendMessage(GlobalConstants.DamageLogChatGroup, Lang.Get("bleedingindepth:damageLog_BleedNotify_External", [bleedToApply_External]) + (Config_Reference.Config_Loaded.Config_BleedReport.BleedReport_Internal ? Lang.Get("bleedingindepth:damageLog_BleedNotify_Internal", [bleedToApply_Internal]) : ""), EnumChatType.Notification);
                }
                else
                {
                    string vagueBleed_External = "None";
                    string vagueBleed_Internal = "None";

                    foreach (var threashold in Config_Reference.Config_Loaded.Config_BleedReport.List_BleedReport_GainedBleed_SeverityThreashold) { if (bleedToApply_External >= threashold.BleedLevel) { vagueBleed_External = threashold.Severity; break; } }
                    foreach (var threashold in Config_Reference.Config_Loaded.Config_BleedReport.List_BleedReport_GainedBleed_SeverityThreashold) { if (bleedToApply_Internal * normalizer_InternalBleed >= threashold.BleedLevel) { vagueBleed_Internal = threashold.Severity; break; } } //multiply by arbitrary value to get internal within a similar range; TODO: tweak
                    ((IServerPlayer)entityPlayer.Player).SendMessage(GlobalConstants.DamageLogChatGroup, Lang.Get("bleedingindepth:damageLog_BleedNotify_External", [vagueBleed_External]) + ((Config_Reference.Config_Loaded.Config_BleedReport.BleedReport_Internal && bleedToApply_Internal > 0) ? Lang.Get("bleedingindepth:damageLog_BleedNotify_Internal", [vagueBleed_Internal]) : ""), EnumChatType.Notification);
                    BID_Lib_FunctionsGeneral.Log_Debug("Player recieved vague report for External: {0}({1}); Internal: {2}({3})", loggers: [vagueBleed_External, bleedToApply_External, vagueBleed_Internal, bleedToApply_Internal]);
                }
            }
        }


        //called for converting to internal bleed; big hits that deal bleed past a specific # will be diverted somewhat (more bleed above threshold means more convert %) into an internal wound that is much harder to heal manually. internal bleeding is slower but heals slower effectively doing more damage; not effected by pressure application
        internal static float BleedDamage_InternalBleedCurve(float input_x) 
        {
            //variables; y0 = -0.5f; a1 = 8.0f; k1 = 1.0f; x1 = 3.0f; a2 = 12.5f; k2 = 0.3f; x2 = 11.5f;
            float offset_Y0 = Config_Reference.Config_Loaded?.Config_Curve?.Curve_InternalConversion_Acc?.InternalConversion_Offset_Y0 ?? BID_ModConfig.Config_Curve.Curve_InternalConversion.Default_InternalConversion_Offset_Y0;//8f;
            float max1 = Config_Reference.Config_Loaded?.Config_Curve?.Curve_InternalConversion_Acc?.InternalConversion_Max1 ?? BID_ModConfig.Config_Curve.Curve_InternalConversion.Default_InternalConversion_Max1;//5.0f;
            float rate1 = Config_Reference.Config_Loaded?.Config_Curve?.Curve_InternalConversion_Acc?.InternalConversion_Rate1 ?? BID_ModConfig.Config_Curve.Curve_InternalConversion.Default_InternalConversion_Rate1;//0.75f;
            float offset_X1 = Config_Reference.Config_Loaded?.Config_Curve?.Curve_InternalConversion_Acc?.InternalConversion_Offset_X1 ?? BID_ModConfig.Config_Curve.Curve_InternalConversion.Default_InternalConversion_Offset_X1;//2.3f;
            float max2 = Config_Reference.Config_Loaded?.Config_Curve?.Curve_InternalConversion_Acc?.InternalConversion_Max2 ?? BID_ModConfig.Config_Curve.Curve_InternalConversion.Default_InternalConversion_Max2;//12f;
            float rate2 = Config_Reference.Config_Loaded?.Config_Curve?.Curve_InternalConversion_Acc?.InternalConversion_Rate2 ?? BID_ModConfig.Config_Curve.Curve_InternalConversion.Default_InternalConversion_Rate2;//0.2f;
            float offset_X2 = Config_Reference.Config_Loaded?.Config_Curve?.Curve_InternalConversion_Acc?.InternalConversion_Offset_X2 ?? BID_ModConfig.Config_Curve.Curve_InternalConversion.Default_InternalConversion_Offset_X2;//10f;

            return BID_Lib_FunctionsGeneral.Calc_Curve_DoubleSigmoid(input_x, offset_Y0, max1, rate1, offset_X1, max2, rate2, offset_X2);
        }
    }
}
