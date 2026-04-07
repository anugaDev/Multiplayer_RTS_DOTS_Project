using Buildings;
using ElementCommons;
using Types;
using UI;
using UI.UIControllers;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PlayerInputs
{
    [UpdateInGroup(typeof(GhostInputSystemGroup))]
    public partial class ElementsSelectionInputSystem : SystemBase
    {
        private const float CLICK_SELECTION_SIZE = 3f;

        private const float CLICK_DISTANCE_THRESHOLD = 10f;

        private const float CLICK_TIME_THRESHOLD = 0.3f;
        
        private UserInterfaceController _sceneReferenceComponent;

        private CheckGameplayInteractionPolicy _interactionPolicy;
        
        private InputActions _inputActionMap;

        private Vector2 _startingPosition;
        
        private Vector2 _lastPosition;

        private float _selectionStartTime;

        private bool _mustKeepSelection;

        private bool _isClickSelection;
        
        private bool _isAvailable;

        private bool _isDragging;

        protected override void OnCreate()
        {
            _interactionPolicy = new CheckGameplayInteractionPolicy();
            _inputActionMap = new InputActions();
            RequireForUpdate<UISceneReferenceComponent>();
            RequireForUpdate<OwnerTagComponent>();
            RequireForUpdate<NetworkTime>();
        }

        protected override void OnStartRunning()
        {
            _sceneReferenceComponent = SystemAPI.ManagedAPI.GetSingleton<UISceneReferenceComponent>().UIReference;
            _inputActionMap.Enable();
            _inputActionMap.GameplayMap.SelectGameEntity.started += StartBoxSelection;
            _inputActionMap.GameplayMap.SelectGameEntity.canceled += EndSelectionBox;
        }
        
        protected override void OnStopRunning()
        {
            _inputActionMap.GameplayMap.SelectGameEntity.started -= StartBoxSelection;
            _inputActionMap.GameplayMap.SelectGameEntity.canceled -= EndSelectionBox;
            _inputActionMap.Disable();
        }

        private void StartBoxSelection(InputAction.CallbackContext _)
        {
            CheckInteractionAvailable();
            _isDragging = _isAvailable;
            EnableBoxSelection();
        }
        
        private void CheckInteractionAvailable()
        {
            foreach (SetPlayerUIActionComponent playerUIActionComponent in SystemAPI.Query<SetPlayerUIActionComponent>())
            {
                if (playerUIActionComponent.Action == PlayerUIActionType.Build)
                {
                    _isAvailable = false;
                }
            }

            _isAvailable = _interactionPolicy.IsAllowed();
        }

        private void EnableBoxSelection()
        {
            if (!_isDragging)
            {
                return;
            }

            _sceneReferenceComponent.SelectionBoxController.Enable();
            _startingPosition = GetPointerPosition();
            _lastPosition = _startingPosition;
            _selectionStartTime = UnityEngine.Time.time;
        }

        private void UpdateBoxSelection()
        {
            if (!_isDragging)
            {
                return;
            }

            _mustKeepSelection = _inputActionMap.GameplayMap.KeepSelectionKey.IsPressed();
            _lastPosition = GetPointerPosition();
            _sceneReferenceComponent.SelectionBoxController.UpdateBoxSize(_startingPosition, _lastPosition);
        }

        private void EndSelectionBox(InputAction.CallbackContext _)
        {
            _isDragging = false;
            _lastPosition = GetPointerPosition();
            _sceneReferenceComponent.SelectionBoxController.Disable();
            if (!_interactionPolicy.IsAllowed())
            {
                return;
            }

            SelectElements();
        }
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    
        private Vector2 GetPointerPosition()
        {
            return _inputActionMap.GameplayMap.PointerPosition.ReadValue<Vector2>();
        }

        private void SelectElements()
        {
            NormalizeSelectionClick();
            EntityCommandBuffer entityCommandBuffer = new EntityCommandBuffer(Allocator.Temp);
            TeamType clientTeam = GetClientTeam();

            foreach ((RefRO<SelectableElementTypeComponent> _, ElementTeamComponent elementTeam, Entity entity) in
                     SystemAPI.Query<RefRO<SelectableElementTypeComponent>, ElementTeamComponent>().WithEntityAccess())
            {
                if (elementTeam.Team == clientTeam)
                {
                    entityCommandBuffer.AddComponent(entity, GetUnitPositionComponent());
                }
            }

            entityCommandBuffer.Playback(EntityManager);
            entityCommandBuffer.Dispose();
        }

        private TeamType GetClientTeam()
        {
            foreach (PlayerTeamComponent playerTeam in
                     SystemAPI.Query<PlayerTeamComponent>().WithAll<OwnerTagComponent>())
            {
                return playerTeam.Team;
            }

            return TeamType.Red;
        }

        private void NormalizeSelectionClick()
        {
            float distance = Vector2.Distance(_startingPosition, _lastPosition);
            float selectionDuration = UnityEngine.Time.time - _selectionStartTime;

            _isClickSelection = distance < CLICK_DISTANCE_THRESHOLD && selectionDuration < CLICK_TIME_THRESHOLD;

            if (_isClickSelection)
            {
                _startingPosition -= Vector2.one * CLICK_SELECTION_SIZE;
                _lastPosition   += Vector2.one * CLICK_SELECTION_SIZE;
            }
        }

        private NewSelectionComponent GetUnitPositionComponent()
        {
            return new NewSelectionComponent
            {
                SelectionRect = GetBoxScreenRect(_startingPosition, _lastPosition),
                MustKeepSelection = _mustKeepSelection,
                IsClickSelection = _isClickSelection
            };
        }

        private Rect GetBoxScreenRect(Vector2 startingPosition, Vector2 endingPosition)
        {
            Vector2 min = Vector2.Min(startingPosition, endingPosition);
            Vector2 max = Vector2.Max(startingPosition, endingPosition);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        protected override void OnUpdate()
        {
            UpdateBoxSelection();
        }
    }
}