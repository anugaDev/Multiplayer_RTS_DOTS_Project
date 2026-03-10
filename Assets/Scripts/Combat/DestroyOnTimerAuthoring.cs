using Unity.Entities;
using UnityEngine;

namespace Combat
{
    public class DestroyOnTimerAuthoring : MonoBehaviour
    {
        public float DestroyOnTimer;

        public class DestroyOnTimerBaker : Baker<DestroyOnTimerAuthoring>
        {
            public override void Bake(DestroyOnTimerAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new DestroyOnTimer { Value = authoring.DestroyOnTimer });
            }
        }
    }
}