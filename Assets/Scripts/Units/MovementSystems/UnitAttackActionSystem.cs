using Combat;
using ElementCommons;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Units.Worker;

namespace Units.MovementSystems
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(UnitStateSystem))]
    [UpdateBefore(typeof(WorkerActionSystem))]
    public partial class UnitAttackActionSystem : SystemBase
    {
        private ComponentLookup<CurrentHitPointsComponent> _hpLookup;
        private ComponentLookup<ElementTeamComponent> _teamLookup;

        protected override void OnCreate()
        {
            _hpLookup   = GetComponentLookup<CurrentHitPointsComponent>(true);
            _teamLookup = GetComponentLookup<ElementTeamComponent>(true);
            RequireForUpdate<UnitTagComponent>();
        }

        protected override void OnUpdate()
        {
            _hpLookup.Update(this);
            _teamLookup.Update(this);

            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach ((RefRO<SetInputStateTargetComponent>  inputTarget,RefRW<SetServerStateTargetComponent> serverTarget,
                      Entity entity)in SystemAPI.Query<RefRO<SetInputStateTargetComponent>,RefRW<SetServerStateTargetComponent>>()
                         .WithAll<UnitTagComponent, Simulate, UnitAttackingTagComponent>().WithEntityAccess())
            {
                if (inputTarget.ValueRO.TargetVersion <= serverTarget.ValueRO.TargetVersion)
                {
                    continue;
                }

                ecb.RemoveComponent<UnitAttackingTagComponent>(entity);
                serverTarget.ValueRW.TargetVersion = inputTarget.ValueRO.TargetVersion;
            }

            foreach ((RefRO<UnitStateComponent> unitState,RefRO<SetInputStateTargetComponent> inputTarget,
                      RefRW<SetServerStateTargetComponent> serverTarget,RefRO<ElementTeamComponent> unitTeam,
                      Entity entity) in SystemAPI.Query<RefRO<UnitStateComponent>,RefRO<SetInputStateTargetComponent>, 
                             RefRW<SetServerStateTargetComponent>, RefRO<ElementTeamComponent>>()
                         .WithAll<UnitTagComponent, Simulate>().WithNone<UnitAttackingTagComponent>().WithEntityAccess())
            {
                if (inputTarget.ValueRO.TargetVersion <= serverTarget.ValueRO.TargetVersion)
                {
                    continue;
                }

                if (unitState.ValueRO.State != UnitState.Idle)
                {
                    continue;
                }

                Entity target = inputTarget.ValueRO.TargetEntity;

                if (target == Entity.Null || !EntityManager.Exists(target))
                {
                    serverTarget.ValueRW.TargetVersion = inputTarget.ValueRO.TargetVersion;
                    continue;
                }

                if (!_hpLookup.HasComponent(target) || _hpLookup[target].Value <= 0)
                {
                    serverTarget.ValueRW.TargetVersion = inputTarget.ValueRO.TargetVersion;
                    continue;
                }

                if (!_teamLookup.TryGetComponent(target, out ElementTeamComponent targetTeam) ||
                    targetTeam.Team == unitTeam.ValueRO.Team)
                {
                    serverTarget.ValueRW.TargetVersion = inputTarget.ValueRO.TargetVersion;
                    continue;
                }

                ecb.AddComponent(entity, new UnitAttackingTagComponent { TargetEntity = target });
                serverTarget.ValueRW.TargetVersion = inputTarget.ValueRO.TargetVersion;
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
