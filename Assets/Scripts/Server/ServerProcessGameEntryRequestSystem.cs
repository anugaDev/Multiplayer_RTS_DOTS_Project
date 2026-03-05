using Buildings;
using Client;
using ElementCommons;
using PlayerInputs;
using Units;
using Types;
using UI;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace Server
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct ServerProcessGameEntryRequestSystem : ISystem
    {
        private EntityCommandBuffer _entityCommandBuffer;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerPrefabComponent>();
            state.RequireForUpdate<BuildingPrefabComponent>();
            state.RequireForUpdate<NetworkTime>();
            EntityQueryBuilder builder = new EntityQueryBuilder(Allocator.Temp).WithAll<TeamRequest, ReceiveRpcCommandRequest>();
            state.RequireForUpdate(state.GetEntityQuery(builder));
        }

        public void OnUpdate(ref SystemState state)
        {
            _entityCommandBuffer = new EntityCommandBuffer(Allocator.Temp);
            Entity townCenterPrefab = SystemAPI.GetSingleton<BuildingPrefabComponent>().TownCenter;
            Entity playerPrefab = SystemAPI.GetSingleton<PlayerPrefabComponent>().Entity;
            NetworkTick serverTick = SystemAPI.GetSingleton<NetworkTime>().ServerTick;

            foreach ((TeamRequest teamRequest, ReceiveRpcCommandRequest requestSource, Entity requestEntity)
                     in SystemAPI.Query<TeamRequest, ReceiveRpcCommandRequest>().WithEntityAccess())
            {
                _entityCommandBuffer.DestroyEntity(requestEntity);
                _entityCommandBuffer.AddComponent<NetworkStreamInGame>(requestSource.SourceConnection);
                int networkId = SystemAPI.GetComponent<NetworkId>(requestSource.SourceConnection).Value;
                float3 townCenterPosition = SpawnInitialTownCenter(townCenterPrefab, networkId, teamRequest.Team, ref state);

                Entity spawnPlayer = SpawnPlayer(networkId, teamRequest, playerPrefab, requestSource, townCenterPosition, serverTick);
                LinkedEntityGroup linkedEntityGroup = new LinkedEntityGroup();
                linkedEntityGroup.Value = spawnPlayer;
                _entityCommandBuffer.AppendToBuffer(requestSource.SourceConnection, linkedEntityGroup);
            }
            _entityCommandBuffer.Playback(state.EntityManager);
        }

        private Entity SpawnPlayer(int networkId, TeamRequest teamRequest,
            Entity playerPrefab, ReceiveRpcCommandRequest requestSource, float3 townCenterPosition, NetworkTick serverTick)
        {
            Entity connection = requestSource.SourceConnection;
            Entity player = _entityCommandBuffer.Instantiate(playerPrefab);
            TeamType playerTeam = teamRequest.Team;
            _entityCommandBuffer.SetName(player,"Player " + playerTeam);
            _entityCommandBuffer.SetComponent(player, LocalTransform.FromPosition(float3.zero));
            _entityCommandBuffer.SetComponent(connection, new CommandTarget{targetEntity = player});
            _entityCommandBuffer.SetComponent(player, GetGhostOwner(networkId));
            _entityCommandBuffer.SetComponent(player, new PlayerTeamComponent{Team = playerTeam});
            _entityCommandBuffer.AddComponent(player, GetLastProcessedBuildingCommand());
            _entityCommandBuffer.AddComponent(player, GetLastProcessedUnitCommand());
            _entityCommandBuffer.AddComponent(player, GetLastProcessedQueueCommand());
            _entityCommandBuffer.AddComponent(player, GetTestEnemyTeamComponent(teamRequest.Team));
            DynamicBuffer<SpawnUnitCommand> spawnUnitCommands = _entityCommandBuffer.AddBuffer<SpawnUnitCommand>(player);
            spawnUnitCommands.AddCommandData(GetSpawnUnitCommand(townCenterPosition, serverTick));
            _entityCommandBuffer.AddBuffer<QueueUnitCommand>(player);
            return player;
        }

        private SpawnTestEnemyTeamTag GetTestEnemyTeamComponent(TeamType teamRequestTeam)
        {
            return new SpawnTestEnemyTeamTag { PlayerTeam = teamRequestTeam };
        }

        private LastProcessedBuildingCommand GetLastProcessedBuildingCommand()
        {
            return new LastProcessedBuildingCommand
            {
                Tick = NetworkTick.Invalid,
                Position = float3.zero,
                BuildingType = BuildingType.Center
            };
        }

        private LastProcessedUnitCommand GetLastProcessedUnitCommand()
        {
            return new LastProcessedUnitCommand()
            {
                CommandId = -1
            };
        }

        private LastProcessedQueueCommand GetLastProcessedQueueCommand()
        {
            return new LastProcessedQueueCommand()
            {
                CommandId = -1
            };
        }

        private SpawnUnitCommand GetSpawnUnitCommand(float3 townCenterPosition, NetworkTick serverTick)
        {

            return new SpawnUnitCommand
            {
                UnitType = UnitType.Worker,
                BuildingPosition = townCenterPosition,
                Tick = serverTick,
                CommandId = GetCommandId(townCenterPosition, serverTick)
            };
        }

        private static int GetCommandId(float3 townCenterPosition, NetworkTick serverTick)
        {
            int positionHash = (int)(townCenterPosition.x * 1000 + townCenterPosition.z * 100);
            int commandId = (int)serverTick.TickIndexForValidTick * 10000 + (int)UnitType.Worker * 100 + (positionHash % 100);
            return commandId;
        }

        private float3 SpawnInitialTownCenter(Entity townCenterPrefab, int networkId, TeamType team, ref SystemState state)
        {
            Entity newBuilding = _entityCommandBuffer.Instantiate(townCenterPrefab);

            float3 buildingPosition = GetTownCenterPosition(team);
            LocalTransform prefabTransform = state.EntityManager.GetComponentData<LocalTransform>(townCenterPrefab);
            LocalTransform newTransform = LocalTransform.FromPositionRotationScale(
                buildingPosition,
                prefabTransform.Rotation,
                prefabTransform.Scale);

            _entityCommandBuffer.SetComponent(newBuilding, newTransform);
            _entityCommandBuffer.SetComponent(newBuilding, new GhostOwner{NetworkId = networkId});
            _entityCommandBuffer.SetComponent(newBuilding, new ElementTeamComponent{Team = team});
            var progress = state.EntityManager.GetComponentData<Buildings.BuildingConstructionProgressComponent>(townCenterPrefab);
            progress.Value = progress.ConstructionTime;
            _entityCommandBuffer.SetComponent(newBuilding, progress);

            return buildingPosition;
        }

        private ElementTeamComponent GetTeamComponent(TeamType team)
        {
            return new ElementTeamComponent{Team = team};
        }

        private GhostOwner GetGhostOwner(int clientId)
        {
            return new GhostOwner
            {
                NetworkId = clientId
            };
        }

        private float3 GetTownCenterPosition(TeamType team)
        {
            if (team is TeamType.Red)
            {
                return new float3(50f, GlobalParameters.DEFAULT_SCENE_HEIGHT, 50f);
            }
            else
            {
                return new float3(-50f, GlobalParameters.DEFAULT_SCENE_HEIGHT, -50f);
            }
        }
    }
}