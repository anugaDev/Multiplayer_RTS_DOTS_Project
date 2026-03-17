using System.Collections.Generic;
using Buildings;
using ElementCommons;
using ScriptableObjects;
using Types;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace Units
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    public partial struct InitializeLocalBuildingSystem : ISystem 
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkId>();
        }
        
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer entityCommandBuffer = new EntityCommandBuffer(Allocator.Temp);
            foreach ((BuildingComponents _, BuildingTypeComponent typeComponent, Entity entity)
                     in SystemAPI.Query<BuildingComponents, BuildingTypeComponent>().WithAll<GhostOwnerIsLocal>().WithNone<OwnerTagComponent>().WithEntityAccess())
            {
                entityCommandBuffer.AddComponent<OwnerTagComponent>(entity);
                entityCommandBuffer.SetComponent(entity, GetDetailsComponent(typeComponent));
            }

            entityCommandBuffer.Playback(state.EntityManager);
        }
        
        private ElementDisplayDetailsComponent GetDetailsComponent(BuildingTypeComponent buildingTypeComponent)
        {
            BuildingType buildingType = buildingTypeComponent.Type;
            BuildingsConfigurationComponent configurationComponent = SystemAPI.ManagedAPI.GetSingleton<BuildingsConfigurationComponent>();
            Dictionary<BuildingType, BuildingScriptableObject> unitScriptableObjects = configurationComponent.Configuration.GetBuildingsDictionary();
            string displayName = unitScriptableObjects[buildingType].Name;
            Sprite displayImage = unitScriptableObjects[buildingType].Sprite;
            return new ElementDisplayDetailsComponent
            {
                Name = displayName,
                Sprite = displayImage
            };
        }
    }
}