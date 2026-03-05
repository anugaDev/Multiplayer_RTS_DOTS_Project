using ElementCommons;
using Buildings;
using Combat;
using GatherableResources;
using Types;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using Units.MovementSystems;
using Units.Worker;
using Unity.Mathematics;

namespace Units
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(UnitStateSystem))]
    [UpdateBefore(typeof(Worker.WorkerGatheringSystem))]
    public partial class WorkerActionSystem : SystemBase
    {
        private ComponentLookup<ResourceTypeComponent> _resourceTypeLookup;

        private ComponentLookup<BuildingConstructionProgressComponent> _constructionProgressLookup;

        private ComponentLookup<CurrentHitPointsComponent> _hpLookup;

        private ComponentLookup<ElementTeamComponent> _teamLookup;

        protected override void OnCreate()
        {
            _resourceTypeLookup = GetComponentLookup<ResourceTypeComponent>(true);
            _constructionProgressLookup = GetComponentLookup<BuildingConstructionProgressComponent>(true);
            _hpLookup   = GetComponentLookup<CurrentHitPointsComponent>(true);
            _teamLookup = GetComponentLookup<ElementTeamComponent>(true);
            RequireForUpdate<UnitTagComponent>();
        }

        protected override void OnUpdate()
        {

            _resourceTypeLookup.Update(this);
            _constructionProgressLookup.Update(this);
            _hpLookup.Update(this);
            _teamLookup.Update(this);

            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach ((RefRO<UnitTypeComponent>            unitType,
                      RefRO<SetInputStateTargetComponent> inputTarget,
                      RefRW<SetServerStateTargetComponent> serverTarget,
                      Entity                              entity)
                     in SystemAPI.Query<RefRO<UnitTypeComponent>,
                                       RefRO<SetInputStateTargetComponent>,
                                       RefRW<SetServerStateTargetComponent>>()
                         .WithAll<UnitTagComponent, Simulate>()
                         .WithEntityAccess())
            {
                if (unitType.ValueRO.Type != UnitType.Worker)
                    continue;

                if (inputTarget.ValueRO.TargetVersion <= serverTarget.ValueRO.TargetVersion)
                    continue;

                ResetActionComponents(entity, ecb);

                serverTarget.ValueRW.TargetVersion = inputTarget.ValueRO.TargetVersion;
            }


            foreach ((RefRO<UnitTypeComponent>            unitType,
                      RefRO<UnitStateComponent>           unitState,
                      RefRW<SetInputStateTargetComponent> inputTarget,
                      Entity                              entity)
                     in SystemAPI.Query<RefRO<UnitTypeComponent>,
                                       RefRO<UnitStateComponent>,
                                       RefRW<SetInputStateTargetComponent>>()
                         .WithAll<UnitTagComponent, Simulate>()
                         .WithNone<WorkerGatheringTagComponent,
                                   WorkerStoringTagComponent,
                                   WorkerConstructionTagComponent>()
                         .WithEntityAccess())
            {
                if (unitType.ValueRO.Type != UnitType.Worker)
                    continue;

                if (unitState.ValueRO.State != UnitState.Idle)
                    continue;

                Entity targetEntity = inputTarget.ValueRO.TargetEntity;

                if (inputTarget.ValueRO.IsFollowingTarget && (targetEntity == Entity.Null || !EntityManager.Exists(targetEntity)))
                {
                    float3 targetPos = inputTarget.ValueRO.TargetPosition;
                    float closestDistSq = float.MaxValue;
                    Entity closestTarget = Entity.Null;

                    // Search for closest resource
                    foreach ((RefRO<Unity.Transforms.LocalTransform> resTransform, RefRO<ResourceTypeComponent> resType, Entity resEntity) 
                        in SystemAPI.Query<RefRO<Unity.Transforms.LocalTransform>, RefRO<ResourceTypeComponent>>().WithEntityAccess())
                    {
                        float distSq = Unity.Mathematics.math.distancesq(targetPos, resTransform.ValueRO.Position);
                        if (distSq < closestDistSq)
                        {
                            closestDistSq = distSq;
                            closestTarget = resEntity;
                        }
                    }

                    // Search for closest building under construction
                    foreach ((RefRO<Unity.Transforms.LocalTransform> bTransform, RefRO<BuildingConstructionProgressComponent> bProgress, Entity bEntity) 
                        in SystemAPI.Query<RefRO<Unity.Transforms.LocalTransform>, RefRO<BuildingConstructionProgressComponent>>().WithEntityAccess())
                    {
                        if (bProgress.ValueRO.Value >= bProgress.ValueRO.ConstructionTime)
                            continue; // Skip finished buildings

                        float distSq = Unity.Mathematics.math.distancesq(targetPos, bTransform.ValueRO.Position);
                        if (distSq < closestDistSq)
                        {
                            closestDistSq = distSq;
                            closestTarget = bEntity;
                        }
                    }

                    if (closestTarget != Entity.Null && closestDistSq <= 36.0f)
                    {
                        targetEntity = closestTarget;
                    }
                }

                if (targetEntity == Entity.Null || !EntityManager.Exists(targetEntity))
                {
                    inputTarget.ValueRW.IsFollowingTarget = false;
                    continue;
                }

                if (_constructionProgressLookup.TryGetComponent(targetEntity, out BuildingConstructionProgressComponent progress) 
                    && progress.Value < progress.ConstructionTime)
                {
                    ecb.AddComponent(entity, new WorkerConstructionTagComponent
                    {
                        BuildingEntity = targetEntity
                    });

                    if (EntityManager.HasComponent<BuildingObstacleSizeComponent>(targetEntity))
                    {
                        inputTarget.ValueRW.StoppingDistance = 1.6f;
                    }

                    inputTarget.ValueRW.TargetEntity = Entity.Null;
                    inputTarget.ValueRW.IsFollowingTarget = false;
                    continue;
                }

                if (!_resourceTypeLookup.HasComponent(targetEntity))
                {
                    if (_hpLookup.HasComponent(targetEntity) &&
                        _teamLookup.TryGetComponent(targetEntity, out ElementTeamComponent targetTeam) &&
                        _teamLookup.TryGetComponent(entity, out ElementTeamComponent unitTeam) &&
                        targetTeam.Team != unitTeam.Team)
                    {
                        continue;
                    }

                    inputTarget.ValueRW.TargetEntity = Entity.Null;
                    inputTarget.ValueRW.IsFollowingTarget = false;
                    continue;
                }


                ecb.AddComponent(entity, new WorkerGatheringTagComponent
                {
                    ResourceEntity = targetEntity
                });

                inputTarget.ValueRW.TargetEntity = Entity.Null;
                inputTarget.ValueRW.IsFollowingTarget = false;
            }

            foreach ((RefRO<UnitTypeComponent>                       unitType,
                      RefRO<UnitStateComponent>                      unitState,
                      RefRO<LocalTransform>                          workerTransform,
                      RefRO<CurrentWorkerResourceQuantityComponent>   workerResource,
                      RefRO<ElementTeamComponent>                    workerTeam,
                      Entity                                         entity)
                     in SystemAPI.Query<RefRO<UnitTypeComponent>,
                                       RefRO<UnitStateComponent>,
                                       RefRO<LocalTransform>,
                                       RefRO<CurrentWorkerResourceQuantityComponent>,
                                       RefRO<ElementTeamComponent>>()
                         .WithAll<UnitTagComponent, Simulate>()
                         .WithNone<WorkerGatheringTagComponent,
                                   WorkerStoringTagComponent,
                                   WorkerConstructionTagComponent>()
                         .WithEntityAccess())
            {
                if (unitType.ValueRO.Type != UnitType.Worker)
                    continue;

                if (unitState.ValueRO.State != UnitState.Idle)
                    continue;

                if (workerResource.ValueRO.Value <= 0)
                    continue;

                Entity closestTownCenter = Entity.Null;
                float closestDistSq = float.MaxValue;

                foreach ((RefRO<BuildingTypeComponent> buildingType,
                          RefRO<ElementTeamComponent>  buildingTeam,
                          RefRO<LocalTransform>        buildingTransform,
                          Entity                       buildingEntity)
                         in SystemAPI.Query<RefRO<BuildingTypeComponent>,
                                            RefRO<ElementTeamComponent>,
                                            RefRO<LocalTransform>>()
                             .WithAll<BuildingComponents>()
                             .WithEntityAccess())
                {
                    if (buildingType.ValueRO.Type != BuildingType.Center ||
                        buildingTeam.ValueRO.Team != workerTeam.ValueRO.Team)
                        continue;

                    if (_constructionProgressLookup.TryGetComponent(buildingEntity, out BuildingConstructionProgressComponent progress)
                        && (progress.ConstructionTime <= 0 || progress.Value < progress.ConstructionTime))
                        continue;

                    float distSq = math.distancesq(workerTransform.ValueRO.Position, buildingTransform.ValueRO.Position);
                    if (distSq < closestDistSq)
                    {
                        closestDistSq = distSq;
                        closestTownCenter = buildingEntity;
                    }
                }

                if (closestTownCenter != Entity.Null && closestDistSq <= 8.0f * 8.0f)
                {
                    ecb.AddComponent(entity, new WorkerStoringTagComponent
                    {
                        BuildingEntity = closestTownCenter
                    });
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private void ResetActionComponents(Entity entity, EntityCommandBuffer ecb)
        {
            if (SystemAPI.HasComponent<WorkerGatheringTagComponent>(entity))
                ecb.RemoveComponent<WorkerGatheringTagComponent>(entity);
            if (SystemAPI.HasComponent<WorkerStoringTagComponent>(entity))
                ecb.RemoveComponent<WorkerStoringTagComponent>(entity);
            if (SystemAPI.HasComponent<WorkerConstructionTagComponent>(entity))
                ecb.RemoveComponent<WorkerConstructionTagComponent>(entity);
        }
    }
}
