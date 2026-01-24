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

namespace BleedingInDepth.lib
{
    internal class BID_Lib_FunctionsEffect
    {
        private static ICoreAPI API = BleedingInDepthModSystem.API;
        private static ICoreClientAPI clientAPI = BleedingInDepthModSystem.clientAPI;


        internal class BID_Effect_VFX
        {
            internal static void BleedEffect_Particle_Bleeding(Entity entity) //called for creating particles: actively bleeding
            {
                if (!Config_Reference.Config_Loaded.Config_Effect.VFX_Bleeding_Acc.Particle_Toggle) { return; }
                if (entity.GetBehavior<BID_Lib_EntityManager.EntityBehavior_Bleed>() is not BID_Lib_EntityManager.EntityBehavior_Bleed entity_BleedBehavior) { return; }

                float particle_CurrentDPS = Config_Reference.Config_Loaded.Config_Rate.Rate_BleedDamage_External * entity_BleedBehavior.Bleed_CurrentLevel_External;

                //amount
                float particle_Amount_SlopeRate = Config_Reference.Config_Loaded.Config_Effect.VFX_Bleeding_Acc.Particle_AmtMulti_SlopeRate; float particle_Amount_Max = Config_Reference.Config_Loaded.Config_Effect.VFX_Bleeding_Acc.Particle_AmtMulti_SpawnCap;
                float particle_Amount = (NatFloat.createUniform(BID_Lib_FunctionsGeneral.Calc_Curve_ExpoEaseOut(particle_CurrentDPS * 5f, particle_Amount_SlopeRate, particle_Amount_Max, 0f, 0.5f), 1f).nextFloat());

                //size
                float particle_SizeMulti_SlopeRate = Config_Reference.Config_Loaded.Config_Effect.VFX_Bleeding_Acc.Particle_SizeMulti_SlopeRate; float particle_SizeMulti_Max = Config_Reference.Config_Loaded.Config_Effect.VFX_Bleeding_Acc.Particle_SizeMulti_SpawnCap;
                float particle_SizeMulti = BID_Lib_FunctionsGeneral.Calc_Curve_SingleSigmoid(particle_CurrentDPS * 8f, particle_SizeMulti_SlopeRate, particle_SizeMulti_Max - 1f, 2.4f, 1f);

                //reducer
                float particle_FarEntityDiv = (entity.Pos.SquareDistanceTo(clientAPI.World.Player.Entity.Pos) > MathF.Pow(Config_Reference.Config_Loaded.Config_Effect.VFX_Bleeding_Acc.Particle_SimDistance, 2f)) ? 3f : 1f;
                float particle_Divider = MathF.Max((entity_BleedBehavior.State_BleedReduction) + (entity.Alive ? 0f : 5f) + (particle_SizeMulti - 1f), 1f) * particle_FarEntityDiv;

                //float facing_Yaw = entity.Pos.Yaw; //TODO: facing or random direction? save hit directions somehow? entity facing - hit from -> list, choose a random/cycle through "wound" from the list based on bleedlevel and add facing to entity facing every 1/list.length 'th bleeding vfx call

                BID_Lib_FunctionsGeneral.Log_Debug("divider: {0}, distance: {1}", loggers: [particle_Divider, entity.Pos.SquareDistanceTo(clientAPI.World.Player.Entity.Pos)]);
                Particle_Blood_Bleeding particle_BloodProps = new(entity)
                {
                    Quantity = NatFloat.createUniform((particle_Amount / particle_Divider), (MathF.Max((particle_Amount / particle_Divider), 3f) / 5f)),
                    Size = NatFloat.createUniform(particle_SizeMulti * Config_Reference.Config_Loaded.Config_Effect.VFX_Bleeding_Acc.Particle_SizeMulti_Base, 0.1f)
                };
                entity.Api.World.SpawnParticles(particle_BloodProps);
            }


            internal static void BleedEffect_Particle_BloodSplash(Entity entity, DamageSource damageSource)//TODO: this can also be used for the "blood pop" effect, just pass specific applied bleed amount
            {
                if (!Config_Reference.Config_Loaded.Config_Effect.VFX_BloodSplash_Acc.Particle_Toggle) { return; }
                if (Config_Reference.Config_Loaded.Config_DamageType.Dict_DamageType.ContainsKey(damageSource.Type) is not true) { BID_Lib_FunctionsGeneral.Log_Debug("damageSource was null or missing key: {0}", loggers: [damageSource.Type.ToString() ?? "null"]); return; }
                if (entity.GetBehavior<BID_Lib_EntityManager.EntityBehavior_Bleed>() is not BID_Lib_EntityManager.EntityBehavior_Bleed entity_BleedBehavior) { return; }
                
                float attackedDirection_Yaw = GameMath.NormaliseAngleRad((float)(damageSource.GetAttackAngle(entity.Pos.XYZ, out var yaw, out _) ? yaw : 0d)); //yaw = 0-6.28 ... but somehow dummy gets out a 7.xxx???

                //amount
                float particle_SlopeRate = Config_Reference.Config_Loaded.Config_Effect.VFX_BloodSplash_Acc.Particle_AmtMulti_SlopeRate; float particle_Max = Config_Reference.Config_Loaded.Config_Effect.VFX_BloodSplash_Acc.Particle_AmtMulti_SpawnCap;
                float particle_Amount = NatFloat.createUniform(BID_Lib_FunctionsGeneral.Calc_Curve_ExpoEaseOut(entity_BleedBehavior.Bleed_CurrentLevel_External * 2f, particle_SlopeRate, particle_Max, 0.05f, -0.2f), 1f).nextFloat();

                //size
                float particle_SizeBase = Config_Reference.Config_Loaded.Config_Effect.VFX_BloodSplash_Acc.Particle_SizeMulti_Base;
                float particle_SizeMulti = BID_Lib_FunctionsGeneral.Calc_Curve_SingleSigmoid(entity_BleedBehavior.Bleed_CurrentLevel_External * 4f, 1.2f, 2f, 2.4f, 1f);


                Particle_Blood_Splash particle_BloodProps = new(entity)
                {
                    Quantity = NatFloat.createUniform(MathF.Max(particle_Amount - particle_SizeMulti, 1f), particle_Amount / 4f),
                    Size = NatFloat.createUniform(particle_SizeMulti * particle_SizeBase, particle_SizeMulti * particle_SizeBase / 5f),
                    Velocity = [NatFloat.createUniform(MathF.Sin(attackedDirection_Yaw) * 1f, 0.6f), NatFloat.createUniform(1.6f, 0.2f), NatFloat.createUniform(MathF.Cos(attackedDirection_Yaw) * 1f, 0.6f)]//TODO: add slight variation (based on damagetype)
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
            Velocity = [NatFloat.createUniform(0f, 0.5f), NatFloat.createUniform(0.1f, 0f), NatFloat.createUniform(0f, 0.5f)];//TODO: find way to tell if entity is moving and increase spread; TODO: there was something i swear, check mod walking stick
            LifeLength = NatFloat.createUniform(45f, 20f);//TODO: add master particle that spawns occasionally when bleeding that lasts much longer to allow longer lasting trails. when i figure out merging particles on collision this one can be the absorber maybe?
            basePos = entity.Pos.XYZ.Add(rand.NextDouble() / 5 - 0.1d, entity.SelectionBox.Height / 2, rand.NextDouble() / 5 - 0.1d);
            //baseVelocity = entity.Pos.Motion.ToVec3f();
            //RedEvolve = EvolvingNatFloat.create(0f, 1f, 10f);//TODO: finish this
        }
    }


    internal class Particle_Blood_Splash : Particle_Blood_Base
    {
        internal Particle_Blood_Splash(Entity entity) : base(entity)
        {
            LifeLength = NatFloat.createUniform(1f, 0.5f);
            basePos = entity.Pos.XYZ.Add(0d, entity.SelectionBox.Height / 1.5f, 0d);
            GravityEffect = NatFloat.createUniform(0.8f, 0.1f);
            ParticleModel = EnumParticleModel.Quad;
        }
    }


    abstract class Particle_Blood_Base : AdvancedParticleProperties, IParticlePropertiesProvider //ToBytes/FromBytes to save; gamemath.*; RandomVelocityChange?
    {
        protected Entity entity;
        protected Random rand = new();

        internal Particle_Blood_Base(Entity entity)
        {
            this.entity = entity;

            //Async = true;
            Quantity = NatFloat.createUniform(1f, 0f);
            Size = NatFloat.createUniform(0.8f, 0f);
            LifeLength = NatFloat.createUniform(1f, 0f); //4? sec per
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

