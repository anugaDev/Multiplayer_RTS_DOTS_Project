using System;
using Cinemachine;
using Client;
using ElementCommons;
using Types;
using Units;
using Unity.Entities;
using UnityEngine;

namespace PlayerCamera
{
    public class CameraController : MonoBehaviour
    { 
        [SerializeField] 
        private CinemachineVirtualCamera _cinemachineVirtualCamera;

        [Header("Move Settings")] 

        [SerializeField]
        private bool _drawBounds;

        [SerializeField] 
        private Bounds _cameraBounds;
        
        [SerializeField] 
        private float _camSpeed;
        
        [SerializeField] 
        private Vector2 _screenPercentageDetection;

        [Header("Zoom Settings")] 

        [SerializeField]
        private float _minZoomDistance;

        [SerializeField] 
        private float _maxZoomDistance;

        [SerializeField] 
        private float _zoomSpeed;

        [Header("Camera Start Positions")] 
        
        [SerializeField]
        private Vector3 _redTeamPosition = new(GlobalParameters.MAP_EXTREME_AXIS, GlobalParameters.MAP_EXTREME_AXIS, GlobalParameters.MAP_EXTREME_AXIS);

        [SerializeField] 
        private Vector3 _blueTeamPosition = new(-GlobalParameters.MAP_EXTREME_AXIS, GlobalParameters.MAP_EXTREME_AXIS, -GlobalParameters.MAP_EXTREME_AXIS);

        [SerializeField] 
        private Vector3 _spectatorPosition = new(0f, GlobalParameters.MAP_EXTREME_AXIS, 0f);

        private CinemachineFramingTransposer _transposer;

        private EntityManager _entityManager;

        private EntityQuery _teamControllerQuery;

        private EntityQuery _localChampQuery;

        private bool _cameraSet;

        public event Action<Vector3> OnPositionUpdated;

        public event Action<float> OnCameraZoomed;

        private void Awake()
        {
            _transposer = _cinemachineVirtualCamera.GetCinemachineComponent<CinemachineFramingTransposer>();
        }

        private void Start()
        {
            if (World.DefaultGameObjectInjectionWorld == null) return;
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            _teamControllerQuery = _entityManager.CreateEntityQuery(typeof(ClientTeamRequest));
            _localChampQuery = _entityManager.CreateEntityQuery(typeof(OwnerTagComponent));
            SetCameraToOwnerTeam();
        }

        private void SetCameraToOwnerTeam()
        {
            if (!_teamControllerQuery.TryGetSingleton(out ClientTeamRequest requestedTeam))
            {
                return;
            }

            SetCameraTeamStartingPosition(requestedTeam);
        }

        private void SetCameraTeamStartingPosition(ClientTeamRequest requestedTeam)
        {
            TeamType team = requestedTeam.Value;
            Vector3 cameraPosition = GetCameraPosition(team);
            transform.position = cameraPosition;

            if (team != TeamType.AutoAssign)
            {
                _cameraSet = true;
                SetOnPositionUpdated();
                SetOnCameraZoomed();
            }
        }

        private Vector3 GetCameraPosition(TeamType team)
        {
            return team switch
            {
                TeamType.Blue => _blueTeamPosition,
                TeamType.Red => _redTeamPosition,
                _ => _spectatorPosition
            };
        }

        private void Update()
        {
            SetCameraForAutoAssignTeam();
            MoveCamera();
            ZoomCamera();
        }

        private void MoveCamera()
        {
            if (Input.GetKey(KeyCode.A))
            {
                UpdateCameraPosition(Vector3.left);
            }

            if (Input.GetKey(KeyCode.D))
            {
                UpdateCameraPosition(Vector3.right);
            }

            if (Input.GetKey(KeyCode.S))
            {
                UpdateCameraPosition(Vector3.back);
            }

            if (Input.GetKey(KeyCode.W))
            {
                UpdateCameraPosition(Vector3.forward);
            }

            if (!_cameraBounds.Contains(transform.position))
            {
                SetCameraToBounds();
            }
        }

        private void SetCameraToBounds()
        {
            transform.position = _cameraBounds.ClosestPoint(transform.position);
            SetOnPositionUpdated();
        }

        private void SetOnPositionUpdated()
        {
            OnPositionUpdated?.Invoke(transform.position);
        }

        private void UpdateCameraPosition(Vector3 direction)
        {
            transform.position += direction * (_camSpeed * Time.deltaTime);
            SetOnPositionUpdated();
        }

        private void ZoomCamera()
        {
            if (!(Mathf.Abs(Input.mouseScrollDelta.y) > float.Epsilon))
            {
                return;
            }

            UpdateCameraZoom();
        }

        private void UpdateCameraZoom()
        {
            _transposer.m_CameraDistance -= Input.mouseScrollDelta.y * _zoomSpeed * Time.deltaTime;
            _transposer.m_CameraDistance =
                Mathf.Clamp(_transposer.m_CameraDistance, _minZoomDistance, _maxZoomDistance);
            SetOnCameraZoomed();
        }

        private void SetOnCameraZoomed()
        {
            float zoomCameraDistance = _transposer != null ? _transposer.m_CameraDistance : 0f;
            OnCameraZoomed?.Invoke(zoomCameraDistance);
        }

        private void SetCameraForAutoAssignTeam()
        {
            if (_cameraSet || !_localChampQuery.TryGetSingletonEntity<OwnerTagComponent>(out Entity localUnit))
            {
                return;
            }

            TeamType team = _entityManager.GetComponentData<ElementTeamComponent>(localUnit).Team;
            SetInitialCameraPosition(team);
        }

        private void SetInitialCameraPosition(TeamType team)
        {
            Vector3 cameraPosition = GetCameraPosition(team);
            transform.position = cameraPosition;
            _cameraSet = true;
            SetOnPositionUpdated();
            SetOnCameraZoomed();
        }

        private void OnDrawGizmos()
        {
            if (!_drawBounds) return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(_cameraBounds.center, _cameraBounds.size);
        }

        public void SetCameraPosition(Vector3 position)
        {
            transform.position = position;
        }

    }
}