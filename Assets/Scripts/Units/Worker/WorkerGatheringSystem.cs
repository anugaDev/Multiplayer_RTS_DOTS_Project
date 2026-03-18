using Buildings;
using ElementCommons;
using GatherableResources;
using Types;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace Units.Worker
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(MovementSystems.UnitStateSystem))]
    public partial class WorkerGatheringSystem : SystemBase
    {
        private const int MAX_GATHERING_AMOUNT = 50;
        private const float GATHERING_DISTANCE_THRESHOLD = 4.0f;
        private const int AMOUNT_TO_GATHER = 1;
        private const float GATHER_INTERVAL_SECONDS = 0.25f;

        private float _gatherTimer;

        private ComponentLookup<CurrentResourceQuantityComponent> _resourceQuantityLookup;
        private ComponentLookup<ResourceTypeComponent> _resourceTypeLookup;
        private ComponentLookup<ElementTeamComponent> _teamLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<BuildingConstructionProgressComponent> _constructionProgressLookup;

        protected override void OnCreate()
        {
            _resourceTypeLookup = GetComponentLookup<ResourceTypeComponent>(true);
            _resourceQuantityLookup = GetComponentLookup<CurrentResourceQuantityComponent>();
            _teamLookup = GetComponentLookup<ElementTeamComponent>(true);
            _transformLookup = GetComponentLookup<LocalTransform>(true);
            _constructionProgressLookup = GetComponentLookup<BuildingConstructionProgressComponent>(true);
            RequireForUpdate<UnitTagComponent>();
        }

        protected override void OnUpdate()
        {

            _resourceTypeLookup.Update(this);
            _resourceQuantityLookup.Update(this);
            _teamLookup.Update(this);
            _transformLookup.Update(this);
            _constructionProgressLookup.Update(this);

            _gatherTimer += SystemAPI.Time.DeltaTime;
            bool canGather = _gatherTimer >= GATHER_INTERVAL_SECONDS;
            if (canGather)
                _gatherTimer -= GATHER_INTERVAL_SECONDS;

            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach ((RefRW<LocalTransform>                      workerTransform,
                      RefRO<WorkerGatheringTagComponent>         gatheringTag,
                      RefRO<UnitStateComponent>                  unitState,
                      RefRW<CurrentWorkerResourceQuantityComponent> workerResource,
                      RefRO<ElementTeamComponent>                workerTeam,
                      Entity                                     workerEntity)
                     in SystemAPI.Query<RefRW<LocalTransform>,
                                        RefRO<WorkerGatheringTagComponent>,
                                        RefRO<UnitStateComponent>,
                                        RefRW<CurrentWorkerResourceQuantityComponent>,
                                        RefRO<ElementTeamComponent>>()
                         .WithAll<Simulate, UnitTagComponent>()
                         .WithEntityAccess())
            { 
                if (unitState.ValueRO.State != UnitState.Acting)
                    continue;

                ProcessGathering(ref workerTransform.ValueRW, gatheringTag.ValueRO,
                               ref workerResource.ValueRW, workerTeam.ValueRO.Team,
                               workerEntity, ref ecb, canGather);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private void ProcessGathering(ref LocalTransform workerTransform, WorkerGatheringTagComponent gatheringTag,
                                     ref CurrentWorkerResourceQuantityComponent workerResource, TeamType workerTeam,
                                     Entity workerEntity, ref EntityCommandBuffer ecb, bool canGather)
        {
            Entity resourceEntity = gatheringTag.ResourceEntity;

            if (!EntityManager.Exists(resourceEntity) ||
                !_resourceQuantityLookup.TryGetComponent(resourceEntity, out CurrentResourceQuantityComponent resourceQuantity) ||
                resourceQuantity.Value <= 0)
            {
                ecb.RemoveComponent<WorkerGatheringTagComponent>(workerEntity);

                if (workerResource.Value > 0)
                {
                    workerResource.PreviousResourceEntity = resourceEntity;
                    Entity depletedTC = FindClosestTownCenter(workerTransform.Position, workerTeam);
                    if (depletedTC != Entity.Null)
                    {
                        SetNextTarget(workerEntity, depletedTC, ecb);
                        ecb.AddComponent(workerEntity, new WorkerStoringTagComponent { BuildingEntity = depletedTC });
                    }
                }
                return;
            }

            if (!_transformLookup.TryGetComponent(resourceEntity, out LocalTransform resourceTransform))
                return;

            float distanceSq = math.distancesq(workerTransform.Position, resourceTransform.Position);
            if (distanceSq > GATHERING_DISTANCE_THRESHOLD * GATHERING_DISTANCE_THRESHOLD)
                return;

            float3 direction = math.normalizesafe(resourceTransform.Position - workerTransform.Position);
            if (!math.all(direction == float3.zero))
            {
                direction.y = 0;
                workerTransform.Rotation = quaternion.LookRotationSafe(direction, math.up());
            }

            if (workerResource.ResourceType == ResourceType.None &&
                _resourceTypeLookup.TryGetComponent(resourceEntity, out ResourceTypeComponent resourceType))
            {
                workerResource.ResourceType = resourceType.Type;
            }

            if (!canGather)
                return;

            int amountToGather = math.min(AMOUNT_TO_GATHER, resourceQuantity.Value);
            amountToGather     = math.min(amountToGather, MAX_GATHERING_AMOUNT - workerResource.Value);

            if (amountToGather > 0)
            {
                workerResource.Value   += amountToGather;
                resourceQuantity.Value -= amountToGather;
                ecb.SetComponent(resourceEntity, resourceQuantity);

                if (resourceQuantity.Value <= 0)
                {
                    ecb.DestroyEntity(resourceEntity);

                    workerResource.PreviousResourceEntity = Entity.Null;
                    ecb.RemoveComponent<WorkerGatheringTagComponent>(workerEntity);
                    Entity depletedTC2 = FindClosestTownCenter(workerTransform.Position, workerTeam);
                    if (depletedTC2 != Entity.Null)
                    {
                        SetNextTarget(workerEntity, depletedTC2, ecb);
                        ecb.AddComponent(workerEntity, new WorkerStoringTagComponent { BuildingEntity = depletedTC2 });
                    }
                    return;
                }
            }

            if (workerResource.Value < MAX_GATHERING_AMOUNT)
                return;

            workerResource.PreviousResourceEntity = resourceEntity;
            Entity townCenter = FindClosestTownCenter(workerTransform.Position, workerTeam);

            if (townCenter != Entity.Null)
            {
                SetNextTarget(workerEntity, townCenter, ecb);
                ecb.RemoveComponent<WorkerGatheringTagComponent>(workerEntity);
                ecb.AddComponent(workerEntity, new WorkerStoringTagComponent { BuildingEntity = townCenter });
            }
            else
            {
                ecb.RemoveComponent<WorkerGatheringTagComponent>(workerEntity);
            }
        }

        private Entity FindClosestTownCenter(float3 workerPosition, TeamType workerTeam)
        {
            Entity closestTownCenter = Entity.Null;
            float  closestDistanceSq = float.MaxValue;

            foreach ((RefRO<BuildingTypeComponent> buildingType,
                      RefRO<ElementTeamComponent> buildingTeam,
                      RefRO<LocalTransform> buildingTransform,
                      Entity buildingEntity)
                     in SystemAPI.Query<RefRO<BuildingTypeComponent>,
                                        RefRO<ElementTeamComponent>,
                                        RefRO<LocalTransform>>()
                         .WithAll<BuildingComponents>()
                         .WithEntityAccess())
            {
                if (buildingType.ValueRO.Type != BuildingType.Center ||
                    buildingTeam.ValueRO.Team != workerTeam)
                    continue;

                if (_constructionProgressLookup.TryGetComponent(buildingEntity, out BuildingConstructionProgressComponent progress)
                    && (progress.ConstructionTime <= 0 || progress.Value < progress.ConstructionTime))
                    continue;

                float distanceSq = math.distancesq(workerPosition, buildingTransform.ValueRO.Position);
                if (distanceSq < closestDistanceSq)
                {
                    closestDistanceSq = distanceSq;
                    closestTownCenter = buildingEntity;
                }
            }

            return closestTownCenter;
        }

        private void SetNextTarget(Entity workerEntity, Entity targetEntity, EntityCommandBuffer ecb)
        {
            if (!_transformLookup.TryGetComponent(targetEntity, out LocalTransform targetTransform))
                return;

            int currentVersion = EntityManager.GetComponentData<SetServerStateTargetComponent>(workerEntity).TargetVersion;

            ecb.SetComponent(workerEntity, new SetServerStateTargetComponent
            {
                TargetEntity = targetEntity,
                TargetPosition = targetTransform.Position,
                IsFollowingTarget = true,
                StoppingDistance = 2.0f,
                TargetVersion = currentVersion + 1
            });
        }
    }
}
