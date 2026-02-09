using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MenuUIProvider : MonoBehaviour
{
    [Header("Menu UI References")]
    [SerializeField] public GameObject mainPanel;
    [SerializeField] public GameObject overlayPanel;
    [SerializeField] public Button continueButton;
    [SerializeField] public Button returnToTitleButton;
    [SerializeField] public Button quitGameButton;

    [Header("Volume Sliders")]
    [SerializeField] public Slider masterVolumeSlider;
    [SerializeField] public Slider sfxVolumeSlider;
    [SerializeField] public Slider musicVolumeSlider;

    [Header("Volume Text Displays")]
    [SerializeField] public TMP_Text masterVolumeText;
    [SerializeField] public TMP_Text sfxVolumeText;
    [SerializeField] public TMP_Text musicVolumeText;

    [Header("Quest Reset")]
    [SerializeField] private Button resetQuestButton;

    private void Awake()
    {
        if (GameMenuManager.Instance != null)
        {
            GameMenuManager.Instance.SetMenuUI(this);
        }

        if (resetQuestButton != null)
        {
            resetQuestButton.onClick.RemoveAllListeners();
            resetQuestButton.onClick.AddListener(OnResetQuestButtonClicked);
        }
    }

    private void OnEnable()
    {
        if (GameMenuManager.Instance != null)
        {
            GameMenuManager.Instance.SetMenuUI(this);
        }
    }

    private void OnDestroy()
    {
        if (resetQuestButton != null)
        {
            resetQuestButton.onClick.RemoveAllListeners();
        }
    }

    private void OnResetQuestButtonClicked()
    {
        if (QuestDataManager.Instance == null)
        {
            Debug.LogWarning("MenuUIProvider: QuestDataManager.Instance is null. Cannot reset quest progress.");
            return;
        }

        QuestDataManager.Instance.ResetAllQuestProgress();
        Debug.Log("MenuUIProvider: Quest progress has been reset.");
    }
}
