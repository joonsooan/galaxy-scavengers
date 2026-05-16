using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

public class BtnManager_Title : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private Button quitButton;

    private TMP_Text _resetProgressLabel;

    private void Awake()
    {
        if (startButton != null)
        {
            startButton.onClick.AddListener(BaseScene);
        }

        if (quitButton != null)
        {
            quitButton.onClick.AddListener(QuitGame);
        }

        CacheResetProgressLabel();
    }

    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
    }

    private void OnSelectedLocaleChanged(Locale _)
    {
        ApplyResetProgressLabel();
    }

    private void CacheResetProgressLabel()
    {
        foreach (Button btn in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!btn.gameObject.name.Contains("Button_Red"))
            {
                continue;
            }

            TMP_Text tmp = btn.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null)
            {
                _resetProgressLabel = tmp;
                return;
            }
        }
    }

    private void ApplyResetProgressLabel()
    {
        if (_resetProgressLabel == null)
        {
            return;
        }

        _resetProgressLabel.text = GameLocalization.GetOrDefault("UI_Common", "menu.resetProgress", "Reset Progress");
    }

    private void Start()
    {
        if (BgmManager.Instance != null)
        {
            BgmManager.Instance.PlayTitleBgm();
        }

        ApplyResetProgressLabel();
    }

    private void OnDestroy()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(BaseScene);
        }
        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(QuitGame);
        }
    }

    private void BaseScene()
    {
        SceneLoader.Instance.LoadBaseScene();
    }

    private void QuitGame()
    {
        SceneLoader.Instance.QuitGame();
    }
}