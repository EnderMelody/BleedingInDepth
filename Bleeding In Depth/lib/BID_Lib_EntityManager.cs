using BleedingInDepth.config;
using System;
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
    public class BID_Lib_EntityManager
    {
        private static ICoreAPI API = BleedingInDepthModSystem.API;


        public class EntityBehavior_Bleed(Entity entity) : EntityBehavior(entity)
        {
            public override string PropertyName() { return "bleed"; }

            internal DamageSource? lastBleedSource;
            internal float appliedDamage_Base;
            internal float health_PreDamage;
            internal float deltaTimeSum;
            internal float tickCounter_Client;


            //synced states
            internal ITreeAttribute State_SyncTree_Bleed
            {
                get => entity.WatchedAttributes.GetOrAddTreeAttribute("BID_Tree_Bleed");
                set => entity.WatchedAttributes.SetAttribute("BID_Tree_Bleed", value);
            }
            internal float Bleed_CurrentLevel_External
            {
                get => State_SyncTree_Bleed.GetFloat("BID_Bleed_CurrentLevel_External");
                set
                {
                    State_SyncTree_Bleed.SetFloat("BID_Bleed_CurrentLevel_External", value);
                    entity.WatchedAttributes.MarkPathDirty("BID_Tree_Bleed");
                }
            }
            internal float Bleed_CurrentLevel_Internal
            {
                get => State_SyncTree_Bleed.GetFloat("BID_Bleed_CurrentLevel_Internal");
                set
                {
                    State_SyncTree_Bleed.SetFloat("BID_Bleed_CurrentLevel_Internal", value);
                    entity.WatchedAttributes.MarkPathDirty("BID_Tree_Bleed");
                }
            }

            internal ITreeAttribute State_States_SyncTree
            {
                get => entity.WatchedAttributes.GetOrAddTreeAttribute("BID_SyncTree_State");
                set => entity.WatchedAttributes.SetAttribute("BID_SyncTree_State", value);
            }
            internal float State_BleedReduction
            {
                get => State_States_SyncTree.GetFloat("BID_State_BleedReduction");
                set
                {
                    State_States_SyncTree.SetFloat("BID_State_BleedReduction", value);
                    entity.WatchedAttributes.MarkPathDirty("BID_SyncTree_State");
                }
            }

            //unsynced states
            internal bool State_IsRagged
            {
                get => entity.Attributes.GetBool("BID_State_IsRagged");
                set => entity.Attributes.SetBool("BID_State_IsRagged", value);
            }
            internal bool State_IsBandaged
            {
                get => entity.Attributes.GetBool("BID_State_IsBandaged");
                set => entity.Attributes.SetBool("BID_State_IsBandaged", value);
            }



            public static void Entity_AddBleedBehavior(Entity entity)
            {
                if (entity is null) { BID_Lib_FunctionsGeneral.Log_Debug("Recived null entity", loggers: []); return; }
                if (entity is EntityPlayer || entity is not null) //TODO: add check for type of entity; TODO: add list of allowed entities to config
                {
                    if (entity.IsCreature && entity.GetBehavior<EntityBehavior_Bleed>() is null)
                    {
                        EntityBehavior_Bleed EntityBleedable = new(entity);
                        entity.AddBehavior(EntityBleedable);
                        EntityBleedable.AfterInitialized(false);//TODO: see if there is a way to guarentee (my) afterinitialized is called after all behaviors are already set (apply mine last) -> isFirstTick() ?
                    }
                }
            }


            public override void AfterInitialized(bool onFirstSpawn)
            {
                base.AfterInitialized(onFirstSpawn);

                if (entity.GetBehavior<EntityBehaviorHealth>() is not EntityBehaviorHealth entity_BehaviorHealth) { return; }
                switch (entity.World.Side)
                {
                    case EnumAppSide.Server:
                        {
                            if (API.ModLoader.GetMod("combatoverhaul") is not null) //TODO: create a list with any mod that changes damageTypes to not just blunt, then check if any of the mods are loaded and apply enable damagetypes
                            {
                                Config_Reference.Config_Loaded.Config_System.System_UseDamageTypeCompat = true;
                            }
                            entity_BehaviorHealth.onDamaged += BID_DelegateTo_BleedConversion; //TODO: find a way to recognize if damagetypes are used (through CO or other mods) and autoset compat instead of using a list of mods
                            break;
                        }
                    case EnumAppSide.Client:
                        {
                            break;
                        }
                }
            }


            private float BID_DelegateTo_BleedConversion(float dmg, DamageSource dmgSource) //TODO: see if i can redo BleedDamage_Conversion to not need the entity passed without moving it here so i can call it directly
            {
                if (entity is IServerPlayer && Config_Reference.Config_Loaded.Config_System.System_KokoaIsInvincibleGodMode && ((IServerPlayer)entity).PlayerName == "kokoa_real") { return 0f; }//TODO: remove
                float damageApplied = BID_Lib_FunctionsBleed.BleedDamage_Conversion_Store(entity, dmg, ref dmgSource);
                if (damageApplied < 0f || damageApplied == BID_Lib_FunctionsBleed.CancelAndReturnValue) { return dmg; }
                else { return damageApplied; } //TODO: add/move more checks here?
            }


            public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
            {
                base.OnEntityReceiveDamage(damageSource, ref damage);
                if (entity.World.Side is EnumAppSide.Client && entity.HasBehavior<EntityBehavior_Bleed>()) { BID_Lib_FunctionsEffect.BID_Effect_VFX.BleedEffect_Particle_BloodSplash(entity, damageSource); }
            }


            public override void OnGameTick(float deltaTime) //called 75 times a second (i think its actually 33? maybe logger spam was causing lag)
            {
                base.OnGameTick(deltaTime);
                if (entity is null) { BID_Lib_FunctionsGeneral.Log_Debug("Recieved entity was null on side: {0}", loggers: [entity?.World.Side.ToString() ?? API.Side.ToString()]); return; }
                if (entity.GetBehavior<EntityBehavior_Bleed>() is not EntityBehavior_Bleed entity_BleedBehavior) { BID_Lib_FunctionsGeneral.Log_Debug("Recieved entity had null EntityBehavior_Bleed: {0}", loggers: [entity?.GetPrefixAndCreatureName() ?? "null entity"]); return; }

                switch (entity.World.Side)
                {
                    case EnumAppSide.Client: //calls clientside effect systems
                        if (Config_Reference.Config_Loaded.Config_System.System_Effects_System)
                        {
                            if (tickCounter_Client >= Config_Reference.Config_Loaded.Config_TimeScale.TickCounter_BleedParticle)
                            {
                                if (entity_BleedBehavior.Bleed_CurrentLevel_External > 0) { BID_Lib_FunctionsEffect.BID_Effect_VFX.BleedEffect_Particle_Bleeding(entity); }
                                tickCounter_Client = 0;
                            }
                            else { tickCounter_Client++; }
                        }
                        break;
                    case EnumAppSide.Server: //calls serverside bleed damage calculations and applications
                        if (entity is EntityPlayer entityPlayer) { if (((IServerPlayer)entityPlayer.Player).ConnectionState != EnumClientState.Playing) { return; } }
                        if (lastBleedSource is not null && appliedDamage_Base is not 0) { BID_Lib_FunctionsBleed.BleedDamage_Conversion_Apply(entity, lastBleedSource); }
                        if (deltaTimeSum >= Config_Reference.Config_Loaded.Config_TimeScale.DeltaTime_SumRequired_BleedRate)
                        {
                            if (entity_BleedBehavior.Bleed_CurrentLevel_External > 0f || entity_BleedBehavior.Bleed_CurrentLevel_Internal > 0f) { BID_Lib_FunctionsBleed.BleedDamage_Tick(entity, deltaTimeSum); }
                            deltaTimeSum = 0f;
                        }
                        else { deltaTimeSum += deltaTime; }
                        break;
                }
            }


            public override void OnEntityRevive() //TODO: see if player revive mod differentiates between respawn revive and revived through mod itself, add config for if bleed levels should be reset upon mod revival. this would require bandaging the bleeding (or waiting it out) to revive the entity or risk it bleeding out again
            {
                base.OnEntityRevive();
                if (entity.GetBehavior<EntityBehavior_Bleed>() is not EntityBehavior_Bleed entity_BleedBehavior) { return; }

                entity_BleedBehavior.Bleed_CurrentLevel_External = 0;
                entity_BleedBehavior.Bleed_CurrentLevel_Internal = 0;
            }
            //TODO: add per entity config stuff here at some point; why here?
        }
    }
}
