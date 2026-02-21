using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FMODUnity;

public class LaunchUIController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject launchPanel;
    [SerializeField] private GameObject requiredAetherPanel;
    [SerializeField] private GameObject countdownPanel;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private TMP_Text neededAetherText;
    [SerializeField] private Button launchButton;
    [SerializeField] private LaunchCompleteUI launchCompleteUI;

    [Header("Launch Settings")]
    [SerializeField] private float countdownDurationSeconds = 10f;
    [SerializeField] private float launchCompleteDisplayDuration = 2f;
    [SerializeField] private float fadeToBlackDuration = 1f;
    [SerializeField] private float blackScreenWaitDuration = 1f;
    [SerializeField] private float tutorialPanelHideDelay = 0.5f;

    [Header("Audio")]
    [SerializeField] private EventReference buttonClickSound;
    [SerializeField] private EventReference countdownBgm;
    [SerializeField] private float bgmFadeOutTime = 0.5f;
    
    [Header("Countdown UI Visibility")]
    [SerializeField] private Transform uiVisibilityRoot;
    [SerializeField] private GameObject timeUI;
    [SerializeField] private GameObject menuButton;
    [SerializeField] private GameObject helpButton;
    [SerializeField] private GameObject noisePanel;
    [SerializeField] private GameObject debuffPanel;

    private bool _isCountingDown;
    private Coroutine _countdownCoroutine;
    private WaitForSeconds _launchCompleteDisplayWait;
    private WaitForSecondsRealtime _blackScreenWaitWait;
    private WaitForSecondsRealtime _tutorialPanelHideDelayWait;
    private WaitForSecondsRealtime _fadeToBlackDurationWait;
    private float _cachedTutorialPanelHideDelay;
    private float _cachedFadeToBlackDuration;
    private GameObject _fadeOverlay;
    private bool _isMainEngineRepairAlertActive;

    private void Awake()
    {
        if (launchPanel != null)
        {
            launchPanel.SetActive(false);
        }

        if (countdownPanel != null)
        {
            countdownPanel.SetActive(false);
        }

        UpdateNeededAetherText();
        UpdateCachedWaits();
        UpdateLaunchAvailability();
    }

    private void OnEnable()
    {
        if (CoreRepairManager.Instance != null)
        {
            CoreRepairManager.Instance.OnRepairStatusChanged += UpdateLaunchAvailability;
        }
        TutorialManager.OnTutorialEnded += UpdateLaunchAvailability;
    }

    private void Start()
    {
        StartCoroutine(SyncLaunchAvailabilityWhenGameplayReady());
    }

    private void OnDisable()
    {
        if (CoreRepairManager.Instance != null)
        {
            CoreRepairManager.Instance.OnRepairStatusChanged -= UpdateLaunchAvailability;
        }
        TutorialManager.OnTutorialEnded -= UpdateLaunchAvailability;
    }

    private void UpdateCachedWaits()
    {
        _launchCompleteDisplayWait = CoroutineCache.GetWaitForSeconds(launchCompleteDisplayDuration);
        _blackScreenWaitWait = CoroutineCache.GetWaitForSecondsRealtime(blackScreenWaitDuration);

        if (_tutorialPanelHideDelayWait == null || Mathf.Abs(_cachedTutorialPanelHideDelay - tutorialPanelHideDelay) > 0.001f)
        {
            _cachedTutorialPanelHideDelay = tutorialPanelHideDelay;
            _tutorialPanelHideDelayWait = CoroutineCache.GetWaitForSecondsRealtime(tutorialPanelHideDelay);
        }

        if (_fadeToBlackDurationWait == null || Mathf.Abs(_cachedFadeToBlackDuration - fadeToBlackDuration) > 0.001f)
        {
            _cachedFadeToBlackDuration = fadeToBlackDuration;
            _fadeToBlackDurationWait = CoroutineCache.GetWaitForSecondsRealtime(fadeToBlackDuration);
        }
    }

    private void CreateFadeOverlay()
    {
        if (_fadeOverlay != null)
        {
            return;
        }

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("FadeCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10000;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        _fadeOverlay = new GameObject("FadeOverlay");
        _fadeOverlay.transform.SetParent(canvas.transform, false);
        RectTransform rectTransform = _fadeOverlay.AddComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.anchoredPosition = Vector2.zero;

        Image fadeImage = _fadeOverlay.AddComponent<Image>();
        fadeImage.color = new Color(0f, 0f, 0f, 0f);

        _fadeOverlay.SetActive(false);
    }

    public void ShowLaunchPanel()
    {
        if (_isCountingDown)
        {
            return;
        }

        if (GameManager.Instance != null && GameManager.Instance.uiManager != null)
        {
            GameManager.Instance.uiManager.UnpinAndHideAllPanels();
        }

        MainControlPanel mainControl = FindFirstObjectByType<MainControlPanel>();
        if (mainControl != null)
        {
            mainControl.HideAllPanels();
        }

        if (launchPanel != null)
        {
            launchPanel.SetActive(true);
        }

        if (GameManager.Instance != null && !GameManager.Instance.IsPaused)
        {
            GameManager.Instance.TogglePause();
        }

        if (countdownPanel != null)
        {
            countdownPanel.SetActive(false);
        }

        UpdateNeededAetherText();
        UpdateLaunchAvailability();
    }

    public void OnCancelLaunch()
    {
        if (!FMODUIButton.HasPlayedClickSoundThisFrame && !buttonClickSound.IsNull)
        {
            RuntimeManager.PlayOneShot(buttonClickSound);
        }

        if (launchPanel != null)
        {
            launchPanel.SetActive(false);
        }
        
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
        {
            GameManager.Instance.TogglePause();
        }
    }

    public void OnConfirmLaunch()
    {
        if (_isCountingDown)
        {
            return;
        }

        if (!FMODUIButton.HasPlayedClickSoundThisFrame && !buttonClickSound.IsNull)
        {
            RuntimeManager.PlayOneShot(buttonClickSound);
        }

        MainStructure mainStructure = FindFirstObjectByType<MainStructure>();
        if (mainStructure == null)
        {
            return;
        }

        InventorySystem inventorySystem = mainStructure.GetComponent<InventorySystem>();
        if (inventorySystem == null)
        {
            return;
        }

        if (launchPanel != null)
        {
            launchPanel.SetActive(false);
        }
        
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
        {
            GameManager.Instance.TogglePause();
        }

        inventorySystem.ToggleInventory();
    }

    public void StartLaunchCountdown()
    {
        if (_isCountingDown)
        {
            return;
        }

        GameSceneQuestUIManager questUIManager = FindFirstObjectByType<GameSceneQuestUIManager>();
        if (questUIManager != null)
        {
            questUIManager.HideQuestPanel();
        }

        MainStructure mainStructure = FindFirstObjectByType<MainStructure>();
        if (mainStructure == null)
        {
            return;
        }

        InventorySystem inventorySystem = mainStructure.GetComponent<InventorySystem>();
        if (inventorySystem == null)
        {
            return;
        }

        inventorySystem.SetTransferEnabled(false);
        HideUiForLaunchCountdown();

        if (BgmManager.Instance != null)
        {
            if (!countdownBgm.IsNull)
            {
                BgmManager.Instance.PlayBgm(countdownBgm, true, bgmFadeOutTime);
            }
            else
            {
                BgmManager.Instance.StopBgm(bgmFadeOutTime);
            }
        }

        if (countdownPanel != null)
        {
            countdownPanel.SetActive(true);
        }

        _isCountingDown = true;

        if (_countdownCoroutine != null)
        {
            StopCoroutine(_countdownCoroutine);
        }
        _countdownCoroutine = StartCoroutine(CountdownCoroutine());
    }

    private IEnumerator CountdownCoroutine()
    {
        float remaining = Mathf.Max(0f, countdownDurationSeconds);

        while (remaining > 0f)
        {
            UpdateCountdownText(remaining);
            yield return null;
            remaining -= Time.deltaTime;
        }

        remaining = 0f;
        UpdateCountdownText(0f);

        yield return null;

        if (countdownPanel != null)
        {
            countdownPanel.SetActive(false);
        }

        MainStructure mainStructure = FindFirstObjectByType<MainStructure>();
        if (mainStructure != null)
        {
            InventorySystem inventorySystem = mainStructure.GetComponent<InventorySystem>();
            if (inventorySystem != null)
            {
                inventorySystem.TransferAllToBaseInventory();
            }
        }

        if (launchCompleteUI != null)
        {
            launchCompleteUI.Show();
            yield return _launchCompleteDisplayWait;
        }

        if (TutorialManager.Instance != null)
        {
            TutorialManager.Instance.HideAllUIPanels();
        }

        UpdateCachedWaits();
        yield return _tutorialPanelHideDelayWait;

        if (BgmManager.Instance != null)
        {
            BgmManager.Instance.StopBgm(bgmFadeOutTime);
        }

        yield return StartCoroutine(FadeToBlack());

        yield return _blackScreenWaitWait;

        _isCountingDown = false;
        _countdownCoroutine = null;

        SceneLoader.Instance.LoadBaseScene(SceneLoader.ReturnFromGameState.Success);
    }

    private IEnumerator FadeToBlack()
    {
        if (_fadeOverlay == null)
        {
            CreateFadeOverlay();
        }

        if (_fadeOverlay == null)
        {
            yield break;
        }

        _fadeOverlay.SetActive(true);
        Image fadeImage = _fadeOverlay.GetComponent<Image>();
        if (fadeImage == null)
        {
            yield break;
        }

        UpdateCachedWaits();
        fadeImage.DOFade(1f, fadeToBlackDuration).SetUpdate(true);
        yield return _fadeToBlackDurationWait;
    }

    private void HideUiForLaunchCountdown()
    {
        Transform root = uiVisibilityRoot;
        if (root == null)
        {
            if (launchPanel != null && launchPanel.transform.parent != null)
            {
                root = launchPanel.transform.parent;
            }
            else if (countdownPanel != null && countdownPanel.transform.parent != null)
            {
                root = countdownPanel.transform.parent;
            }
        }

        if (root == null)
        {
            return;
        }

        HashSet<Transform> keepSet = new HashSet<Transform>();
        AddKeepTransform(keepSet, countdownPanel);
        AddKeepTransform(keepSet, timeUI);
        AddKeepTransform(keepSet, menuButton);
        AddKeepTransform(keepSet, helpButton);
        AddKeepTransform(keepSet, noisePanel);
        AddKeepTransform(keepSet, debuffPanel);

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (ShouldKeepVisible(child, keepSet))
            {
                continue;
            }

            GameObject target = child.gameObject;
            if (target.activeSelf)
            {
                target.SetActive(false);
            }
        }
    }

    private static void AddKeepTransform(HashSet<Transform> keepSet, GameObject target)
    {
        if (target != null)
        {
            keepSet.Add(target.transform);
        }
    }

    private static bool ShouldKeepVisible(Transform candidate, HashSet<Transform> keepSet)
    {
        foreach (Transform keep in keepSet)
        {
            if (keep == null)
            {
                continue;
            }

            if (candidate == keep || keep.IsChildOf(candidate))
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateNeededAetherText()
    {
        if (neededAetherText == null)
        {
            return;
        }
        
        neededAetherText.text = "0";
        if (requiredAetherPanel != null)
        {
            RectTransform panelRect = requiredAetherPanel.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
            }
        }
    }

    private void UpdateCountdownText(float remainingSeconds)
    {
        if (countdownText == null)
        {
            return;
        }

        int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(remainingSeconds));
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        countdownText.text = $"{minutes:00} : {seconds:00}";
    }

    private void UpdateLaunchAvailability()
    {
        bool isEngineRepaired = CoreRepairManager.Instance != null && CoreRepairManager.Instance.IsPartRepaired(CorePart.Engine);
        bool isTutorialActive = TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive();
        GameAlertUIManager alertManager = FindFirstObjectByType<GameAlertUIManager>();
        bool shouldShowEngineRepairAlert = !isEngineRepaired && GameManager.IsGameplayReady && !isTutorialActive;
        if (alertManager != null)
        {
            if (shouldShowEngineRepairAlert && !_isMainEngineRepairAlertActive)
            {
                alertManager.RegisterAlert(GameAlertType.MainEngineRepair);
                _isMainEngineRepairAlertActive = true;
            }
            else if (!shouldShowEngineRepairAlert && _isMainEngineRepairAlertActive)
            {
                alertManager.UnregisterAlert(GameAlertType.MainEngineRepair);
                _isMainEngineRepairAlertActive = false;
            }
        }
        else
        {
            _isMainEngineRepairAlertActive = false;
        }
        
        if (launchButton != null)
        {
            launchButton.interactable = isEngineRepaired;

            TMP_Text buttonText = launchButton.GetComponentInChildren<TMP_Text>();
            if (buttonText != null)
            {
                if (isEngineRepaired)
                {
                    buttonText.text = "탈출";
                }
                else
                {
                    buttonText.text = "탈출\n불가";
                }
            }
        }
    }

    private IEnumerator SyncLaunchAvailabilityWhenGameplayReady()
    {
        while (!GameManager.IsGameplayReady)
        {
            yield return null;
        }

        UpdateLaunchAvailability();
    }
}

