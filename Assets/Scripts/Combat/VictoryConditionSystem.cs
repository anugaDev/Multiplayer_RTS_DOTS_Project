using Buildings;
using ElementCommons;
using Types;
using UI;
using Units;
using Unity.Entities;

namespace Combat
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial struct VictoryConditionSystem : ISystem
    {
        private EntityQuery _destroyQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTagComponent>();
            _destroyQuery = SystemAPI.QueryBuilder().WithAll<DestroyEntityTag>().Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            bool isGameOver = false;
            foreach (RefRO<GameOverTag> tag in SystemAPI.Query<RefRO<GameOverTag>>().WithAll<PlayerTagComponent>())
            {
                if (tag.ValueRO.WinnerTeam != TeamType.None)
                {
                    isGameOver = true;
                    break;
                }
            }

            if (isGameOver)
                return;

            foreach (RefRO<PlayerManualExitTag> exitTag in
                     SystemAPI.Query<RefRO<PlayerManualExitTag>>())
            {
                TeamType exitingTeam = exitTag.ValueRO.ExitingTeam;
                TeamType winnerTeam  = exitingTeam == TeamType.Blue ? TeamType.Red : TeamType.Blue;
                TriggerGameOver(ref state, winnerTeam);
                return;
            }

            if (!_destroyQuery.IsEmpty)
            {
                if (!SystemAPI.HasSingleton<CheckVictoryDelay>())
                {
                    EntityCommandBuffer ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
                    Entity delayEntity = ecb.CreateEntity();
                    ecb.AddComponent(delayEntity, new CheckVictoryDelay { FramesRemaining = 1 });
                    ecb.Playback(state.EntityManager);
                    ecb.Dispose();
                }
            }

            if (SystemAPI.HasSingleton<CheckVictoryDelay>())
            {
                Entity delayEntity = SystemAPI.GetSingletonEntity<CheckVictoryDelay>();
                CheckVictoryDelay delay = SystemAPI.GetComponent<CheckVictoryDelay>(delayEntity);

                if (delay.FramesRemaining > 0)
                {
                    delay.FramesRemaining--;
                    SystemAPI.SetComponent(delayEntity, delay);
                    return;
                }

                EntityCommandBuffer checkEcb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
                checkEcb.DestroyEntity(delayEntity);
                checkEcb.Playback(state.EntityManager);
                checkEcb.Dispose();

                PerformVictoryCheck(ref state);
            }
        }

        private void PerformVictoryCheck(ref SystemState state)
        {
            bool blueHasWorker = false;
            bool redHasWorker = false;
            bool blueHasTownCenter = false;
            bool redHasTownCenter = false;

            foreach ((RefRO<UnitTypeComponent> unitType, RefRO<ElementTeamComponent> team, RefRO<CurrentHitPointsComponent> hp) in
                     SystemAPI.Query<RefRO<UnitTypeComponent>, RefRO<ElementTeamComponent>, RefRO<CurrentHitPointsComponent>>()
                              .WithAll<Simulate>()
                              .WithNone<DestroyEntityTag>())
            {
                if (unitType.ValueRO.Type != UnitType.Worker || hp.ValueRO.Value <= 0)
                {
                    continue;
                }

                if (team.ValueRO.Team == TeamType.Blue)
                {
                    blueHasWorker = true;
                }
                else if (team.ValueRO.Team == TeamType.Red)
                {
                    redHasWorker = true;
                }
            }

            foreach ((RefRO<BuildingTypeComponent> buildingType, RefRO<ElementTeamComponent> team, RefRO<CurrentHitPointsComponent> hp) in
                     SystemAPI.Query<RefRO<BuildingTypeComponent>, RefRO<ElementTeamComponent>, RefRO<CurrentHitPointsComponent>>()
                              .WithAll<Simulate>()
                              .WithNone<DestroyEntityTag>())
            {
                if (buildingType.ValueRO.Type != BuildingType.Center || hp.ValueRO.Value <= 0)
                {
                    continue;
                }

                if (team.ValueRO.Team == TeamType.Blue)
                {
                    blueHasTownCenter = true;
                }
                else if (team.ValueRO.Team == TeamType.Red)
                {
                    redHasTownCenter = true;
                }
            }

            TeamType eliminatedTeam = TeamType.None;

            if (!blueHasWorker && !blueHasTownCenter)
            {
                eliminatedTeam = TeamType.Blue;
            }
            else if (!redHasWorker && !redHasTownCenter)
            {
                eliminatedTeam = TeamType.Red;
            }

            if (eliminatedTeam == TeamType.None)
            {
                return;
            }

            TeamType winner = eliminatedTeam == TeamType.Blue ? TeamType.Red : TeamType.Blue;
            TriggerGameOver(ref state, winner);
        }

        private void TriggerGameOver(ref SystemState state, TeamType winnerTeam)
        {
            foreach (RefRW<GameOverTag> gameOverTag in SystemAPI.Query<RefRW<GameOverTag>>().WithAll<PlayerTagComponent>())
            {
                gameOverTag.ValueRW.WinnerTeam = winnerTeam;
            }
        }
    }
}
