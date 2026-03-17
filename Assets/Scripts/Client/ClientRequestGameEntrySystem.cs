using Types;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace Client
{
    [WorldSystemFilter((WorldSystemFilterFlags.ClientSimulation) | WorldSystemFilterFlags.ThinClientSimulation)]
    public partial struct ClientRequestGameEntrySystem : ISystem
    {
        private EntityQuery _pendingNetworkIdQuery;

        public void OnCreate(ref SystemState state)
        {
            EntityQueryBuilder builder = new EntityQueryBuilder(Allocator.Temp).WithAll<NetworkId>().WithNone<NetworkStreamInGame>();
            _pendingNetworkIdQuery = state.GetEntityQuery(builder);
            state.RequireForUpdate(_pendingNetworkIdQuery);
            state.RequireForUpdate<ClientTeamRequest>();
        }

        public void OnUpdate(ref SystemState state)
        {
            TeamType teamType = SystemAPI.GetSingleton<ClientTeamRequest>().Value;
            EntityCommandBuffer entityCommandBuffer = new EntityCommandBuffer(Allocator.Temp);
             NativeArray<Entity> pendingNetworkIds = _pendingNetworkIdQuery.ToEntityArray(Allocator.Temp);

            foreach (Entity networkId in pendingNetworkIds)
            {
                entityCommandBuffer.AddComponent<NetworkStreamInGame>(networkId);
                Entity requestTeamEntity = entityCommandBuffer.CreateEntity();
                TeamRequest clientTeamRequest = new TeamRequest();
                clientTeamRequest.Team = teamType;
                entityCommandBuffer.AddComponent(requestTeamEntity, clientTeamRequest);
                SendRpcCommandRequest sendRpcCommandRequest = new SendRpcCommandRequest();
                sendRpcCommandRequest.TargetConnection = networkId;
                entityCommandBuffer.AddComponent(requestTeamEntity, sendRpcCommandRequest);
            }
        
            entityCommandBuffer.Playback(state.EntityManager);
        }

    }
}