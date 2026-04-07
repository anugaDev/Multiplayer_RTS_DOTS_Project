using Buildings;
using Combat;
using ElementCommons;
using GatherableResources;
using UI.UIControllers;
using Units.Worker;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace UI
{
    [UpdateAfter(typeof(TransformSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class UserInterfaceDetailsSystem : SystemBase
    {
        private SelectedDetailsDisplayController _selectionDetailsController;

        private bool _isSelecting; 

        private Entity _trackedEntity;

        protected override void OnCreate()
        {
            RequireForUpdate<OwnerTagComponent>();
            RequireForUpdate<UISceneReferenceComponent>();
        }

        protected override void OnStartRunning()
        {
            UISceneReferenceComponent uiSceneReferenceComponent = SystemAPI.ManagedAPI.GetSingleton<UISceneReferenceComponent>();
            _selectionDetailsController = uiSceneReferenceComponent.UIReference.SelectedDetailsController;
            base.OnStartRunning();
        }

        protected override void OnUpdate()
        {
            EntityCommandBuffer entityCommandBuffer = new EntityCommandBuffer(Allocator.Temp);
            foreach ((SetUIDisplayDetailsComponent detailsComponent, Entity entity) 
                     in SystemAPI.Query<SetUIDisplayDetailsComponent>().WithEntityAccess())
            {
                _trackedEntity = detailsComponent.Entity;
                _isSelecting = true;
                _selectionDetailsController.EnableDetails();
                SetTrackedEntityDetails();
                entityCommandBuffer.RemoveComponent<SetUIDisplayDetailsComponent>(entity);
            }
            
            foreach ((SetEmptyDetailsComponent _, Entity entity) 
                     in SystemAPI.Query<SetEmptyDetailsComponent>().WithEntityAccess())
            {
                _selectionDetailsController.DisableDetails();
                _isSelecting = false;
                entityCommandBuffer.RemoveComponent<SetEmptyDetailsComponent>(entity);
            }
            
            foreach ((SelectableElementTypeComponent _, Entity entity) 
                     in SystemAPI.Query<SelectableElementTypeComponent>().WithEntityAccess())
            {
                if(entity != _trackedEntity && !_isSelecting)
                {
                    continue;
                }

                SetTrackedEntityDetails();
                entityCommandBuffer.RemoveComponent<UpdateResourcesPanelTag>(entity);
            }

            entityCommandBuffer.Playback(EntityManager);
        }

        private void SetTrackedEntityDetails()
        {
            SetName();
            SetHitPoints();
            SetResources();
        }

        private void SetResources()
        {
            if (!EntityManager.Exists(_trackedEntity))
            {
                _selectionDetailsController.DisableResources();
                return;
            }

            if (EntityManager.HasComponent<BuildingConstructionProgressComponent>(_trackedEntity))
            {
                BuildingConstructionProgressComponent constructionProgress =
                    EntityManager.GetComponentData<BuildingConstructionProgressComponent>(_trackedEntity);

                if(constructionProgress.Value >= constructionProgress.ConstructionTime)
                {
                    _selectionDetailsController.DisableResources();
                    return;
                }

                float progressPercentage = (constructionProgress.Value / constructionProgress.ConstructionTime) * 100f;
                int progressInt = UnityEngine.Mathf.Clamp((int)progressPercentage, 0, 100);

                _selectionDetailsController.DisableResourceImage();
                _selectionDetailsController.EnableResources();
                _selectionDetailsController.SetResourcesText($"{progressInt}%");
                return;
            }

            if (EntityManager.HasComponent<CurrentWorkerResourceQuantityComponent>(_trackedEntity))
            {
                CurrentWorkerResourceQuantityComponent resourceComponent =
                    EntityManager.GetComponentData<CurrentWorkerResourceQuantityComponent>(_trackedEntity);

                int value = resourceComponent.Value;
                _selectionDetailsController.EnableResourceImage();
                EnableResources(value);
                _selectionDetailsController.SetResourcesText(value.ToString());
                return;
            }

            _selectionDetailsController.DisableResources();
        }

        private void EnableResources(int value)
        {
            if(value <= 0)
            {
                _selectionDetailsController.DisableResources();
            }
            else
            {
                _selectionDetailsController.EnableResources();
            }
        }

        private void SetHitPoints()
        {
            int currentHitPoints = EntityManager.GetComponentData<CurrentHitPointsComponent>(_trackedEntity).Value;
            int maxHitPoints = EntityManager.GetComponentData<MaxHitPointsComponent>(_trackedEntity).Value;
            _selectionDetailsController.UpdateHitPoints(currentHitPoints, maxHitPoints);
        }

        private void SetName()
        {
            EntityManager.GetComponentData<ElementDisplayDetailsComponent>(_trackedEntity);
            ElementDisplayDetailsComponent details = EntityManager.GetComponentData<ElementDisplayDetailsComponent>(_trackedEntity);
            _selectionDetailsController.SetName(details.Name);
            _selectionDetailsController.SetImage(details.Sprite);
        }
    }
}