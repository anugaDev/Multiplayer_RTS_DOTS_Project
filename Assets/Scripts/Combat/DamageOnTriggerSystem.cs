using ElementCommons;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;

namespace Combat
{
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(PhysicsSimulationGroup))]
    public partial struct DamageOnTriggerSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationSingleton>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            EndSimulationEntityCommandBufferSystem.Singleton ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            DamageOnTriggerJob damageOnTriggerJob = new DamageOnTriggerJob
            {
                DamageOnTriggerLookup = SystemAPI.GetComponentLookup<DamageOnTrigger>(true),
                TeamLookup = SystemAPI.GetComponentLookup<ElementTeamComponent>(true),
                AlreadyDamagedLookup = SystemAPI.GetBufferLookup<AlreadyDamagedEntity>(true),
                DamageBufferLookup = SystemAPI.GetBufferLookup<DamageBufferElement>(true),
                ECB = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged)
            };
            SimulationSingleton simulationSingleton = SystemAPI.GetSingleton<SimulationSingleton>();
            state.Dependency = damageOnTriggerJob.Schedule(simulationSingleton, state.Dependency);
        }
    }
    
    public struct DamageOnTriggerJob : ITriggerEventsJob
    {
        [ReadOnly] public ComponentLookup<DamageOnTrigger> DamageOnTriggerLookup;
        [ReadOnly] public ComponentLookup<ElementTeamComponent> TeamLookup;
        [ReadOnly] public BufferLookup<AlreadyDamagedEntity> AlreadyDamagedLookup;
        [ReadOnly] public BufferLookup<DamageBufferElement> DamageBufferLookup;

        public EntityCommandBuffer ECB;
        
        public void Execute(TriggerEvent triggerEvent)
        {
            Entity damageDealingEntity;
            Entity damageReceivingEntity;
            
            if (DamageBufferLookup.HasBuffer(triggerEvent.EntityA) &&
                DamageOnTriggerLookup.HasComponent(triggerEvent.EntityB))
            {
                damageReceivingEntity = triggerEvent.EntityA;
                damageDealingEntity = triggerEvent.EntityB;
            }
            else if (DamageOnTriggerLookup.HasComponent(triggerEvent.EntityA) &&
                     DamageBufferLookup.HasBuffer(triggerEvent.EntityB))
            {
                damageDealingEntity = triggerEvent.EntityA;
                damageReceivingEntity = triggerEvent.EntityB;
            }
            else
            {
                return;
            }
            
            DynamicBuffer<AlreadyDamagedEntity> alreadyDamagedBuffer = AlreadyDamagedLookup[damageDealingEntity];
            foreach (AlreadyDamagedEntity alreadyDamagedEntity in alreadyDamagedBuffer)
            {
                if (alreadyDamagedEntity.Value.Equals(damageReceivingEntity)) return;
            }
            
            if (TeamLookup.TryGetComponent(damageDealingEntity, out ElementTeamComponent damageDealingTeam) &&
                TeamLookup.TryGetComponent(damageReceivingEntity, out ElementTeamComponent damageReceivingTeam))
            {
                if (damageDealingTeam.Team == damageReceivingTeam.Team) return;
            }
            
            DamageOnTrigger damageOnTrigger = DamageOnTriggerLookup[damageDealingEntity];
            ECB.AppendToBuffer(damageReceivingEntity, new DamageBufferElement { Value = damageOnTrigger.Value });
            ECB.AppendToBuffer(damageDealingEntity, new AlreadyDamagedEntity { Value = damageReceivingEntity });
        }
    }
}