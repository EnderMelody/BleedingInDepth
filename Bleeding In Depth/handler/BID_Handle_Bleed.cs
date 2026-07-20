using BleedingInDepth.config;
using BleedingInDepth.lib;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace BleedingInDepth.handler
{
    internal class BID_Handle_Bleed
    {
        private static ICoreAPI API = BID_VarRef.API;
        //private static ICoreClientAPI API_Client = BID_VarRef.API_Client;
        //private static ICoreServerAPI API_Server = BID_VarRef.API_Server;

        internal static FrozenDictionary<EnumDamageType, Dictionary<string, float>>? DamageType_Dict_ConfigCache;



        /// <summary>
        /// called on entity BehaviorBleed.OnEntityReceiveDamage;
        /// performs damagetype checks -> stores values to be used in post-armor bleed conversion -> reduces direct damage applied and resumes vanilla logic
        /// </summary>
        /// <param name="entity">the entity that recieved damage</param>
        /// <param name="damageSource">the damage source that applied damage to the entity</param>
        /// <param name="appliedDamage">the actual value of damage applied to the entity; PRE armor damage reduction</param>
        internal static float Bleed_Conversion_Store(Entity entity, float appliedDamage, DamageSource damageSource)
        {
            //applicable verification
            if (appliedDamage <= 0f) { BID_Function_General.Log_Debug("appliedDamage was not positive! {0}", loggers: [appliedDamage]); return appliedDamage; }
            if (entity.GetBehavior<EntityBehaviorHealth>() is not EntityBehaviorHealth entity_BehaviorHealth || entity.GetBehavior<BID_Handle_Entity.EntityBehavior_Bleed>() is not BID_Handle_Entity.EntityBehavior_Bleed entity_BehaviorBleed)
            { BID_Function_General.Log_Debug("Entity was missing BehaviorHealth or BehaviorBleed: {0}", loggers: [entity.GetName() ?? entity.GetPrefixAndCreatureName() ?? "null entity"]); return appliedDamage; }


            //damagetype verification
            switch (damageSource.Type)
            {
                case EnumDamageType.Heal: { return BleedDamage_Calc_Heal(entity, appliedDamage); } //TODO: find way to hook into initial bandage application instead
                case EnumDamageType.Fire: { return BleedDamage_Calc_Cauterize(entity, appliedDamage); } //TODO: see if i can inject in entity.ApplyFireDamage or similar method instead//needed?
            }

            if (Config_Reference.Config_Loaded.Config_System.System_Damage_Acc.UseDamageTypeCompat is false && Config_Reference.Config_Loaded.Config_TypeModifier?.TypeMod_Damage_Acc?.Dict_DamageType?.ContainsKey(damageSource.Type) == true)//TODO: remove... eventually
            { damageSource.Type = EnumDamageType.SlashingAttack; } //if vanilla updates attacks to use DamageTypes, this toggle will allow the system to use per DamageType values without requiring me to update the mod immediately

            if (DamageType_Dict_ConfigCache?.TryGetValue(damageSource.Type, out var DamageType_OuterDict) is not true) { BID_Function_General.Log_Debug("Dict_DamageType was null or missing damageType: {0}", loggers: [damageSource.Type]); return appliedDamage; }
            if (DamageType_OuterDict?.TryGetValue(BID_Config_Main.Config_TypeModifier.TypeMod_Damage.NameOf_DamageType_DirectMulti, out var _) is not true || DamageType_OuterDict?.TryGetValue(BID_Config_Main.Config_TypeModifier.TypeMod_Damage.NameOf_DamageType_BleedMultiExternal, out var _) is not true)
            { BID_Function_General.Log_Debug("Dict_DamageType does not contain innerdict value(s) within Dict_DamageType: {0}", loggers: [damageSource.Type]); return appliedDamage; } //dont check for internal bleed mods as DamageTypes can not have internal bleeding. Instead skip applying internal if missing


            //check and store entity category type
            if (entity_BehaviorBleed.CategoryType_Dict is null)//TODO: build tagset from list in config
            {
                string entity_CategoryType = "no matching tag";
                foreach (string checkTag in Config_Reference.Config_Loaded.Config_TypeModifier.TypeMod_Entity_Acc.Dict_EntityCategory.Keys)
                {
                    BID_Function_General.Log_Debug_Verbose("checked entity {0} for tag {1}", loggers: [entity.GetPrefixAndCreatureName(), checkTag]);
                    API.EntityTagRegistry.TryCreateTagSet(out TagSetFast checkTagFast, checkTag);
                    if (entity.Tags.Overlaps(checkTagFast))
                    {
                        entity_CategoryType = checkTag;
                        break;
                    }
                }
                if (Config_Reference.Config_Loaded.Config_TypeModifier?.TypeMod_Entity_Acc?.Dict_EntityCategory?.TryGetValue(entity_CategoryType, out Dictionary<string, float>? entityCategoryDict) is not true)
                {
                    BID_Function_General.Log_Debug("{0} failed to match tags in Dict_EntityCategory or dict was null or missing categoryType: {1}, defaulting 1f", loggers: [entity.GetPrefixAndCreatureName(), entity_CategoryType]);
                    entityCategoryDict = new() { [BID_Config_Main.Config_TypeModifier.TypeMod_Entity.NameOf_EntityCategory_DamageMod_Direct] = 1f, [BID_Config_Main.Config_TypeModifier.TypeMod_Entity.NameOf_EntityCategory_DamageMod_Bleed_External] = 1f,
                        [BID_Config_Main.Config_TypeModifier.TypeMod_Entity.NameOf_EntityCategory_DamageMod_Bleed_Internal] = 1f, [BID_Config_Main.Config_TypeModifier.TypeMod_Entity.NameOf_EntityCategory_Effect_Particle_Color] = 0f };
                }
                else { BID_Function_General.Log_Debug("{0} successfully matched categoryType: {1}", loggers: [entity.GetPrefixAndCreatureName(), entity_CategoryType]); }
                entity_BehaviorBleed.CategoryType_Dict = entityCategoryDict.ToFrozenDictionary();
            }


            //convert and store values
            entity_BehaviorBleed.Health_PreDamage = entity_BehaviorHealth.Health + entity_BehaviorBleed.AppliedDamage_Base;
            entity_BehaviorBleed.AppliedDamage_Base += appliedDamage;
            entity_BehaviorBleed.LastBleedSource = damageSource;
            float returnedDamage = MathF.Round(MathF.Max(appliedDamage * DamageType_OuterDict[BID_Config_Main.Config_TypeModifier.TypeMod_Damage.NameOf_DamageType_DirectMulti] * entity_BehaviorBleed.CategoryType_Dict[BID_Config_Main.Config_TypeModifier.TypeMod_Entity.NameOf_EntityCategory_DamageMod_Direct], 0.0f), 4);
            BID_Function_General.Log_Debug("Stored [health_PreDamage: {0}; appliedDamage_Base: {1} lastBleedSource.Type: {2}]", loggers: [entity_BehaviorBleed.Health_PreDamage, entity_BehaviorBleed.AppliedDamage_Base, entity_BehaviorBleed.LastBleedSource.Type]);

            return returnedDamage;
        }


        /// <summary>
        /// called on entity BehaviorBleed.OnGameTick;
        /// compare health before and after armor damage reduction -> calculate actual damage taken -> calculate and apply Bleed_CurrentLevel_External/Internal increases
        /// </summary>
        /// <param name="entity">the entity that recieved damage</param>
        /// <param name="deltaTimeSum">UNUSED; the total time passed since the last OnGameTick was called</param>
        internal static void BleedDamage_Conversion_Apply(Entity entity)
        {
            //applicable verification
            if (entity.GetBehavior<EntityBehaviorHealth>() is not EntityBehaviorHealth entity_BehaviorHealth || entity.GetBehavior<BID_Handle_Entity.EntityBehavior_Bleed>() is not BID_Handle_Entity.EntityBehavior_Bleed entity_BehaviorBleed)
            { BID_Function_General.Log_Debug("Entity was missing BehaviorHealth or BehaviorBleed: {0}", loggers: [entity.GetName() ?? entity.GetPrefixAndCreatureName() ?? "null entity"]); return; }
            if (!(entity_BehaviorBleed.LastBleedSource is not null && entity_BehaviorBleed.AppliedDamage_Base is not 0f)) { return; }

            if (Config_Reference.Config_Loaded.Config_TypeModifier?.TypeMod_Damage_Acc?.Dict_DamageType?.TryGetValue(entity_BehaviorBleed.LastBleedSource.Type, out var DamageType_OuterDict) is not true) { BID_Function_General.Log_Debug("Dict_DamageType was null or missing damageType: {0}", loggers: [entity_BehaviorBleed.LastBleedSource.Type]); return; }//redeclare dict in case another entity calls bleed conversion in the same tick
            if (DamageType_OuterDict?.TryGetValue(BID_Config_Main.Config_TypeModifier.TypeMod_Damage.NameOf_DamageType_DirectMulti, out var _) is not true || DamageType_OuterDict?.TryGetValue(BID_Config_Main.Config_TypeModifier.TypeMod_Damage.NameOf_DamageType_BleedMultiExternal, out var _) is not true)
            { BID_Function_General.Log_Debug("Dict_DamageType does not contain innerdict value(s) within Dict_DamageType: {0}", loggers: [entity_BehaviorBleed.LastBleedSource.Type]); return; } //dont check for internal bleed mods as DamageTypes can not have internal bleeding. Instead skip applying internal if missing

            float bleedToApply_External = 0f;
            float bleedToApply_Internal = 0f;

            float damageTaken_PreArmor = entity_BehaviorBleed.AppliedDamage_Base * DamageType_OuterDict[BID_Config_Main.Config_TypeModifier.TypeMod_Damage.NameOf_DamageType_DirectMulti];
            float damageTaken_PostArmor = entity_BehaviorBleed.Health_PreDamage - entity_BehaviorHealth.Health;
            float damageReduced_ByArmor = damageTaken_PostArmor / damageTaken_PreArmor;

            float damage_ToConvert = (entity_BehaviorBleed.AppliedDamage_Base * damageReduced_ByArmor) + ((1f + Config_Reference.Config_Loaded.Curve_Variable.Variable_External_Acc.External_ArmorPierce) * (damageTaken_PreArmor - damageTaken_PostArmor) * DamageType_OuterDict[BID_Config_Main.Config_TypeModifier.TypeMod_Damage.NameOf_DamageType_BleedMultiExternal]);


            //calc bleed to apply
            //external bleed
            bleedToApply_External = MathF.Round(MathF.Max(damage_ToConvert * DamageType_OuterDict[BID_Config_Main.Config_TypeModifier.TypeMod_Damage.NameOf_DamageType_BleedMultiExternal], 0f), 4);

            //internal bleed
            if (Config_Reference.Config_Loaded.Config_System.System_Damage_Acc.Bleed_Internal)
            {
                if (DamageType_OuterDict?.TryGetValue(BID_Config_Main.Config_TypeModifier.TypeMod_Damage.NameOf_DamageType_BleedMultiInternal, out var _) is true && DamageType_OuterDict?.TryGetValue(BID_Config_Main.Config_TypeModifier.TypeMod_Damage.NameOf_DamageType_BleedConversionThresholdInternal, out var _) is true)
                {
                    if (DamageType_OuterDict[BID_Config_Main.Config_TypeModifier.TypeMod_Damage.NameOf_DamageType_BleedMultiInternal] == 0f) //if [Bleed_Multi_Internal] is 0 all external bleed applied is converted directly into internal 
                    {
                        bleedToApply_Internal = MathF.Round(MathF.Max(bleedToApply_External - (DamageType_OuterDict[BID_Config_Main.Config_TypeModifier.TypeMod_Damage.NameOf_DamageType_BleedConversionThresholdInternal] * DamageType_OuterDict[BID_Config_Main.Config_TypeModifier.TypeMod_Damage.NameOf_DamageType_BleedMultiExternal]), 0f), 4);
                        bleedToApply_External = 0;
                    }
                    else if (damage_ToConvert > DamageType_OuterDict[BID_Config_Main.Config_TypeModifier.TypeMod_Damage.NameOf_DamageType_BleedConversionThresholdInternal])
                    {
                        float curve_Output = BleedDamage_InternalBleedCurve((damage_ToConvert - DamageType_OuterDict[BID_Config_Main.Config_TypeModifier.TypeMod_Damage.NameOf_DamageType_BleedConversionThresholdInternal]) * DamageType_OuterDict[BID_Config_Main.Config_TypeModifier.TypeMod_Damage.NameOf_DamageType_BleedMultiExternal]) * 0.01f;
                        bleedToApply_Internal = bleedToApply_External * curve_Output;
                        bleedToApply_External -= bleedToApply_Internal;
                    } else { BID_Function_General.Log_Debug_Verbose("Damage did not surpass [{0}]", loggers: [BID_Config_Main.Config_TypeModifier.TypeMod_Damage.NameOf_DamageType_BleedConversionThresholdInternal]); }
                } else { BID_Function_General.Log_Debug_Verbose("Dict_DamageType does not contain internal bleed values and is invalid for DamageType: {0}", loggers: [entity_BehaviorBleed.LastBleedSource.Type]); }
            }  else { BID_Function_General.Log_Debug_Verbose("Bleed_Internal is disabled in config"); }

            //apply bleed
            entity_BehaviorBleed.State_BleedReductionFlag = BID_Function_General.Calc_Flag_SetBit(entity_BehaviorBleed.State_BleedReductionFlag, 2, false); entity_BehaviorBleed.State_BleedReductionFlag = BID_Function_General.Calc_Flag_SetBit(entity_BehaviorBleed.State_BleedReductionFlag, 3, false);
            entity_BehaviorBleed.Bleed_CurrentLevel_External += (Config_Reference.Config_Loaded.Config_System.System_Damage_Acc.Bleed_External ? bleedToApply_External : 0f);
            entity_BehaviorBleed.Bleed_CurrentLevel_Internal += bleedToApply_Internal;
            if (Config_Reference.Config_Loaded.Config_BleedReport.BleedReport_NotifyOnHit && entity is EntityPlayer entityPlayer) { BleedDamage_NotifyOnHit(entityPlayer, bleedToApply_External, bleedToApply_Internal); }
            BID_Function_General.Log_Debug("Applied {0} damage, {1} Bleed_External and {2} Bleed_Internal to {3} from {4} which dealt {5} base damage of {6} DamageType",
                loggers: [entity_BehaviorBleed.Health_PreDamage - entity_BehaviorHealth.Health, bleedToApply_External, bleedToApply_Internal, entity.GetName() ?? entity.GetPrefixAndCreatureName(), entity_BehaviorBleed.LastBleedSource.CauseEntity?.GetPrefixAndCreatureName() ?? entity_BehaviorBleed.LastBleedSource.SourceEntity?.GetPrefixAndCreatureName() ?? "null", entity_BehaviorBleed.AppliedDamage_Base, entity_BehaviorBleed.LastBleedSource.Type]);

            //reset values
            entity_BehaviorBleed.Health_PreDamage = 0f;
            entity_BehaviorBleed.AppliedDamage_Base = 0f;
            entity_BehaviorBleed.DeltaTime_LastHit = 0f; //TODO: remove once care animation is implemented
        }


        internal static float BleedDamage_Calc_Heal(Entity entity, float appliedDamage) //TODO: rewrite to run from item use event
        {
            if (entity.GetBehavior<BID_Handle_Entity.EntityBehavior_Bleed>() is not BID_Handle_Entity.EntityBehavior_Bleed entity_BehaviorBleed) { return appliedDamage; }
            float bleed_ToRemove = MathF.Max(appliedDamage * Config_Reference.Config_Loaded.Curve_Variable.Variable_HealBonus_Acc.HealBonus_BleedReduction_Percent_Bandage + Config_Reference.Config_Loaded.Curve_Variable.Variable_HealBonus_Acc.HealBonus_BleedReduction_Flat_Bandage, 0f);
            float heal_PreCalc = appliedDamage;

            if (appliedDamage >= Config_Reference.Config_Loaded.Curve_Variable.Variable_HealBonus_Acc.HealBonus_BleedHeal_Min_Bandage) { entity_BehaviorBleed.State_BleedReductionFlag = BID_Function_General.Calc_Flag_SetBit(entity_BehaviorBleed.State_BleedReductionFlag, 3, true);entity_BehaviorBleed.State_BleedReductionFlag = BID_Function_General.Calc_Flag_SetBit(entity_BehaviorBleed.State_BleedReductionFlag, 2, false); }
            else if (appliedDamage >= Config_Reference.Config_Loaded.Curve_Variable.Variable_HealBonus_Acc.HealBonus_BleedHeal_Min_Rag && !BID_Function_General.Calc_Flag_CheckBit(entity_BehaviorBleed.State_BleedReductionFlag, 3)) { entity_BehaviorBleed.State_BleedReductionFlag = BID_Function_General.Calc_Flag_SetBit(entity_BehaviorBleed.State_BleedReductionFlag, 2, true); }
            if (entity_BehaviorBleed.Bleed_CurrentLevel_External > 0) { appliedDamage = MathF.Max(appliedDamage * Config_Reference.Config_Loaded.Curve_Variable.Variable_HealBonus_Acc.HealBonus_HealReduction_Bandage, 0f); }
            entity_BehaviorBleed.Bleed_CurrentLevel_External -= bleed_ToRemove;

            BID_Function_General.Log_Debug_Verbose("Healing Item healed Bleed_CurrentLevel_External by: {0}; New State_BleedReduction: {1}; Heal amount reduced: {2}->{3}", loggers: [bleed_ToRemove, entity_BehaviorBleed.State_BleedReductionFlag, heal_PreCalc, appliedDamage]);
            return appliedDamage;
        }


        internal static float BleedDamage_Calc_Cauterize(Entity entity, float appliedDamage)
        {
            if (entity.GetBehavior<BID_Handle_Entity.EntityBehavior_Bleed>() is not BID_Handle_Entity.EntityBehavior_Bleed entity_BehaviorBleed) { return appliedDamage; }

            if (entity_BehaviorBleed.Bleed_CurrentLevel_External > 0)
            {
                float cauterizeAmount = appliedDamage * Config_Reference.Config_Loaded.Curve_Variable.Variable_HealBonus_Acc.HealBonus_BleedHeal_Cauterize;
                entity_BehaviorBleed.Bleed_CurrentLevel_External = MathF.Max(entity_BehaviorBleed.Bleed_CurrentLevel_External - cauterizeAmount, 0f);
                if (entity is EntityPlayer entityPlayer && Config_Reference.Config_Loaded.Config_BleedReport.BleedReport_NotifyOnHit)
                { ((IServerPlayer)entityPlayer.Player).SendMessage(GlobalConstants.DamageLogChatGroup, Lang.Get("bleedingindepth:damageLog_BleedNotify_Cauterize", [cauterizeAmount]), EnumChatType.Notification); }
                
                BID_Function_General.Log_Debug("Cauterized [BleedLevel_External] by: {0}", loggers: [cauterizeAmount]);
            }

            return appliedDamage;
        }


        /// <summary>
        /// called on entity BehaviorBleed.OnGameTick;
        /// applies damage from bleed based on time passed since the last bleed damage tick was called
        /// </summary>
        /// <param name="entity">the entity bleeding</param>
        /// <param name="deltaTimeSum">the total time passed since the last OnGameTick was called</param>
        internal static void BleedDamage_Tick(Entity entity, float deltaTime_Sum)
        {
            if (deltaTime_Sum < Config_Reference.Config_Loaded.Config_TimeScale.DeltaTime_SumRequired_BleedRate) { return; }
            if (entity.GetBehavior<BID_Handle_Entity.EntityBehavior_Bleed>() is not BID_Handle_Entity.EntityBehavior_Bleed entity_BehaviorBleed || entity.GetBehavior<EntityBehaviorHealth>() is not EntityBehaviorHealth entity_BehaviorHealth) { return; }
            EntityPlayer? entityPlayer = entity as EntityPlayer;

            float damageToApply = 0;
            float state_HealBonus_Resting;
            float state_BleedReduction_PressureOrCare;
            float state_BleedReduction_BandagedOrRagged;
            bool isResting = false;
            deltaTime_Sum *= (entity.World.Calendar.CalendarSpeedMul * entity.World.Calendar.SpeedOfTime); deltaTime_Sum /= (35f / Config_Reference.Config_Loaded.Config_TimeScale.TimeScale_BleedRate);


            //reductions
            if (entityPlayer is not null)
            {
                if (entityPlayer.Player.WorldData.CurrentGameMode == EnumGameMode.Creative || entityPlayer.Player.WorldData.CurrentGameMode == EnumGameMode.Spectator) { entity_BehaviorBleed.Bleed_CurrentLevel_External = 0; entity_BehaviorBleed.Bleed_CurrentLevel_Internal = 0; return; }
                if (Config_Reference.Config_Loaded.Config_System.System_Damage_Acc.Bleed_SlowAtLowHealth) { deltaTime_Sum *= BID_Function_General.Calc_Curve_ExpoEaseOut(entity_BehaviorHealth.Health, 0.8f, 0.8f, 0f, 0.2f); } //TODO: expose these in config

                isResting = (entityPlayer.Controls.Sneak || entityPlayer.Controls.FloorSitting || entityPlayer.MountedOn is BlockEntityBed); //TODO: add confort check

                entity_BehaviorBleed.State_BleedReductionFlag = BID_Function_General.Calc_Flag_SetBit(entity_BehaviorBleed.State_BleedReductionFlag, 1, isResting);
                if (isResting) { entity_BehaviorBleed.State_BleedReductionFlag = BID_Function_General.Calc_Flag_SetBit(entity_BehaviorBleed.State_BleedReductionFlag, 1, true); } else { entity_BehaviorBleed.State_BleedReductionFlag = BID_Function_General.Calc_Flag_SetBit(entity_BehaviorBleed.State_BleedReductionFlag, 1, false); }
                state_HealBonus_Resting = entityPlayer.MountedOn is BlockEntityBed ? Config_Reference.Config_Loaded.Curve_Variable.Variable_HealBonus_Acc.HealBonus_BleedHeal_Bed : (entityPlayer.MountedOn is BlockEntityPie ? Config_Reference.Config_Loaded.Curve_Variable.Variable_HealBonus_Acc.HealBonus_BleedHeal_Comfort : (entityPlayer.Controls.FloorSitting ? Config_Reference.Config_Loaded.Curve_Variable.Variable_HealBonus_Acc.HealBonus_BleedHeal_Ground : 0f));

            }
            else //TODO: add State_IsCare check once nonplayer entitys can care for bleed
            {
                if (entity_BehaviorBleed.DeltaTime_LastHit > Config_Reference.Config_Loaded.Curve_Variable.Variable_HealBonus_Acc.HealBonus_DeltaTimeSum_Care)
                { entity_BehaviorBleed.State_BleedReductionFlag = BID_Function_General.Calc_Flag_SetBit(entity_BehaviorBleed.State_BleedReductionFlag, 0, true); }
                else { entity_BehaviorBleed.DeltaTime_LastHit += deltaTime_Sum; entity_BehaviorBleed.State_BleedReductionFlag = BID_Function_General.Calc_Flag_SetBit(entity_BehaviorBleed.State_BleedReductionFlag, 0, false); }
                state_HealBonus_Resting = BID_Function_General.Calc_Flag_CheckBit(entity_BehaviorBleed.State_BleedReductionFlag, 0) ? Config_Reference.Config_Loaded.Curve_Variable.Variable_HealBonus_Acc.HealBonus_BleedReduction_Care : 0f;
            }
            state_BleedReduction_PressureOrCare = BID_Function_General.Calc_Flag_CheckBit(entity_BehaviorBleed.State_BleedReductionFlag, 1) ? Config_Reference.Config_Loaded.Curve_Variable.Variable_HealBonus_Acc.HealBonus_BleedReduction_Pressure : (BID_Function_General.Calc_Flag_CheckBit(entity_BehaviorBleed.State_BleedReductionFlag, 0) ? Config_Reference.Config_Loaded.Curve_Variable.Variable_HealBonus_Acc.HealBonus_BleedReduction_Care : 0f);
            state_BleedReduction_BandagedOrRagged = BID_Function_General.Calc_Flag_CheckBit(entity_BehaviorBleed.State_BleedReductionFlag, 3) ? Config_Reference.Config_Loaded.Curve_Variable.Variable_HealBonus_Acc.HealBonus_BleedReduction_Bandage : (BID_Function_General.Calc_Flag_CheckBit(entity_BehaviorBleed.State_BleedReductionFlag, 2) ? Config_Reference.Config_Loaded.Curve_Variable.Variable_HealBonus_Acc.HealBonus_BleedReduction_Rag : 0f);

            //activity
            if (Config_Reference.Config_Loaded.Config_System.System_Damage_Acc.Bleed_ActivityIncreaseRate) //TODO: apply a curve instead so values target a peak value (in config); replace toggle with max multiplier and set to 1.0 to disable
            {
                float entity_ActivityMulti = 1f; bool entity_IsPressure = false;
                if (entityPlayer is not null) //AcitivityMulti for player specific actions
                {
                    entity_ActivityMulti += (entityPlayer.Controls.Sprint) ? Config_Reference.Config_Loaded.Curve_Variable.Variable_Activity_Acc.ActivityMulti_Sprint : 0f;
                    entity_ActivityMulti += (entityPlayer.Controls.LeftMouseDown) ? Config_Reference.Config_Loaded.Curve_Variable.Variable_Activity_Acc.ActivityMulti_Hit : 0f;
                    entity_IsPressure = entityPlayer.Controls.Sneak;
                }
                entity_ActivityMulti += (entity.Pos.Motion.HorLength() > 0 && !entity_IsPressure) ? Config_Reference.Config_Loaded.Curve_Variable.Variable_Activity_Acc.ActivityMulti_Walk : 0f;
                deltaTime_Sum *= entity_ActivityMulti;
            }

            //calc external bleed damage
            if (entity_BehaviorBleed.Bleed_CurrentLevel_External > 0f)
            {
                float damageToAdd = entity_BehaviorBleed.Bleed_CurrentLevel_External * (deltaTime_Sum * Config_Reference.Config_Loaded.Curve_Variable.Variable_External_Acc.External_Rate / MathF.Max((state_BleedReduction_PressureOrCare + state_BleedReduction_BandagedOrRagged), 1f));
                if (Config_Reference.Config_Loaded.Config_System.System_Damage_Acc.BleedCanDamage_External) { damageToApply += Config_Reference.Config_Loaded.Config_System.System_Damage_Acc.BleedCanKill_External ? damageToAdd : MathF.Min(damageToAdd, MathF.Max((entity_BehaviorHealth.Health - 0.01f), 0f)); }
                entity_BehaviorBleed.Bleed_CurrentLevel_External -= (deltaTime_Sum * Config_Reference.Config_Loaded.Curve_Variable.Variable_External_Acc.External_FlatHeal * MathF.Max(state_HealBonus_Resting, 1f)) + (deltaTime_Sum * entity_BehaviorBleed.Bleed_CurrentLevel_External * Config_Reference.Config_Loaded.Curve_Variable.Variable_External_Acc.External_ScaledHeal);
            }

            //calc internal bleed damage
            if (entity_BehaviorBleed.Bleed_CurrentLevel_Internal > 0f)
            {
                float deltaTime_Sum_Temp = deltaTime_Sum * (entityPlayer is not null ? (isResting ? 1.2f : 1f) : 1f);
                float damageToAdd = entity_BehaviorBleed.Bleed_CurrentLevel_Internal * (deltaTime_Sum_Temp * Config_Reference.Config_Loaded.Curve_Variable.Variable_Internal_Acc.Internal_Rate / MathF.Max(0f/*TODO: add limited internal stemming check*/, 1f));
                if (Config_Reference.Config_Loaded.Config_System.System_Damage_Acc.BleedCanDamage_Internal) { damageToApply += Config_Reference.Config_Loaded.Config_System.System_Damage_Acc.BleedCanKill_Internal ? damageToAdd : MathF.Min(damageToAdd, MathF.Max((entity_BehaviorHealth.Health - 0.01f), 0f)); }
                entity_BehaviorBleed.Bleed_CurrentLevel_Internal -= (deltaTime_Sum_Temp * Config_Reference.Config_Loaded.Curve_Variable.Variable_Internal_Acc.Internal_FlatHeal * MathF.Max(state_HealBonus_Resting * (entityPlayer?.Controls.FloorSitting is true ? Config_Reference.Config_Loaded.Curve_Variable.Variable_HealBonus_Acc.HealBonus_BleedHeal_InternalRestBonus : 1f), 1f)) + (deltaTime_Sum_Temp * entity_BehaviorBleed.Bleed_CurrentLevel_Internal * Config_Reference.Config_Loaded.Curve_Variable.Variable_Internal_Acc.Internal_ScaledHeal);
            }

            //apply all bleed damage
            if (entity.HasBehavior<EntityBehaviorRideable>()) { damageToApply *= 0.75f; }//TODO: remove once players are able to bandage other entitys
            if (entity_BehaviorBleed.Bleed_CurrentLevel_External <= 0) { entity_BehaviorBleed.State_BleedReductionFlag = BID_Function_General.Calc_Flag_SetBit(entity_BehaviorBleed.State_BleedReductionFlag, 3, false); entity_BehaviorBleed.State_BleedReductionFlag = BID_Function_General.Calc_Flag_SetBit(entity_BehaviorBleed.State_BleedReductionFlag, 2, false); }
            if (!entity.Alive) { entity_BehaviorBleed.Bleed_CurrentLevel_External *= 0.9f; entity_BehaviorBleed.Bleed_CurrentLevel_Internal = 0f; return; } //dead entitys can still have bleed effects so bleeding still needs to occur. reduce bleed level more to prevent dead entitys from creating excess particles
            if (entity_BehaviorHealth.Health > damageToApply) { entity_BehaviorHealth.Health -= damageToApply; }
            else { entity.Die(EnumDespawnReason.Death, entity_BehaviorBleed.LastBleedSource); }
            return;
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

                foreach (var threashold in Config_Reference.Config_Loaded.Config_BleedReport.List_BleedReport_DPS_SeverityThreashold) { if ((bleedToApply_External * Config_Reference.Config_Loaded.Curve_Variable.Variable_External_Acc.External_Rate) >= threashold.BleedLevel) { vagueBleed_External = threashold.Severity; break; } }
                foreach (var threashold in Config_Reference.Config_Loaded.Config_BleedReport.List_BleedReport_DPS_SeverityThreashold) { if ((bleedToApply_Internal * Config_Reference.Config_Loaded.Curve_Variable.Variable_Internal_Acc.Internal_Rate) * BID_VarRef.normalizer_InternalBleed >= threashold.BleedLevel) { vagueBleed_Internal = threashold.Severity; break; } } //multiply by arbitrary value to get internal within a similar range; TODO: tweak
                ((IServerPlayer)entityPlayer.Player).SendMessage(GlobalConstants.DamageLogChatGroup, Lang.Get("bleedingindepth:damageLog_BleedNotify_External", [vagueBleed_External]) + ((Config_Reference.Config_Loaded.Config_BleedReport.BleedReport_Internal && bleedToApply_Internal > 0) ? Lang.Get("bleedingindepth:damageLog_BleedNotify_Internal", [vagueBleed_Internal]) : ""), EnumChatType.Notification);
                BID_Function_General.Log_Debug("Player recieved vague report for External: {0}({1}); Internal: {2}({3})", loggers: [vagueBleed_External, bleedToApply_External, vagueBleed_Internal, bleedToApply_Internal]);
            }
        }


        /// <summary>
        /// called for converting to internal bleed;
        /// big hits that deal bleed past a specific # will be diverted somewhat (more bleed above threshold means more convert %) into an internal wound that is much harder to heal manually.
        /// internal bleeding is slower but heals slower effectively doing more damage; not effected by pressure application
        /// </summary>
        /// <param name="input_x"></param>
        /// <returns></returns>
        internal static float BleedDamage_InternalBleedCurve(float input_x)
        {
            float offset_Y0 = Config_Reference.Config_Loaded?.Curve_Variable.Variable_InternalConversion_Acc?.InternalConversion_Offset_Y0 ?? BID_Config_Main.Config_Curve_Variable.Curve_InternalConversion.Default_InternalConversion_Offset_Y0;//8f;
            float max1 = Config_Reference.Config_Loaded?.Curve_Variable.Variable_InternalConversion_Acc?.InternalConversion_Max1 ?? BID_Config_Main.Config_Curve_Variable.Curve_InternalConversion.Default_InternalConversion_Max1;//5.0f;
            float rate1 = Config_Reference.Config_Loaded?.Curve_Variable.Variable_InternalConversion_Acc?.InternalConversion_Rate1 ?? BID_Config_Main.Config_Curve_Variable.Curve_InternalConversion.Default_InternalConversion_Rate1;//0.75f;
            float offset_X1 = Config_Reference.Config_Loaded?.Curve_Variable.Variable_InternalConversion_Acc?.InternalConversion_Offset_X1 ?? BID_Config_Main.Config_Curve_Variable.Curve_InternalConversion.Default_InternalConversion_Offset_X1;//2.3f;
            float max2 = Config_Reference.Config_Loaded?.Curve_Variable.Variable_InternalConversion_Acc?.InternalConversion_Max2 ?? BID_Config_Main.Config_Curve_Variable.Curve_InternalConversion.Default_InternalConversion_Max2;//12f;
            float rate2 = Config_Reference.Config_Loaded?.Curve_Variable.Variable_InternalConversion_Acc?.InternalConversion_Rate2 ?? BID_Config_Main.Config_Curve_Variable.Curve_InternalConversion.Default_InternalConversion_Rate2;//0.2f;
            float offset_X2 = Config_Reference.Config_Loaded?.Curve_Variable.Variable_InternalConversion_Acc?.InternalConversion_Offset_X2 ?? BID_Config_Main.Config_Curve_Variable.Curve_InternalConversion.Default_InternalConversion_Offset_X2;//10f;

            return BID_Function_General.Calc_Curve_DoubleSigmoid(input_x, offset_Y0, max1, rate1, offset_X1, max2, rate2, offset_X2);
        }
    }
}
