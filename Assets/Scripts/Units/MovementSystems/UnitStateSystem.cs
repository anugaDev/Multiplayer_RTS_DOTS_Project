using Units.Worker;
using Combat;
using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

namespace Units.MovementSystems
{
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(UnitMoveSystem))]
    [UpdateAfter(typeof(ServerUnitMoveSystem))]
    [BurstCompile]
    public partial struct UnitStateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<UnitTagComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach ((RefRO<PathComponent>    path,
                      RefRW<UnitStateComponent> unitState,
                      Entity                     entity)
                     in SystemAPI.Query<RefRO<PathComponent>,
                                        RefRW<UnitStateComponent>>()
                         .WithAll<UnitTagComponent, Simulate>()
                         .WithEntityAccess())
            {
                if (path.ValueRO.HasPath)
                {
                    unitState.ValueRW.State = UnitState.Moving;
                    continue;
                }

                bool hasWorkerTag =
                    SystemAPI.HasComponent<WorkerGatheringTagComponent>(entity)  ||
                    SystemAPI.HasComponent<WorkerStoringTagComponent>(entity)     ||
                    SystemAPI.HasComponent<WorkerConstructionTagComponent>(entity) ||
                    SystemAPI.HasComponent<UnitAttackingTagComponent>(entity);

                unitState.ValueRW.State = hasWorkerTag ? UnitState.Acting : UnitState.Idle;
            }
        }
    }
}
