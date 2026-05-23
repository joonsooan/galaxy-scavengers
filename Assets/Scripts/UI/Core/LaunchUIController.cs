using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using FMODUnity;

public class LaunchUIController : MonoBehaviour
{
    public static event Action OnLaunchSequenceStarted;
    public static event Action OnLaunchSequenceFinished;
    public static event Action<int> OnLaunchSequenceSecondTick;

    [Header("UI References")]
    [SerializeField] private GameObject launchPanel;
    [SerializeField] private GameObject requiredAetherPanel;
    [SerializeField] private TMP_Text neededAetherText;
    [SerializeField] private Button launchButton;
    [SerializeField] private LaunchCompleteUI launchCompleteUI;

    [Header("Launch Settings")]
    [Tooltip("Seconds to wait after resources are transferred to base, before escape completes.")]
    [SerializeField] private float launchDelayAfterTransferSeconds = 3f;
    [Tooltip("If true, delay uses unscaled time (runs while paused).")]
    [SerializeField] private bool useUnscaledLaunchDelay = true;
    [SerializeField] private float launchCompleteDisplayDuration = 2f;
    [SerializeField] private float fadeToBlackDuration = 1f;
    [SerializeField] private float blackScreenWaitDuration = 1f;
    [SerializeField] private float tutorialPanelHideDelay = 0.5f;

    [Header("Audio")]
    [SerializeField] private EventReference buttonClickSound;
    [SerializeField] private float bgmFadeOutTime = 0.5f;

    [Header("Launch Hidden UI")]
    [SerializeField] private GameObject speedUI;
    [SerializeField] private GameObject menuButton;
    [SerializeField] private GameObject launchButtonObject;

    private Coroutine _launchSequenceCoroutine;
    private WaitForSeconds _launchCompleteDisplayWait;
    private WaitForSecondsRealtime _blackScreenWaitWait;
    private WaitForSecondsRealtime _tutorialPanelHideDelayWait;
    private WaitForSecondsRealtime _fadeToBlackDurationWait;
    private float _cachedTutorialPanelHideDelay;
    private float _cachedFadeToBlackDuration;
    private GameObject _fadeOverlay;
    private bool _isLaunchPausePanelLockActive;
    private bool _isLaunchSequenceRunning;

    private void Update()
    {
        if (launchPanel != null && launchPanel.activeSelf && !_isLaunchSequenceRunning)
        {
            if (Input.GetMouseButtonUp(1))
            {
                OnCancelLaunch();
            }
        }
    }

    private void Awake()
    {
        if (launchPanel != null)
        {
            launchPanel.SetActive(false);
        }

        UpdateNeededAetherText();
        UpdateCachedWaits();
        UpdateLaunchAvailability();
    }

    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
        UpdateLaunchAvailability();
    }

    private void Start()
    {
        StartCoroutine(SyncLaunchAvailabilityWhenGameplayReady());
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
        SetLaunchPausePanelLock(false);
    }

    private void OnSelectedLocaleChanged(Locale _)
    {
        UpdateLaunchAvailability();
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
        if (_isLaunchSequenceRunning)
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
        SetLaunchPausePanelLock(true);

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
        SetLaunchPausePanelLock(false);
    }

    public void OnConfirmLaunch()
    {
        if (_isLaunchSequenceRunning)
        {
            return;
        }

        if (!FMODUIButton.HasPlayedClickSoundThisFrame && !buttonClickSound.IsNull)
        {
            RuntimeManager.PlayOneShot(buttonClickSound);
        }

        InventorySystem inventorySystem = GetLaunchInventorySystem();
        if (inventorySystem == null)
        {
            return;
        }

        if (launchPanel != null)
        {
            launchPanel.SetActive(false);
        }
        
        if (GameManager.Instance != null && !GameManager.Instance.IsPaused)
        {
            GameManager.Instance.TogglePause();
        }
        SetLaunchPausePanelLock(true);
        inventorySystem.ToggleInventory();
    }

    public void StartLaunchSequence()
    {
        if (_isLaunchSequenceRunning)
        {
            return;
        }

        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
        {
            GameManager.Instance.TogglePause();
        }
        SetLaunchPausePanelLock(false);

        InventorySystem inventorySystem = GetLaunchInventorySystem();
        if (inventorySystem == null)
        {
            return;
        }

        inventorySystem.SetTransferEnabled(false);
        HideUiForLaunchSequence();

        if (_launchSequenceCoroutine != null)
        {
            StopCoroutine(_launchSequenceCoroutine);
        }
        _launchSequenceCoroutine = StartCoroutine(LaunchSequenceCoroutine());
    }

    private InventorySystem GetLaunchInventorySystem()
    {
        UIManager uiManager = null;
        if (GameManager.Instance != null)
        {
            uiManager = GameManager.Instance.uiManager;
        }
        if (uiManager == null)
        {
            uiManager = FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
        }
        if (uiManager != null)
        {
            return uiManager.GetInventorySystem();
        }

        return FindFirstObjectByType<InventorySystem>(FindObjectsInactive.Include);
    }

    private void SetLaunchPausePanelLock(bool active)
    {
        _isLaunchPausePanelLockActive = active;

        if (GameManager.Instance != null && GameManager.Instance.uiManager != null)
        {
            GameManager.Instance.uiManager.SetPausePanelLock(active);
            GameManager.Instance.uiManager.SetPausePanelActive(active);
        }
    }

    public bool IsLaunchInputLockActive()
    {
        return _isLaunchPausePanelLockActive || (launchPanel != null && launchPanel.activeSelf);
    }

    public bool IsPauseInputLocked()
    {
        return _isLaunchPausePanelLockActive;
    }

    public bool IsMenuInputBlocked()
    {
        if (_isLaunchPausePanelLockActive)
        {
            return true;
        }

        return _isLaunchSequenceRunning;
    }

    public bool IsCountdownSequenceActive()
    {
        return _isLaunchSequenceRunning;
    }

    private IEnumerator LaunchSequenceCoroutine()
    {
        _isLaunchSequenceRunning = true;
        UpdateCachedWaits();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetMainCanvasVisible(false);
        }

        if (BgmManager.Instance != null)
        {
            BgmManager.Instance.StopBgm(bgmFadeOutTime);
        }

        InventorySystem inventorySystem = GetLaunchInventorySystem();
        if (inventorySystem != null)
        {
            inventorySystem.TransferAllToBaseInventory();
        }

        OnLaunchSequenceStarted?.Invoke();

        float delay = Mathf.Max(0f, launchDelayAfterTransferSeconds);
        int prevWhole = -1;
        while (delay > 0f)
        {
            float dt = useUnscaledLaunchDelay ? Time.unscaledDeltaTime : Time.deltaTime;
            delay = Mathf.Max(0f, delay - dt);
            int whole = Mathf.Max(0, Mathf.CeilToInt(delay));
            if (whole != prevWhole)
            {
                prevWhole = whole;
                OnLaunchSequenceSecondTick?.Invoke(whole);
            }
            yield return null;
        }

        if (launchCompleteUI != null)
        {
            launchCompleteUI.Show();
            yield return _launchCompleteDisplayWait;
        }

        UpdateCachedWaits();
        yield return _tutorialPanelHideDelayWait;

        if (BgmManager.Instance != null)
        {
            BgmManager.Instance.StopBgm(bgmFadeOutTime);
        }

        yield return StartCoroutine(FadeToBlack());

        yield return _blackScreenWaitWait;

        _isLaunchSequenceRunning = false;
        _launchSequenceCoroutine = null;
        OnLaunchSequenceFinished?.Invoke();

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

    private void HideUiForLaunchSequence()
    {
        HideTargetUi(speedUI);
        HideTargetUi(menuButton);
        HideTargetUi(launchButtonObject);
    }

    private static void HideTargetUi(GameObject target)
    {
        if (target != null && target.activeSelf)
        {
            target.SetActive(false);
        }
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

    private void UpdateLaunchAvailability()
    {
        if (launchButton != null)
        {
            launchButton.interactable = true;

            TMP_Text buttonText = launchButton.GetComponentInChildren<TMP_Text>(true);
            if (buttonText != null)
            {
                buttonText.text = GameLocalization.GetOrDefault("UI_Common", "button.launch", "탈출");
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
