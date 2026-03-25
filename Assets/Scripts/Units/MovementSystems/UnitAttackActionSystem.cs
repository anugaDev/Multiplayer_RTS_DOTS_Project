using Combat;
using ElementCommons;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Units.Worker;

namespace Units.MovementSystems
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(UnitStateSystem))]
    [UpdateBefore(typeof(WorkerActionSystem))]
    public partial class UnitAttackActionSystem : SystemBase
    {
        private const float DEFAULT_ATTACK_RANGE = 4.0f;

        private ComponentLookup<CurrentHitPointsComponent> _hpLookup;
        private ComponentLookup<ElementTeamComponent>      _teamLookup;
        private ComponentLookup<LocalTransform>            _transformLookup;
        private ComponentLookup<UnitAttackRange>           _attackRangeLookup;

        protected override void OnCreate()
        {
            _hpLookup          = GetComponentLookup<CurrentHitPointsComponent>(true);
            _teamLookup        = GetComponentLookup<ElementTeamComponent>(true);
            _transformLookup   = GetComponentLookup<LocalTransform>(true);
            _attackRangeLookup = GetComponentLookup<UnitAttackRange>(true);
            RequireForUpdate<UnitTagComponent>();
        }

        protected override void OnUpdate()
        {
            _hpLookup.Update(this);
            _teamLookup.Update(this);
            _transformLookup.Update(this);
            _attackRangeLookup.Update(this);

            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            // First loop: new player input cancels any ongoing attack
            foreach ((RefRO<SetInputStateTargetComponent>  inputTarget,
                      RefRW<SetServerStateTargetComponent> serverTarget,
                      Entity entity) in SystemAPI.Query<RefRO<SetInputStateTargetComponent>,
                                                        RefRW<SetServerStateTargetComponent>>()
                         .WithAll<UnitTagComponent, Simulate, UnitAttackingTagComponent>().WithEntityAccess())
            {
                if (inputTarget.ValueRO.TargetVersion <= serverTarget.ValueRO.TargetVersion)
                    continue;

                ecb.RemoveComponent<UnitAttackingTagComponent>(entity);
            }

            foreach ((RefRO<UnitStateComponent>            unitState,
                      RefRO<SetInputStateTargetComponent>  inputTarget,
                      RefRW<SetServerStateTargetComponent> serverTarget,
                      RefRO<ElementTeamComponent>          unitTeam,
                      RefRO<LocalTransform>                unitTransform,
                      Entity entity) in SystemAPI.Query<RefRO<UnitStateComponent>,
                                                       RefRO<SetInputStateTargetComponent>,
                                                       RefRW<SetServerStateTargetComponent>,
                                                       RefRO<ElementTeamComponent>,
                                                       RefRO<LocalTransform>>()
                         .WithAll<UnitTagComponent, Simulate>()
                         .WithNone<UnitAttackingTagComponent>()
                         .WithEntityAccess())
            {
                if (inputTarget.ValueRO.TargetVersion <= serverTarget.ValueRO.TargetVersion)
                    continue;

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

                bool targetInRange = false;
                if (_transformLookup.TryGetComponent(target, out LocalTransform targetTransform))
                {
                    float attackRange = _attackRangeLookup.TryGetComponent(entity, out UnitAttackRange rangeComp)
                        ? rangeComp.Value : DEFAULT_ATTACK_RANGE;

                    float3 toTarget  = targetTransform.Position - unitTransform.ValueRO.Position;
                    toTarget.y = 0f;
                    targetInRange = math.lengthsq(toTarget) <= attackRange * attackRange;
                }

                if (!targetInRange && unitState.ValueRO.State != UnitState.Idle)
                    continue;

                ecb.AddComponent(entity, new UnitAttackingTagComponent { TargetEntity = target });

                if (targetInRange && unitState.ValueRO.State == UnitState.Moving)
                {
                    ecb.SetComponent(entity, new PathComponent
                    {
                        HasPath              = false,
                        CurrentWaypointIndex = 0,
                        LastTargetPosition   = float3.zero,
                        LastTargetEntity     = Entity.Null
                    });
                }

                serverTarget.ValueRW.TargetVersion = inputTarget.ValueRO.TargetVersion;
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
