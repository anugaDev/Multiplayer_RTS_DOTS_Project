using System;
using System.Collections;
using System.Collections.Generic;
using GatherableResources;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

public class ActionCostPopUpView : MonoBehaviour
{
    [SerializeField]
    private GameObject _gameObject;

    [SerializeField]
    private RectTransform _rectTransform;

    [SerializeField] 
    private TextMeshProUGUI _titleText;

    [SerializeField]
    private List<ResourcePanelCostController>  _resourceCostControllers; 

    private bool _isEnabled;

    public void SetTitleText(string title)
    {
        _titleText.text = title;
    }

    public void Enable()
    {
        _isEnabled = true;
        _gameObject.SetActive(true);
        SetPosition(Input.mousePosition);
    }

    public void Disable()
    {
        _isEnabled = false;
        _gameObject.SetActive(false);
    }

    private void SetPosition(float3 position)
    {
        _rectTransform.anchoredPosition = new Vector2(position.x, position.y);
    }
    
    public void SetCostTexts(List<ResourceCostEntity> resourceCost)
    {
        foreach (ResourcePanelCostController costController in _resourceCostControllers)
        {
            SetResourceText(resourceCost, costController);
        }
    }

    private void SetResourceText(List<ResourceCostEntity> resourceCost, ResourcePanelCostController costController)
    {
        if (!resourceCost.Exists(IsSameResource(costController)))
        {
            costController.Disable();
            return;
        }

        costController.Enable();
        ResourceCostEntity resourceCostEntity = resourceCost.Find(IsSameResource(costController));
        costController.SetText(resourceCostEntity.Cost.ToString());
    }

    private Predicate<ResourceCostEntity> IsSameResource(ResourcePanelCostController costController)
    {
        return resourceCost => resourceCost.ResourceType == costController.ResourceType;
    }

    private void FixedUpdate()
    {
        UpdatePosition();
    }

    private void UpdatePosition()
    {
        if (!_isEnabled)
        {
            return;
        }
        
        SetPosition(Input.mousePosition);
    }
}