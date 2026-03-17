using System;
using System.Collections.Generic;
using ElementCommons;
using GatherableResources;
using ScriptableObjects;
using Types;
using UI;
using Units;
using Units.Worker;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace Buildings
{
    [UpdateInGroup(typeof(GhostSimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class PlaceBuildingCommandServerSystem : SystemBase
    {
        private const float HALF_EXTENT = 0.5F;
        
        private const int WORKER_DELAY = 5;

        private BuildingsPrefabEntityFactory _prefabFactory;

        private EntityCommandBuffer _entityCommandBuffer;

        private Dictionary<ResourceType, Action<Entity, int>> _resourceDeductionActions;

        protected override void OnCreate()
        {
            _prefabFactory = new BuildingsPrefabEntityFactory();
            InitializeResourceDeductionActions();

            RequireForUpdate<PlayerTagComponent>();
            RequireForUpdate<BuildingPrefabComponent>();
            RequireForUpdate<NetworkTime>();
            RequireForUpdate<BuildingsConfigurationComponent>();

            base.OnCreate();
        }

        private void InitializeResourceDeductionActions()
        {
            _resourceDeductionActions = new Dictionary<ResourceType, Action<Entity, int>>
            {
                [ResourceType.Wood] = DeductWood,
                [ResourceType.Food] = DeductFood,
                [ResourceType.Population] = DeductPopulation
            };
        }

        protected override void OnUpdate()
        {
            InitializeFactory();
            _entityCommandBuffer = new EntityCommandBuffer(Allocator.Temp);

            NetworkTick serverTick = SystemAPI.GetSingleton<NetworkTime>().ServerTick;

            foreach ((DynamicBuffer<PlaceBuildingCommand> buildingCommands, RefRW<LastProcessedBuildingCommand> lastProcessedCommand,
                         PlayerTeamComponent playerTeam, GhostOwner ghostOwner, Entity playerEntity)
                     in SystemAPI.Query<DynamicBuffer<PlaceBuildingCommand>, RefRW<LastProcessedBuildingCommand>, PlayerTeamComponent, GhostOwner>()
                               .WithAll<PlayerTagComponent>().WithEntityAccess())
            {
                ProcessBuildingCommands(buildingCommands, serverTick, lastProcessedCommand, playerTeam.Team, ghostOwner.NetworkId, playerEntity);
            }

            ProcessDelayedWorkerCommands();

            _entityCommandBuffer.Playback(EntityManager);
            _entityCommandBuffer.Dispose();
        }

        private void ProcessDelayedWorkerCommands()
        {
            float stoppingDistance = 1.6f;

            foreach ((RefRW<DelayWorkerToBuildingCommandComponent> delay, Entity entity) in 
                     SystemAPI.Query<RefRW<DelayWorkerToBuildingCommandComponent>>().WithEntityAccess())
            {
                delay.ValueRW.FramesToWait--;

                if (delay.ValueRO.FramesToWait > 0)
                    continue;

                if (EntityManager.Exists(delay.ValueRO.WorkerEntity) && EntityManager.Exists(delay.ValueRO.BuildingEntity))
                {
                    int currentVersion = EntityManager.GetComponentData<SetServerStateTargetComponent>(delay.ValueRO.WorkerEntity).TargetVersion;
                    int inputVersion = EntityManager.GetComponentData<SetInputStateTargetComponent>(delay.ValueRO.WorkerEntity).TargetVersion;
                    int maxVersion = math.max(currentVersion, inputVersion);

                    SetServerStateTargetComponent serverTarget = new SetServerStateTargetComponent
                    {
                        TargetEntity = delay.ValueRO.BuildingEntity,
                        TargetPosition = delay.ValueRO.TargetPosition,
                        IsFollowingTarget = true,
                        StoppingDistance = stoppingDistance,
                        TargetVersion = maxVersion + 1
                    };

                    _entityCommandBuffer.SetComponent(delay.ValueRO.WorkerEntity, serverTarget);
                }

                _entityCommandBuffer.DestroyEntity(entity);
            }
        }

        private void ProcessBuildingCommands(DynamicBuffer<PlaceBuildingCommand> buildingCommands,
            NetworkTick serverTick,
            RefRW<LastProcessedBuildingCommand> lastProcessedCommand, TeamType playerTeam, int networkId,
            Entity playerEntity)
        {
            buildingCommands.GetDataAtTick(serverTick, out PlaceBuildingCommand command);

            if (!command.Tick.IsValid)
                return;

            if (IsDuplicateCommand(command, lastProcessedCommand.ValueRO))
                return;

            lastProcessedCommand.ValueRW = GetLastProcessedBuildingCommand(command);
            DeductBuildingCost(command.BuildingType, playerEntity);
            InstantiateBuilding(command, playerTeam, networkId);
        }

        private LastProcessedBuildingCommand GetLastProcessedBuildingCommand(PlaceBuildingCommand command)
        {
            return new LastProcessedBuildingCommand
            {
                Tick = command.Tick,
                Position = command.Position,
                BuildingType = command.BuildingType
            };
        }

        private bool IsDuplicateCommand(PlaceBuildingCommand newCommand, LastProcessedBuildingCommand lastCommand)
        {
            if (!lastCommand.Tick.IsValid)
                return false;

            bool samePosition = math.distancesq(newCommand.Position, lastCommand.Position) < 0.01f;
            bool sameType = newCommand.BuildingType == lastCommand.BuildingType;

            return samePosition && sameType;
        }

        private void InstantiateBuilding(PlaceBuildingCommand placeBuildingCommand, TeamType playerTeam, int networkId)
        {
            Entity buildingEntity = _prefabFactory.Get(placeBuildingCommand.BuildingType);
            Entity newBuilding = _entityCommandBuffer.Instantiate(buildingEntity);

            LocalTransform prefabTransform = EntityManager.GetComponentData<LocalTransform>(buildingEntity);
            LocalTransform newTransform = LocalTransform.FromPositionRotationScale(
                placeBuildingCommand.Position,
                prefabTransform.Rotation,
                prefabTransform.Scale);

            _entityCommandBuffer.SetComponent(newBuilding, newTransform);
            _entityCommandBuffer.SetComponent(newBuilding, new GhostOwner{NetworkId = networkId});
            _entityCommandBuffer.SetComponent(newBuilding, new ElementTeamComponent{Team = playerTeam});
            CommandSelectedWorkers(buildingEntity, newBuilding, placeBuildingCommand.Position, playerTeam);
        }

        private void CommandSelectedWorkers(Entity buildingPrefab, Entity newBuilding, float3 targetPosition, TeamType playerTeam)
        {
            float stoppingDistance = 1.0f;

            float3 buildingSize = new float3(1, 1, 1);
            if (EntityManager.HasComponent<BuildingObstacleSizeComponent>(buildingPrefab))
            {
                buildingSize = EntityManager.GetComponentData<BuildingObstacleSizeComponent>(buildingPrefab).Size;
            }

            foreach ((RefRO<ElementTeamComponent> team, UnitTypeComponent unitType, RefRO<SetInputStateTargetComponent> inputTarget, RefRO<LocalTransform> transform, Entity entity) in
                     SystemAPI.Query<RefRO<ElementTeamComponent>, UnitTypeComponent, RefRO<SetInputStateTargetComponent>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                if (team.ValueRO.Team != playerTeam || unitType.Type != UnitType.Worker)
                {
                    return;
                }

                CommandSelectedWorker(newBuilding, targetPosition, inputTarget, transform, buildingSize, entity);
            }
        }

        private void CommandSelectedWorker(Entity newBuilding, float3 targetPosition, RefRO<SetInputStateTargetComponent> inputTarget, RefRO<LocalTransform> transform, float3 buildingSize, Entity entity)
        {

            if (IsInputTargetAvailable(targetPosition, inputTarget))
            {
                return;
            }

            float3 clampedTarget = GetClampedTargetPosition(targetPosition, transform, buildingSize);
            Entity delayEntity = _entityCommandBuffer.CreateEntity();
            _entityCommandBuffer.AddComponent(delayEntity, GetWorkerBuildingCommandComponent(newBuilding, entity, clampedTarget));
        }

        private DelayWorkerToBuildingCommandComponent GetWorkerBuildingCommandComponent(Entity newBuilding, Entity entity, float3 clampedTarget)
        {
            return new DelayWorkerToBuildingCommandComponent
            {
                WorkerEntity = entity,
                BuildingEntity = newBuilding,
                TargetPosition = clampedTarget,
                FramesToWait = WORKER_DELAY
            };
        }

        private bool IsInputTargetAvailable(float3 targetPosition, RefRO<SetInputStateTargetComponent> inputTarget)
        {
            if (!inputTarget.ValueRO.IsFollowingTarget || inputTarget.ValueRO.TargetEntity != Entity.Null)
            {
                return true;
            }

            if (math.distancesq(inputTarget.ValueRO.TargetPosition, targetPosition) > 0.1f)
            {
                return true;
                
            }

            return false;
        }

        private float3 GetClampedTargetPosition(float3 targetPosition, RefRO<LocalTransform> transform, float3 buildingSize)
        {
            float3 workerPos = transform.ValueRO.Position;
            float3 halfExtents = buildingSize * HALF_EXTENT;
            float3 dir = GetDirection(targetPosition, workerPos);

            float hitPoint = GetHitPoint(dir, halfExtents);
            float3 clampedTarget = targetPosition + dir * (hitPoint + HALF_EXTENT);
            clampedTarget.y = workerPos.y;
            return clampedTarget;
        }

        private float GetHitPoint(float3 dir, float3 halfExtents)
        {
            float verticalHitPoint = dir.x != 0f ? math.abs(halfExtents.x / dir.x) : float.MaxValue;
            float horizontalHitPoint = dir.z != 0f ? math.abs(halfExtents.z / dir.z) : float.MaxValue;
            return math.min(verticalHitPoint, horizontalHitPoint);
        }

        private float3 GetDirection(float3 targetPosition, float3 workerPos)
        {
            float3 dir = new float3(workerPos.x - targetPosition.x, 0f, workerPos.z - targetPosition.z);

            if (math.lengthsq(dir) < 0.001f)
            {
                dir = new float3(1f, 0f, 0f);
            }

            return math.normalize(dir);
        }

        private void InitializeFactory()
        {
            if (_prefabFactory.IsInitialized)
            {
                return;
            }

            BuildingPrefabComponent prefabComponent = SystemAPI.GetSingleton<BuildingPrefabComponent>();
            _prefabFactory.Set(prefabComponent);
        }

        private void DeductBuildingCost(BuildingType buildingType, Entity playerEntity)
        {
            BuildingsConfigurationComponent config = SystemAPI.ManagedAPI.GetSingleton<BuildingsConfigurationComponent>();
            BuildingScriptableObject buildingConfig = config.Configuration.GetBuildingsDictionary()[buildingType];

            if (buildingConfig == null || buildingConfig.ConstructionCost == null)
            {
                return;
            }

            DeductCosts(playerEntity, buildingConfig);
        }

        private void DeductCosts(Entity playerEntity, BuildingScriptableObject buildingConfig)
        {
            foreach (var cost in buildingConfig.ConstructionCost)
            {
                SetDeductAction(playerEntity, cost);
            }

            _entityCommandBuffer.AddComponent<UpdateResourcesPanelTag>(playerEntity);
        }

        private void SetDeductAction(Entity playerEntity, ResourceCostEntity cost)
        {
            if (!_resourceDeductionActions.TryGetValue(cost.ResourceType, out var deductAction))
            {
                return;
            }

            deductAction(playerEntity, cost.Cost);
        }

        private void DeductWood(Entity playerEntity, int cost)
        {
            if (!EntityManager.HasComponent<CurrentWoodComponent>(playerEntity))
            {
                return;
            }

            CurrentWoodComponent wood = EntityManager.GetComponentData<CurrentWoodComponent>(playerEntity);
            wood.Value -= cost;
            _entityCommandBuffer.SetComponent(playerEntity, wood);
        }

        private void DeductFood(Entity playerEntity, int cost)
        {
            if (!EntityManager.HasComponent<CurrentFoodComponent>(playerEntity))
            {
                return;
            }

            CurrentFoodComponent food = EntityManager.GetComponentData<CurrentFoodComponent>(playerEntity);
            food.Value -= cost;
            _entityCommandBuffer.SetComponent(playerEntity, food);
        }

        private void DeductPopulation(Entity playerEntity, int cost)
        {
            if (!EntityManager.HasComponent<CurrentPopulationComponent>(playerEntity))
            {
                return;
            }

            CurrentPopulationComponent population = EntityManager.GetComponentData<CurrentPopulationComponent>(playerEntity);
            population.CurrentPopulation += cost;
            _entityCommandBuffer.SetComponent(playerEntity, population);
        }
    }
}