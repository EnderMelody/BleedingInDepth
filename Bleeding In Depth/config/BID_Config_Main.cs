using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection.Emit;
using BleedingInDepth.lib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace BleedingInDepth.config //searchables: TODO/WIP
{
    public class BID_ModConfig
    {
        public class Config_System
        {
            public string Comment_System_Title => "-------------------- Global System Toggles --------------------";
            public string Comment_DefaultResetTip => "Config manager will reset values that are null or invalid. Simply delete any value you want to reset and load the mod. Some values have a clamp and will reset when out of bounds.";
            public System_BleedDamage System_BleedDamage_Acc = new();
            public class System_BleedDamage
            {
                public bool Bleed_External {  get; set; } = Default_Bleed_External; public string Comment_Bleed_External => $"Default: {Default_Bleed_External}; Toggle; Enables system for external bleed accumulation and damage";
                public const bool Default_Bleed_External = true;
                public bool Bleed_Internal { get; set; } = Default_Bleed_Internal; public string Comment_Bleed_Internal => $"Default: {Default_Bleed_Internal}; Toggle; Enables system for internal bleed accumulation and damage";
                public const bool Default_Bleed_Internal = true;
                public bool BleedCanDamage_External { get; set; } = Default_BleedCanDamage_External; public string Comment_BleedCanDamage_External => $"Default: {Default_BleedCanDamage_External}; Toggle; Enables damage from external bleeding; Disabling this will make bleeding only cosmetic";
                public const bool Default_BleedCanDamage_External = true;
                public bool BleedCanDamage_Internal { get; set; } = Default_BleedCanDamage_Internal; public string Comment_BleedCanDamage_Internal => $"Default: {Default_BleedCanDamage_Internal}; Toggle; Enables damage from internal bleeding; Disabling this does effectively the same as disabling the system, maybe this will be useful later";
                public const bool Default_BleedCanDamage_Internal = true;
                public bool BleedCanKill_External { get; set; } = Default_BleedCanKill_External; public string Comment_BleedCanKill_External => $"Default: {Default_BleedCanKill_External}; Toggle; Enables death from external bleeding";
                public const bool Default_BleedCanKill_External = true;
                public bool BleedCanKill_Internal { get; set; } = Default_BleedCanKill_Internal; public string Comment_BleedCanKill_Internal => $"Default: {Default_BleedCanKill_Internal}; Toggle; Enables death from internal bleeding";
                public const bool Default_BleedCanKill_Internal = true;
                public bool UseDamageTypeCompat { get; set; } = Default_UseDamageTypeCompat; public string Comment_UseDamageTypeCompat => $"Default: {Default_UseDamageTypeCompat}; Toggle; Enables damageTypes to be used, otherwise all bleedable damageTypes will use slash modifiers; Auto-enabled when compatible mods that modify damagetypes are found"; //TODO: remove this once vanilla impliments proper damage types per weapon
                public const bool Default_UseDamageTypeCompat = false;
                public bool Bleed_SlowAtLowHealth { get; set; } = Default_SlowAtLowHealth; public string Comment_SlowAtLowHealth => $"Default: {Default_SlowAtLowHealth}; Toggle; Enables bleeding dammage application to slow down when the player is at critical health levels (~3hp); Applies to both bleed types";
                public const bool Default_SlowAtLowHealth = true;
            }
            public System_Effects System_Effects_Acc = new();
            public class System_Effects
            {
                public bool Effects_MasterToggle { get; set; } = Default_Effects_MasterToggle; public string Comment_Effects_MasterToggle => $"Default: {Default_Effects_MasterToggle}; Toggle; Enables systems for ALL vanity effects";
                public const bool Default_Effects_MasterToggle = true;
                public bool Effects_Bleeding { get; set; } = Default_Effects_Bleeding; public string Comment_Effects_Bleeding => $"Default: {Default_Effects_Bleeding}; Toggle; Enables system for particle creation from bleeding";
                public const bool Default_Effects_Bleeding = true;
                public bool Effects_BloodSplash { get; set; } = Default_Effects_BloodSplash; public string Comment_Effects_BloodSplash => $"Default: {Default_Effects_BloodSplash}; Toggle; Enables system for particle creation from initial bleed application";
                public const bool Default_Effects_BloodSplash = true;
            }
            public System_Debug System_Debug_Acc = new();
            public class System_Debug
            {
                public bool DebugLogging { get; set; } = Default_DebugLogging; public string Comment_DebugLogging => $"Default: {Default_DebugLogging}; Toggle; Enables debug logging output";
                public const bool Default_DebugLogging = false;
                public bool DebugLogging_Verbose { get; set; } = Default_DebugLogging_Verbose; public string Comment_DebugLogging_Verbose => $"Default: {Default_DebugLogging_Verbose}; Toggle; Enables verbose debug logging output";
                public const bool Default_DebugLogging_Verbose = false;
                public bool KokoaIsInvincibleGodMode { get; set; } = Default_KokoaIsInvincibleGodMode; public string Comment_KokoaIsInvincibleGodMode => $"Default: {Default_KokoaIsInvincibleGodMode}; Toggle; For testing, Enables a 'Kokoa' to be unable to take damage. Does not prevent bleeding build up or sources of suffering. This config option contains chemicals known to the state of California to cause cancer"; //TODO: remove after testing phase
                public const bool Default_KokoaIsInvincibleGodMode = false;
            }
            public bool System_Hardcore_System { get; set; } = Default_Hardcore_System; public string Comment_Hardcore_System => $"Default: {Default_Hardcore_System}; Toggle; WIP; Unsure exact effects"; //TODO: add this; not sure exactly what this will be yet, generally the concept will be making systems more demanding to maintain and use that will effectively require a specialized doctor in the group that can heal more advanced things (like internal bleeding through surgery) potentially make a seperate mod that adds things like deseases/infections to make doctors more relevant
            public const bool Default_Hardcore_System = true;
            public bool System_NPCConfigs { get; set; } = Default_NPCConfig; public string Comment_NPCConfig => $"Default: {Default_NPCConfig}; Toggle; WIP; Enables the creation and usage of per entity config files";
            public const bool Default_NPCConfig = true;
        }


        public class Config_BleedApplication
        {
            public string Comment_BleedApplication_Title => "-------------------- Alternative Bleed Damage Application Curves; All false will use the mods default bleed damage --------------------";
            public bool BleedType_Pool {  get; set; } = Default_BleedType_Pool; public string Comment_BleedType_Pool => $"Default: {Default_BleedType_Pool}; Toggle; WIP; Removes curve for calculating bleed damage and instead uses a pooled bleed system. This system will be more linear and predictable in bleed damage scaling as more bleed is acumulated. Pool will accumulate bleed damage that is converted from initial damage taken. " +
                $"Damage dealt from the pool is directly removed resulting in the exact hp in the pool being applied to entity health once bleed the pool is empty. Bleed damage rate still increases with a bigger pool, but the amount removed from the pool scales equally.";
            public const bool Default_BleedType_Pool = false;
            public bool BleedType_DOT { get; set; } = Default_BleedType_DOT; public string Comment_BleedType_DOT => $"Default: {Default_BleedType_DOT}; Toggle; WIP; Removes curve for claculating bleed damage and instead converts each instance of bleed into a Damage Over Time using built in vanilla systems.";
            public const bool Default_BleedType_DOT = false;
        }


        public class Config_Rate //TODO: balance these rates better (probably done now? need external feedback and gameplay testing)
        {
            public string Comment_Rate_Title => "-------------------- Bleeding and Healing Rates --------------------";
            [Range(0f, float.PositiveInfinity)] public float Rate_BleedDamage_External { get; set; } = Default_BleedDamage_External; public string Comment_BleedDamage_External => $"Default: {Default_BleedDamage_External}; Multiplier; Determines % of [{nameof(BID_Lib_EntityManager.EntityBehavior_Bleed.Bleed_CurrentLevel_External)}] is applied to hp as damage per tick";
            public const float Default_BleedDamage_External = 0.09f;
            [Range(0f, float.PositiveInfinity)] public float Rate_BleedDamage_Internal { get; set; } = Default_BleedDamage_Internal; public string Comment_BleedDamage_Internal => $"Default: {Default_BleedDamage_Internal}; Multiplier; Determines % of [{nameof(BID_Lib_EntityManager.EntityBehavior_Bleed.Bleed_CurrentLevel_Internal)}] is applied to hp as damage per tick";
            public const float Default_BleedDamage_Internal = 0.2f;
            [Range(0f, float.PositiveInfinity)] public float Rate_BleedHeal_External { get; set; } = Default_BleedHeal_External; public string Comment_BleedHeal_External => $"Default: {Default_BleedHeal_External}; Flat; Determines how much of [{nameof(BID_Lib_EntityManager.EntityBehavior_Bleed.Bleed_CurrentLevel_External)}] is removed per tick";
            public const float Default_BleedHeal_External = 0.035f;
            [Range(0f, float.PositiveInfinity)] public float Rate_BleedHeal_Internal { get; set; } = Default_BleedHeal_Internal; public string Comment_BleedHeal_Internal => $"Default: {Default_BleedHeal_Internal}; Flat; Determines how much of [{nameof(BID_Lib_EntityManager.EntityBehavior_Bleed.Bleed_CurrentLevel_Internal)}] is removed per tick";
            public const float Default_BleedHeal_Internal = 0.0005f;
            [Range(0f, float.PositiveInfinity)]public float Rate_ScaledtHeal_External { get; set; } = Default_ScaledHeal_External; public string Comment_ScaledHeal_External => $"Default: {Default_ScaledHeal_External}; Multipler; Determines % of [{nameof(BID_Lib_EntityManager.EntityBehavior_Bleed.Bleed_CurrentLevel_External)}] that is removed per tick";
            public const float Default_ScaledHeal_External = 0.015f;
            [Range(0f, float.PositiveInfinity)]public float Rate_ScaledHeal_Internal { get; set; } = Default_ScaledHeal_Internal; public string Comment_ScaledHeal_Internal => $"Default: {Default_ScaledHeal_Internal}; Multipler; Determines % of [{nameof(BID_Lib_EntityManager.EntityBehavior_Bleed.Bleed_CurrentLevel_Internal)}] that is removed per tick";
            public const float Default_ScaledHeal_Internal = 0.005f;
        }


        public class Config_Curve //TODO: add other curves here; make each its own class for seperation
        {
            public Curve_InternalConversion Curve_InternalConversion_Acc = new();
            public class Curve_InternalConversion
            {
                public string Comment_Bleed_InternalConversionOverview => "Determines how rapidly higher levels of damage are diverted into internal bleeding; Double Sigmoid"; //TODO: add internalcurve modifier per damage type; variable x?
                public string Comment_Bleed_InternalConversionFormula => $"(% of damage over [{BID_ModConfig.Config_DamageType.NameOf_DamageType_BleedConversionThresholdInternal}] converted) = y = Offset_Y0 + (Max1 / (1 + e^(-Rate1 * ([{BID_ModConfig.Config_DamageType.NameOf_DamageType_BleedConversionThresholdInternal}] - Offset_X1)))) + (Max2 / (1 + e^(-Rate2 * ([{BID_ModConfig.Config_DamageType.NameOf_DamageType_BleedConversionThresholdInternal}] - Offset_X2))))";
                public float InternalConversion_Offset_Y0 { get; set; } = Default_InternalConversion_Offset_Y0; public string Comment_InternalConversion_Offset_Y0 => $"Default: {Default_InternalConversion_Offset_Y0}; Curve mod; See formula";
                public const float Default_InternalConversion_Offset_Y0 = 8.0f;
                public float InternalConversion_Max1 { get; set; } = Default_InternalConversion_Max1; public string Comment_InternalConversion_Max1 => $"Default: {Default_InternalConversion_Max1}; Curve mod; See formula";
                public const float Default_InternalConversion_Max1 = 5.0f;
                public float InternalConversion_Rate1 { get; set; } = Default_InternalConversion_Rate1; public string Comment_InternalConversion_Rate1 => $"Default: {Default_InternalConversion_Rate1}; Curve mod; See formula";
                public const float Default_InternalConversion_Rate1 = 0.75f;
                public float InternalConversion_Offset_X1 { get; set; } = Default_InternalConversion_Offset_X1; public string Comment_InternalConversion_Offset_X1 => $"Default: {Default_InternalConversion_Offset_X1}; Curve mod; See formula";
                public const float Default_InternalConversion_Offset_X1 = 2.3f;

                public float InternalConversion_Max2 { get; set; } = Default_InternalConversion_Max2; public string Comment_InternalConversion_Max2 => $"Default: {Default_InternalConversion_Max2}; Curve mod; See formula";
                public const float Default_InternalConversion_Max2 = 12.0f;
                public float InternalConversion_Rate2 { get; set; } = Default_InternalConversion_Rate2; public string Comment_InternalConversion_Rate2 => $"Default: {Default_InternalConversion_Rate2}; Curve mod; See formula";
                public const float Default_InternalConversion_Rate2 = 0.2f;
                public float InternalConversion_Offset_X2 { get; set; } = Default_InternalConversion_Offset_X2; public string Comment_InternalConversion_Offset_X2 => $"Default: {Default_InternalConversion_Offset_X2}; Curve mod; See formula";
                public const float Default_InternalConversion_Offset_X2 = 10.0f;
            }
            
        }


        public class Config_HealBonus
        {
            public string Comment_HealBonus_Title => "-------------------- Bleed Recovery Bonuses --------------------";
            [Range(0f, float.PositiveInfinity)] public float HealBonus_BleedHeal_Bed { get; set; } = Default_BleedHeal_Bed;  public string Comment_BleedHeal_Bed => $"Default: {Default_BleedHeal_Bed}; Multiplier; Determines how much external and internal bleed recovery [Bleed_HealRate_*] increases when laying in a bed";
            public const float Default_BleedHeal_Bed = 3.0f;
            [Range(0f, float.PositiveInfinity)] public float HealBonus_BleedHeal_Comfort { get; set; } = Default_BleedHeal_Comfort; public string Comment_BleedHeal_Comfort => $"Default: {Default_BleedHeal_Comfort}; Multiplier; WIP; Determines how much external and internal bleed recovery [Bleed_HealRate_*] increases when sitting on a 'comfort' object";
            public const float Default_BleedHeal_Comfort = 1.6f;
            [Range(0f, float.PositiveInfinity)] public float HealBonus_BleedHeal_Ground { get; set; } = Default_BleedHeal_Ground; public string Comment_BleedHeal_Ground => $"Default: {Default_BleedHeal_Ground}; Multiplier; Determines how much external and internal bleed recovery [Bleed_HealRate_*] increases when sitting on the ground";
            public const float Default_BleedHeal_Ground = 1.2f;
            [Range(0f, float.PositiveInfinity)] public float HealBonus_BleedHeal_Cauterize { get; set; } = Default_BleedHeal_Cauterize; public string Comment_BleedHeal_Cauterize => $"Default: {Default_BleedHeal_Cauterize}; Multipler; Determines how much an instance of fire damage reduces (flat) [{nameof(BID_Lib_EntityManager.EntityBehavior_Bleed.Bleed_CurrentLevel_External)}]";
            public const float Default_BleedHeal_Cauterize = 1.0f;
            [Range(0f, float.PositiveInfinity)] public float HealBonus_BleedReduction_Rag { get; set; } = Default_BleedReduction_Rag; public string Comment_BleedReduction_Rag => $"Default: {Default_BleedReduction_Rag}; Multipler; WIP; Using a 'rag'(any clothes item, cloth, ect.) applies a 'ragged' effect which reduces [{nameof(BID_ModConfig.Config_Rate.Rate_BleedDamage_External)}] until either external bleeding stops or another instance of damage is taken; does not stack with itself and is overridden by 'bandaged' effect; stacks with pressure";
            public const float Default_BleedReduction_Rag = 1.0f;
            [Range(0f, float.PositiveInfinity)] public float HealBonus_BleedReduction_Bandage { get; set; } = Default_BleedReduction_Bandage; public string Comment_BleedReduction_Bandage => $"Default: {Default_BleedReduction_Bandage}; Multiplier; Using a bandage applies a 'bandaged' effect which reduces [{nameof(BID_ModConfig.Config_Rate.Rate_BleedDamage_External)}] until either external bleeding stops or another instance of damage is taken; does not stack with itself; stacks with pressure";
            public const float Default_BleedReduction_Bandage = 2.0f;
            [Range(0f, float.PositiveInfinity)] public float HealBonus_BleedReduction_Flat_Bandage { get; set; } = Default_BleedReduction_Flat_Bandage; public string Comment_BleedReduction_Flat_Bandage => $"Default: {Default_BleedReduction_Flat_Bandage}; Multiplier; Determines how much using a bandage reduces (flat) [{nameof(BID_Lib_EntityManager.EntityBehavior_Bleed.Bleed_CurrentLevel_External)}]. Based off a % of the healed health of the item";
            public const float Default_BleedReduction_Flat_Bandage = 0.0f;
            [Range(0f, float.PositiveInfinity)] public float HealBonus_HealReduction_Bandage { get; set; } = Default_HealReduction_Bandage; public string Comment_HealReduction_Bandage => $"Default: {Default_HealReduction_Bandage}; Multiplier; Determines how much a bandage heals from its normal heal value. This is done as removing bleeding is healing (yet to be taken) damage effectively increasing the healing output of the bandage. Applied AFTER [{nameof(BID_ModConfig.Config_HealBonus.HealBonus_BleedReduction_Bandage)}] is calculated";
            public const float Default_HealReduction_Bandage = 1.0f;
            [Range(0f, float.PositiveInfinity)] public float HealBonus_BleedReduction_Pressure { get; set; } = Default_BleedReduction_Pressure; public string Comment_BleedReduction_Pressure => $"Default: {Default_BleedReduction_Pressure}; Multiplier; Divider to [{nameof(BID_ModConfig.Config_Rate.Rate_BleedDamage_External)}] while the entity is applying pressure; stacks with rag and bandage";
            public const float Default_BleedReduction_Pressure = 3.0f;
            [Range(0f, float.PositiveInfinity)] public float HealBonus_BleedReduction_Care { get; set; } = Default_BleedReduction_Care; public string Comment_BleedReduction_Care => $"Default: {Default_BleedReduction_Care}; Multiplier; WIP; Divider to external bleeding [{nameof(BID_ModConfig.Config_Rate.Rate_BleedDamage_External)}] when an entity 'cares' for its wounds";
            public const float Default_BleedReduction_Care = 1.8f; //TODO: this will be more barbaric in nature therefor less effective. currently this is just being out of combat for a certain period of time but will be replaced with specific actions such as "licking the wound" idle animation (will use the vanilla sitting animation till i make one)
        }


        public class Config_DamageType
        {
            public string Comment_DamageType_Title => "-------------------- Damage and Bleed on Damaged Modifiers --------------------";
            public string Comment_PerDamageTypeIdentifier => "-------------------- PerDamageType --------------------";
            public string Comment_PerDamageTypeExplained => "Different DamageTypes each have a config that determines how much damage is dealt as direct health reduciton and how much is converted to bleed DOT. If damage type is not listed it is not supported by default, though you should be able to add any to the config if it follows vintage story's EnumDamageSource.Type";
            public string Comment_PerDamageType_Direct_Multi => "Multiplier; Determines % of [DamageType] damage taken that is applied directly to health";
            public string Comment_PerDamageType_Bleed_Multi_External => "Multiplier; Determines % of [DamageType] damage taken that is converted to bleed";
            public string Comment_PerDamageType_Bleed_Multi_Internal => "Curve; WIP; How effective [DamageType] is at diverting to internal bleeding from regular bleed damage. This only applies to damage over [DamageType][Bleed_ConversionThreshold_Internal]; Set to 0 to convert ALL bleed over the start value"; //wolf hits in vanilla do 8 damage to unarmored; bears do 10. the initial bleed modifier [DamageMultiplier_(DamageType)_Bleed] will reduce this so this value needs to take that into account
            public string Comment_PerDamageType_Bleed_ConversionThreshold_Internal => $"Flat; Determines the damage delt in a single hit multiplied by [{nameof(BID_Lib_EntityManager.EntityBehavior_Bleed.Bleed_CurrentLevel_External)}] minimum before internal bleeding conversion curve starts. Internal bleed curve only takes the damage past this threshold into account; If [DamageType][Bleed_Multi_Internal] is 0 the applied damage converted to internal bleed will be reduced by this amount";


            internal static string NameOf_DamageType_DirectMulti = "Direct_Multi";//TODO: make sure these work
            internal static string NameOf_DamageType_BleedMultiExternal = "Bleed_Multi_External";
            internal static string NameOf_DamageType_BleedMultiInternal = "Bleed_Multi_Internal";
            internal static string NameOf_DamageType_BleedConversionThresholdInternal = "Bleed_ConversionThreshold_Internal";
            public Dictionary<EnumDamageType, Dictionary<string, float>> Dict_DamageType { get; set; } = new Dictionary<EnumDamageType, Dictionary<string, float>>//TODO: add clamps
            {
                [EnumDamageType.SlashingAttack] = new(StringComparer.OrdinalIgnoreCase)
                {
                    [NameOf_DamageType_DirectMulti] = Default_Slash_Direct_Multi,
                    [NameOf_DamageType_BleedMultiExternal] = Default_Slash_Bleed_Multi_External,
                    [NameOf_DamageType_BleedMultiInternal] = Default_Slash_Bleed_Multi_Internal,
                    [NameOf_DamageType_BleedConversionThresholdInternal] = Default_Slash_Bleed_ConversionThreshold_Internal
                },
                [EnumDamageType.BluntAttack] = new(StringComparer.OrdinalIgnoreCase)
                {
                    [NameOf_DamageType_DirectMulti] = Default_Blunt_Direct_Multi,
                    [NameOf_DamageType_BleedMultiExternal] = Default_Blunt_Bleed_Multi_External,
                    [NameOf_DamageType_BleedMultiInternal] = Default_Blunt_Bleed_Multi_Internal,
                    [NameOf_DamageType_BleedConversionThresholdInternal] = Default_Blunt_Bleed_ConversionThreshold_Internal
                },
                [EnumDamageType.PiercingAttack] = new(StringComparer.OrdinalIgnoreCase)
                {
                    [NameOf_DamageType_DirectMulti] = Default_Pierce_Direct_Multi,
                    [NameOf_DamageType_BleedMultiExternal] = Default_Pierce_Bleed_Multi_External,
                    [NameOf_DamageType_BleedMultiInternal] = Default_Pierce_Bleed_Multi_Internal,
                    [NameOf_DamageType_BleedConversionThresholdInternal] = Default_Pierce_Bleed_ConversionThreshold_Internal
                },
                [EnumDamageType.Poison] = new(StringComparer.OrdinalIgnoreCase)
                {
                    [NameOf_DamageType_DirectMulti] = Default_Poison_Direct_Multi,
                    [NameOf_DamageType_BleedMultiExternal] = Default_Poison_Bleed_Multi_External,
                    [NameOf_DamageType_BleedMultiInternal] = Default_Poison_Bleed_Multi_Internal,
                    [NameOf_DamageType_BleedConversionThresholdInternal] = Default_Poison_Bleed_ConversionThreshold_Internal
                }
            };


            public string Comment_Slash_Identifier => "--------------------Slash Defaults: See PerDamageType comments--------------------";
            public string Comment_Slash_Direct_Multi => $"Default: {Default_Slash_Direct_Multi}";
            public const float Default_Slash_Direct_Multi = 0.4f;
            public string Comment_Slash_Bleed_Multi_External => $"Default: {Default_Slash_Bleed_Multi_External}";
            public const float Default_Slash_Bleed_Multi_External = 0.4f;
            public string Comment_Slash_Bleed_Multi_Internal => $"Default: {Default_Slash_Bleed_Multi_Internal}; ";
            public const float Default_Slash_Bleed_Multi_Internal = 0.1f; //TODO: figure out the curve formula to use this value per DamageType
            public string Comment_Slash_Bleed_ConversionThreshold_Internal => $"Default: {Default_Slash_Bleed_ConversionThreshold_Internal}";
            public const float Default_Slash_Bleed_ConversionThreshold_Internal = 8.0f;


            public string Comment_Blunt_Identifier => "--------------------Blunt Defaults: See PerDamageType comments--------------------";
            public string Comment_Blunt_Direct_Multi => $"Default: {Default_Blunt_Direct_Multi}";
            public const float Default_Blunt_Direct_Multi = 0.95f;
            public string Comment_Blunt_Bleed_Multi_External => $"Default: {Default_Blunt_Bleed_Multi_External}";
            public const float Default_Blunt_Bleed_Multi_External = 0.02f;
            public string Comment_Blunt_Bleed_Multi_Internal => $"Default: {Default_Blunt_Bleed_Multi_Internal}";
            public const float Default_Blunt_Bleed_Multi_Internal = 0.0f;
            public string Comment_Blunt_Bleed_ConversionThreshold_Internal => $"Default: {Default_Blunt_Bleed_ConversionThreshold_Internal}";
            public const float Default_Blunt_Bleed_ConversionThreshold_Internal = 1.0f;


            public string Comment_Pierce_Identifier => "--------------------Pierce Defaults: See PerDamageType comments--------------------";
            public string Comment_Pierce_Direct_Multi => $"Default: {Default_Pierce_Direct_Multi}";
            public const float Default_Pierce_Direct_Multi = 0.75f;
            public string Comment_Pierce_Bleed_Multi_External => $"Default: {Default_Pierce_Bleed_Multi_External}";
            public const float Default_Pierce_Bleed_Multi_External = 0.15f;
            public string Comment_Pierce_Bleed_Multi_Internal => $"Default: {Default_Pierce_Bleed_Multi_Internal}";
            public const float Default_Pierce_Bleed_Multi_Internal = 0.7f;
            public string Comment_Pierce_Bleed_ConversionThreshold_Internal => $"Default: {Default_Pierce_Bleed_ConversionThreshold_Internal}";
            public const float Default_Pierce_Bleed_ConversionThreshold_Internal = 3.0f;


            public string Comment_Poison_Identifier => "--------------------Poison Defaults: See PerDamageType comments--------------------"; //poison damage can be converted fully into internal bleeding to more accurately represent the effects of poison over the instant damage of vanilla
            public string Comment_Poison_Direct_Multi => $"Default: {Default_Poison_Direct_Multi}";
            public const float Default_Poison_Direct_Multi = 0.0f;
            public string Comment_Poison_Bleed_Multi_External => $"Default: {Default_Poison_Bleed_Multi_External}"; //TODO: Rebalance this value
            public const float Default_Poison_Bleed_Multi_External = 1.0f;
            public string Comment_Poison_Bleed_Multi_Internal => $"Default: {Default_Poison_Bleed_Multi_Internal}";
            public const float Default_Poison_Bleed_Multi_Internal = 0.0f;
            public string Comment_Poison_Bleed_ConversionThreshold_Internal => $"Default: {Default_Poison_Bleed_ConversionThreshold_Internal}";
            public const float Default_Poison_Bleed_ConversionThreshold_Internal = 0.0f;
        }


        public class Config_BleedReport
        {
            public string Comment_BleedReport_Title => "-------------------- Bleed Level Reporting --------------------";
            public bool BleedReport_Detailed { get; set; } = Default_BleedReport_Detailed; public string Comment_BleedReport_Detailed => $"Default: {Default_BleedReport_Detailed}; Toggle; Determines whether bleed check will return (true=) exacty values or (false=) vague info on the [Bleed_CurrentLevel_*]";
            public const bool Default_BleedReport_Detailed = false;
            public bool BleedReport_Internal { get; set; } = Default_BleedReport_Internal; public string Comment_BleedReport_Internal => $"Default: {Default_BleedReport_Internal}; Toggle; Determines whether bleed check will report internal bleed levels alongside external bleed levels; if false internal bleeding will be completely hidden from the player (outside of taking DOT)";
            public const bool Default_BleedReport_Internal = true;
            public bool BleedReport_NotifyOnHit { get; set; } = Default_BleedReport_NotifyOnHit; public string Comment_BleedReport_NotifyOnHit => $"Default: {Default_BleedReport_NotifyOnHit}; Toggle; Determines whether a chat message notifying the player that bleed has been applied will be logged to chat (similar to how damage is logged); Uses rules from [{nameof(BleedReport_Detailed)}] and [{nameof(BleedReport_Internal)}]";
            public const bool Default_BleedReport_NotifyOnHit = true;


            public class BleedSeverityThreashold
            {
                public float BleedLevel { get; set; }
                public string Severity { get; set; } = "";
            }

            public string Comment_BleedSeverity_Levels => $"When [{nameof(BleedReport_Detailed)}] is false, these values will determine when the next severity of vague description of [Bleed_CurrentLevel_*] is displayed based on the DPS of that bleed type; Internal DPS is multiplied by 10 to normalize";
            public string Comment_BleedReport_List => "Values in list are fully customizable. Any number of entries can be used. Only requirement is high -> low decending order. List will be defaulted when empty";
            public string Comment_BleedReport_OnCheck => $"When [{nameof(BleedReport_Detailed)}] is false, this list of values is used for bleed checks that use DPS";
            public List<BleedSeverityThreashold> List_BleedReport_DPS_SeverityThreashold { get; set; } = [];//TODO: calc values for this on game start from config_loaded bleed damage and heal rates
            public string Comment_BleedReport_DPS_Severe => $"Default: {Default_BleedReport_DPS_Severe}";
            public const float Default_BleedReport_DPS_Severe = 0.3f;
            public string Comment_BleedReport_DPS_Moderate => $"Default: {Default_BleedReport_DPS_Moderate}";
            public const float Default_BleedReport_DPS_Moderate = 0.1f;
            public string Comment_BleedReport_DPS_Minor => $"Default: {Default_BleedReport_DPS_Minor}";
            public const float Default_BleedReport_DPS_Minor = 0.03f;
            public string Comment_BleedReport_DPS_Trivial => $"Default: {Default_BleedReport_DPS_Trivial}";
            public const float Default_BleedReport_DPS_Trivial = 0.01f;
            public string comment_Bleedreport_RawDamage => $"When [{nameof(BleedReport_Detailed)}] is false, this list of values is used for bleed checks that use an instance of applied bleed";
            public List<BleedSeverityThreashold> List_BleedReport_GainedBleed_SeverityThreashold { get; set; } = [];//TODO: calc values for this on game start from config_loaded bleed damage and heal rates
            public string Comment_BleedReport_GainedBleed_Severe => $"Default: {Default_BleedReport_GainedBleed_Severe}";
            public const float Default_BleedReport_GainedBleed_Severe = 5.2f;
            public string Comment_BleedReport_GainedBleed_Significant => $"Default: {Default_BleedReport_GainedBleed_Significant}";
            public const float Default_BleedReport_GainedBleed_Significant = 4.0f;
            public string Comment_BleedReport_GainedBleed_Moderate => $"Default: {Default_BleedReport_GainedBleed_Moderate}";
            public const float Default_BleedReport_GainedBleed_Moderate = 2.8f;
            public string Comment_BleedReport_GainedBleed_Minor => $"Default: {Default_BleedReport_GainedBleed_Minor}";
            public const float Default_BleedReport_GainedBleed_Minor = 1.0f;
            public string Comment_BleedReport_GainedBleed_Trivial => $"Default: {Default_BleedReport_GainedBleed_Trivial}";
            public const float Default_BleedReport_GainedBleed_Trivial = 0.2f;
            public string Comment_BleedReport_GainedBleed_Insignificant => $"Default: {Default_BleedReport_GainedBleed_Insignificant}";
            public const float Default_BleedReport_GainedBleed_Insignificant = 0.05f;
        }


        public class Config_Effect
        {
            public string Comment_Effect_Title => "-------------------- Bleed EFX - Visual and Auditorial --------------------";
            public VFX_Bleeding VFX_Bleeding_Acc = new();
            public class VFX_Bleeding
            {
                public bool Particle_Toggle { get; set; } = Default_Particle_Toggle; public string Comment_Particle_Toggle => $"Default: {Default_Particle_Toggle}; Toggle; Determines if bleeding entitys create blood particles based on [{nameof(BID_Lib_EntityManager.EntityBehavior_Bleed.Bleed_CurrentLevel_External)}]";
                public const bool Default_Particle_Toggle = true;
                [Range(0f, 3f)] public float Particle_AmtMulti_SlopeRate { get; set; } = Default_Particle_AmtMulti_SlopeRate; public string Comment_Particle_AmtMulti_SlopeRate => $"Default: {Default_Particle_AmtMulti_SlopeRate}; Multiplier; Multi to the number of particles spawning curve based on [{nameof(BID_Lib_EntityManager.EntityBehavior_Bleed.Bleed_CurrentLevel_External)}]";
                public const float Default_Particle_AmtMulti_SlopeRate = 0.5f;
                [Range(0f, float.PositiveInfinity)] public float Particle_AmtMulti_Max { get; set; } = Default_Particle_AmtMulti_Max; public string Comment_Particle_AmtMulti_Max => $"Default: {Default_Particle_AmtMulti_Max}; Flat; Max value for number of paricles spawning curve based on [{nameof(BID_Lib_EntityManager.EntityBehavior_Bleed.Bleed_CurrentLevel_External)}]";
                public const float Default_Particle_AmtMulti_Max = 12.0f;
                [Range(0f, float.PositiveInfinity)] public float Particle_SizeMulti_Base { get; set; } = Default_Particle_SizeMulti_Base; public string Comment_Particle_SizeMulti_Base => $"Default: {Default_Particle_SizeMulti_Base}; Multiplier; Base particle size for curve based on [{nameof(BID_Lib_EntityManager.EntityBehavior_Bleed.Bleed_CurrentLevel_External)}]";
                public const float Default_Particle_SizeMulti_Base = 0.8f;
                [Range(0f, 3f)] public float Particle_SizeMulti_SlopeRate { get; set; } = Default_Particle_SizeMulti_SlopeRate; public string Comment_Particle_SizeMulti_SlopeRate => $"Default: {Default_Particle_SizeMulti_SlopeRate}; Multiplier; Multi to the size of particles spawning curve based on [{nameof(BID_Lib_EntityManager.EntityBehavior_Bleed.Bleed_CurrentLevel_External)}]";
                public const float Default_Particle_SizeMulti_SlopeRate = 1.2f;
                [Range(0f, float.PositiveInfinity)] public float Particle_SizeMulti_Max { get; set; } = Default_Particle_SizeMulti_Max; public string Comment_Particle_SizeMulti_Max => $"Default: {Default_Particle_SizeMulti_Max}; Flat; Max value for size of paricles spawning curve based on [{nameof(BID_Lib_EntityManager.EntityBehavior_Bleed.Bleed_CurrentLevel_External)}]";
                public const float Default_Particle_SizeMulti_Max = 2.0f;
                [Range(0f, float.PositiveInfinity)] public float Particle_SimDistance { get; set; } = Default_Particle_SimDistance; public string Comment_Particle_SimDistance => $"Default: {Default_Particle_SimDistance}; Flat; Distance from the client at which an entity's bleeding particles spawn quantity will be greatly reduced";
                public const float Default_Particle_SimDistance = 30.0f;
            }
            public VFX_BloodSplash VFX_BloodSplash_Acc = new();
            public class VFX_BloodSplash
            {
                public bool Particle_Toggle { get; set; } = Default_Particle_Toggle; public string Comment_Particle_Toggle => $"Default: {Default_Particle_Toggle}; Toggle; Determines if damage that causes bleeding will splash blood particles on hit based on [{nameof(BID_Lib_EntityManager.EntityBehavior_Bleed.Bleed_CurrentLevel_External)}]";
                public const bool Default_Particle_Toggle = true;
                [Range(0f, 3f)] public float Particle_AmtMulti_SlopeRate { get; set; } = Default_Particle_AmtMulti_SlopeRate; public string Comment_Particle_AmtMulti_SlopeRate => $"Default: {Default_Particle_AmtMulti_SlopeRate}; Multiplier; Multi to the number of particles spawning curve based on [{nameof(BID_Lib_EntityManager.EntityBehavior_Bleed.Bleed_CurrentLevel_External)}]";
                public const float Default_Particle_AmtMulti_SlopeRate = 0.5f;
                [Range(0f, float.PositiveInfinity)] public float Particle_AmtMulti_Max { get; set; } = Default_Particle_AmtMulti_Max; public string Comment_Particle_AmtMulti_Max => $"Default: {Default_Particle_AmtMulti_Max}; Flat; Max value for paricle spawning curve based on [{nameof(BID_Lib_EntityManager.EntityBehavior_Bleed.Bleed_CurrentLevel_External)}]";
                public const float Default_Particle_AmtMulti_Max = 12.0f;
                [Range(0f, float.PositiveInfinity)] public float Particle_SizeMulti_Base { get; set; } = Default_Particle_SizeMulti_Base; public string Comment_Particle_SizeMulti_Base => $"Default: {Default_Particle_SizeMulti_Base}; Multiplier; Base particle size for curve based on [{nameof(BID_Lib_EntityManager.EntityBehavior_Bleed.Bleed_CurrentLevel_External)}]";
                public const float Default_Particle_SizeMulti_Base = 0.25f;
                [Range(0f, 3f)] public float Particle_SizeMulti_SlopeRate { get; set; } = Default_Particle_SizeMulti_SlopeRate; public string Comment_Particle_SizeMulti_SlopeRate => $"Default: {Default_Particle_SizeMulti_SlopeRate}; Multiplier; Multi to the size of particles spawning curve based on [{nameof(BID_Lib_EntityManager.EntityBehavior_Bleed.Bleed_CurrentLevel_External)}]";
                public const float Default_Particle_SizeMulti_SlopeRate = 1.2f;
                [Range(0f, float.PositiveInfinity)] public float Particle_SizeMulti_Max { get; set; } = Default_Particle_SizeMulti_Max; public string Comment_Particle_SizeMulti_Max => $"Default: {Default_Particle_SizeMulti_Max}; Flat; Max value for size of paricles spawning curve based on [{nameof(BID_Lib_EntityManager.EntityBehavior_Bleed.Bleed_CurrentLevel_External)}]";
                public const float Default_Particle_SizeMulti_Max = 2.0f;
            }
            public VFX_DeathPop VFX_DeathPop_Acc = new();
            public class VFX_DeathPop
            {
                public bool Particle_Toggle { get; set; } = Default_Particle_Toggle; public string Comment_Particle_Toggle => $"Default: {Default_Particle_Toggle}; Toggle; WIP; Determines if entities that die while (heavily?) bleeding explode in a shower of blood particles amount based on max health";
                public const bool Default_Particle_Toggle = false;
                [Range(0f, float.PositiveInfinity)] public float Particle_RequiredBleed { get; set; } = Default_Particle_RequiredBleed; public string Comment_Particle_RequiredBleed => $"Default: {Default_Particle_RequiredBleed}; Flat; Required [{nameof(BID_Lib_EntityManager.EntityBehavior_Bleed.Bleed_CurrentLevel_External)}] for death pop to occur";
                public const float Default_Particle_RequiredBleed = 8.0f;
                [Range(0f, float.PositiveInfinity)] public float Particle_Amount_Max { get; set; } = Default_Particle_Amount_Max; public string Comment_Particle_Amount_Max => $"Default: {Default_Particle_Amount_Max}; Flat; Required [{nameof(BID_Lib_EntityManager.EntityBehavior_Bleed.Bleed_CurrentLevel_External)}] for death pop to occur";
                public const float Default_Particle_Amount_Max = 8.0f;
            }
            public VFX_DrippingHpBar VFX_DrippingHpBar_Acc = new();
            public class VFX_DrippingHpBar
            {
                public bool DrippingBar_Toggle { get; set; } = Default_DrippingBar_Toggle; public string Comment_DrippingBar_Toggle => $"Default: {Default_DrippingBar_Toggle}; Toggle; WIP; Determines if a visual dripping appears on the hp bar when bleeding";
                public const bool Default_DrippingBar_Toggle = false;
            }


            public SFX_Drip SFX_Drip_Acc = new();
            public class SFX_Drip
            {
                public bool Drip_Toggle { get; set; } = Default_Drip_Toggle; public string Comment_Drip_Toggle => $"Default: {Default_Drip_Toggle}; Toggle; WIP; Determines if a dripping sound effect plays when bleeding entitys stand on certain materials based on [{nameof(BID_Lib_EntityManager.EntityBehavior_Bleed.Bleed_CurrentLevel_External)}]";
                public const bool Default_Drip_Toggle = false;
                [Range(0f, float.PositiveInfinity)] public float Drip_Rate { get; set; } = Default_Drip_Rate; public string Comment_Drip_Rate => $"Default: {Default_Drip_Rate}; Multiplier; Determines how often the bleed drip sfx will play per entity";
                public const float Default_Drip_Rate = 1.0f;
                public List<string>? Drip_Materials { get; set; }
                public string Comment_Drip_Materials => $"Default: ({string.Join(",", Default_Drip_Materials)})";
                public const string Default_Drip_Materials = "Stone, Ore, Metal, Glass, Cermaic";
                public string Comment_Drip_Materials_Types => $"Determines block material types that will allow the drip sfx to play when standing on them while bleeding. Uses vanilla block material catagories: ({string.Join(", ", Drip_VanillaMaterialList)})";
                private readonly List<string> Drip_VanillaMaterialList = ["Air", "Soil", "Gravel", "Sand", "Wood", "Leaves", "Stone", "Ore", "Liquid", "Snow", "Ice", "Metal", "Plant", "Glass", "Ceramic", "Cloth", "Brick"];
                public bool Drip_CaveDetection { get; set; } = Default_Drip_CaveDetection; public string Comment_Drip_CaveDetection => $"Default: {Default_Drip_CaveDetection}; Toggle; Determines if system checks for being in caves and cause echo effect on drip sfx";
                public const bool Default_Drip_CaveDetection = false;
            }
        }


        public class Config_TimeScale
        {
            public string Comment_TimeScale_Title => "-------------------- Time Scale Adjustment Values --------------------";
            [Range(1, 30)] public float TimeScale_BleedRate { get; set; } = Default_TimeScale_BleedRate; public string Comment_TimeScale_BleedRate => $"Default: {Default_TimeScale_BleedRate}; Multiplier, Divides the BleedTick time passage multiplier which results in adjusting the scale of time that bleed is applied. Inversely effects speed that bleed damage is applied.";
            public const float Default_TimeScale_BleedRate = 1.0f;
            [Range(0, 10)] public float DeltaTime_SumRequired_BleedRate{ get; set; } = Default_SumRequired_BleedRate; public string Comment_SumRequired_BleedRate => $"Default: {Default_SumRequired_BleedRate}; Multiplier, Divides the BleedTick time passage multiplier which results in adjusting the scale of time that bleed is applied. Inversely effects speed that bleed damage is applied.";
            public const float Default_SumRequired_BleedRate = 0.1f;
            [Range(1, 100)] public int TickCounter_BleedParticle { get; set; } = Default_BleedParticle; public string Comment_BleedParticle => $"Default: {Default_BleedParticle}; Flat, Required number of ticks to pass between each clients bleed particle spawn call; Slows visual bleed effect rate.";
            public const int Default_BleedParticle = 5;
        }
    }



    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class Config_Reference
    {
        public static Config_Reference? Config_Loaded { get; set; }
        public const string Config_FilePath = ("BleedingInDepth_Config.json");
        [JsonExtensionData] public Dictionary<string, JToken>? Config_UnknownValues { get; set; } = [];


        public BID_ModConfig.Config_System Config_System { get; set; } = new();
        public BID_ModConfig.Config_BleedApplication Config_BleedApplication { get; set; } = new();
        public BID_ModConfig.Config_Rate Config_Rate { get; set; } = new();
        public BID_ModConfig.Config_Curve Config_Curve { get; set; } = new();
        public BID_ModConfig.Config_HealBonus Config_HealBonus { get; set; } = new();
        public BID_ModConfig.Config_DamageType Config_DamageType { get; set; } = new();
        public BID_ModConfig.Config_BleedReport Config_BleedReport { get; set; } = new();
        public BID_ModConfig.Config_Effect Config_Effect { get; set; } = new();
        public BID_ModConfig.Config_TimeScale Config_TimeScale { get; set; } = new();
    }
}
