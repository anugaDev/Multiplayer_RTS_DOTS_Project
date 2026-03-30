using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace UI.UIControllers
{
    public class GameOverScreenController : MonoBehaviour
    {
        public static GameOverScreenController Instance { get; private set; }

        [Header("Panel")]
        [SerializeField] private GameObject _rootPanel;

        [Header("Labels")]
        [SerializeField] private TextMeshProUGUI _titleLabel;
        
        [SerializeField] private TextMeshProUGUI _subtitleLabel;

        [Header("Button")]
        [SerializeField] private Button _mainButton;
        
        [SerializeField] private TextMeshProUGUI _buttonLabel;

        [Header("Victory Config")]
        [SerializeField] private string _victoryTitle    = "VICTORY";
        
        [SerializeField] private string _victorySubtitle = "You have conquered the battlefield!";
        
        [SerializeField] private Color  _victoryColor = new Color(1f, 0.85f, 0.1f);

        [Header("Defeat Config")]
        [SerializeField] private string _defeatTitle    = "DEFEAT";
       
        [SerializeField] private string _defeatSubtitle = "Your forces have been annihilated.";
        
        [SerializeField] private Color  _defeatColor = new Color(0.75f, 0.1f, 0.1f);

        [Header("Button Text")]
        
        [SerializeField] private string _buttonText = "Return to Menu";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (_rootPanel != null)
                _rootPanel.SetActive(false);

            if (_mainButton != null)
                _mainButton.onClick.AddListener(OnMainButtonClicked);

            if (_buttonLabel != null)
                _buttonLabel.text = _buttonText;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            if (_mainButton != null)
                _mainButton.onClick.RemoveListener(OnMainButtonClicked);
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
                SetText(_titleLabel,    _defeatTitle,    _defeatColor);
                SetText(_subtitleLabel, _defeatSubtitle, Color.white);
            }
        }

        private void SetText(TextMeshProUGUI label, string text, Color color)
        {
            if (label == null) return;
            label.text  = text;
            label.color = color;
        }

        private void OnMainButtonClicked()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(GlobalParameters.MENU_SCENE_INDEX);
        }
    }
}
