using ElementCommons;
using Types;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine.Rendering;

namespace Buildings
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class InitializeBuildingSystem : SystemBase
    { 
        private const int MAX_PARENT_DEPTH = 32;
        
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
            _redMaterial = entitiesGraphicsSystem.RegisterMaterial(_materialsConfiguration.RedTeamMaterial);
            _blueMaterial = entitiesGraphicsSystem.RegisterMaterial(_materialsConfiguration.BlueTeamMaterial);
        }

        protected override void OnUpdate()
        {
            EntityCommandBuffer entityCommandBuffer = new EntityCommandBuffer(Allocator.Temp);

            foreach ((ElementTeamComponent buildingTeam, DynamicBuffer<LinkedEntityGroup> linkedEntities, Entity _)
                     in SystemAPI.Query<ElementTeamComponent, DynamicBuffer<LinkedEntityGroup>>()
                         .WithAll<NewBuildingTagComponent>().WithEntityAccess())
            {
                SetTeamMaterialOnChildren(linkedEntities, buildingTeam.Team, entityCommandBuffer);
            }

            entityCommandBuffer.Playback(EntityManager);
            entityCommandBuffer.Dispose();
            UpdateConstructionVisuals();
        }

        private void UpdateConstructionVisuals()
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach ((BuildingPivotReferencesComponent pivotRefs,
                      BuildingTypeComponent _,
                      Entity buildingEntity)
                     in SystemAPI.Query<BuildingPivotReferencesComponent,
                                        BuildingTypeComponent>()
                         .WithEntityAccess())
            {
                UpdateBuildingConstructionVisuals(buildingEntity, pivotRefs, ecb);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private void UpdateBuildingConstructionVisuals(Entity buildingEntity, BuildingPivotReferencesComponent pivotRefs,
            EntityCommandBuffer ecb)
        {
            bool isFinished = IsFinishedBuildingOnSpawn(buildingEntity);

            if (!EntityManager.HasBuffer<LinkedEntityGroup>(buildingEntity))
                return;

            DynamicBuffer<LinkedEntityGroup> linkedEntities =
                EntityManager.GetBuffer<LinkedEntityGroup>(buildingEntity);
            SetBuildingConstructionVisuals(buildingEntity, pivotRefs, ecb, linkedEntities, isFinished);
        }

        private void SetBuildingConstructionVisuals(Entity buildingEntity, BuildingPivotReferencesComponent pivotRefs,
            EntityCommandBuffer ecb, DynamicBuffer<LinkedEntityGroup> linkedEntities, bool isFinished)
        {
            for (int i = 0; i < linkedEntities.Length; i++)
            {
                Entity childEntity = linkedEntities[i].Value;
                if (childEntity == buildingEntity)
                {
                    continue;
                }

                SetBuildingConstructionVisual(pivotRefs, isFinished, ecb, childEntity);
            }
        }

        private void SetBuildingConstructionVisual(BuildingPivotReferencesComponent pivotRefs, bool isFinished,
            EntityCommandBuffer ecb, Entity childEntity)
        {
            bool belongsToPivot = IsChildOf(childEntity, pivotRefs.PivotEntity);
            bool belongsToSite = IsChildOf(childEntity, pivotRefs.ConstructionSiteEntity);

            if (belongsToPivot)
            {
                SetEntityEnabled(childEntity, isFinished, ecb);
            }
            else if (belongsToSite)
            {
                SetEntityEnabled(childEntity, !isFinished, ecb);
            }
        }

        private bool IsFinishedBuildingOnSpawn(Entity buildingEntity)
        {
            if (!EntityManager.HasComponent<BuildingConstructionProgressComponent>(buildingEntity))
            {
                return true;
            }

            return GetIsFinishedBuildingByComponent(buildingEntity);
        }

        private bool GetIsFinishedBuildingByComponent(Entity buildingEntity)
        {
            bool isFinished = true;
            
            BuildingConstructionProgressComponent progress =
                EntityManager.GetComponentData<BuildingConstructionProgressComponent>(buildingEntity);

            return progress.ConstructionTime > 0 && progress.Value >= progress.ConstructionTime;
        }

        private bool IsChildOf(Entity entity, Entity parent)
        {
            if (parent == Entity.Null)
            {
                return false;
            }

            if (entity == parent)
            {
                return true;
            }

            return IsSameParent(entity, parent);
        }

        private bool IsSameParent(Entity entity, Entity parent)
        {
            Entity current = entity;

            for (int i = 0; i < MAX_PARENT_DEPTH; i++)
            {
                if (!EntityManager.HasComponent<Parent>(current))
                {
                    return false;
                }

                Entity parentEntity = EntityManager.GetComponentData<Parent>(current).Value;

                if (parentEntity == parent)
                {
                    return true;
                }

                current = parentEntity;
            }

            return false;
        }

        private void SetEntityEnabled(Entity entity, bool enabled, EntityCommandBuffer ecb)
        {
            if (!EntityManager.Exists(entity))
            {
                return;
            }

            if (enabled)
            {
                RemoveDisabledComponent(entity, ecb);
            }
            else
            {
                AddDisableComponent(entity, ecb);
            }
        }

        private void AddDisableComponent(Entity entity, EntityCommandBuffer ecb)
        {
            if (EntityManager.HasComponent<Disabled>(entity))
            {
                return;
            }

            ecb.AddComponent<Disabled>(entity);
        }

        private void RemoveDisabledComponent(Entity entity, EntityCommandBuffer ecb)
        {
            if (!EntityManager.HasComponent<Disabled>(entity))
            {
                return;
            }

            ecb.RemoveComponent<Disabled>(entity);
        }

        private void SetTeamMaterialOnChildren(DynamicBuffer<LinkedEntityGroup> linkedEntities, TeamType team, EntityCommandBuffer ecb)
        {
            BatchMaterialID batchMaterialID = team == TeamType.Red ? _redMaterial : _blueMaterial;

            for (int i = 0; i < linkedEntities.Length; i++)
            {
                Entity childEntity = linkedEntities[i].Value;

                SetMaterialComponent(ecb, childEntity, batchMaterialID);
            }
        }

        private void SetMaterialComponent(EntityCommandBuffer ecb, Entity childEntity, BatchMaterialID batchMaterialID)
        {
            if (!EntityManager.HasComponent<MaterialMeshInfo>(childEntity))
            {
                return;
            }

            MaterialMeshInfo materialMeshInfo = EntityManager.GetComponentData<MaterialMeshInfo>(childEntity);
            materialMeshInfo.MaterialID = batchMaterialID;
            ecb.SetComponent(childEntity, materialMeshInfo);
        }
    }
}