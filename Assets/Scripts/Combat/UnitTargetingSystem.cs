using ElementCommons;
using Units.MovementSystems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace Combat
{
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(PhysicsSimulationGroup))]
    [UpdateBefore(typeof(ExportPhysicsWorld))]
    public partial struct UnitTargetingSystem : ISystem
    {
        private CollisionFilter _UnitAttackFilter;
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
            _UnitAttackFilter = new CollisionFilter
            {
                BelongsTo = 1 << 6,
                CollidesWith = 1 << 1 | 1 << 2 | 1 << 4
            };
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new UnitTargetingJob
            {
                CollisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld,
                CollisionFilter = _UnitAttackFilter,
                MobaTeamLookup = SystemAPI.GetComponentLookup<ElementTeamComponent>(true)
            }.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct UnitTargetingJob : IJobEntity
    {
        [ReadOnly] public CollisionWorld CollisionWorld;
        [ReadOnly] public CollisionFilter CollisionFilter;
        [ReadOnly] public ComponentLookup<ElementTeamComponent> MobaTeamLookup;

        [BurstCompile]
        private void Execute(Entity UnitEntity, ref UnitTargetEntity targetEntity, in LocalTransform transform,
            in UnitTargetRadius targetRadius)
        {
            NativeList<DistanceHit> hits = new NativeList<DistanceHit>(Allocator.TempJob);

            if (CollisionWorld.OverlapSphere(transform.Position, targetRadius.Value, ref hits, CollisionFilter))
            {
                float closestDistance = float.MaxValue;
                Entity closestEntity  = Entity.Null;

                foreach (DistanceHit hit in hits)
                {
                    if (!MobaTeamLookup.TryGetComponent(hit.Entity, out ElementTeamComponent mobaTeam)) continue;
                    if(mobaTeam.Team == MobaTeamLookup[UnitEntity].Team) continue;
                    if (hit.Distance < closestDistance)
                    {
                        closestDistance = hit.Distance;
                        closestEntity = hit.Entity;
                    }
                }

                targetEntity.Value = closestEntity;
            }
            else
            {
                targetEntity.Value = Entity.Null;
            }

            hits.Dispose();
        }
    }
}