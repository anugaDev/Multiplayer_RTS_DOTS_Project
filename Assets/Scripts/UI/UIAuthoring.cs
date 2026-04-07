using UI.UIControllers;
using Unity.Entities;
using UnityEngine;

namespace UI
{
    public class UIAuthoring : MonoBehaviour
    { 
        
        [SerializeField] 
        private UnitUIController _unitUIPrefab;

        [SerializeField] 
        private GameObject _resourceUIPrefab;
        
        public UnitUIController UnitUIPrefab => _unitUIPrefab;
        
        public GameObject ResourceUIPrefab => _resourceUIPrefab;

        public class UnitPrefabBaker : Baker<UIAuthoring>
        {
            public override void Bake(UIAuthoring authoring)
            {
                Entity userInterfaceEntity = GetEntity(TransformUsageFlags.Dynamic);
                Entity prefabContainerEntity = GetEntity(TransformUsageFlags.None);
                AddComponentObject(prefabContainerEntity, GetUIPrefabs(authoring));
            }

            private UIPrefabs GetUIPrefabs(UIAuthoring authoring)
            {
                return new UIPrefabs
                {
                    UnitUI = authoring.UnitUIPrefab,
                    ResourceUI = authoring.ResourceUIPrefab
                };
            }
        }
    }
}