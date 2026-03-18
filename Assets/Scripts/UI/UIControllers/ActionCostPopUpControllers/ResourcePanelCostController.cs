using System;
using TMPro;
using Types;
using UnityEngine;

[Serializable]
public class ResourcePanelCostController
{
    [SerializeField]
    private ResourceType _resourceType;

    [SerializeField]
    private GameObject _gameObject;

    [SerializeField]
    private TextMeshProUGUI _text;

    public ResourceType ResourceType => _resourceType;

    public void SetText(string text)
    {
        _text.text = text;
    }

    public void Enable()
    {
        _gameObject.SetActive(true);
    }

    public void Disable()
    {
        _gameObject.SetActive(false);
    }
}
