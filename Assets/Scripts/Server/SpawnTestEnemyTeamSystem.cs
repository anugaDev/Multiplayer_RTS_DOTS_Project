using Buildings;
using ElementCommons;
using Types;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Units;

namespace Server
{
    [UpdateInGroup(typeof(GhostSimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct SpawnTestEnemyTeamSystem : ISystem
    {
        private const int TEST_NETWORK_ID = -1;

        private BuildingsPrefabEntityFactory _buildingFactory;

        private EntityCommandBuffer _entityCommandBuffer;

        private UnitsPrefabEntityFactory _unitFactory;

        public void OnCreate(ref SystemState state)
        {
            _buildingFactory = new BuildingsPrefabEntityFactory();
            _unitFactory = new UnitsPrefabEntityFactory();
            state.RequireForUpdate<BuildingPrefabComponent>();
            state.RequireForUpdate<SpawnTestEnemyTeamTag>();
            state.RequireForUpdate<UnitPrefabComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            _entityCommandBuffer = new EntityCommandBuffer(Allocator.Temp);

            InitializeFactories(ref state);

            foreach ((SpawnTestEnemyTeamTag spawnTag, Entity entity) in
                     SystemAPI.Query<SpawnTestEnemyTeamTag>().WithEntityAccess())
            {
                TeamType enemyTeam = GetOppositeTeam(spawnTag.PlayerTeam);
                float3 basePosition = GetEnemyBasePosition(enemyTeam);

                SpawnEnemyBase(basePosition, enemyTeam, ref state);
                SpawnEnemyUnits(basePosition, enemyTeam, ref state);

                _entityCommandBuffer.RemoveComponent<SpawnTestEnemyTeamTag>(entity);
            }
            
            state.Enabled = false;
            _entityCommandBuffer.Playback(state.EntityManager);
            _entityCommandBuffer.Dispose();
        }

        private void InitializeFactories(ref SystemState state)
        {
            if (!_buildingFactory.IsInitialized)
            {
                BuildingPrefabComponent buildingPrefabs = SystemAPI.GetSingleton<BuildingPrefabComponent>();
                _buildingFactory.Set(buildingPrefabs);
            }

            if (!_unitFactory.IsInitialized)
            {
                UnitPrefabComponent unitPrefabs = SystemAPI.GetSingleton<UnitPrefabComponent>();
                _unitFactory.Set(unitPrefabs);
            }
        }

        private TeamType GetOppositeTeam(TeamType playerTeam)
        {
            return playerTeam == TeamType.Red ? TeamType.Blue : TeamType.Red;
        }

        private float3 GetEnemyBasePosition(TeamType enemyTeam)
        {
            if (enemyTeam == TeamType.Red)
            {
                return new float3(50f, GlobalParameters.DEFAULT_SCENE_HEIGHT, 50f);
            }
            else
            {
                return new float3(-50f, GlobalParameters.DEFAULT_SCENE_HEIGHT, -50f);
            }
        }

        private void SpawnEnemyBase(float3 basePosition, TeamType team, ref SystemState state)
        {
            float direction = team == TeamType.Red ? 1f : -1f;
            SpawnBuilding(BuildingType.Center, basePosition, team, ref state);

            float3 barracksPos = basePosition + new float3(-8f * direction, 0f, 0f);
            SpawnBuilding(BuildingType.Barracks, barracksPos, team, ref state);

            float3 house1Pos = basePosition + new float3(-10f * direction, 0f, 12f * direction);
            SpawnBuilding(BuildingType.House, house1Pos, team, ref state);

            float3 house2Pos = basePosition + new float3(-10f * direction, 0f, -12f * direction);
            SpawnBuilding(BuildingType.House, house2Pos, team, ref state);

            float3 farm1Pos = basePosition + new float3(-6f * direction, 0f, -10f * direction);
            SpawnBuilding(BuildingType.Farm, farm1Pos, team, ref state);

            float3 farm2Pos = basePosition + new float3(-6f * direction, 0f, 10f * direction);
            SpawnBuilding(BuildingType.Farm, farm2Pos, team, ref state);
        }

        private void SpawnEnemyUnits(float3 basePosition, TeamType team, ref SystemState state)
        {
            float direction = team == TeamType.Red ? 1f : -1f;

            for (int i = 0; i < 3; i++)
            {
                float3 warriorPos = basePosition + new float3(-20f * direction, 0f, (-3f + i * 3f) * direction);
                SpawnUnit(UnitType.Warrior, warriorPos, team, ref state);
            }

            for (int i = 0; i < 2; i++)
            {
                float3 workerPos = basePosition + new float3(-15f * direction, 0f, (-2f + i * 4f) * direction);
                SpawnUnit(UnitType.Worker, workerPos, team, ref state);
            }
        }

        private void SpawnBuilding(BuildingType buildingType, float3 position, TeamType team, ref SystemState state)
        {
            Entity buildingPrefab = _buildingFactory.Get(buildingType);
            Entity newBuilding = _entityCommandBuffer.Instantiate(buildingPrefab);

            LocalTransform prefabTransform = state.EntityManager.GetComponentData<LocalTransform>(buildingPrefab);
            LocalTransform newTransform = LocalTransform.FromPositionRotationScale(
                position,
                prefabTransform.Rotation,
                prefabTransform.Scale);

            _entityCommandBuffer.SetComponent(newBuilding, newTransform);
            _entityCommandBuffer.SetComponent(newBuilding, new GhostOwner { NetworkId = TEST_NETWORK_ID });
            _entityCommandBuffer.SetComponent(newBuilding, new ElementTeamComponent { Team = team });
            SetFullConstructionProgressComponent(buildingPrefab, newBuilding, ref state);
        }



        private void SpawnUnit(UnitType unitType, float3 position, TeamType team, ref SystemState state)
        {
            Entity unitPrefab = _unitFactory.Get(unitType);
            Entity newUnit = _entityCommandBuffer.Instantiate(unitPrefab);

            LocalTransform prefabTransform = state.EntityManager.GetComponentData<LocalTransform>(unitPrefab);
            LocalTransform newTransform = LocalTransform.FromPositionRotationScale(
                position,
                prefabTransform.Rotation,
                prefabTransform.Scale);

            _entityCommandBuffer.SetComponent(newUnit, newTransform);
            _entityCommandBuffer.SetComponent(newUnit, new GhostOwner { NetworkId = TEST_NETWORK_ID });
            _entityCommandBuffer.SetComponent(newUnit, new ElementTeamComponent { Team = team });
        }

        private void SetFullConstructionProgressComponent(Entity prefabEntity, Entity newBuilding, ref SystemState state)
        {
            if (!state.EntityManager.HasComponent<Buildings.BuildingConstructionProgressComponent>(prefabEntity))
                return;

            var progress = state.EntityManager.GetComponentData<Buildings.BuildingConstructionProgressComponent>(prefabEntity);
            progress.Value = progress.ConstructionTime;
            _entityCommandBuffer.SetComponent(newBuilding, progress);
        }
    }
}