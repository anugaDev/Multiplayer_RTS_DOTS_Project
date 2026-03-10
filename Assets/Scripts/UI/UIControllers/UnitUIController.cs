using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Utils;

namespace UI
{
    public class UnitUIController : MonoBehaviour
    {
        [SerializeField]
        private Slider _healthBarSlider;
        
        [SerializeField]
        private Image _healthBarImage;
        
        [SerializeField]
        private Canvas _healthbarCanvas;

        [SerializeField]
        private Canvas _selectionCanvas; 
        
        [SerializeField]
        private Transform _healthBarTransform; 
        
        [SerializeField]
        private RectTransform _selectionTransform; 

        [SerializeField]
        private Image _selectionImage;
        
        [SerializeField]
        private Renderer _minimapRenderer;

        public void UpdateHealthBar(int curHitPoints, int maxHitPoints)
        {
            _healthBarSlider.minValue = 0;
            _healthBarSlider.maxValue = maxHitPoints;
            _healthBarSlider.value = curHitPoints;
        }

        public void SetRectTransform(float sizeX, float sizeY)
        {
            _selectionTransform.sizeDelta = new Vector2(sizeX, sizeY);
        }

        public void SetTeamColor(Color color)
        {
            _healthBarImage.color = color;
            _selectionImage.color = color;
            _minimapRenderer.material.color = color;
        }

        public void EnableUI()
        {
            EnableHealthBar();
            _selectionCanvas.enabled = true;
        }

        public void EnableHealthBar()
        {
            _healthbarCanvas.enabled = true;
        }

        public void SetHealthBarOffset(float3 offset)
        {
            _healthBarTransform.localPosition = offset;
        }

        public void DisableUI()
        {
            _healthbarCanvas.enabled = false;
            _selectionCanvas.enabled = false;
        }
    }
}