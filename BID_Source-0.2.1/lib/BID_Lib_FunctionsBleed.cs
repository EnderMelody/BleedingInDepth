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
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.Server;
using static BleedingInDepth.config.BID_ModConfig.Config_EntityType;

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

            if (DamageType_Dict_ConfigCache?.TryGetValue(damageSource.Type, out var DamageType_OuterDict) is not true) { BID_Lib_FunctionsGeneral.Log_Debug("Dict_DamageType was null or missing damageType: {0}", loggers: [damageSource.Type]); return CancelAndReturnValue; }
            if (DamageType_OuterDict?.TryGetValue(BID_ModConfig.Config_DamageType.NameOf_DamageType_DirectMulti, out var _) is not true || DamageType_OuterDict?.TryGetValue(BID_ModConfig.Config_DamageType.NameOf_DamageType_BleedMultiExternal, out var _) is not true) { BID_Lib_FunctionsGeneral.Log_Debug("Dict_DamageType does not contain innerdict value(s) within Dict_DamageType: {0}", loggers: [damageSource.Type]); return CancelAndReturnValue; } //dont check for internal bleed mods as DamageTypes can not have internal bleeding. Instead skip applying internal if missing

            //store entity category type
            if (entity_BleedBehavior.categoryType_Dict is null)
            {
                string entity_CategoryType = "no matching tag";//TODO: entity type check and assignment here
                foreach (string entityType in Config_Reference.Config_Loaded.Config_EntityType.Dict_EntityCategory.Keys)
                {
                    BID_Lib_FunctionsGeneral.Log_Debug_Verbose("checked entity {0} for tag {1}", loggers: [entity.GetPrefixAndCreatureName(), entityType]);
                    if (entity.HasTags(entityType))
                    {
                        entity_CategoryType = entityType;
                        break;
                    }
                }
                if (Config_Reference.Config_Loaded.Config_EntityType.Dict_EntityCategory?.TryGetValue(entity_CategoryType, out Dictionary<string, float>? entityCategoryDict) is not true)
                {
                    BID_Lib_FunctionsGeneral.Log_Debug("{0} failed to match tags in Dict_EntityCategory or dict was null or missing categoryType: {1}, defaulting 1f", loggers: [entity.GetPrefixAndCreatureName(), entity_CategoryType]);
                    entityCategoryDict = new() { [BID_ModConfig.Config_EntityType.NameOf_EntityCategory_DamageMod_Direct] = 1f, [BID_ModConfig.Config_EntityType.NameOf_EntityCategory_DamageMod_Bleed_External] = 1f, [BID_ModConfig.Config_EntityType.NameOf_EntityCategory_DamageMod_Bleed_Internal] = 1f };
                }
                else { BID_Lib_FunctionsGeneral.Log_Debug("{0} successfully matched categoryType: {1}", loggers: [entity.GetPrefixAndCreatureName(), entity_CategoryType]); }
                entity_BleedBehavior.categoryType_Dict = entityCategoryDict.ToFrozenDictionary();
                //entity_BleedBehavior.State_EntityCategory = entity_CategoryType;//TODO: find a better way to pass this through; also this passes the null category if no valid tag is found
            }
            

            //convert and store values
            entity_BleedBehavior.health_PreDamage = entity_HealthBehavior.Health;
            entity_BleedBehavior.appliedDamage_Base = appliedDamage;
            entity_BleedBehavior.lastBleedSource = damageSource;
            float returnedDamage = MathF.Round(MathF.Max(appliedDamage * DamageType_OuterDict[BID_ModConfig.Config_DamageType.NameOf_DamageType_DirectMulti] * entity_BleedBehavior.categoryType_Dict[BID_ModConfig.Config_EntityType.NameOf_EntityCategory_DamageMod_Direct], 0.0f), 4);
            BID_Lib_FunctionsGeneral.Log_Debug("Stored [health_PreDamage: {0}; appliedDamage_Base: {1} lastBleedSource.Type: {2}]", loggers: [entity_BleedBehavior.health_PreDamage, entity_BleedBehavior.appliedDamage_Base, entity_BleedBehavior.lastBleedSource.Type]);

            return returnedDamage;
        }


        //called on entity.OnGameTick; compares health before and after armor damage reduction -> calculate the actual damage taken -> calculates and applies the Bleed_CurrentLevel_External and Internal increases
        internal static void BleedDamage_Conversion_Apply(Entity entity, DamageSource lastBleedSource)
        {
            //verification//TODO: add return check if damage was even taken
            if (entity.GetBehavior<EntityBehaviorHealth>() is not EntityBehaviorHealth entity_HealthBehavior || entity.GetBehavior<BID_Lib_EntityManager.EntityBehavior_Bleed>() is not BID_Lib_EntityManager.EntityBehavior_Bleed entity_BleedBehavior) { BID_Lib_FunctionsGeneral.Log_Debug("Entity was missing BehaviorHealth or BehaviorBleed: {0}", loggers: [entity.GetName() ?? entity.GetPrefixAndCreatureName() ?? "null entity"]); return; }
            if (Config_Reference.Config_Loaded.Config_DamageType?.Dict_DamageType?.TryGetValue(lastBleedSource.Type, out var DamageType_OuterDict) is not true) { BID_Lib_FunctionsGeneral.Log_Debug("Dict_DamageType was null or missing damageType: {0}", loggers: [lastBleedSource.Type]); return; }//redeclare dict in case another entity calls bleed conversion in the same tick
            if (DamageType_OuterDict?.TryGetValue(BID_ModConfig.Config_DamageType.NameOf_DamageType_DirectMulti, out var _) is not true || DamageType_OuterDict?.TryGetValue(BID_ModConfig.Config_DamageType.NameOf_DamageType_BleedMultiExternal, out var _) is not true) { BID_Lib_FunctionsGeneral.Log_Debug("Dict_DamageType does not contain innerdict value(s) within Dict_DamageType: {0}", loggers: [lastBleedSource.Type]); return; } //dont check for internal bleed mods as DamageTypes can not have internal bleeding. Instead skip applying internal if missing


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
                    bleedToApply_External = MathF.Round(MathF.Max(damage_ToConvert * DamageType_OuterDict[BID_ModConfig.Config_DamageType.NameOf_DamageType_BleedMultiExternal], 0.0f), 4);
                }
                else { BID_Lib_FunctionsGeneral.Log_Debug("Bleed_External is disabled in config"); return; }

                //internal bleed
                if (Config_Reference.Config_Loaded.Config_System.System_BleedDamage_Acc.Bleed_Internal)
                {
                    if (DamageType_OuterDict?.TryGetValue(BID_ModConfig.Config_DamageType.NameOf_DamageType_BleedMultiInternal, out var _) is true && DamageType_OuterDict?.TryGetValue(BID_ModConfig.Config_DamageType.NameOf_DamageType_BleedConversionThresholdInternal, out var _) is true)
                    {
                        if (damage_ToConvert > DamageType_OuterDict[BID_ModConfig.Config_DamageType.NameOf_DamageType_BleedConversionThresholdInternal] && DamageType_OuterDict[BID_ModConfig.Config_DamageType.NameOf_DamageType_BleedMultiInternal] != 0)
                        {
                            float curve_Output = BleedDamage_InternalBleedCurve((damage_ToConvert - DamageType_OuterDict[BID_ModConfig.Config_DamageType.NameOf_DamageType_BleedConversionThresholdInternal]) * DamageType_OuterDict[BID_ModConfig.Config_DamageType.NameOf_DamageType_BleedMultiExternal]) * 0.01f;
                            bleedToApply_Internal = bleedToApply_External * curve_Output;
                            bleedToApply_External -= bleedToApply_Internal;
                        }
                        if (DamageType_OuterDict[BID_ModConfig.Config_DamageType.NameOf_DamageType_BleedMultiInternal] == 0) //if [Bleed_Multi_Internal] is 0 all external bleed applied is converted directly into internal 
                        {
                            bleedToApply_Internal = MathF.Round(MathF.Max(damage_ToConvert * DamageType_OuterDict[BID_ModConfig.Config_DamageType.NameOf_DamageType_BleedMultiExternal] - DamageType_OuterDict[BID_ModConfig.Config_DamageType.NameOf_DamageType_BleedConversionThresholdInternal], 0.0f), 4);
                            bleedToApply_External = 0;
                        }
                        else { BID_Lib_FunctionsGeneral.Log_Debug_Verbose("Damage did not surpass [{0}]", loggers: [BID_ModConfig.Config_DamageType.NameOf_DamageType_BleedConversionThresholdInternal]); }
                    }
                    else { BID_Lib_FunctionsGeneral.Log_Debug_Verbose("Dict_DamageType does not contain internal bleed values and is invalid for DamageType: {0}", loggers: [lastBleedSource.Type]); }
                }
                else { BID_Lib_FunctionsGeneral.Log_Debug_Verbose("Bleed_Internal is disabled in config"); return; }
            }
            catch(Exception e) { BID_Lib_FunctionsGeneral.Log_Error("Exception caught: {0}", loggers: [e.Message]); return; }


            //apply bleed
            entity_BleedBehavior.State_BleedReductionFlag = BID_Lib_FunctionsGeneral.Calc_Flag_SetBit(entity_BleedBehavior.State_BleedReductionFlag, 2, false); entity_BleedBehavior.State_BleedReductionFlag = BID_Lib_FunctionsGeneral.Calc_Flag_SetBit(entity_BleedBehavior.State_BleedReductionFlag, 3, false);
            entity_BleedBehavior.Bleed_CurrentLevel_External += bleedToApply_External * entity_BleedBehavior.categoryType_Dict[BID_ModConfig.Config_EntityType.NameOf_EntityCategory_DamageMod_Bleed_External];
            entity_BleedBehavior.Bleed_CurrentLevel_Internal += bleedToApply_Internal * entity_BleedBehavior.categoryType_Dict[BID_ModConfig.Config_EntityType.NameOf_EntityCategory_DamageMod_Bleed_Internal];
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
            if (entity.GetBehavior<BID_Lib_EntityManager.EntityBehavior_Bleed>() is not BID_Lib_EntityManager.EntityBehavior_Bleed entity_BleedBehavior) { return; } if (entity.GetBehavior<EntityBehaviorHealth>() is not EntityBehaviorHealth entity_HealthBehavior) { return; }
            EntityPlayer? entityPlayer = entity as EntityPlayer;


            float damageToApply = 0;
            float state_HealBonus_Resting;
            float state_BleedReduction_PressureOrCare;
            float state_BleedReduction_BandagedOrRagged;
            deltaTimeSum *= (entity.World.Calendar.CalendarSpeedMul * entity.World.Calendar.SpeedOfTime); deltaTimeSum /= (35f / Config_Reference.Config_Loaded.Config_TimeScale.TimeScale_BleedRate);

            //reductions
            if (entityPlayer is not null)
            {
                if (entityPlayer.Player.WorldData.CurrentGameMode == EnumGameMode.Creative || entityPlayer.Player.WorldData.CurrentGameMode == EnumGameMode.Spectator) { entity_BleedBehavior.Bleed_CurrentLevel_External = 0; entity_BleedBehavior.Bleed_CurrentLevel_Internal = 0 ; return; }
                if (Config_Reference.Config_Loaded.Config_System.System_BleedDamage_Acc.Bleed_SlowAtLowHealth) { deltaTimeSum *= BID_Lib_FunctionsGeneral.Calc_Curve_ExpoEaseOut(entity_HealthBehavior.Health, 0.8f, 0.8f, 0f, 0.2f); } //TODO: expose these in config

                bool isResting = (entityPlayer.Controls.Sneak || entityPlayer.Controls.FloorSitting || entityPlayer.MountedOn is BlockEntityBed) ? true : false; //TODO: add confort check

                entity_BleedBehavior.State_BleedReductionFlag = BID_Lib_FunctionsGeneral.Calc_Flag_SetBit(entity_BleedBehavior.State_BleedReductionFlag, 1, (isResting ? true : false));
                if (isResting) { entity_BleedBehavior.State_BleedReductionFlag = BID_Lib_FunctionsGeneral.Calc_Flag_SetBit(entity_BleedBehavior.State_BleedReductionFlag, 1, true); } else { entity_BleedBehavior.State_BleedReductionFlag = BID_Lib_FunctionsGeneral.Calc_Flag_SetBit(entity_BleedBehavior.State_BleedReductionFlag, 1, false); }
                state_HealBonus_Resting = entityPlayer.MountedOn is BlockEntityBed ? Config_Reference.Config_Loaded.Config_HealBonus.HealBonus_BleedHeal_Bed : (entityPlayer.MountedOn is BlockEntityPie ? Config_Reference.Config_Loaded.Config_HealBonus.HealBonus_BleedHeal_Comfort : (entityPlayer.Controls.FloorSitting ? Config_Reference.Config_Loaded.Config_HealBonus.HealBonus_BleedHeal_Ground : 0f));

            }
            else //TODO: add State_IsCare check once nonplayer entitys can care for bleed
            {
                if (false) { entity_BleedBehavior.State_BleedReductionFlag = BID_Lib_FunctionsGeneral.Calc_Flag_SetBit(entity_BleedBehavior.State_BleedReductionFlag, 0, true); } else { entity_BleedBehavior.State_BleedReductionFlag = BID_Lib_FunctionsGeneral.Calc_Flag_SetBit(entity_BleedBehavior.State_BleedReductionFlag, 0, false); }
                state_HealBonus_Resting = false ? Config_Reference.Config_Loaded.Config_HealBonus.HealBonus_BleedReduction_Care : 0f;
            }
            state_BleedReduction_PressureOrCare = BID_Lib_FunctionsGeneral.Calc_Flag_CheckBit(entity_BleedBehavior.State_BleedReductionFlag, 1) ? Config_Reference.Config_Loaded.Config_HealBonus.HealBonus_BleedReduction_Pressure : (BID_Lib_FunctionsGeneral.Calc_Flag_CheckBit(entity_BleedBehavior.State_BleedReductionFlag, 0) ? Config_Reference.Config_Loaded.Config_HealBonus.HealBonus_BleedReduction_Care : 0f);
            state_BleedReduction_BandagedOrRagged = BID_Lib_FunctionsGeneral.Calc_Flag_CheckBit(entity_BleedBehavior.State_BleedReductionFlag, 3) ? Config_Reference.Config_Loaded.Config_HealBonus.HealBonus_BleedReduction_Bandage : (BID_Lib_FunctionsGeneral.Calc_Flag_CheckBit(entity_BleedBehavior.State_BleedReductionFlag, 2) ? Config_Reference.Config_Loaded.Config_HealBonus.HealBonus_BleedReduction_Rag : 0f);

            //activity
            if (Config_Reference.Config_Loaded.Config_System.System_BleedDamage_Acc.Bleed_ActivityIncreaseRate) //TODO: apply a curve instead so values target a peak value (in config); replace toggle with max multiplier and set to 1.0 to disable
            {
                float entity_ActivityMulti = 1f; bool entity_IsPressure = false;
                if (entityPlayer is not null) //AcitivityMulti for player specific actions
                {
                    entity_ActivityMulti += (entityPlayer.Controls.Sprint) ? Config_Reference.Config_Loaded.Config_Rate.Rate_Activity_Acc.ActivityMulti_Sprint : 0f;
                    entity_ActivityMulti += (entityPlayer.Controls.LeftMouseDown) ? Config_Reference.Config_Loaded.Config_Rate.Rate_Activity_Acc.ActivityMulti_Hit : 0f;
                    entity_IsPressure = entityPlayer.Controls.Sneak;
                }
                entity_ActivityMulti += (entity.Pos.Motion.HorLength() > 0 && !entity_IsPressure) ? Config_Reference.Config_Loaded.Config_Rate.Rate_Activity_Acc.ActivityMulti_Walk : 0f;
                deltaTimeSum *= entity_ActivityMulti;
            }

            //calc external bleed damage
            if (entity_BleedBehavior.Bleed_CurrentLevel_External > 0f)
            {
                float damageToAdd = entity_BleedBehavior.Bleed_CurrentLevel_External * (deltaTimeSum * Config_Reference.Config_Loaded.Config_Rate.Rate_Bleed_Acc.Damage_External / MathF.Max((state_BleedReduction_PressureOrCare + state_BleedReduction_BandagedOrRagged), 1f));
                if (Config_Reference.Config_Loaded.Config_System.System_BleedDamage_Acc.BleedCanDamage_External) { damageToApply += Config_Reference.Config_Loaded.Config_System.System_BleedDamage_Acc.BleedCanKill_External ? damageToAdd : MathF.Min(damageToAdd, MathF.Max((entity_HealthBehavior.Health - 0.01f), 0f)); }
                entity_BleedBehavior.Bleed_CurrentLevel_External -= (deltaTimeSum * Config_Reference.Config_Loaded.Config_Rate.Rate_Bleed_Acc.FlatHeal_External * MathF.Max(state_HealBonus_Resting, 1f)) + (deltaTimeSum * entity_BleedBehavior.Bleed_CurrentLevel_External * Config_Reference.Config_Loaded.Config_Rate.Rate_Bleed_Acc.ScaledtHeal_External);
            }

            //calc internal bleed damage
            if (entity_BleedBehavior.Bleed_CurrentLevel_Internal > 0f)
            {
                float damageToAdd = entity_BleedBehavior.Bleed_CurrentLevel_Internal * (deltaTimeSum * Config_Reference.Config_Loaded.Config_Rate.Rate_Bleed_Acc.Damage_Internal / MathF.Max( 0f/*TODO: add limited internal stemming check*/, 1f));
                if (Config_Reference.Config_Loaded.Config_System.System_BleedDamage_Acc.BleedCanDamage_Internal) { damageToApply += Config_Reference.Config_Loaded.Config_System.System_BleedDamage_Acc.BleedCanKill_Internal ? damageToAdd : MathF.Min(damageToAdd, MathF.Max((entity_HealthBehavior.Health - 0.01f), 0f)); }
                entity_BleedBehavior.Bleed_CurrentLevel_Internal -= (deltaTimeSum * Config_Reference.Config_Loaded.Config_Rate.Rate_Bleed_Acc.FlatHeal_Internal * MathF.Max(state_HealBonus_Resting * (entityPlayer?.Controls.FloorSitting is true ? Config_Reference.Config_Loaded.Config_HealBonus.HealBonus_BleedHeal_InternalRestBonus : 1f), 1f)) + (deltaTimeSum * entity_BleedBehavior.Bleed_CurrentLevel_Internal * Config_Reference.Config_Loaded.Config_Rate.Rate_Bleed_Acc.ScaledHeal_Internal);
            }
            
            //apply all bleed damage
            if (entity_BleedBehavior.Bleed_CurrentLevel_External <= 0) { entity_BleedBehavior.State_BleedReductionFlag = BID_Lib_FunctionsGeneral.Calc_Flag_SetBit(entity_BleedBehavior.State_BleedReductionFlag, 3, false); entity_BleedBehavior.State_BleedReductionFlag = BID_Lib_FunctionsGeneral.Calc_Flag_SetBit(entity_BleedBehavior.State_BleedReductionFlag, 2, false); }
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
            float heal_PreCalc = appliedDamage;

            if (appliedDamage >= Config_Reference.Config_Loaded.Config_HealBonus.HealBonus_BleedHeal_RagToBandageThreshold) { entity_BleedBehavior.State_BleedReductionFlag = BID_Lib_FunctionsGeneral.Calc_Flag_SetBit(entity_BleedBehavior.State_BleedReductionFlag, 3, true); } else { entity_BleedBehavior.State_BleedReductionFlag = BID_Lib_FunctionsGeneral.Calc_Flag_SetBit(entity_BleedBehavior.State_BleedReductionFlag, 2, true); }//TODO: only apply this if the item used is certain healing items (ie. bandage)
            if (entity_BleedBehavior.Bleed_CurrentLevel_External > 0) { appliedDamage = MathF.Max(appliedDamage * Config_Reference.Config_Loaded.Config_HealBonus.HealBonus_HealReduction_Bandage, 0f); }
            entity_BleedBehavior.Bleed_CurrentLevel_External -= bleed_ToRemove;

            BID_Lib_FunctionsGeneral.Log_Debug_Verbose("Healing Item healed Bleed_CurrentLevel_External by: {0}; New State_BleedReduction: {1}; Heal amount reduced: {2}->{3}", loggers: [bleed_ToRemove, entity_BleedBehavior.State_BleedReductionFlag, heal_PreCalc, appliedDamage]);
            return appliedDamage;
        }


        internal static void BleedDamage_NotifyOnHit(EntityPlayer entityPlayer, float bleedToApply_External, float bleedToApply_Internal)
        {
            if (Config_Reference.Config_Loaded.Config_BleedReport.BleedReport_Detailed)
            {
                ((IServerPlayer)entityPlayer.Player).SendMessage(GlobalConstants.DamageLogChatGroup, Lang.Get("bleedingindepth:damageLog_BleedNotify_External", [bleedToApply_External]) + (Config_Reference.Config_Loaded.Config_BleedReport.BleedReport_Internal ? Lang.Get("bleedingindepth:damageLog_BleedNotify_Internal", [bleedToApply_Internal]) : ""), EnumChatType.Notification);
            }
            else
            {
                string vagueBleed_External = "None";
                string vagueBleed_Internal = "None";

                foreach (var threashold in Config_Reference.Config_Loaded.Config_BleedReport.List_BleedReport_DPS_SeverityThreashold) { if ((bleedToApply_External * Config_Reference.Config_Loaded.Config_Rate.Rate_Bleed_Acc.Damage_External) >= threashold.BleedLevel) { vagueBleed_External = threashold.Severity; break; } }
                foreach (var threashold in Config_Reference.Config_Loaded.Config_BleedReport.List_BleedReport_DPS_SeverityThreashold) { if ((bleedToApply_Internal * Config_Reference.Config_Loaded.Config_Rate.Rate_Bleed_Acc.Damage_Internal) * normalizer_InternalBleed >= threashold.BleedLevel) { vagueBleed_Internal = threashold.Severity; break; } } //multiply by arbitrary value to get internal within a similar range; TODO: tweak
                ((IServerPlayer)entityPlayer.Player).SendMessage(GlobalConstants.DamageLogChatGroup, Lang.Get("bleedingindepth:damageLog_BleedNotify_External", [vagueBleed_External]) + ((Config_Reference.Config_Loaded.Config_BleedReport.BleedReport_Internal && bleedToApply_Internal > 0) ? Lang.Get("bleedingindepth:damageLog_BleedNotify_Internal", [vagueBleed_Internal]) : ""), EnumChatType.Notification);
                BID_Lib_FunctionsGeneral.Log_Debug("Player recieved vague report for External: {0}({1}); Internal: {2}({3})", loggers: [vagueBleed_External, bleedToApply_External, vagueBleed_Internal, bleedToApply_Internal]);
            }
        }


        //called for converting to internal bleed; big hits that deal bleed past a specific # will be diverted somewhat (more bleed above threshold means more convert %) into an internal wound that is much harder to heal manually. internal bleeding is slower but heals slower effectively doing more damage; not effected by pressure application
        internal static float BleedDamage_InternalBleedCurve(float input_x) 
        {
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
