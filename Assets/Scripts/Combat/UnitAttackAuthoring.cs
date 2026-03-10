using Units.MovementSystems;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace Combat
{
    public class UnitAttackAuthoring : MonoBehaviour
    {
        [SerializeField]
        private float _targetRadius;

        [SerializeField]
        private float _attackRange;

        [SerializeField]
        private Vector3 _firePointOffset;

        [SerializeField]
        private float _attackCooldownTime;

        [SerializeField]
        private int _attackDamage;

        [SerializeField]
        private NetCodeConfig _netCodeConfig;
        
        public int SimulationTickRate => _netCodeConfig.ClientServerTickRate.SimulationTickRate;


        public float TargetRadius => _targetRadius;

        public float AttackRange => _attackRange;

        public Vector3 FirePointOffset => _firePointOffset;

        public float AttackCooldownTime => _attackCooldownTime;

        public int AttackDamage => _attackDamage;

        public class UnitAttackBaker : Baker<UnitAttackAuthoring>
        {
            public override void Bake(UnitAttackAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new UnitTargetRadius { Value = authoring.TargetRadius });
                AddComponent(entity, new UnitAttackRange { Value = authoring.AttackRange });
                AddComponent(entity, new UnitAttackProperties
                {
                    FirePointOffset = authoring.FirePointOffset,
                    CooldownTickCount = (uint)(authoring.AttackCooldownTime * authoring.SimulationTickRate),
                    Damage = authoring.AttackDamage,
                });
                AddComponent<UnitTargetEntity>(entity);
                AddBuffer<UnitAttackCooldown>(entity);
            }
        }
    }
}