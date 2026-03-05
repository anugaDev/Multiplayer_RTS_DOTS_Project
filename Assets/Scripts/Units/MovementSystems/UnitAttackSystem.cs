using Combat;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace Units.MovementSystems
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(UnitAttackActionSystem))]
    public partial class UnitAttackSystem : SystemBase
    {
        private const float ATTACK_DISTANCE_THRESHOLD = 4.0f;
        private const int   DEFAULT_DAMAGE             = 10;

        private ComponentLookup<CurrentHitPointsComponent> _hpLookup;
        private ComponentLookup<LocalTransform>            _transformLookup;
        private ComponentLookup<UnitAttackProperties>      _attackPropsLookup;
        private BufferLookup<DamageBufferElement>          _damageBufferLookup;

        protected override void OnCreate()
        {
            _hpLookup           = GetComponentLookup<CurrentHitPointsComponent>(true);
            _transformLookup    = GetComponentLookup<LocalTransform>(true);
            _attackPropsLookup  = GetComponentLookup<UnitAttackProperties>(true);
            _damageBufferLookup = GetBufferLookup<DamageBufferElement>(false);
            RequireForUpdate<UnitTagComponent>();
            Debug.Log("[ATTACK] SystemCreated");
        }

        protected override void OnUpdate()
        {
            CompleteDependency();

            _hpLookup.Update(this);
            _transformLookup.Update(this);
            _attackPropsLookup.Update(this);
            _damageBufferLookup.Update(this);

            float deltaTime = SystemAPI.Time.DeltaTime;
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach ((RefRW<LocalTransform>            unitTransform,
                      RefRO<UnitAttackingTagComponent> attackingTag,
                      Entity                           unitEntity)
                     in SystemAPI.Query<RefRW<LocalTransform>,
                                        RefRO<UnitAttackingTagComponent>>()
                         .WithAll<Simulate, UnitTagComponent>()
                         .WithEntityAccess())
            {
                int damage = _attackPropsLookup.TryGetComponent(unitEntity, out UnitAttackProperties props)
                             ? props.Damage
                             : DEFAULT_DAMAGE;

                ProcessAttack(ref unitTransform.ValueRW, attackingTag.ValueRO,
                              damage, unitEntity, deltaTime, ref ecb);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private void ProcessAttack(ref LocalTransform unitTransform,
                                   UnitAttackingTagComponent attackingTag,
                                   int damage,
                                   Entity unitEntity, float deltaTime, ref EntityCommandBuffer ecb)
        {
            Entity target = attackingTag.TargetEntity;

            if (!EntityManager.Exists(target))
            {
                Debug.Log($"[ATTACK] unit={unitEntity.Index} target gone — removing tag");
                ecb.RemoveComponent<UnitAttackingTagComponent>(unitEntity);
                return;
            }

            if (!_hpLookup.TryGetComponent(target, out CurrentHitPointsComponent targetHP))
            {
                Debug.LogWarning($"[ATTACK] unit={unitEntity.Index} target={target.Index} has no HP component — add HitPointAuthoring to prefab!");
                ecb.RemoveComponent<UnitAttackingTagComponent>(unitEntity);
                return;
            }

            if (targetHP.Value <= 0)
            {
                Debug.Log($"[ATTACK] unit={unitEntity.Index} target={target.Index} HP<=0 — removing tag");
                ecb.RemoveComponent<UnitAttackingTagComponent>(unitEntity);
                return;
            }

            if (!_transformLookup.TryGetComponent(target, out LocalTransform targetTransform))
                return;

            float distanceSq = math.distancesq(unitTransform.Position, targetTransform.Position);

            Debug.Log($"[ATTACK] unit={unitEntity.Index} dist²={distanceSq:F1} threshold²={ATTACK_DISTANCE_THRESHOLD * ATTACK_DISTANCE_THRESHOLD} targetHP={targetHP.Value}");

            if (distanceSq > ATTACK_DISTANCE_THRESHOLD * ATTACK_DISTANCE_THRESHOLD)
                return;

            float3 dir = math.normalizesafe(targetTransform.Position - unitTransform.Position);
            if (!math.all(dir == float3.zero))
            {
                dir.y = 0;
                unitTransform.Rotation = quaternion.LookRotationSafe(dir, math.up());
            }

            if (!_damageBufferLookup.HasBuffer(target))
            {
                Debug.LogWarning($"[ATTACK] target={target.Index} has NO DamageBufferElement — add HitPointAuthoring to prefab!");
                return;
            }

            _damageBufferLookup[target].Add(new DamageBufferElement { Value = damage });
            Debug.Log($"[ATTACK] ✓ unit={unitEntity.Index} dealt {damage} to {target.Index} (HP={targetHP.Value})");
        }
    }
}
