using ElementCommons;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace Buildings
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class BuildingConstructionVisualsSystem : SystemBase
    {
        private Entity _currentBuilding;

        private const int MAX_PARENT_DEPTH = 32;

        protected override void OnUpdate()
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach ((BuildingPivotReferencesComponent pivotRefs,
                      BuildingTypeComponent _,
                      Entity buildingEntity)
                     in SystemAPI.Query<BuildingPivotReferencesComponent,
                                        BuildingTypeComponent>()
                         .WithNone<BuildingVisualsReadyTagComponent>()
                         .WithEntityAccess())
            {
                _currentBuilding = buildingEntity;
                UpdateBuildingConstructionVisuals(pivotRefs, ecb);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private void UpdateBuildingConstructionVisuals(BuildingPivotReferencesComponent pivotRefs, EntityCommandBuffer ecb)
        {
            bool isFinished = IsFinishedBuilding();

            if (!EntityManager.HasBuffer<LinkedEntityGroup>(_currentBuilding))
                return;

            DynamicBuffer<LinkedEntityGroup> linkedEntities =
                EntityManager.GetBuffer<LinkedEntityGroup>(_currentBuilding);

            SetBuildingConstructionVisuals(pivotRefs, ecb, linkedEntities, isFinished);

            if (isFinished)
            {
                ecb.AddComponent<BuildingVisualsReadyTagComponent>(_currentBuilding);
                UpdateSelectionUI();
            }
        }

        private void SetBuildingConstructionVisuals(BuildingPivotReferencesComponent pivotRefs, EntityCommandBuffer ecb,
            DynamicBuffer<LinkedEntityGroup> linkedEntities, bool isFinished)
        {
            for (int i = 0; i < linkedEntities.Length; i++)
            {
                Entity childEntity = linkedEntities[i].Value;
                if (childEntity == _currentBuilding)
                    continue;

                SetBuildingConstructionVisual(pivotRefs, isFinished, ecb, childEntity);
            }
        }

        private void SetBuildingConstructionVisual(BuildingPivotReferencesComponent pivotRefs,
            bool isFinished, EntityCommandBuffer ecb, Entity childEntity)
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

        private void UpdateSelectionUI()
        {
            ElementSelectionComponent elementSelectionComponent = EntityManager.GetComponentData<ElementSelectionComponent>(_currentBuilding);
            elementSelectionComponent.MustUpdateUI = true;
            EntityManager.SetComponentData(_currentBuilding, elementSelectionComponent);
        }

        private bool IsFinishedBuilding()
        {
            if (!EntityManager.HasComponent<BuildingConstructionProgressComponent>(_currentBuilding))
                return true;

            return GetIsFinishedBuildingByComponent(_currentBuilding);
        }

        private bool GetIsFinishedBuildingByComponent(Entity buildingEntity)
        {
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
                    return false;

                Entity parentEntity = EntityManager.GetComponentData<Parent>(current).Value;

                if (parentEntity == parent)
                    return true;

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
    }
}
