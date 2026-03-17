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
        private const float DEFAULT_ATTACK_RANGE = 4.0f;

        private const int   DEFAULT_DAMAGE       = 10;

        private ComponentLookup<UnitAttackProperties> _attackPropsLookup;

        private ComponentLookup<UnitMoveSpeedComponent> _moveSpeedLookup;

        private BufferLookup<DamageBufferElement> _damageBufferLookup;

        private ComponentLookup<CurrentHitPointsComponent> _hpLookup;

        private ComponentLookup<UnitAttackRange> _attackRangeLookup;

        private ComponentLookup<LocalTransform> _transformLookup;
        
        protected override void OnCreate()
        {
            _hpLookup = GetComponentLookup<CurrentHitPointsComponent>(true);
            _transformLookup = GetComponentLookup<LocalTransform>(true);
            _attackPropsLookup = GetComponentLookup<UnitAttackProperties>(true);
            _attackRangeLookup = GetComponentLookup<UnitAttackRange>(true);
            _moveSpeedLookup = GetComponentLookup<UnitMoveSpeedComponent>(true);
            _damageBufferLookup = GetBufferLookup<DamageBufferElement>(false);
            RequireForUpdate<UnitTagComponent>();
        }

        protected override void OnUpdate()
        {
            CompleteDependency();

            _hpLookup.Update(this);
            _transformLookup.Update(this);
            _attackPropsLookup.Update(this);
            _attackRangeLookup.Update(this);
            _moveSpeedLookup.Update(this);
            _damageBufferLookup.Update(this);

            float deltaTime = SystemAPI.Time.DeltaTime;
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach ((RefRW<LocalTransform> unitTransform, RefRO<UnitAttackingTagComponent> attackingTag,
                      Entity unitEntity) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<UnitAttackingTagComponent>>()
                         .WithAll<Simulate, UnitTagComponent>().WithEntityAccess())
            {
                int damage = _attackPropsLookup.TryGetComponent(unitEntity, out UnitAttackProperties props)
                             ? props.Damage : DEFAULT_DAMAGE;

                float attackRange = _attackRangeLookup.TryGetComponent(unitEntity, out UnitAttackRange range)
                                    ? range.Value : DEFAULT_ATTACK_RANGE;

                float moveSpeed = _moveSpeedLookup.TryGetComponent(unitEntity, out UnitMoveSpeedComponent speed)
                                  ? speed.Speed : 0f;

                ProcessAttack(ref unitTransform.ValueRW, attackingTag.ValueRO,
                              damage, attackRange, moveSpeed, unitEntity, deltaTime, ref ecb);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private void ProcessAttack(ref LocalTransform unitTransform, UnitAttackingTagComponent attackingTag, 
            int damage, float attackRange, float moveSpeed,  Entity unitEntity, float deltaTime, ref EntityCommandBuffer ecb)
        {
            Entity target = attackingTag.TargetEntity;

            if (!EntityManager.Exists(target))
            {
                ecb.RemoveComponent<UnitAttackingTagComponent>(unitEntity);
                return;
            }

            if (!_hpLookup.TryGetComponent(target, out CurrentHitPointsComponent targetHP))
            {
                ecb.RemoveComponent<UnitAttackingTagComponent>(unitEntity);
                return;
            }

            if (targetHP.Value <= 0)
            {
                ecb.RemoveComponent<UnitAttackingTagComponent>(unitEntity);
                return;
            }

            if (!_transformLookup.TryGetComponent(target, out LocalTransform targetTransform))
                return;

            float3 toTarget = targetTransform.Position - unitTransform.Position;
            toTarget.y = 0f;
            float distanceSq = math.lengthsq(toTarget);
            float rangeSq = attackRange * attackRange;

            if (distanceSq > rangeSq)
            {
                if (moveSpeed > 0f)
                {
                    float distance = math.sqrt(distanceSq);
                    float3 dir    = toTarget / distance;
                    float  step   = math.min(moveSpeed * deltaTime, distance - attackRange);
                    if (step > 0f)
                    {
                        unitTransform.Position += dir * step;
                        unitTransform.Rotation  = quaternion.LookRotationSafe(dir, math.up());
                    }
                }
                return;
            }

            float3 faceDir = math.normalizesafe(toTarget);
            if (!math.all(faceDir == float3.zero))
                unitTransform.Rotation = quaternion.LookRotationSafe(faceDir, math.up());

            if (!_damageBufferLookup.HasBuffer(target))
            {
                return;
            }

            int tickDamage = Mathf.RoundToInt(damage * deltaTime);
            if (tickDamage <= 0) tickDamage = 1;

            _damageBufferLookup[target].Add(new DamageBufferElement { Value = tickDamage });
        }
    }
}
