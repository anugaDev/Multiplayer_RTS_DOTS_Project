using Buildings;
using ElementCommons;
using GatherableResources;
using Types;
using UI;
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
    public partial class WorkerStoringSystem : SystemBase
    {
        private const float STORING_DISTANCE_THRESHOLD = 8.0f;

        private ComponentLookup<CurrentWoodComponent>  _woodLookup;
        private ComponentLookup<CurrentFoodComponent>  _foodLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<GhostOwner> _ghostOwnerLookup;

        protected override void OnCreate()
        {
            _woodLookup = GetComponentLookup<CurrentWoodComponent>();
            _foodLookup = GetComponentLookup<CurrentFoodComponent>();
            _ghostOwnerLookup = GetComponentLookup<GhostOwner>(true);
            _transformLookup = GetComponentLookup<LocalTransform>(true);
            RequireForUpdate<UnitTagComponent>();
        }

        protected override void OnUpdate()
        {

            _woodLookup.Update(this);
            _foodLookup.Update(this);
            _ghostOwnerLookup.Update(this);
            _transformLookup.Update(this);

            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach ((RefRW<LocalTransform>                      workerTransform,
                      RefRO<WorkerStoringTagComponent>           storingTag,
                      RefRO<UnitStateComponent>                  unitState,
                      RefRW<CurrentWorkerResourceQuantityComponent> workerResource,
                      RefRO<GhostOwner>                          workerOwner,
                      Entity                                     workerEntity)
                     in SystemAPI.Query<RefRW<LocalTransform>,
                                        RefRO<WorkerStoringTagComponent>,
                                        RefRO<UnitStateComponent>,
                                        RefRW<CurrentWorkerResourceQuantityComponent>,
                                        RefRO<GhostOwner>>()
                         .WithAll<Simulate, UnitTagComponent>()
                         .WithEntityAccess())
            {
                if (unitState.ValueRO.State != UnitState.Acting)
                    continue;

                Entity buildingEntity = storingTag.ValueRO.BuildingEntity;
                if (buildingEntity == Entity.Null || !EntityManager.Exists(buildingEntity))
                {
                    ecb.RemoveComponent<WorkerStoringTagComponent>(workerEntity);
                    continue;
                }

                if (!_transformLookup.TryGetComponent(buildingEntity, out LocalTransform buildingTransform))
                    continue;

                float distanceSq = math.distancesq(workerTransform.ValueRO.Position, buildingTransform.Position);
                if (distanceSq > STORING_DISTANCE_THRESHOLD * STORING_DISTANCE_THRESHOLD)
                    continue;

                float3 direction = math.normalizesafe(buildingTransform.Position - workerTransform.ValueRO.Position);
                if (!math.all(direction == float3.zero))
                {
                    direction.y = 0;
                    workerTransform.ValueRW.Rotation = quaternion.LookRotationSafe(direction, math.up());
                }

                ProcessStoring(workerTransform.ValueRO, storingTag.ValueRO, ref workerResource.ValueRW,
                             workerOwner.ValueRO, workerEntity, ref ecb);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private void ProcessStoring(LocalTransform workerTransform, WorkerStoringTagComponent storingTag,
                                   ref CurrentWorkerResourceQuantityComponent workerResource,
                                   GhostOwner workerOwner, Entity workerEntity, ref EntityCommandBuffer ecb)
        {
            if (workerResource.Value <= 0)
            {
                ecb.RemoveComponent<WorkerStoringTagComponent>(workerEntity);
                return;
            }

            Entity playerEntity = FindPlayerEntity(workerOwner.NetworkId);
            if (playerEntity == Entity.Null)
                return;

            StoreResourceToPlayer(playerEntity, workerResource.ResourceType, workerResource.Value, ecb);

            ResourceType gatheredType = workerResource.ResourceType;
            workerResource.Value        = 0;
            workerResource.ResourceType = ResourceType.None;
            ecb.RemoveComponent<WorkerStoringTagComponent>(workerEntity);

            Entity previousResource = workerResource.PreviousResourceEntity;
            if (previousResource != Entity.Null && EntityManager.Exists(previousResource))
            {
                SetNextTarget(workerEntity, previousResource, ecb);
                ecb.AddComponent(workerEntity, new WorkerGatheringTagComponent { ResourceEntity = previousResource });
                workerResource.PreviousResourceEntity = Entity.Null;
            }
            else
            {
                workerResource.PreviousResourceEntity = Entity.Null;

                Entity nearest = FindNearestResourceOfType(workerTransform.Position, gatheredType);
                if (nearest != Entity.Null)
                {
                    SetNextTarget(workerEntity, nearest, ecb);
                    ecb.AddComponent(workerEntity, new WorkerGatheringTagComponent { ResourceEntity = nearest });
                }
            }
        }

        private void SetNextTarget(Entity workerEntity, Entity targetEntity, EntityCommandBuffer ecb)
        {
            if (!_transformLookup.TryGetComponent(targetEntity, out LocalTransform targetTransform))
                return;

            int currentVersion = EntityManager.GetComponentData<SetServerStateTargetComponent>(workerEntity).TargetVersion;

            ecb.SetComponent(workerEntity, new SetServerStateTargetComponent
            {
                TargetEntity      = targetEntity,
                TargetPosition    = targetTransform.Position,
                IsFollowingTarget = true,
                StoppingDistance  = 2.0f,
                TargetVersion     = currentVersion + 1
            });
        }

        private Entity FindPlayerEntity(int networkId)
        {
            foreach ((RefRO<GhostOwner> ghostOwner, Entity playerEntity)
                     in SystemAPI.Query<RefRO<GhostOwner>>().WithAll<PlayerTagComponent>().WithEntityAccess())
            {
                if (ghostOwner.ValueRO.NetworkId == networkId)
                    return playerEntity;
            }
            return Entity.Null;
        }

        private void StoreResourceToPlayer(Entity playerEntity, ResourceType resourceType, int amount,
                                           EntityCommandBuffer ecb)
        {
            switch (resourceType)
            {
                case ResourceType.Wood:
                    if (_woodLookup.TryGetComponent(playerEntity, out CurrentWoodComponent wood))
                    {
                        wood.Value += amount;
                        ecb.SetComponent(playerEntity, wood);
                    }
                    break;

                case ResourceType.Food:
                    if (_foodLookup.TryGetComponent(playerEntity, out CurrentFoodComponent food))
                    {
                        food.Value += amount;
                        ecb.SetComponent(playerEntity, food);
                    }
                    break;
            }

            ecb.AddComponent<UpdateResourcesPanelTag>(playerEntity);
        }

        private Entity FindNearestResourceOfType(float3 position, ResourceType type)
        {
            Entity nearest = Entity.Null;
            float closestDistSq = float.MaxValue;

            foreach ((RefRO<LocalTransform> resTransform,
                      RefRO<ResourceTypeComponent> resType,
                      RefRO<CurrentResourceQuantityComponent> resQty,
                      Entity resEntity)
                     in SystemAPI.Query<RefRO<LocalTransform>,
                                       RefRO<ResourceTypeComponent>,
                                       RefRO<CurrentResourceQuantityComponent>>()
                         .WithEntityAccess())
            {
                if (resType.ValueRO.Type != type || resQty.ValueRO.Value <= 0)
                    continue;

                float distSq = math.distancesq(position, resTransform.ValueRO.Position);
                if (distSq < closestDistSq)
                {
                    closestDistSq = distSq;
                    nearest = resEntity;
                }
            }

            return nearest;
        }
    }
}
