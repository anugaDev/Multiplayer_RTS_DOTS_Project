using System;
using System.Collections.Generic;
using ElementCommons;
using GatherableResources;
using ScriptableObjects;
using Types;
using UI;
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
                    int currentVersion = EntityManager.GetComponentData<Units.Worker.SetServerStateTargetComponent>(delay.ValueRO.WorkerEntity).TargetVersion;
                    int inputVersion = EntityManager.GetComponentData<Units.Worker.SetInputStateTargetComponent>(delay.ValueRO.WorkerEntity).TargetVersion;
                    int maxVersion = math.max(currentVersion, inputVersion);

                    Units.Worker.SetServerStateTargetComponent serverTarget = new Units.Worker.SetServerStateTargetComponent
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

            lastProcessedCommand.ValueRW = new LastProcessedBuildingCommand
            {
                Tick = command.Tick,
                Position = command.Position,
                BuildingType = command.BuildingType
            };

            DeductBuildingCost(command.BuildingType, playerEntity);
            InstantiateBuilding(command, playerTeam, networkId);
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
            float stoppingDistance = 1.0f; // Since TargetPosition is already the edge, we only need a marginal stop distance.

            float3 buildingSize = new float3(1, 1, 1);
            if (EntityManager.HasComponent<BuildingObstacleSizeComponent>(buildingPrefab))
            {
                buildingSize = EntityManager.GetComponentData<BuildingObstacleSizeComponent>(buildingPrefab).Size;
            }

            foreach ((RefRO<ElementTeamComponent> team, Units.UnitTypeComponent unitType, RefRO<Units.Worker.SetInputStateTargetComponent> inputTarget, RefRO<LocalTransform> transform, Entity entity) in
                     SystemAPI.Query<RefRO<ElementTeamComponent>, Units.UnitTypeComponent, RefRO<Units.Worker.SetInputStateTargetComponent>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                if (team.ValueRO.Team != playerTeam)
                    continue;

                if (unitType.Type != Types.UnitType.Worker)
                    continue;

                if (!inputTarget.ValueRO.IsFollowingTarget || inputTarget.ValueRO.TargetEntity != Entity.Null)
                    continue;
                if (math.distancesq(inputTarget.ValueRO.TargetPosition, targetPosition) > 0.1f)
                    continue;

                int currentVersion = EntityManager.GetComponentData<Units.Worker.SetServerStateTargetComponent>(entity).TargetVersion;

                float3 workerPos = transform.ValueRO.Position;
                float3 halfExtents = buildingSize * 0.5f;
                float3 dir = new float3(workerPos.x - targetPosition.x, 0f, workerPos.z - targetPosition.z);

                if (math.lengthsq(dir) < 0.001f)
                    dir = new float3(1f, 0f, 0f);

                dir = math.normalize(dir);
                float tx = dir.x != 0f ? math.abs(halfExtents.x / dir.x) : float.MaxValue;
                float tz = dir.z != 0f ? math.abs(halfExtents.z / dir.z) : float.MaxValue;
                float t = math.min(tx, tz);

                float3 clampedTarget = targetPosition + dir * (t + 0.5f);
                clampedTarget.y = workerPos.y;

                Entity delayEntity = _entityCommandBuffer.CreateEntity();
                _entityCommandBuffer.AddComponent(delayEntity, new DelayWorkerToBuildingCommandComponent
                {
                    WorkerEntity = entity,
                    BuildingEntity = newBuilding,
                    TargetPosition = clampedTarget,
                    PlayerTeam = playerTeam,
                    FramesToWait = 5
                });
            }
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