using System.Collections;
using DG.Tweening;
using FMODUnity;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

public class BaseSceneManager : MonoBehaviour
{
    [Header("UI Buttons")]
    [SerializeField] private Button titleButton;
    [SerializeField] private Button inventoryButton;
    [SerializeField] private Button moduleButton;
    [SerializeField] private Button laboratoryButton;
    [SerializeField] private Button farmButton;
    [SerializeField] private Button mapButton;
    [SerializeField] private Button coreLaunchButton;

    [Header("Audio")]
    [SerializeField] private EventReference buttonClickSound;

    [Header("UI Panels")]
    [SerializeField] private GameObject inventoryUIPanel;
    [SerializeField] private GameObject moduleUIPanel;
    [SerializeField] private GameObject laboratoryUIPanel;
    [SerializeField] private GameObject farmUIPanel;
    [SerializeField] private GameObject mapUIPanel;
    [SerializeField] private GameObject coreLaunchUIPanel;
    [SerializeField] private GameObject fadePanel;
    [SerializeField] private float fadeInDuration = 1f;
    private BaseInventorySystem _baseInventorySystem;
    private int _currentPanelIndex = -1;

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
        ApplyLocalizedSidebarButtonLabels();
        LocalizeQuestSidebarButton();
        LocalizeModuleCraftButtons();
    }

    private void Awake()
    {
        if (titleButton != null) {
            titleButton.onClick.AddListener(LoadTitle);
        }

        if (inventoryButton != null) {
            inventoryButton.onClick.AddListener(ToggleInventoryPanel);
        }

        if (moduleButton != null) {
            moduleButton.onClick.AddListener(() => {
                PlayButtonSound();
                OpenUIPanel(1);
            });
        }

        if (laboratoryButton != null) {
            laboratoryButton.onClick.AddListener(() => {
                PlayButtonSound();
                OpenUIPanel(2);
            });
        }

        if (farmButton != null) {
            farmButton.onClick.AddListener(() => {
                PlayButtonSound();
                OpenUIPanel(3);
            });
        }

        if (mapButton != null) {
            mapButton.onClick.AddListener(() => {
                PlayButtonSound();
                OpenUIPanel(4);
            });
        }

        if (coreLaunchButton != null) {
            coreLaunchButton.onClick.AddListener(() => {
                PlayButtonSound();
                OpenUIPanel(5);
            });
        }

        if (BgmManager.Instance != null) {
            // BgmManager.Instance.PlayBaseBgm();
        }
    }

    private void Start()
    {
        _baseInventorySystem = FindFirstObjectByType<BaseInventorySystem>();
        ApplyLocalizedSidebarButtonLabels();
        LocalizeQuestSidebarButton();
        LocalizeModuleCraftButtons();
        StartCoroutine(HandleSceneEntryFade());
    }

    private void ApplyLocalizedSidebarButtonLabels()
    {
        SetButtonLabelText(titleButton, "base.titleScreen", "\uD0C0\uC774\uD2C0 \uD654\uBA74");
        SetButtonLabelText(inventoryButton, "base.inventoryTab", "\uC778\uBCA4\uD1A0\uB9AC [Tab]");
        SetButtonLabelText(moduleButton, "base.moduleStation", "\uBAA8\uB4C8 \uC2A4\uD14C\uC774\uC158");
        SetButtonLabelText(mapButton, "title.mapSelection", "\uB9F5 \uC120\uD0DD");
        SetButtonLabelText(coreLaunchButton, "base.coreLaunch", "\uCF54\uC5B4 \uBC1C\uC0AC");
    }

    private static void SetButtonLabelText(Button button, string key, string fallback)
    {
        if (button == null)
        {
            return;
        }

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = GameLocalization.GetOrDefault("UI_Common", key, fallback);
        }
    }

    private void LocalizeQuestSidebarButton()
    {
        foreach (Button btn in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!btn.gameObject.name.Contains("Button_Quest"))
            {
                continue;
            }

            TMP_Text tmp = btn.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null)
            {
                tmp.text = GameLocalization.GetOrDefault("UI_Common", "base.quest", "\uD018\uC2A4\uD2B8");
            }
        }
    }

    private void LocalizeModuleCraftButtons()
    {
        foreach (Button btn in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!IsModuleCraftButton(btn.gameObject.name))
            {
                continue;
            }

            TMP_Text tmp = btn.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null)
            {
                tmp.text = GameLocalization.GetOrDefault("UI_Common", "base.moduleCraft", "\uBAA8\uB4C8 \uC81C\uC791");
            }
        }
    }

    private static bool IsModuleCraftButton(string objectName)
    {
        return objectName.Contains("Button_Shop") || objectName.Contains("Button Shop");
    }


    private IEnumerator HandleSceneEntryFade()
    {
        yield return null;
        yield return null;

        GameObject fadePanelObj = GetFadePanel();
        if (fadePanelObj != null)
        {
            Image fadeImage = fadePanelObj.GetComponent<Image>();
            if (fadeImage != null)
            {
                Color currentColor = fadeImage.color;
                if (currentColor.a > 0f)
                {
                    fadePanelObj.SetActive(true);
                    fadeImage.DOFade(0f, fadeInDuration).SetUpdate(true);
                    yield return new WaitForSecondsRealtime(fadeInDuration);
                }
            }
        }

        BtnManager_Base btnManager = FindFirstObjectByType<BtnManager_Base>();
        if (btnManager != null)
        {
            btnManager.ResetFadeCanvasGroup();
        }
    }

    private GameObject GetFadePanel()
    {
        if (fadePanel != null) return fadePanel;
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Transform child = c.transform.Find("Fade Panel");
            if (child != null) return child.gameObject;
        }
        return null;
    }

    private void Update()
    {
        if (IsLoadingScreenActive())
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Tab)) {
            ToggleInventoryPanel();
        }
    }

    public bool TryClosePanelForEscape()
    {
        if (_baseInventorySystem == null)
        {
            _baseInventorySystem = FindFirstObjectByType<BaseInventorySystem>();
        }

        if (_currentPanelIndex < 0)
        {
            if (_baseInventorySystem != null)
            {
                GameObject inv = _baseInventorySystem.GetInventoryPanel();
                if (inv != null && inv.activeSelf)
                {
                    _baseInventorySystem.ToggleInventory();
                    return true;
                }
            }
            return false;
        }

        int idx = _currentPanelIndex;

        if (idx == 0 && _baseInventorySystem != null)
        {
            GameObject inv = _baseInventorySystem.GetInventoryPanel();
            if (inv != null && inv.activeSelf)
            {
                _baseInventorySystem.ToggleInventory();
                _currentPanelIndex = -1;
                return true;
            }
        }

        CloseUIPanel(idx);
        return true;
    }

    private void OnDestroy()
    {
        if (titleButton != null) {
            titleButton.onClick.RemoveAllListeners();
        }

        if (inventoryButton != null) {
            inventoryButton.onClick.RemoveAllListeners();
        }

        if (moduleButton != null) {
            moduleButton.onClick.RemoveAllListeners();
        }

        if (laboratoryButton != null) {
            laboratoryButton.onClick.RemoveAllListeners();
        }

        if (farmButton != null) {
            farmButton.onClick.RemoveAllListeners();
        }

        if (mapButton != null) {
            mapButton.onClick.RemoveAllListeners();
        }

        if (coreLaunchButton != null) {
            coreLaunchButton.onClick.RemoveAllListeners();
        }
    }

    private void PlayButtonSound()
    {
        if (FMODUIButton.HasPlayedClickSoundThisFrame) {
            return;
        }

        if (!buttonClickSound.IsNull) {
            RuntimeManager.PlayOneShot(buttonClickSound);
        }
    }

    private void LoadTitle()
    {
        SceneLoader.Instance.LoadTitleScene();
    }

    private void ToggleInventoryPanel()
    {
        if (_baseInventorySystem != null) {
            GameObject inventoryPanel = _baseInventorySystem.GetInventoryPanel();
            if (inventoryPanel != null) {
                bool isActive = inventoryPanel.activeSelf;
                if (isActive) {
                    _baseInventorySystem.ToggleInventory();
                    _currentPanelIndex = -1;
                }
                else {
                    _baseInventorySystem.ToggleInventory();
                    _currentPanelIndex = 0;
                }
            }
        }
        else if (inventoryUIPanel != null) {
            bool isActive = inventoryUIPanel.activeSelf;
            if (isActive) {
                inventoryUIPanel.SetActive(false);
                _currentPanelIndex = -1;
            }
            else {
                inventoryUIPanel.SetActive(true);
                _currentPanelIndex = 0;
            }
        }
    }

    private void OpenUIPanel(int panelIndex)
    {
        GameObject targetPanel = null;
        _currentPanelIndex = panelIndex;

        switch (panelIndex) {
        case 0:
            targetPanel = inventoryUIPanel;
            break;
        case 1:
            targetPanel = moduleUIPanel;
            break;
        case 2:
            targetPanel = laboratoryUIPanel;
            break;
        case 3:
            targetPanel = farmUIPanel;
            break;
        case 4:
            targetPanel = mapUIPanel;
            break;
        case 5:
            targetPanel = coreLaunchUIPanel;
            break;
        }

        if (targetPanel != null) {
            targetPanel.SetActive(true);
        }
    }

    private void CloseUIPanel(int panelIndex)
    {
        GameObject targetPanel = null;
        _currentPanelIndex = -1;

        switch (panelIndex) {
        case 0:
            targetPanel = inventoryUIPanel;
            break;
        case 1:
            targetPanel = moduleUIPanel;
            break;
        case 2:
            targetPanel = laboratoryUIPanel;
            break;
        case 3:
            targetPanel = farmUIPanel;
            break;
        case 4:
            targetPanel = mapUIPanel;
            break;
        case 5:
            targetPanel = coreLaunchUIPanel;
            break;
        }

        if (targetPanel != null) {
            targetPanel.SetActive(false);
        }
    }

    private bool IsLoadingScreenActive()
    {
        if (LoadingUIManager.Instance == null)
        {
            return false;
        }

        return LoadingUIManager.Instance.IsAnyLoadingScreenActive();
    }
}
