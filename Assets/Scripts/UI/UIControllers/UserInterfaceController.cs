using PlayerCamera;
using Unity.Entities;
using UnityEngine;

namespace UI.UIControllers
{
    public class UserInterfaceController : MonoBehaviour
    {
        [SerializeField]
        private SelectionBoxController _selectionBoxController;

        [SerializeField]
        private ResourcesPanelController _resourcesPanelController;

        [SerializeField]
        private SelectionActionsDisplayController _selectionActionsDisplayerController;

        [SerializeField]
        private SelectedDetailsDisplayController _selectedDetailsController;
        
        [SerializeField]
        private SelectedGroupDisplayController _selectedGroupController;

        [SerializeField]
        private GameOverScreenController _gameOverScreenController;

        [SerializeField]
        private MinimapController _minimapController;
        
        [SerializeField]
        private CameraController _cameraController;
        

        private EntityManager _entityManager;
        
        private Entity _uiEntity;

        public SelectionActionsDisplayController SelectionActionsDisplayerController => _selectionActionsDisplayerController;

        public SelectedDetailsDisplayController SelectedDetailsController => _selectedDetailsController;

        public SelectedGroupDisplayController SelectedGroupController => _selectedGroupController;

        public ResourcesPanelController ResourcesPanelController => _resourcesPanelController;

        public GameOverScreenController GameOverScreenController => _gameOverScreenController;
        
        public SelectionBoxController SelectionBoxController => _selectionBoxController;

        private void Awake()
        {
            _selectionBoxController.Disable();
            AddListeners();
            AddUIReference();
        }

        private void AddUIReference()
        {
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            _uiEntity = _entityManager.CreateEntity();
            _entityManager.AddComponentObject(_uiEntity, new UISceneReferenceComponent() 
            { 
                UIReference = this 
            });
        }

        private void AddListeners()
        {
            _cameraController.OnPositionUpdated += _minimapController.UpdateCameraIndicatorPosition;
            _cameraController.OnCameraZoomed += _minimapController.UpdateCameraIndicatorSize;
            _minimapController.OnMinimapClicked += _cameraController.SetCameraPosition;
            _minimapController.OnMinimapDragged += _cameraController.SetCameraPosition;
        }

        private void OnDestroy()
        {
            RemoveListeners();
            RemoveSceneReference();
        }

        private void RemoveListeners()
        {
            _cameraController.OnPositionUpdated -= _minimapController.UpdateCameraIndicatorPosition;
            _cameraController.OnCameraZoomed -= _minimapController.UpdateCameraIndicatorSize;
            _minimapController.OnMinimapClicked -= _cameraController.SetCameraPosition;
            _minimapController.OnMinimapDragged -= _cameraController.SetCameraPosition;
        }
        
        private void RemoveSceneReference()
        {
            if (World.DefaultGameObjectInjectionWorld != null && _entityManager.Exists(_uiEntity))
            {
                _entityManager.DestroyEntity(_uiEntity);
            }
        }
    }
}