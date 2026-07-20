using BleedingInDepth.config;
using BleedingInDepth.lib;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace BleedingInDepth.handler
{
    internal class BID_Handle_Entity
    {
        private static ICoreAPI API = BID_VarRef.API;
        //private static ICoreClientAPI API_Client = BID_VarRef.API_Client;
        //private static ICoreServerAPI API_Server = BID_VarRef.API_Server;


        public class EntityBehavior_Bleed(Entity entity) : EntityBehavior(entity)
        {
            public override string PropertyName() { return "bleed"; }

            internal DamageSource? LastBleedSource;
            internal float AppliedDamage_Base;
            internal float Health_PreDamage;
            internal float DeltaTime_Sum;
            internal float DeltaTime_LastHit;
            internal int TickCounter;
            internal bool WasAlive_Client;
            internal FrozenDictionary<string, float>? CategoryType_Dict;
            internal List<float> AttackedDirection_List = [];


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

            internal ITreeAttribute State_SyncTree_State
            {
                get => entity.WatchedAttributes.GetOrAddTreeAttribute("BID_SyncTree_State");
                set
                {
                    if (API.Side is not EnumAppSide.Server) { return; }
                    entity.WatchedAttributes.SetAttribute("BID_SyncTree_State", value);
                }
            }
            /// <summary>
            /// bit placement: 0=care, 1=pressure, 2=ragged, 3=bandaged, 4=stitched
            /// </summary>
            internal int State_BleedReductionFlag
            {
                get => State_SyncTree_State.GetInt("BID_State_Flag_BleedReduction");
                set
                {
                    if (API.Side is not EnumAppSide.Server) { return; }
                    State_SyncTree_State.SetInt("BID_State_Flag_BleedReduction", value);
                    entity.WatchedAttributes.MarkPathDirty("BID_SyncTree_State");
                }
            }
            internal string State_EntityCategory
            {
                get => State_SyncTree_State.GetString("BID_State_EntityCategory");
                set
                {
                    if (API.Side is not EnumAppSide.Server) { return; }
                    State_SyncTree_State.SetString("BID_State_EntityCategory", value);
                    entity.WatchedAttributes.MarkPathDirty("BID_SyncTree_State");
                }
            }



            public static void Entity_AddBehavior_Bleed(Entity entity)
            {
                if (entity is null) { BID_Function_General.Log_Debug("Recived null entity", loggers: []); return; }

                if (entity.IsCreature && entity.GetBehavior<EntityBehavior_Bleed>() is null)
                {
                    EntityBehavior_Bleed entity_BehaviorBleed = new(entity);
                    entity.AddBehavior(entity_BehaviorBleed);
                    
                    entity_BehaviorBleed.AfterInitialized(false);//TODO: see if there is a way to guarentee (my) afterinitialized is called after all behaviors are already set (apply mine last) -> isFirstTick() ? //id like to move this so i dont need the AfterInitialized at all
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
                            entity_BehaviorHealth.onDamaged += (dmg, damageSource) => { if (BleedHandle_Server_OnDamaged_Pre is not null) { return BleedHandle_Server_OnDamaged_Pre.Invoke(entity, dmg, damageSource); } else { return dmg; } };
                            break;
                        }
                    case EnumAppSide.Client:
                        {
                            break;
                        }
                }
            }


            public override void OnEntityRevive()
            {
                base.OnEntityRevive();
                if (entity.GetBehavior<EntityBehavior_Bleed>() is not EntityBehavior_Bleed entity_BehaviorBleed) { return; }

                entity_BehaviorBleed.Bleed_CurrentLevel_External = 0;
                entity_BehaviorBleed.Bleed_CurrentLevel_Internal = 0;
            }


            //invoke events from game actions
            public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage) //fires AFTER damage is dealt
            {
                base.OnEntityReceiveDamage(damageSource, ref damage);

                switch (entity.World.Side)
                {
                    case EnumAppSide.Server:
                        {
                            BleedHandle_Server_OnDamaged_Post?.Invoke(entity);
                            break;
                        }
                    case EnumAppSide.Client:
                        {
                            BleedHandle_Client_OnDamaged?.Invoke(entity, damageSource);
                            break;
                        }
                }
            }

            public override void OnGameTick(float deltaTime)
            {
                base.OnGameTick(deltaTime);
                if (entity?.GetBehavior<EntityBehavior_Bleed>() is not EntityBehavior_Bleed entity_BehaviorBleed) { BID_Function_General.Log_Debug("Recieved entity had null EntityBehavior_Bleed: {0}", loggers: [entity?.GetPrefixAndCreatureName() ?? "null entity"]); return; }

                switch (entity.World.Side)
                {
                    case EnumAppSide.Server:
                        {
                            if (entity is EntityPlayer entityPlayer && ((IServerPlayer)entityPlayer.Player).ConnectionState != EnumClientState.Playing) { return; }

                            if (DeltaTime_Sum < Config_Reference.Config_Loaded.Config_TimeScale.DeltaTime_SumRequired_BleedRate) { DeltaTime_Sum += deltaTime; return; }
                            BleedHandle_Server_OnTick?.Invoke(entity, DeltaTime_Sum);
                            DeltaTime_Sum = 0f;
                            break;
                        }
                    case EnumAppSide.Client:
                        {
                            if (TickCounter < Config_Reference.Config_Loaded.Config_TimeScale.TickCounter_BleedParticle) { TickCounter++; return; }
                            BleedHandle_Client_OnTick?.Invoke(entity);
                            TickCounter = 0;
                            break;
                        }
                }
            }
        }


        //events
        private delegate float BID_OnDamaged(Entity entity, float appliedDamage, DamageSource damageSource);

        private static event BID_OnDamaged? BleedHandle_Server_OnDamaged_Pre;
        private static event Action<Entity>? BleedHandle_Server_OnDamaged_Post;
        private static event Action<Entity, DamageSource>? BleedHandle_Client_OnDamaged;

        private static event Action<Entity, float>? BleedHandle_Server_OnTick;
        private static event Action<Entity>? BleedHandle_Client_OnTick;


        //check config and sub events that are enabled
        internal static void BleedHandle_SubscribeEvent()
        {
            Config_Reference loadedConfig = Config_Reference.Config_Loaded;
            BleedHandle_Server_OnDamaged_Pre = null; BleedHandle_Client_OnDamaged = null; BleedHandle_Server_OnTick = null; BleedHandle_Client_OnTick = null;


            //serverside
            BleedHandle_Server_OnDamaged_Pre += (appliedDamage, damageSource, entity) => BID_Handle_Bleed.Bleed_Conversion_Store(appliedDamage, damageSource, entity);
            BleedHandle_Server_OnDamaged_Post += BID_Handle_Bleed.BleedDamage_Conversion_Apply;

            BleedHandle_Server_OnTick += BID_Handle_Bleed.BleedDamage_Tick;


            //clientside
            if (loadedConfig.Config_System.System_Effect_Acc.Effects_MasterToggle)
            {
                BleedHandle_Client_OnDamaged += BID_Handle_Effect.BID_Effect_VFX.BleedEffect_Store_AttackDirection;
                if (loadedConfig.Config_Effect.VFX_BloodSplash_Acc.Particle_Toggle) { BleedHandle_Client_OnDamaged += BID_Handle_Effect.BID_Effect_VFX.BleedEffect_Particle_BloodSplash; }

                if (loadedConfig.Config_Effect.VFX_Bleeding_Acc.Particle_Toggle) { BleedHandle_Client_OnTick += BID_Handle_Effect.BID_Effect_VFX.BleedEffect_Particle_Bleeding; }
            }
        }
    }
}
