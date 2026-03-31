using PlayerCamera;
using UnityEngine;

namespace UI.UIControllers
{
    public class UserInterfaceController : MonoBehaviour
    {
        public static UserInterfaceController Instance;

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

        public SelectionActionsDisplayController SelectionActionsDisplayerController => _selectionActionsDisplayerController;

        public SelectedDetailsDisplayController SelectedDetailsController => _selectedDetailsController;

        public SelectedGroupDisplayController SelectedGroupController => _selectedGroupController;

        public ResourcesPanelController ResourcesPanelController => _resourcesPanelController;

        public GameOverScreenController GameOverScreenController => _gameOverScreenController;
        
        public SelectionBoxController SelectionBoxController => _selectionBoxController;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _selectionBoxController.Disable();
            AddListeners();
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
        }

        private void RemoveListeners()
        {
            _cameraController.OnPositionUpdated -= _minimapController.UpdateCameraIndicatorPosition;
            _cameraController.OnCameraZoomed -= _minimapController.UpdateCameraIndicatorSize;
            _minimapController.OnMinimapClicked -= _cameraController.SetCameraPosition;
            _minimapController.OnMinimapDragged -= _cameraController.SetCameraPosition;
        }
    }
}