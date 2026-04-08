using System;
using System.Collections.Generic;
using System.Linq;
using Audio;
using Buildings;
using ElementCommons;
using Types;
using UI.Entities;
using Units;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace UI
{
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    public partial class UserInterfaceUpdateSelectionSystem : SystemBase
    {
        private SelectableElementType _currentSelection;

        private Dictionary<SelectionEntity, bool> _unitTypesSelected;

        private Dictionary<SelectionEntity, bool> _buildingTypesSelected;

        private BuildingFactoryActionsFactory _buildingActionsFactory;

        private Dictionary<SelectableElementType, Action> _selectableToAction;

        private EntityCommandBuffer _entityCommandBuffer;

        private DynamicBuffer<UpdateUIActionPayload> _payloadActionsBuffer;

        private Entity _UIUpdateEntity;

        protected override void OnCreate()
        {
            RequireForUpdate<PlayerTagComponent>();
            _buildingActionsFactory = new BuildingFactoryActionsFactory();
            _buildingTypesSelected = new Dictionary<SelectionEntity, bool>();
            _unitTypesSelected = new Dictionary<SelectionEntity, bool>();
            FillSelectableDictionary();
            base.OnCreate();
        }

        private void FillSelectableDictionary()
        {
            _selectableToAction = new Dictionary<SelectableElementType, Action>
            {
                [SelectableElementType.Building] = SetBuildingActions,
                [SelectableElementType.Resource] = SetNoneSelected,
                [SelectableElementType.Unit] = SetUnitActions,
                [SelectableElementType.None] = SetNoneSelected
            };
        }

        protected override void OnUpdate()
        {
            _entityCommandBuffer = new EntityCommandBuffer(Allocator.Temp);
            CheckUnitsSelection();
            SetBuildingsSelection();
            CheckUISelection();
            ResetSelectionData();
            _entityCommandBuffer.Playback(EntityManager);
        }

        private void CheckUnitsSelection()
        {
            foreach ((ElementSelectionComponent selectionComponent, UnitTypeComponent unitTypeComponent, Entity entity)
                     in SystemAPI.Query<ElementSelectionComponent, UnitTypeComponent>().WithEntityAccess())
            {
                CheckUnitSelection(selectionComponent, unitTypeComponent, entity);
            }

            SetSelectionAsUnit();
        }

        private void SetSelectionAsUnit()
        {
            if (_unitTypesSelected.ContainsValue(true))
            {
                _currentSelection = SelectableElementType.Unit;
            }
            else if(_unitTypesSelected.Any())
            {
                _currentSelection = SelectableElementType.None;
            }
        }

        private void CheckUnitSelection(ElementSelectionComponent selectionComponent, UnitTypeComponent unitTypeComponent, Entity entity)
        {
            if (!selectionComponent.MustUpdateUI)
            {
                return;
            }

            selectionComponent.MustUpdateUI = false;
            SelectionEntity selectionEntity = GetNewSelectionEntity(_unitTypesSelected.Keys.ToList(), (int)unitTypeComponent.Type, entity);
            _unitTypesSelected.Add(selectionEntity, selectionComponent.IsSelected);
            SetComponentData(selectionComponent, entity);
        }

        private void SetBuildingsSelection()
        {
            CheckBuildingsSelection();
        }

        private void CheckBuildingsSelection()
        {
            foreach ((ElementSelectionComponent selectionComponent, BuildingTypeComponent buildingTypeComponent, Entity entity)
                     in SystemAPI.Query<ElementSelectionComponent, BuildingTypeComponent>().WithEntityAccess())
            {
                CheckBuildingSelection(selectionComponent, buildingTypeComponent, entity);
            }

            SetSelectionAsBuilding();
        }

        private void SetSelectionAsBuilding()
        {
            if (_buildingTypesSelected.ContainsValue(true))
            {
                _currentSelection = SelectableElementType.Building;
            }
            else if (_buildingTypesSelected.Any() && _currentSelection != SelectableElementType.Unit)
            {
                _currentSelection = SelectableElementType.None;
            }
        }

        private void CheckBuildingSelection(ElementSelectionComponent selectionComponent,
            BuildingTypeComponent buildingTypeComponent, Entity entity)
        {
            if (!selectionComponent.MustUpdateUI)
            {
                return;
            }

            selectionComponent.MustUpdateUI = false;
            SelectionEntity selectionEntity = GetNewSelectionEntity(_buildingTypesSelected.Keys.ToList(), (int)buildingTypeComponent.Type, entity);
            _buildingTypesSelected.Add(selectionEntity, selectionComponent.IsSelected); 
            SetComponentData(selectionComponent, entity);
        }

        private void SetComponentData(ElementSelectionComponent selectionComponent, Entity entity)
        {
            _entityCommandBuffer.SetComponent(entity, selectionComponent);
        }

        private SelectionEntity GetNewSelectionEntity(List<SelectionEntity> selectionEntities, int type, Entity entity)
        {
            int lastTypeId = -1;
            
            if (selectionEntities.Any(entity => entity.Type == type))
            {
                lastTypeId = selectionEntities.Last(entity => entity.Type == type).Id;
            }

            return new SelectionEntity(lastTypeId + 1, type, entity);
        }

        private void ResetSelectionData()
        {
            _currentSelection = SelectableElementType.Empty;
            _buildingTypesSelected.Clear();
            _unitTypesSelected.Clear();
        }

        private void CheckUISelection()
        {
            if (_currentSelection is SelectableElementType.Empty)
            {
                return;
            }

            _UIUpdateEntity = SystemAPI.GetSingletonEntity<PlayerTagComponent>();
            _payloadActionsBuffer = EntityManager.GetBuffer<UpdateUIActionPayload>(_UIUpdateEntity);
            _payloadActionsBuffer.Clear();
            SetUISelectionBySelected();
        }

        private void SetUISelectionBySelected()
        {
            _selectableToAction[_currentSelection]?.Invoke();
            SetDetailsDisplay();
        }

        private void SetDetailsDisplay()
        {
            if(_currentSelection is SelectableElementType.None)
            {
                SendEmptyDetails();
                return;
            }

            SetSelectionSoundFeedback();
            Entity detailsEntity = GetDetailsEntity();
            _UIUpdateEntity = SystemAPI.GetSingletonEntity<PlayerTagComponent>();
            _entityCommandBuffer.AddComponent(_UIUpdateEntity, GetDetailsComponent(detailsEntity));
        }

        private void SetSelectionSoundFeedback()
        {
            Entity audioEntity = SystemAPI.ManagedAPI.GetSingletonEntity<AudioManagerReferenceComponent>();
            AudioRequestComponent audioRequest = new AudioRequestComponent
            {
                AudioId = AudioSourceType.SelectedEntity,
                Is3D = false
            };

            EntityManager.SetComponentData(audioEntity, audioRequest);
        }

        private void SendEmptyDetails()
        {
            _entityCommandBuffer.AddComponent(_UIUpdateEntity, new SetEmptyDetailsComponent());
        }

        private SetUIDisplayDetailsComponent GetDetailsComponent(Entity detailsEntity)
        {
            return new SetUIDisplayDetailsComponent
            {
                Entity = detailsEntity
            };
        }

        private Entity GetDetailsEntity()
        {
            if (_currentSelection is SelectableElementType.Building)
            {
                return _buildingTypesSelected.First(building => building.Value).Key.SelectedEntity;
            }

            if (_unitTypesSelected.Any(unit => unit.Value && unit.Key.Type is (int)UnitType.Worker))
            {
                return _unitTypesSelected.First(unit => unit.Value && unit.Key.Type is (int)UnitType.Worker).Key.SelectedEntity;
            }

            return _unitTypesSelected.First().Key.SelectedEntity;
        }

        private void SetNoneSelected()
        {
            SetEmptyPayloadActionComponent(PlayerUIActionType.None);
        }

        private void SetUnitActions()
        {
            if (!_unitTypesSelected.Any(unit =>unit.Value && unit.Key.Type is (int)UnitType.Worker))
            {
                SetNoneSelected();
                return;
            }

            SetActionComponent(PlayerUIActionType.Build, GetBuildingsAsPayload());
        }

        private int[] GetBuildingsAsPayload()
        {
            return new[]
            {
                (int)BuildingType.Barracks,
                (int)BuildingType.Center,
                (int)BuildingType.House,
                (int)BuildingType.Farm,
                (int)BuildingType.Tower
            };
        }

        private void SetBuildingActions()
        {
            Entity selectedBuilding = _buildingTypesSelected.First(building => building.Value).Key.SelectedEntity;

            if (EntityManager.HasComponent<BuildingConstructionProgressComponent>(selectedBuilding))
            {
                BuildingConstructionProgressComponent progress =
                    EntityManager.GetComponentData<BuildingConstructionProgressComponent>(selectedBuilding);

                if (progress.ConstructionTime <= 0 || progress.Value < progress.ConstructionTime)
                {
                    SetNoneSelected();
                    return;
                }
            }

            _buildingActionsFactory.Set((BuildingType)_buildingTypesSelected.First(building => building.Value).Key.Type);
            PlayerUIActionType action = _buildingActionsFactory.Get();
            int[] payload = _buildingActionsFactory.GetPayload(action);
            SetActionComponent(action, payload);
        }

        private void SetEmptyPayloadActionComponent(PlayerUIActionType action)
        {
            int[] emptyPayload = { -1 };
            SetActionComponent(action, emptyPayload);
        }

        private void SetActionComponent(PlayerUIActionType action, int[] payload)
        {
            Entity UIUpdateEntity = SystemAPI.GetSingletonEntity<PlayerTagComponent>();
            _entityCommandBuffer.AddComponent(UIUpdateEntity, new UpdateUIActionTag());

            foreach (int payloadId in payload)
            {
                _payloadActionsBuffer.Add(GetUpdateUIActioNPayload(action, payloadId));
            }
        }

        private UpdateUIActionPayload GetUpdateUIActioNPayload(PlayerUIActionType action, int payloadId)
        {
            return new UpdateUIActionPayload
            {
                Action = action,
                PayloadID = payloadId
            };
        }
    }
}