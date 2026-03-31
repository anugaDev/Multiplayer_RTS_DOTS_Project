using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine.SceneManagement;

namespace UI.UIControllers
{
    public class GameOverScreenController : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject _rootPanel;

        [Header("Labels")]
        [SerializeField] private TextMeshProUGUI _titleLabel;
        
        [SerializeField] private TextMeshProUGUI _subtitleLabel;

        [Header("Button")]
        [SerializeField] private Button _mainButton;
        
        [SerializeField] private TextMeshProUGUI _buttonLabel;

        [Header("Victory Config")]
        [SerializeField] private string _victoryTitle;
        
        [SerializeField] private string _victorySubtitle;
        
        [SerializeField] private Color  _victoryColor;

        [Header("Defeat Config")]
        [SerializeField] private string _defeatTitle;
       
        [SerializeField] private string _defeatSubtitle;
        
        [SerializeField] private Color  _defeatColor;

        private void Awake()
        {
            _mainButton.onClick.AddListener(OnExitGameButtonClicked);
        }

        private void OnDestroy()
        {
            if (_mainButton == null)
            {
                return;
            }

            _mainButton.onClick.RemoveListener(OnExitGameButtonClicked);
        }

        public void Show(bool isVictory)
        {
            if (_rootPanel != null)
                _rootPanel.SetActive(true);

            if (isVictory)
            {
                SetText(_titleLabel,    _victoryTitle,    _victoryColor);
                SetText(_subtitleLabel, _victorySubtitle, Color.white);
            }
            else
            {
                SetText(_titleLabel,    _defeatTitle, _defeatColor);
                SetText(_subtitleLabel, _defeatSubtitle, Color.white);
            }
        }

        private void SetText(TextMeshProUGUI label, string text, Color color)
        {
            if (label == null) return;
            label.text  = text;
            label.color = color;
        }

        private void OnExitGameButtonClicked()
        {
            EntityQuery networkConnectionQuery =
                World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntityQuery(typeof(NetworkStreamConnection));

            if (networkConnectionQuery.TryGetSingletonEntity<NetworkStreamConnection>(out var networkConnectionEntity))
            {
                World.DefaultGameObjectInjectionWorld.EntityManager.AddComponent<NetworkStreamRequestDisconnect>(
                    networkConnectionEntity);
            }

            World.DisposeAllWorlds();
            SceneManager.LoadScene(GlobalParameters.MENU_SCENE_INDEX);
        }
    }
}
