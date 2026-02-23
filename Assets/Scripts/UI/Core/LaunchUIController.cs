using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FMODUnity;

public class LaunchUIController : MonoBehaviour
{
    public static event Action OnLaunchCountdownStarted;
    public static event Action OnLaunchCountdownFinished;
    public static event Action<int> OnLaunchCountdownSecondChanged;

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
    [SerializeField] private float countdownStartDelaySeconds = 0.6f;
    [SerializeField] private float launchCompleteDisplayDuration = 2f;
    [SerializeField] private float fadeToBlackDuration = 1f;
    [SerializeField] private float blackScreenWaitDuration = 1f;
    [SerializeField] private float tutorialPanelHideDelay = 0.5f;

    [Header("Audio")]
    [SerializeField] private EventReference buttonClickSound;
    [SerializeField] private EventReference countdownBgm;
    [SerializeField] private float bgmFadeOutTime = 0.5f;

    [Header("Countdown Visual Settings")]
    [SerializeField] private int countdownDangerThresholdSeconds = 10;
    [SerializeField] private Color countdownDefaultColor = Color.white;
    [SerializeField] private Color countdownDangerColor = Color.red;
    [SerializeField] private float normalBounceScale = 0.18f;
    [SerializeField] private float normalBounceDuration = 0.18f;
    [SerializeField] private int normalBounceVibrato = 8;
    [SerializeField] private float normalBounceElasticity = 0.85f;
    [SerializeField] private float dangerBounceScale = 0.3f;
    [SerializeField] private float dangerBounceDuration = 0.24f;
    [SerializeField] private int dangerBounceVibrato = 10;
    [SerializeField] private float dangerBounceElasticity = 0.95f;
    
    [Header("Countdown Hidden UI")]
    [SerializeField] private GameObject speedUI;
    [SerializeField] private GameObject questPanel;
    [SerializeField] private GameObject menuButton;
    [SerializeField] private GameObject launchButtonObject;

    private bool _isCountingDown;
    private Coroutine _countdownCoroutine;
    private WaitForSeconds _launchCompleteDisplayWait;
    private WaitForSecondsRealtime _blackScreenWaitWait;
    private WaitForSecondsRealtime _tutorialPanelHideDelayWait;
    private WaitForSecondsRealtime _fadeToBlackDurationWait;
    private WaitForSecondsRealtime _countdownStartDelayWait;
    private float _cachedTutorialPanelHideDelay;
    private float _cachedFadeToBlackDuration;
    private float _cachedCountdownStartDelay;
    private GameObject _fadeOverlay;
    private bool _isMainEngineRepairAlertActive;
    private bool _isLaunchPausePanelLockActive;
    private bool _isPreparingCountdown;
    private Tween _countdownBounceTween;
    private Vector3 _countdownBaseScale;
    private bool _hasCountdownBaseScale;
    private int _remainingWholeSeconds;
    private float _displayRemainingSeconds;
    private float _lastBeatTimestamp;
    private float _estimatedBeatInterval = 1f;
    private bool _hasPreviousBeatTimestamp;
    private bool _isDangerCountdown;
    private int _pendingBeatStepsForSecond;
    private int _lastDisplayedWholeSeconds = -1;
    private bool _hasAppliedFirstBeatSecondStep;

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
        _isPreparingCountdown = false;
        SetLaunchPausePanelLock(false);
        if (BgmManager.Instance != null)
        {
            BgmManager.Instance.DisableCountdownBeatSync();
        }
        ResetCountdownVisualState();
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

        if (_countdownStartDelayWait == null || Mathf.Abs(_cachedCountdownStartDelay - countdownStartDelaySeconds) > 0.001f)
        {
            _cachedCountdownStartDelay = countdownStartDelaySeconds;
            _countdownStartDelayWait = CoroutineCache.GetWaitForSecondsRealtime(countdownStartDelaySeconds);
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
        SetLaunchPausePanelLock(true);

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
        SetLaunchPausePanelLock(false);
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

    public void StartLaunchCountdown()
    {
        if (_isCountingDown || _isPreparingCountdown)
        {
            return;
        }

        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
        {
            GameManager.Instance.TogglePause();
        }
        SetLaunchPausePanelLock(false);

        GameSceneQuestUIManager questUIManager = FindFirstObjectByType<GameSceneQuestUIManager>();
        if (questUIManager != null)
        {
            questUIManager.HideQuestPanel();
        }

        InventorySystem inventorySystem = GetLaunchInventorySystem();
        if (inventorySystem == null)
        {
            return;
        }

        inventorySystem.SetTransferEnabled(false);
        HideUiForLaunchCountdown();

        _isCountingDown = true;
        OnLaunchCountdownStarted?.Invoke();

        if (_countdownCoroutine != null)
        {
            StopCoroutine(_countdownCoroutine);
        }
        _countdownCoroutine = StartCoroutine(CountdownCoroutine());
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

        return _isPreparingCountdown || _isCountingDown;
    }

    public bool IsCountdownSequenceActive()
    {
        return _isPreparingCountdown || _isCountingDown;
    }

    private IEnumerator CountdownCoroutine()
    {
        _isPreparingCountdown = true;
        UpdateCachedWaits();

        if (BgmManager.Instance != null)
        {
            BgmManager.Instance.StopBgm(bgmFadeOutTime);
        }

        if (countdownPanel != null)
        {
            countdownPanel.SetActive(false);
        }

        if (countdownStartDelaySeconds > 0f)
        {
            yield return _countdownStartDelayWait;
        }

        if (countdownPanel != null)
        {
            countdownPanel.SetActive(true);
        }

        if (BgmManager.Instance != null)
        {
            if (!countdownBgm.IsNull)
            {
                BgmManager.Instance.PlayBgm(countdownBgm, true, 0f);
            }
            else
            {
                BgmManager.Instance.StopBgm(0f);
            }
        }

        _isPreparingCountdown = false;
        bool useBeatSync = false;
        if (BgmManager.Instance != null && !countdownBgm.IsNull)
        {
            useBeatSync = BgmManager.Instance.EnableCountdownBeatSync();
        }

        _remainingWholeSeconds = Mathf.Max(0, Mathf.CeilToInt(countdownDurationSeconds));
        _displayRemainingSeconds = _remainingWholeSeconds;
        _estimatedBeatInterval = 1f;
        _lastBeatTimestamp = Time.unscaledTime;
        _hasPreviousBeatTimestamp = true;
        _pendingBeatStepsForSecond = 0;
        _lastDisplayedWholeSeconds = _remainingWholeSeconds;
        _hasAppliedFirstBeatSecondStep = false;
        ResetCountdownVisualState();
        HandleSecondChanged(-1, _lastDisplayedWholeSeconds);

        float realtimeFallbackRemaining = Mathf.Max(0f, countdownDurationSeconds);

        while (_remainingWholeSeconds > 0)
        {
            if (useBeatSync)
            {
                int beatTicks = BgmManager.Instance != null ? BgmManager.Instance.ConsumeCountdownBeatTicks() : 0;
                if (beatTicks > 0)
                {
                    float tempoBpm = BgmManager.Instance != null ? BgmManager.Instance.GetCountdownTempoBpm() : 0f;
                    int beatsPerCountdownSecond = GetBeatsPerCountdownSecond(tempoBpm);

                    for (int i = 0; i < beatTicks; i++)
                    {
                        if (_remainingWholeSeconds <= 0)
                        {
                            break;
                        }

                        float now = Time.unscaledTime;
                        if (_hasPreviousBeatTimestamp)
                        {
                            float interval = now - _lastBeatTimestamp;
                            if (interval > 0.02f && interval < 5f)
                            {
                                _estimatedBeatInterval = interval;
                            }
                        }

                        _lastBeatTimestamp = now;
                        _hasPreviousBeatTimestamp = true;

                        if (!_hasAppliedFirstBeatSecondStep)
                        {
                            _hasAppliedFirstBeatSecondStep = true;
                            _pendingBeatStepsForSecond = 0;
                            _remainingWholeSeconds = Mathf.Max(0, _remainingWholeSeconds - 1);
                            continue;
                        }

                        _pendingBeatStepsForSecond++;
                        if (_pendingBeatStepsForSecond >= beatsPerCountdownSecond)
                        {
                            _pendingBeatStepsForSecond = 0;
                            _remainingWholeSeconds = Mathf.Max(0, _remainingWholeSeconds - 1);
                        }
                    }
                }

                UpdateBeatSyncedDisplaySeconds();
            }
            else
            {
                realtimeFallbackRemaining = Mathf.Max(0f, realtimeFallbackRemaining - Time.deltaTime);
                _displayRemainingSeconds = realtimeFallbackRemaining;

                int currentSeconds = Mathf.Max(0, Mathf.CeilToInt(realtimeFallbackRemaining));
                _remainingWholeSeconds = currentSeconds;
            }

            UpdateCountdownVisualByDisplayedSeconds();
            UpdateCountdownText(_displayRemainingSeconds);
            yield return null;
        }

        if (BgmManager.Instance != null)
        {
            BgmManager.Instance.DisableCountdownBeatSync();
        }

        UpdateCountdownText(0f);

        yield return null;

        if (countdownPanel != null)
        {
            countdownPanel.SetActive(false);
        }

        InventorySystem inventorySystem = GetLaunchInventorySystem();
        if (inventorySystem != null)
        {
            inventorySystem.TransferAllToBaseInventory();
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
        _isPreparingCountdown = false;
        _countdownCoroutine = null;
        OnLaunchCountdownFinished?.Invoke();

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
        HideTargetUi(speedUI);
        HideTargetUi(questPanel);
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

    private void UpdateCountdownText(float remainingSeconds)
    {
        if (countdownText == null)
        {
            return;
        }

        float clampedSeconds = Mathf.Max(0f, remainingSeconds);
        countdownText.text = $"{clampedSeconds:00.000} s";
    }

    private void UpdateBeatSyncedDisplaySeconds()
    {
        if (_remainingWholeSeconds <= 0)
        {
            _displayRemainingSeconds = 0f;
            return;
        }

        if (!_hasPreviousBeatTimestamp)
        {
            _displayRemainingSeconds = _remainingWholeSeconds;
            return;
        }

        float elapsedSinceBeat = Time.unscaledTime - _lastBeatTimestamp;
        float normalized = Mathf.Clamp01(elapsedSinceBeat / Mathf.Max(0.01f, _estimatedBeatInterval));
        float beatProgress = _pendingBeatStepsForSecond + normalized;
        float beatsPerCountdownSecond = Mathf.Max(1f, GetBeatsPerCountdownSecond(BgmManager.Instance != null ? BgmManager.Instance.GetCountdownTempoBpm() : 0f));
        float secondProgress = beatProgress / beatsPerCountdownSecond;
        _displayRemainingSeconds = Mathf.Max(0f, _remainingWholeSeconds - secondProgress);
    }

    private static int GetBeatsPerCountdownSecond(float tempoBpm)
    {
        if (tempoBpm <= 0f)
        {
            return 1;
        }

        return Mathf.Max(1, Mathf.RoundToInt(tempoBpm / 60f));
    }

    private void HandleSecondChanged(int previousSeconds, int currentSeconds)
    {
        bool isDanger = currentSeconds <= countdownDangerThresholdSeconds;
        if (isDanger != _isDangerCountdown && countdownText != null)
        {
            countdownText.color = isDanger ? countdownDangerColor : countdownDefaultColor;
            _isDangerCountdown = isDanger;
        }

        if (previousSeconds >= 0 && currentSeconds != previousSeconds)
        {
            PlayCountdownBounce(_isDangerCountdown);
        }
    }

    private void UpdateCountdownVisualByDisplayedSeconds()
    {
        int displayedWholeSeconds = Mathf.Max(0, Mathf.CeilToInt(_displayRemainingSeconds));
        if (_lastDisplayedWholeSeconds == displayedWholeSeconds)
        {
            return;
        }

        HandleSecondChanged(_lastDisplayedWholeSeconds, displayedWholeSeconds);
        _lastDisplayedWholeSeconds = displayedWholeSeconds;
        OnLaunchCountdownSecondChanged?.Invoke(displayedWholeSeconds);
    }

    private void ResetCountdownVisualState()
    {
        if (countdownText == null)
        {
            return;
        }

        if (!_hasCountdownBaseScale)
        {
            _countdownBaseScale = countdownText.rectTransform.localScale;
            _hasCountdownBaseScale = true;
        }

        if (_countdownBounceTween != null && _countdownBounceTween.IsActive())
        {
            _countdownBounceTween.Kill();
        }

        countdownText.rectTransform.localScale = _countdownBaseScale;
        countdownText.color = countdownDefaultColor;
        _isDangerCountdown = false;
    }

    private void PlayCountdownBounce(bool danger)
    {
        if (countdownText == null)
        {
            return;
        }

        if (_countdownBounceTween != null && _countdownBounceTween.IsActive())
        {
            _countdownBounceTween.Kill();
        }

        countdownText.rectTransform.localScale = _countdownBaseScale;

        float scale = danger ? dangerBounceScale : normalBounceScale;
        float duration = danger ? dangerBounceDuration : normalBounceDuration;
        int vibrato = danger ? dangerBounceVibrato : normalBounceVibrato;
        float elasticity = danger ? dangerBounceElasticity : normalBounceElasticity;

        _countdownBounceTween = countdownText.rectTransform
            .DOPunchScale(new Vector3(scale, scale, scale), duration, vibrato, elasticity)
            .SetUpdate(true);
    }

    private void UpdateLaunchAvailability()
    {
        bool isEngineRepaired = CoreRepairManager.Instance != null && CoreRepairManager.Instance.IsPartRepaired(CorePart.Engine);
        bool isTutorialActive = TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive();
        GameAlertUIManager alertManager = FindFirstObjectByType<GameAlertUIManager>();
        bool shouldShowEngineRepairAlert = !isEngineRepaired && GameManager.IsGameplayReady && !isTutorialActive && IsReadyToShowMainEngineAlert();
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

        while (!IsReadyToShowMainEngineAlert())
        {
            yield return null;
        }

        UpdateLaunchAvailability();
    }

    private bool IsReadyToShowMainEngineAlert()
    {
        if (LoadingUIManager.Instance != null && LoadingUIManager.Instance.IsAnyLoadingScreenActive())
        {
            return false;
        }

        CameraTargetController cameraTargetController = FindFirstObjectByType<CameraTargetController>();
        if (cameraTargetController == null || cameraTargetController.followTarget == null)
        {
            return false;
        }

        return cameraTargetController.followTarget.GetComponent<Unit_Player>() != null;
    }
}

