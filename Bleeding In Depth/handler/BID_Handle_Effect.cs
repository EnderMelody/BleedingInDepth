using BleedingInDepth.config;
using BleedingInDepth.lib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace BleedingInDepth.handler
{
    internal class BID_Handle_Effect
    {
        private static ICoreAPI API = BID_VarRef.API;
        private static ICoreClientAPI API_Client = BID_VarRef.API_Client;
        //private static ICoreServerAPI API_Server = BID_VarRef.API_Server;

        //internal static float Client_ParticleCount;

        internal class BID_Effect_VFX
        {
            /// <summary>
            /// Called on entity.OnEntityReceiveDamage;
            /// stores hit direction to list for directional VFX to use
            /// </summary>
            /// <param name="entity"></param>
            /// <param name="damageSource"></param>
            internal static void BleedEffect_Store_AttackDirection(Entity entity, DamageSource damageSource)
            {
                if (entity.GetBehavior<BID_Handle_Entity.EntityBehavior_Bleed>() is not BID_Handle_Entity.EntityBehavior_Bleed entity_BehaviorBleed) { return; }
                if (entity_BehaviorBleed.Bleed_CurrentLevel_External <= 0) { entity_BehaviorBleed.AttackedDirection_List.Clear(); }

                float facingDirection_Yaw = entity.Pos.Yaw;
                float attackedDirection_Yaw = GameMath.NormaliseAngleRad((float)(damageSource.GetAttackAngle(entity.Pos.XYZ, out var yaw, out _) ? yaw : 0d));
                float attackedFromFacingDirection_Yaw = GameMath.AngleRadDistance(facingDirection_Yaw, attackedDirection_Yaw);

                entity_BehaviorBleed.AttackedDirection_List.Add(attackedFromFacingDirection_Yaw);
            }


            /// <summary>
            /// called on entity.OnGameTick;
            /// for creating particles: actively bleeding
            /// </summary>
            /// <param name="entity"></param>
            internal static void BleedEffect_Particle_Bleeding(Entity entity)
            {
                if (entity.GetBehavior<BID_Handle_Entity.EntityBehavior_Bleed>() is not BID_Handle_Entity.EntityBehavior_Bleed entity_BehaviorBleed) { return; }
                if (entity_BehaviorBleed.Bleed_CurrentLevel_External <= 0) { return; }

                float particle_VeloMulti = MathF.Sqrt(entity.CollisionBox.Width * entity.CollisionBox.Length);
                float state_BleedReduction = 0f;
                float particle_CurrentDPS = Config_Reference.Config_Loaded.Curve_Variable.Variable_External_Acc.External_Rate * entity_BehaviorBleed.Bleed_CurrentLevel_External;
                float entity_ActivityMulti = 1f + ((entity.Pos.Motion.HorLength() > 0) ? Config_Reference.Config_Loaded.Curve_Variable.Variable_Activity_Acc.ActivityMulti_Walk : 0f);
                if (entity is EntityPlayer entityPlayer) //AcitivityMulti for player specific actions
                {
                    entity_ActivityMulti += (entityPlayer.Controls.Sprint) ? Config_Reference.Config_Loaded.Curve_Variable.Variable_Activity_Acc.ActivityMulti_Sprint : 0f;
                    entity_ActivityMulti += (entityPlayer.Controls.LeftMouseDown) ? Config_Reference.Config_Loaded.Curve_Variable.Variable_Activity_Acc.ActivityMulti_Hit : 0f;
                }


                //amount
                float particle_Amount = (NatFloat.createUniform(BID_Function_General.Calc_Curve_ExpoEaseOut(
                    particle_CurrentDPS * entity_ActivityMulti * 5f,
                    Config_Reference.Config_Loaded.Config_Effect.VFX_Bleeding_Acc.Particle_AmtMulti_SlopeRate,
                    Config_Reference.Config_Loaded.Config_Effect.VFX_Bleeding_Acc.Particle_AmtMulti_Max,
                    0f, 0.5f), 1f).nextFloat());

                //size
                float particle_SizeBase = Config_Reference.Config_Loaded.Config_Effect.VFX_Bleeding_Acc.Particle_SizeMulti_Base;
                float particle_SizeMulti = BID_Function_General.Calc_Curve_SingleSigmoid(
                    particle_CurrentDPS * 8f,
                    Config_Reference.Config_Loaded.Config_Effect.VFX_Bleeding_Acc.Particle_SizeMulti_SlopeRate,
                    Config_Reference.Config_Loaded.Config_Effect.VFX_Bleeding_Acc.Particle_SizeMulti_Max - 1f,
                    2.4f, 1f);

                //reducer
                state_BleedReduction += BID_Function_General.Calc_Flag_CheckBit(entity_BehaviorBleed.State_BleedReductionFlag, 0) ? Config_Reference.Config_Loaded.Curve_Variable.Variable_HealBonus_Acc.HealBonus_BleedReduction_Care : (BID_Function_General.Calc_Flag_CheckBit(entity_BehaviorBleed.State_BleedReductionFlag, 1) ? Config_Reference.Config_Loaded.Curve_Variable.Variable_HealBonus_Acc.HealBonus_BleedReduction_Pressure : 0f);
                state_BleedReduction += BID_Function_General.Calc_Flag_CheckBit(entity_BehaviorBleed.State_BleedReductionFlag, 3) ? Config_Reference.Config_Loaded.Curve_Variable.Variable_HealBonus_Acc.HealBonus_BleedReduction_Bandage : (BID_Function_General.Calc_Flag_CheckBit(entity_BehaviorBleed.State_BleedReductionFlag, 2) ? Config_Reference.Config_Loaded.Curve_Variable.Variable_HealBonus_Acc.HealBonus_BleedReduction_Rag : 0f);
                float particle_FarEntityDiv = (entity.Pos.SquareDistanceTo(API_Client.World.Player.Entity.Pos) > MathF.Pow(Config_Reference.Config_Loaded.Config_Effect.VFX_Bleeding_Acc.Particle_SimDistance, 2f)) ? 3f : 1f;
                float particle_Divider = MathF.Max((state_BleedReduction) + (entity.Alive ? 0f : 5f) + (particle_SizeMulti - 1f), 1f) * particle_FarEntityDiv;

                //velocity
                float particle_AttackedDirection_Yaw = entity_BehaviorBleed.AttackedDirection_List.ElementAtOrDefault(Random.Shared.Next(entity_BehaviorBleed.AttackedDirection_List.Count));


                Particle_Blood_Cube particle_BloodProps = new(entity)
                {
                    Quantity = NatFloat.createUniform((particle_Amount / particle_Divider), (MathF.Max((particle_Amount / particle_Divider), 3f) / 5f)),
                    Size = NatFloat.createUniform((particle_SizeMulti * particle_SizeBase), (particle_SizeMulti * particle_SizeBase) / 5f),

                    Velocity = [NatFloat.createUniform(MathF.Sin(GameMath.NormaliseAngleRad(entity.Pos.Yaw + particle_AttackedDirection_Yaw)) * entity_ActivityMulti * particle_VeloMulti, 0.2f), NatFloat.createUniform(0.6f, 0.2f), NatFloat.createUniform(MathF.Cos(GameMath.NormaliseAngleRad(entity.Pos.Yaw + particle_AttackedDirection_Yaw)) * entity_ActivityMulti * particle_VeloMulti, 0.2f)],
                    basePos = entity.Pos.XYZ.Add(0f, entity.SelectionBox.Height / 1.5f, 0f),

                    LifeLength = NatFloat.createUniform(Config_Reference.Config_Loaded.Config_Effect.VFX_Bleeding_Acc.Particle_LifeLength_Base * (1 - entity.World.BlockAccessor.GetClimateAt(entity.Pos.AsBlockPos).Rainfall * Config_Reference.Config_Loaded.Config_Effect.VFX_Bleeding_Acc.Particle_LifeLength_RainMulti), Config_Reference.Config_Loaded.Config_Effect.VFX_Bleeding_Acc.Particle_LifeLength_Base * 0.45f),
                };
                entity.Api.World.SpawnParticles(particle_BloodProps);
                BID_Function_General.Log_Debug_Verbose("Spawned particle {0} - bleed: {1}", loggers: [particle_Amount, entity_BehaviorBleed.Bleed_CurrentLevel_External]);
            }


            /// <summary>
            /// called on entity.OnEntityReceiveDamage;
            /// for creating particles: on damaged blood splash
            /// </summary>
            /// <param name="entity"></param>
            /// <param name="damageSource"></param>
            internal static void BleedEffect_Particle_BloodSplash(Entity entity, DamageSource damageSource)
            {
                if (Config_Reference.Config_Loaded.Config_TypeModifier.TypeMod_Damage_Acc.Dict_DamageType.ContainsKey(damageSource.Type) is not true) { BID_Function_General.Log_Debug("damageSource was null or missing key: {0}", loggers: [damageSource.Type.ToString() ?? "null"]); return; }
                if (entity.GetBehavior<BID_Handle_Entity.EntityBehavior_Bleed>() is not BID_Handle_Entity.EntityBehavior_Bleed entity_BehaviorBleed) { return; }

                float attackedDirection_Yaw = GameMath.NormaliseAngleRad((float)(damageSource.GetAttackAngle(entity.Pos.XYZ, out var yaw, out _) ? yaw : 0d));

                //amount
                float particle_Amount = NatFloat.createUniform(BID_Function_General.Calc_Curve_ExpoEaseOut(//TODO: this is negative sometimes?
                    entity_BehaviorBleed.Bleed_CurrentLevel_External * 2f,
                    Config_Reference.Config_Loaded.Config_Effect.VFX_BloodSplash_Acc.Particle_AmtMulti_SlopeRate,
                    Config_Reference.Config_Loaded.Config_Effect.VFX_BloodSplash_Acc.Particle_AmtMulti_Max,
                    0.05f, -0.2f), 1f).nextFloat();

                //size
                float particle_Size_Base = Config_Reference.Config_Loaded.Config_Effect.VFX_BloodSplash_Acc.Particle_SizeMulti_Base;
                float particle_Size_Multi = BID_Function_General.Calc_Curve_SingleSigmoid(
                    entity_BehaviorBleed.Bleed_CurrentLevel_External * 4f,
                    Config_Reference.Config_Loaded.Config_Effect.VFX_BloodSplash_Acc.Particle_SizeMulti_SlopeRate,
                    Config_Reference.Config_Loaded.Config_Effect.VFX_BloodSplash_Acc.Particle_SizeMulti_Max,
                    2.4f, 1f);


                Particle_Blood_Quad particle_BloodProps = new(entity)
                {
                    Quantity = NatFloat.createUniform(MathF.Max(particle_Amount - particle_Size_Multi, 1f), particle_Amount / 4f),
                    Size = NatFloat.createUniform(particle_Size_Multi * particle_Size_Base, (particle_Size_Multi * particle_Size_Base) / 5f),
                    Velocity = [NatFloat.createUniform(MathF.Sin(attackedDirection_Yaw), 0.6f), NatFloat.createUniform(1.6f, 0.2f), NatFloat.createUniform(MathF.Cos(attackedDirection_Yaw), 0.6f)],//TODO: add slight variation (based on damagetype)
                };
                entity.Api.World.SpawnParticles(particle_BloodProps);
                BID_Function_General.Log_Debug_Verbose("Spawned particle - splash: {0}", loggers: [particle_Amount]);
            }


            //called on ???; fors creating particles: on death pop
            internal static void BleedEffect_Particle_DeathPop(Entity entity)
            {
                if (!Config_Reference.Config_Loaded.Config_Effect.VFX_DeathPop_Acc.Particle_Toggle || !Config_Reference.Config_Loaded.Config_System.System_Effect_Acc.Effects_MasterToggle) { return; }
                if (entity.GetBehavior<EntityBehaviorHealth>() is not EntityBehaviorHealth entity_BehaviorHealth) { return; }
                if (entity.GetBehavior<BID_Handle_Entity.EntityBehavior_Bleed>() is not BID_Handle_Entity.EntityBehavior_Bleed entity_BehaviorBleed) { return; }

                BID_Function_General.Log_Debug("dps: {0}", loggers: [entity_BehaviorBleed.Bleed_CurrentLevel_External * Config_Reference.Config_Loaded.Curve_Variable.Variable_External_Acc.External_Rate]);//temp remove
                if (entity_BehaviorHealth.Health > 0) { return; }
                if (entity_BehaviorBleed.Bleed_CurrentLevel_External * Config_Reference.Config_Loaded.Curve_Variable.Variable_External_Acc.External_Rate < Config_Reference.Config_Loaded.Config_Effect.VFX_DeathPop_Acc.Particle_BleedThreshold) { return; }

                //amount
                float particle_Amount = MathF.Min(entity_BehaviorBleed.Bleed_CurrentLevel_External * 2, Config_Reference.Config_Loaded.Config_Effect.VFX_DeathPop_Acc.Particle_Amount_Max);

                //size
                float particle_Size_Base = 0.4f;


                Particle_Blood_Quad particle_BloodProps = new(entity)
                {
                    Quantity = NatFloat.createUniform(particle_Amount, particle_Amount / 5),
                    Size = NatFloat.createUniform(particle_Size_Base, particle_Size_Base / 4),
                    Velocity = [NatFloat.createInvGauss(0f, 1f), NatFloat.createUniform(1.9f, 1.1f), NatFloat.createInvGauss(0f, 1f)],
                    GravityEffect = NatFloat.createUniform(0.3f, 0.1f),
                };
                entity.Api.World.SpawnParticles(particle_BloodProps);
                BID_Function_General.Log_Debug_Verbose("Spawned particle - deathpop: {0}", loggers: [particle_Amount]);
            }

            
            internal static void BleedEffect_DrippingHPBar() //TODO: do
            {

            }
        }


        internal class BID_Effect_SFX//TODO: do
        {

        }
    }


    internal class Particle_Blood_Quad : Particle_Blood_Base
    {
        internal Particle_Blood_Quad(Entity entity) : base(entity)
        {
            LifeLength = NatFloat.createUniform(1f, 0.3f); //4? sec per
            GravityEffect = NatFloat.createUniform(0.8f, 0.1f);
            ParticleModel = EnumParticleModel.Quad;
        }
    }

    internal class Particle_Blood_Cube : Particle_Blood_Base
    {
        private float particle_LifeLengthMedian = Config_Reference.Config_Loaded.Config_Effect.VFX_Bleeding_Acc.Particle_LifeLength_Base;

        internal Particle_Blood_Cube(Entity entity) : base(entity)
        {
            LifeLength = NatFloat.createUniform(particle_LifeLengthMedian, particle_LifeLengthMedian * 0.45f); //4? sec per
            ParticleModel = EnumParticleModel.Cube;
        }
    }


    internal class Particle_Blood_Base : AdvancedParticleProperties, IParticlePropertiesProvider
    {
        protected Entity entity;

        internal Particle_Blood_Base(Entity entity)
        {
            this.entity = entity;

            //Async = true; //TODO: find way to apply this without needing a completely new particle
            DieOnRainHeightmap = false;
            basePos = entity.Pos.XYZ.Add(0d, entity.SelectionBox.Height / 1.5f, 0d);
            HsvaColor =
                [
                NatFloat.createUniform(0f, 0f), //red
                NatFloat.createUniform(255, 0f),
                NatFloat.createUniform(128, 20f), //~50%
                NatFloat.createUniform(255f, 0f)
                ];
        }
    }
}
