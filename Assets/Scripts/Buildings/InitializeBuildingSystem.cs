using ElementCommons;
using Types;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine.Rendering;

namespace Buildings
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class InitializeBuildingSystem : SystemBase
    {
        private BuildingMaterialsConfiguration _materialsConfiguration;

        private BatchMaterialID _blueMaterial;

        private BatchMaterialID _redMaterial;

        protected override void OnCreate()
        {
            RequireForUpdate<BuildingMaterialsConfigurationComponent>();
        }

        protected override void OnStartRunning()
        {
            _materialsConfiguration = SystemAPI.ManagedAPI.GetSingleton<BuildingMaterialsConfigurationComponent>().Configuration;
            EntitiesGraphicsSystem entitiesGraphicsSystem = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
            _redMaterial  = entitiesGraphicsSystem.RegisterMaterial(_materialsConfiguration.RedTeamMaterial);
            _blueMaterial = entitiesGraphicsSystem.RegisterMaterial(_materialsConfiguration.BlueTeamMaterial);
        }

        protected override void OnUpdate()
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach ((ElementTeamComponent buildingTeam, DynamicBuffer<LinkedEntityGroup> linkedEntities, Entity _)
                     in SystemAPI.Query<ElementTeamComponent, DynamicBuffer<LinkedEntityGroup>>()
                         .WithAll<NewBuildingTagComponent>().WithEntityAccess())
            {
                SetTeamMaterialOnChildren(linkedEntities, buildingTeam.Team, ecb);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private void SetTeamMaterialOnChildren(DynamicBuffer<LinkedEntityGroup> linkedEntities,
            TeamType team, EntityCommandBuffer ecb)
        {
            BatchMaterialID batchMaterialID = team == TeamType.Red ? _redMaterial : _blueMaterial;

            for (int i = 0; i < linkedEntities.Length; i++)
            {
                Entity childEntity = linkedEntities[i].Value;
                SetMaterialComponent(ecb, childEntity, batchMaterialID);
            }
        }

        private void SetMaterialComponent(EntityCommandBuffer ecb, Entity childEntity,
            BatchMaterialID batchMaterialID)
        {
            if (!EntityManager.HasComponent<MaterialMeshInfo>(childEntity))
                return;

            MaterialMeshInfo materialMeshInfo = EntityManager.GetComponentData<MaterialMeshInfo>(childEntity);
            materialMeshInfo.MaterialID = batchMaterialID;
            ecb.SetComponent(childEntity, materialMeshInfo);
        }
    }
}