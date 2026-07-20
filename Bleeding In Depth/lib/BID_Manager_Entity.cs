using BleedingInDepth.config;
using HarmonyLib;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace BleedingInDepth.lib
{
    public class BID_Manager_Entity
    {
        private static ICoreAPI API = BID_VarRef.API;


        public class EntityBehavior_Bleed(Entity entity) : EntityBehavior(entity)
        {
            public override string PropertyName() { return "bleed"; }

            internal DamageSource? lastBleedSource;
            internal float appliedDamage_Base;
            internal float health_PreDamage;
            internal float deltaTimeSum;
            internal float tickCounter_Client;
            internal FrozenDictionary<string, float>? categoryType_Dict;
            internal List<float> attackedDirection_List = [];


            //synced states
            internal ITreeAttribute State_SyncTree_Bleed
            {
                get => entity.WatchedAttributes.GetOrAddTreeAttribute("BID_Tree_Bleed");
                set
                {
                    if (API.Side is not EnumAppSide.Server) { return; }
                    entity.WatchedAttributes.SetAttribute("BID_Tree_Bleed", value);
                }
            }
            internal float Bleed_CurrentLevel_External
            {
                get => State_SyncTree_Bleed.GetFloat("BID_Bleed_CurrentLevel_External");
                set
                {
                    if (API.Side is not EnumAppSide.Server) { return; }
                    State_SyncTree_Bleed.SetFloat("BID_Bleed_CurrentLevel_External", value);
                    entity.WatchedAttributes.MarkPathDirty("BID_Tree_Bleed");
                }
            }
            internal float Bleed_CurrentLevel_Internal
            {
                get => State_SyncTree_Bleed.GetFloat("BID_Bleed_CurrentLevel_Internal");
                set
                {
                    if (API.Side is not EnumAppSide.Server) { return; }
                    State_SyncTree_Bleed.SetFloat("BID_Bleed_CurrentLevel_Internal", value);
                    entity.WatchedAttributes.MarkPathDirty("BID_Tree_Bleed");
                }
            }

            internal ITreeAttribute State_States_SyncTree
            {
                get => entity.WatchedAttributes.GetOrAddTreeAttribute("BID_SyncTree_State");
                set
                {
                    if (API.Side is not EnumAppSide.Server) { return; }
                    entity.WatchedAttributes.SetAttribute("BID_SyncTree_State", value);
                }
            }
            internal int State_BleedReductionFlag //bit placement: 0=pressure, 1=care, 2=ragged, 3=bandaged, 4=stitched
            {
                get => State_States_SyncTree.GetInt("BID_State_Flag_BleedReduction");
                set
                {
                    if (API.Side is not EnumAppSide.Server) { return; }
                    State_States_SyncTree.SetInt("BID_State_Flag_BleedReduction", value);
                    entity.WatchedAttributes.MarkPathDirty("BID_SyncTree_State");
                }
            }
            internal string State_EntityCategory
            {
                get => State_States_SyncTree.GetString("BID_State_EntityCategory");
                set
                {
                    if (API.Side is not EnumAppSide.Server) { return; }
                    State_States_SyncTree.SetString("BID_State_EntityCategory", value);
                    entity.WatchedAttributes.MarkPathDirty("BID_SyncTree_State");
                }
            }



            public static void Entity_AddBleedBehavior(Entity entity)
            {
                if (entity is null) { BID_Function_General.Log_Debug("Recived null entity", loggers: []); return; }
                if (entity is not null)
                {
                    if (entity.IsCreature && entity.GetBehavior<EntityBehavior_Bleed>() is null)
                    {
                        EntityBehavior_Bleed entity_BehaviorBleed = new(entity);
                        entity.AddBehavior(entity_BehaviorBleed);
                        entity_BehaviorBleed.AfterInitialized(false);//TODO: see if there is a way to guarentee (my) afterinitialized is called after all behaviors are already set (apply mine last) -> isFirstTick() ?
                        //Entity_BehaviorBleed.State_BleedReductionFlag ??= [0];
                    }
                }
            }


            public override void AfterInitialized(bool onFirstSpawn)
            {
                base.AfterInitialized(onFirstSpawn);

                if (entity.GetBehavior<EntityBehaviorHealth>() is not EntityBehaviorHealth entity_BehaviorHealth) { return; }
                switch (entity.World.Side)//TODO: use event system instead and only subscribe methods that are enabled in the config
                {
                    case EnumAppSide.Server:
                        {
                            entity_BehaviorHealth.onDamaged += BID_DelegateTo_BleedConversion;
                            break;
                        }
                    case EnumAppSide.Client:
                        {
                            break;
                        }
                }
            }


            public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
            {
                //if (entity.GetBehavior<EntityBehaviorHealth>() is not EntityBehaviorHealth entity_BehaviorHealth) { return; } //todo: check fails?
                if (entity.GetBehavior<EntityBehavior_Bleed>() is not EntityBehavior_Bleed entity_BehaviorBleed) { return; }
                switch (entity.World.Side)
                {
                    case EnumAppSide.Server:
                        {
                            break;
                        }
                    case EnumAppSide.Client:
                        {
                            BID_Function_Effect.BID_Effect_VFX.BleedEffect_Store_AttackDirection(entity, damageSource);
                            BID_Function_Effect.BID_Effect_VFX.BleedEffect_Particle_BloodSplash(entity, damageSource);
                            //BID_Function_Effect.BID_Effect_VFX.BleedEffect_Particle_DeathPop(entity);
                            break;
                        }
                }
            }


            private float BID_DelegateTo_BleedConversion(float appliedDamage, DamageSource dmgSource) //TODO: see if i can redo BleedDamage_Conversion to not need the entity passed without moving it here so i can call it directly
            {
                if (entity is IServerPlayer && Config_Reference.Config_Loaded.Config_System.System_Debug_Acc.KokoaIsInvincibleGodMode && ((IServerPlayer)entity).PlayerName == "kokoa_real") { return 0f; }//TODO: remove after testing phase
                if (!Config_Reference.Config_Loaded.Config_System.System_Damage_Acc.Bleed_External && !Config_Reference.Config_Loaded.Config_System.System_Damage_Acc.Bleed_Internal) { BID_Function_General.Log_Debug("Bleeding is disabled in config"); return appliedDamage; }
                
                float postConversion_AppliedDamage = BID_Function_Bleed.BleedDamage_Conversion_Store(entity, appliedDamage, ref dmgSource);
                if (postConversion_AppliedDamage < 0f || postConversion_AppliedDamage == BID_VarRef.CancelAndReturnValue) { return appliedDamage; }
                else { return postConversion_AppliedDamage; } //TODO: add/move more checks here?
            }


            public override void OnGameTick(float deltaTime) //called 75 times a second (i think its actually 33? maybe logger spam was causing lag)
            {
                base.OnGameTick(deltaTime);
                if (entity is null) { BID_Function_General.Log_Debug("Recieved entity was null on: {0}", loggers: [entity?.World.Side.ToString() ?? API.Side.ToString()]); return; }
                if (entity.GetBehavior<EntityBehavior_Bleed>() is not EntityBehavior_Bleed entity_BehaviorBleed) { BID_Function_General.Log_Debug("Recieved entity had null EntityBehavior_Bleed: {0}", loggers: [entity?.GetPrefixAndCreatureName() ?? "null entity"]); return; }
                
                switch (entity.World.Side)
                {
                    case EnumAppSide.Server: //calls serverside bleed damage calculations and applications
                        {
                            if (entity is EntityPlayer entityPlayer) { if (((IServerPlayer)entityPlayer.Player).ConnectionState != EnumClientState.Playing) { return; } }
                            if (lastBleedSource is not null && appliedDamage_Base is not 0) { BID_Function_Bleed.BleedDamage_Conversion_Apply(entity, lastBleedSource); }
                            if (deltaTimeSum >= Config_Reference.Config_Loaded.Config_TimeScale.DeltaTime_SumRequired_BleedRate)
                            {
                                if (entity_BehaviorBleed.Bleed_CurrentLevel_External > 0f || entity_BehaviorBleed.Bleed_CurrentLevel_Internal > 0f) { BID_Function_Bleed.BleedDamage_Tick(entity, deltaTimeSum); }
                                deltaTimeSum = 0f;
                            }
                            else { deltaTimeSum += deltaTime; }
                            break;
                        }
                    case EnumAppSide.Client: //calls clientside effect systems
                        {
                            if (Config_Reference.Config_Loaded.Config_System.System_Effect_Acc.Effects_MasterToggle)
                            {
                                if (tickCounter_Client >= Config_Reference.Config_Loaded.Config_TimeScale.TickCounter_BleedParticle)
                                {
                                    if (Config_Reference.Config_Loaded.Config_Effect.VFX_Bleeding_Acc.Particle_Toggle)
                                    {
                                        if (entity_BehaviorBleed.Bleed_CurrentLevel_External > 0) { BID_Function_Effect.BID_Effect_VFX.BleedEffect_Particle_Bleeding(entity); }
                                    }
                                    tickCounter_Client = 0;
                                }
                                else { tickCounter_Client++; }
                            }
                            break;
                        }
                }
            }


            public override void OnEntityRevive() //TODO: see if player revive mod differentiates between respawn revive and revived through mod itself, add config for if bleed levels should be reset upon mod revival. this would require bandaging the bleeding (or waiting it out) to revive the entity or risk it bleeding out again
            {
                base.OnEntityRevive();
                if (entity.GetBehavior<EntityBehavior_Bleed>() is not EntityBehavior_Bleed entity_BehaviorBleed) { return; }

                entity_BehaviorBleed.Bleed_CurrentLevel_External = 0;
                entity_BehaviorBleed.Bleed_CurrentLevel_Internal = 0;
            }


            public override void OnEntityDeath(DamageSource damageSourceForDeath) //not called on client
            {
                base.OnEntityDeath(damageSourceForDeath);
            }
        }
    }
}
