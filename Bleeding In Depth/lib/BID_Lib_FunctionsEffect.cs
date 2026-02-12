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
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Server;
using static BleedingInDepth.lib.BID_Lib_EntityManager;

namespace BleedingInDepth.lib
{
    internal class BID_Lib_FunctionsEffect
    {
        private static ICoreAPI API = BleedingInDepthModSystem.API;
        private static ICoreClientAPI clientAPI = BleedingInDepthModSystem.clientAPI;


        internal class BID_Effect_VFX
        {
            //Called on entity.OnEntityReceiveDamage; stores hit direction to list for directional VFX to use
            internal static void BleedEffect_Store_AttackDirection(Entity entity, DamageSource damageSource)
            {
                if (!Config_Reference.Config_Loaded.Config_System.System_Effects_Acc.Effects_MasterToggle) { return; }
                if (entity.GetBehavior<BID_Lib_EntityManager.EntityBehavior_Bleed>() is not BID_Lib_EntityManager.EntityBehavior_Bleed entity_BleedBehavior) { return; }
                if (entity_BleedBehavior.Bleed_CurrentLevel_External <= 0) { entity_BleedBehavior.attackedDirection_List.Clear(); }

                float facingDirection_Yaw = entity.Pos.Yaw;
                float attackedDirection_Yaw = GameMath.NormaliseAngleRad((float)(damageSource.GetAttackAngle(entity.Pos.XYZ, out var yaw, out _) ? yaw : 0d));
                float attackedFromFacingDirection_Yaw = GameMath.AngleRadDistance(facingDirection_Yaw, attackedDirection_Yaw);

                entity_BleedBehavior.attackedDirection_List.Add(attackedFromFacingDirection_Yaw);
            }


            //called on entity.OnGameTick; for creating particles: actively bleeding
            internal static void BleedEffect_Particle_Bleeding(Entity entity)
            {
                if (!Config_Reference.Config_Loaded.Config_Effect.VFX_Bleeding_Acc.Particle_Toggle || !Config_Reference.Config_Loaded.Config_System.System_Effects_Acc.Effects_MasterToggle) { return; }
                if (entity.GetBehavior<BID_Lib_EntityManager.EntityBehavior_Bleed>() is not BID_Lib_EntityManager.EntityBehavior_Bleed entity_BleedBehavior) { return; }

                float state_BleedReduction = 0f;
                float particle_CurrentDPS = Config_Reference.Config_Loaded.Config_Rate.Rate_Bleed_Acc.Damage_External * entity_BleedBehavior.Bleed_CurrentLevel_External;
                float entity_ActivityMulti = 1f + ((entity.Pos.Motion.HorLength() > 0) ? Config_Reference.Config_Loaded.Config_Rate.Rate_Activity_Acc.ActivityMulti_Walk : 0f);
                if (entity is EntityPlayer entityPlayer) //AcitivityMulti for player specific actions
                {
                    entity_ActivityMulti += (entityPlayer.Controls.Sprint) ? Config_Reference.Config_Loaded.Config_Rate.Rate_Activity_Acc.ActivityMulti_Sprint : 0f;
                    entity_ActivityMulti += (entityPlayer.Controls.LeftMouseDown) ? Config_Reference.Config_Loaded.Config_Rate.Rate_Activity_Acc.ActivityMulti_Hit : 0f;
                }
                

                //amount
                float particle_Amount = (NatFloat.createUniform(BID_Lib_FunctionsGeneral.Calc_Curve_ExpoEaseOut(
                    particle_CurrentDPS * entity_ActivityMulti * 5f,
                    Config_Reference.Config_Loaded.Config_Effect.VFX_Bleeding_Acc.Particle_AmtMulti_SlopeRate,
                    Config_Reference.Config_Loaded.Config_Effect.VFX_Bleeding_Acc.Particle_AmtMulti_Max,
                    0f, 0.5f), 1f).nextFloat());

                //size
                float particle_SizeBase = Config_Reference.Config_Loaded.Config_Effect.VFX_Bleeding_Acc.Particle_SizeMulti_Base;
                float particle_SizeMulti = BID_Lib_FunctionsGeneral.Calc_Curve_SingleSigmoid(
                    particle_CurrentDPS * 8f,
                    Config_Reference.Config_Loaded.Config_Effect.VFX_Bleeding_Acc.Particle_SizeMulti_SlopeRate,
                    Config_Reference.Config_Loaded.Config_Effect.VFX_Bleeding_Acc.Particle_SizeMulti_Max - 1f,
                    2.4f, 1f);

                //reducer
                state_BleedReduction += BID_Lib_FunctionsGeneral.Calc_Flag_CheckBit(entity_BleedBehavior.State_BleedReductionFlag, 0) ? Config_Reference.Config_Loaded.Config_HealBonus.HealBonus_BleedReduction_Care : (BID_Lib_FunctionsGeneral.Calc_Flag_CheckBit(entity_BleedBehavior.State_BleedReductionFlag, 1) ? Config_Reference.Config_Loaded.Config_HealBonus.HealBonus_BleedReduction_Pressure : 0f);
                state_BleedReduction += BID_Lib_FunctionsGeneral.Calc_Flag_CheckBit(entity_BleedBehavior.State_BleedReductionFlag, 3) ? Config_Reference.Config_Loaded.Config_HealBonus.HealBonus_BleedReduction_Bandage : (BID_Lib_FunctionsGeneral.Calc_Flag_CheckBit(entity_BleedBehavior.State_BleedReductionFlag, 2) ? Config_Reference.Config_Loaded.Config_HealBonus.HealBonus_BleedReduction_Rag : 0f);
                float particle_FarEntityDiv = (entity.Pos.SquareDistanceTo(clientAPI.World.Player.Entity.Pos) > MathF.Pow(Config_Reference.Config_Loaded.Config_Effect.VFX_Bleeding_Acc.Particle_SimDistance, 2f)) ? 3f : 1f;
                float particle_Divider = MathF.Max((state_BleedReduction) + (entity.Alive ? 0f : 5f) + (particle_SizeMulti - 1f), 1f) * particle_FarEntityDiv;

                //velocity
                float particle_AttackedDirection_Yaw = entity_BleedBehavior.attackedDirection_List.ElementAtOrDefault(Random.Shared.Next(entity_BleedBehavior.attackedDirection_List.Count));


                Particle_Blood_Bleeding particle_BloodProps = new(entity)
                {
                    Quantity = NatFloat.createUniform((particle_Amount / particle_Divider), (MathF.Max((particle_Amount / particle_Divider), 3f) / 5f)),
                    Size = NatFloat.createUniform((particle_SizeMulti * particle_SizeBase), (particle_SizeMulti * particle_SizeBase) / 5f),

                    Velocity = [NatFloat.createUniform(MathF.Sin(GameMath.NormaliseAngleRad(entity.Pos.Yaw + particle_AttackedDirection_Yaw)) * entity_ActivityMulti * 0.3f, 0.2f), NatFloat.createUniform(0.5f, 0.2f), NatFloat.createUniform(MathF.Cos(GameMath.NormaliseAngleRad(entity.Pos.Yaw + particle_AttackedDirection_Yaw)) * entity_ActivityMulti * 0.3f, 0.2f)],//if (entityPlayer.Controls.Sprint)
                    basePos = entity.Pos.XYZ.Add(0f, entity.SelectionBox.Height / 1.5f, 0f),
                };
                entity.Api.World.SpawnParticles(particle_BloodProps);
            }


            //called on entity.OnEntityReceiveDamage; for creating particles: on damaged blood splash
            internal static void BleedEffect_Particle_BloodSplash(Entity entity, DamageSource damageSource)//TODO: this can also be used for the "blood pop" effect, just pass something specific like applied bleed amount
            {
                if (!Config_Reference.Config_Loaded.Config_Effect.VFX_BloodSplash_Acc.Particle_Toggle || !Config_Reference.Config_Loaded.Config_System.System_Effects_Acc.Effects_MasterToggle) { return; }
                if (Config_Reference.Config_Loaded.Config_DamageType.Dict_DamageType.ContainsKey(damageSource.Type) is not true) { BID_Lib_FunctionsGeneral.Log_Debug("damageSource was null or missing key: {0}", loggers: [damageSource.Type.ToString() ?? "null"]); return; }
                if (entity.GetBehavior<BID_Lib_EntityManager.EntityBehavior_Bleed>() is not BID_Lib_EntityManager.EntityBehavior_Bleed entity_BleedBehavior) { return; }
                
                float attackedDirection_Yaw = GameMath.NormaliseAngleRad((float)(damageSource.GetAttackAngle(entity.Pos.XYZ, out var yaw, out _) ? yaw : 0d));

                //amount
                float particle_Amount = NatFloat.createUniform(BID_Lib_FunctionsGeneral.Calc_Curve_ExpoEaseOut(//TODO: this is negative sometimes?
                    entity_BleedBehavior.Bleed_CurrentLevel_External * 2f,
                    Config_Reference.Config_Loaded.Config_Effect.VFX_BloodSplash_Acc.Particle_AmtMulti_SlopeRate,
                    Config_Reference.Config_Loaded.Config_Effect.VFX_BloodSplash_Acc.Particle_AmtMulti_Max,
                    0.05f, -0.2f), 1f).nextFloat();

                //size
                float particle_SizeBase = Config_Reference.Config_Loaded.Config_Effect.VFX_BloodSplash_Acc.Particle_SizeMulti_Base;
                float particle_SizeMulti = BID_Lib_FunctionsGeneral.Calc_Curve_SingleSigmoid(
                    entity_BleedBehavior.Bleed_CurrentLevel_External * 4f,
                    Config_Reference.Config_Loaded.Config_Effect.VFX_BloodSplash_Acc.Particle_SizeMulti_SlopeRate,
                    Config_Reference.Config_Loaded.Config_Effect.VFX_BloodSplash_Acc.Particle_SizeMulti_Max,
                    2.4f, 1f);


                Particle_Blood_Splash particle_BloodProps = new(entity)
                {
                    Quantity = NatFloat.createUniform(MathF.Max(particle_Amount - particle_SizeMulti, 1f), particle_Amount / 4f),
                    Size = NatFloat.createUniform(particle_SizeMulti * particle_SizeBase, (particle_SizeMulti * particle_SizeBase) / 5f),
                    Velocity = [NatFloat.createUniform(MathF.Sin(attackedDirection_Yaw), 0.6f), NatFloat.createUniform(1.6f, 0.2f), NatFloat.createUniform(MathF.Cos(attackedDirection_Yaw), 0.6f)],//TODO: add slight variation (based on damagetype)
                };
                entity.Api.World.SpawnParticles(particle_BloodProps);
            }


            internal static void BleedEffect_DrippingHpBar()//TODO: do
            {

            }
        }

        internal class BID_Effect_SFX//TODO: do
        {

        }
    }






    internal class Particle_Blood_Bleeding : Particle_Blood_Base
    {
        internal Particle_Blood_Bleeding(Entity entity) : base(entity)
        {
            //Velocity;//TODO: find way to tell if entity is moving and increase spread
            LifeLength = NatFloat.createUniform(45f, 20f);//TODO: add master bleeding particle that spawns occasionally when bleeding that lasts much longer to allow longer lasting trails. when i figure out merging particles on collision this one can be the absorber maybe?
            //baseVelocity = entity.Pos.Motion.ToVec3f();//TODO: does this work?
            //RedEvolve = EvolvingNatFloat.create(0f, 1f, 10f);//TODO: finish this; change base color to grab from dictionary based on entity type
        }
    }


    internal class Particle_Blood_Splash : Particle_Blood_Base
    {
        internal Particle_Blood_Splash(Entity entity) : base(entity)
        {
            LifeLength = NatFloat.createUniform(1f, 0.5f); //4? sec per
            basePos = entity.Pos.XYZ.Add(0d, entity.SelectionBox.Height / 1.5f, 0d);
            GravityEffect = NatFloat.createUniform(0.8f, 0.1f);
            ParticleModel = EnumParticleModel.Quad;
        }
    }


    abstract class Particle_Blood_Base : AdvancedParticleProperties, IParticlePropertiesProvider
    {
        protected Entity entity;

        internal Particle_Blood_Base(Entity entity)
        {
            this.entity = entity;

            //Quantity = NatFloat.createUniform(1f, 0f); Size = NatFloat.createUniform(0.8f, 0f); LifeLength = NatFloat.createUniform(1f, 0f); RandomVelocityChange?
            //Async = true;
            DieOnRainHeightmap = false;
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
