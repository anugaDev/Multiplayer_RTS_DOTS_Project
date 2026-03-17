using System.Collections.Generic;
using ScriptableObjects;
using Types;
using Unity.Entities;
using UnityEngine;

namespace Buildings
{
    public class BuildingPrefabAuthoring : MonoBehaviour
    {
        [SerializeField]
        private BuildingsScriptableObject _configuration;

        [SerializeField]
        private BuildingMaterialsConfiguration _materialConfiguration;

        public BuildingsScriptableObject Configuration => _configuration;

        public BuildingMaterialsConfiguration MaterialConfiguration => _materialConfiguration;

        public class BuildingPrefabBaker : Baker<BuildingPrefabAuthoring>
        {
            public override void Bake(BuildingPrefabAuthoring prefabAuthoring)
            {
                Entity buildingContainer = GetEntity(TransformUsageFlags.None);
                Entity prefabContainerEntity = GetEntity(TransformUsageFlags.None); 
                AddComponent(buildingContainer, GetBuildingsComponent(prefabAuthoring));
                AddComponentObject(prefabContainerEntity, GetBuildingConfiguration(prefabAuthoring));
                AddComponentObject(prefabContainerEntity, GetBuildingMaterialsConfiguration(prefabAuthoring));
            }

            private BuildingsConfigurationComponent GetBuildingConfiguration(BuildingPrefabAuthoring prefabAuthoring)
            {
                return new BuildingsConfigurationComponent
                {
                    Configuration =  prefabAuthoring.Configuration
                };
            }

            private BuildingMaterialsConfigurationComponent GetBuildingMaterialsConfiguration(BuildingPrefabAuthoring prefabAuthoring)
            {
                return new BuildingMaterialsConfigurationComponent
                {
                    Configuration =  prefabAuthoring.MaterialConfiguration
                };
            }
            private BuildingPrefabComponent GetBuildingsComponent(BuildingPrefabAuthoring prefabAuthoring)
            {
                Dictionary<BuildingType, BuildingScriptableObject> unitsDictionary = prefabAuthoring.Configuration.GetBuildingsDictionary();

                return new BuildingPrefabComponent
                {
                    TownCenter = GetEntity(unitsDictionary[BuildingType.Center].BuildingPrefab, TransformUsageFlags.Dynamic),
                    Barracks = GetEntity(unitsDictionary[BuildingType.Barracks].BuildingPrefab, TransformUsageFlags.Dynamic),
                    House = GetEntity(unitsDictionary[BuildingType.House].BuildingPrefab, TransformUsageFlags.Dynamic),
                    Farm = GetEntity(unitsDictionary[BuildingType.Farm].BuildingPrefab, TransformUsageFlags.Dynamic),
                    Tower = GetEntity(unitsDictionary[BuildingType.Tower].BuildingPrefab, TransformUsageFlags.Dynamic)
                };
            }
        }
    }
}