using System;
using Buildings;
using ScriptableObjects;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI.UIControllers
{
    public class ActionButtonController : MonoBehaviour, IPointerEnterHandler,  IPointerExitHandler
    {
        private const int TOP_RIGHT_CORNER_INDEX = 2;

        [SerializeField]
        private Button _button;

        [SerializeField]
        private TextMeshProUGUI _text;

        [SerializeField]
        private Image _displayImage;

        [SerializeField]
        private Image _feedbackImage;
        
        [SerializeField]
        private RectTransform _transform;

        [SerializeField]
        private GameObject _parent;

        [SerializeField] 
        private Color _enabledColor;

        [SerializeField]
        private Color _disabledColor;
        
        private SetPlayerUIActionComponent _componentData;

        public Action<SetPlayerUIActionComponent> OnClick;
        
        public Action<ActionPopUpPayload, Vector2> OnEnter;
        
        public Action OnExit;
        
        private ActionPopUpPayload _popUpPayload;

        public void Initialize(SetPlayerUIActionComponent componentData, ActionPopUpPayload popupPayload, Sprite image)
        {
            _componentData = componentData;
            _text.text = popupPayload.Name;
            _displayImage.sprite = image;
            _popUpPayload = popupPayload;
            _button.onClick.AddListener(SendAction);
        }

        public PlayerUIActionType GetActionType()
        {
            return _componentData.Action;
        }

        public int GetPayloadId()
        {
            return _componentData.PayloadID;
        }

        private void SendAction()
        {
            OnClick.Invoke(_componentData);
        }
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            Vector3[] corners = new Vector3[4];
            _transform.GetWorldCorners(corners);
            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null, corners[TOP_RIGHT_CORNER_INDEX]);
            OnEnter?.Invoke(_popUpPayload, screenPos);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            OnExit?.Invoke();
        }

        private void OnDestroy()
        {
            _button.onClick.RemoveAllListeners();
        }

        public void Show()
        {
            _parent.SetActive(true);
        }

        public void Hide()
        {
            _parent.SetActive(false);
        }

        public void Enable()
        {
            _button.interactable = true;
            _feedbackImage.color = _enabledColor;
        }

        public void Disable()
        {
            _feedbackImage.color = _disabledColor;
            _button.interactable = false;
        }
    }
}