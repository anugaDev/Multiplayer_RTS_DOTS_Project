using Combat;
using ElementCommons;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Units.Worker;
using UnityEngine;

namespace Units.MovementSystems
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(UnitStateSystem))]
    [UpdateBefore(typeof(WorkerActionSystem))]
    public partial class UnitAttackActionSystem : SystemBase
    {
        private ComponentLookup<CurrentHitPointsComponent> _hpLookup;
        private ComponentLookup<ElementTeamComponent>      _teamLookup;

        protected override void OnCreate()
        {
            _hpLookup   = GetComponentLookup<CurrentHitPointsComponent>(true);
            _teamLookup = GetComponentLookup<ElementTeamComponent>(true);
            RequireForUpdate<UnitTagComponent>();
            Debug.Log("[ATTACK-ACTION] SystemCreated");
        }

        protected override void OnUpdate()
        {
            _hpLookup.Update(this);
            _teamLookup.Update(this);

            Debug.Log("[ATTACK-ACTION] OnUpdate running");

            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach ((RefRO<SetInputStateTargetComponent>  inputTarget,
                      RefRW<SetServerStateTargetComponent> serverTarget,
                      Entity                               entity)
                     in SystemAPI.Query<RefRO<SetInputStateTargetComponent>,
                                        RefRW<SetServerStateTargetComponent>>()
                         .WithAll<UnitTagComponent, Simulate, UnitAttackingTagComponent>()
                         .WithEntityAccess())
            {
                if (inputTarget.ValueRO.TargetVersion > serverTarget.ValueRO.TargetVersion)
                {
                    Debug.Log($"[ATTACK-ACTION] New command for unit {entity.Index} — removing attack tag");
                    ecb.RemoveComponent<UnitAttackingTagComponent>(entity);
                    serverTarget.ValueRW.TargetVersion = inputTarget.ValueRO.TargetVersion;
                }
            }

            foreach ((RefRO<UnitStateComponent>            unitState,
                      RefRO<SetInputStateTargetComponent>  inputTarget,
                      RefRW<SetServerStateTargetComponent> serverTarget,
                      RefRO<ElementTeamComponent>          unitTeam,
                      Entity                               entity)
                     in SystemAPI.Query<RefRO<UnitStateComponent>,
                                        RefRO<SetInputStateTargetComponent>,
                                        RefRW<SetServerStateTargetComponent>,
                                        RefRO<ElementTeamComponent>>()
                         .WithAll<UnitTagComponent, Simulate>()
                         .WithNone<UnitAttackingTagComponent>()
                         .WithEntityAccess())
            {
                if (inputTarget.ValueRO.TargetVersion <= serverTarget.ValueRO.TargetVersion)
                    continue;

                if (unitState.ValueRO.State != UnitState.Idle)
                    continue;

                Entity target = inputTarget.ValueRO.TargetEntity;

                Debug.Log($"[ATTACK-ACTION] unit={entity.Index} Idle, target={target.Index}");

                if (target == Entity.Null || !EntityManager.Exists(target))
                {
                    serverTarget.ValueRW.TargetVersion = inputTarget.ValueRO.TargetVersion;
                    continue;
                }

                if (!_hpLookup.HasComponent(target) || _hpLookup[target].Value <= 0)
                {
                    Debug.Log($"[ATTACK-ACTION] target {target.Index} has no HP or is dead");
                    serverTarget.ValueRW.TargetVersion = inputTarget.ValueRO.TargetVersion;
                    continue;
                }

                if (!_teamLookup.TryGetComponent(target, out ElementTeamComponent targetTeam) ||
                    targetTeam.Team == unitTeam.ValueRO.Team)
                {
                    Debug.Log($"[ATTACK-ACTION] target {target.Index} is same team or no team");
                    serverTarget.ValueRW.TargetVersion = inputTarget.ValueRO.TargetVersion;
                    continue;
                }

                ecb.AddComponent(entity, new UnitAttackingTagComponent { TargetEntity = target });
                serverTarget.ValueRW.TargetVersion = inputTarget.ValueRO.TargetVersion;
                Debug.Log($"[ATTACK-ACTION] ✓ unit={entity.Index} → attack tag added, target={target.Index} HP={_hpLookup[target].Value}");
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
