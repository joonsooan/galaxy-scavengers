using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public enum ReturnFromGameState { None, Success, Failure }

    [Header("Scene Names")]
    [SerializeField] private string titleSceneName = "TitleScene";
    [SerializeField] private string baseSceneName = "BaseScene";
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private string tutorialSceneName = "TutorialScene";

    [Header("Fade Settings")]
    [SerializeField] private float fadeDuration = 0.5f;

    [Header("Game Scene Loading Settings")]
    [SerializeField] private float gameScenePostFadeDelay = 1f;
    [SerializeField] private float gameScenePostOpeningUiDelay = 0.5f;

    [Header("Base Scene Loading Settings")]
    [SerializeField] private float baseSceneBgmStopDelay = 0.5f;
    [SerializeField] private float baseSceneBgmFadeOutTime = 1.0f;
    [SerializeField] private float gameOverLoadingScreenDelay = 0.5f;

    private static readonly WaitForSecondsRealtime _wait01Realtime = CoroutineCache.GetWaitForSecondsRealtime(0.1f);
    private WaitForSecondsRealtime _gameScenePostFadeDelayWait;
    private WaitForSecondsRealtime _gameScenePostOpeningUiDelayWait;
    private WaitForSecondsRealtime _baseSceneBgmStopDelayWait;
    private WaitForSecondsRealtime _gameOverLoadingScreenDelayWait;
    private float _cachedGameScenePostFadeDelay;
    private float _cachedGameScenePostOpeningUiDelay;
    private float _cachedBaseSceneBgmStopDelay;
    private float _cachedGameOverLoadingScreenDelay;

    private bool _isLoading;
    private bool _waitingForContinue;
    private ReturnFromGameState _returnState = ReturnFromGameState.None;
    private AsyncOperation _baseSceneLoadOperation;

    public static SceneLoader Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }
    }

    private void Start()
    {
        UpdateCachedWaits();
    }

    private void UpdateCachedWaits()
    {
        if (_gameScenePostFadeDelayWait == null || Mathf.Abs(_cachedGameScenePostFadeDelay - gameScenePostFadeDelay) > 0.001f)
        {
            _cachedGameScenePostFadeDelay = gameScenePostFadeDelay;
            _gameScenePostFadeDelayWait = CoroutineCache.GetWaitForSecondsRealtime(gameScenePostFadeDelay);
        }
        
        if (_gameScenePostOpeningUiDelayWait == null || Mathf.Abs(_cachedGameScenePostOpeningUiDelay - gameScenePostOpeningUiDelay) > 0.001f)
        {
            _cachedGameScenePostOpeningUiDelay = gameScenePostOpeningUiDelay;
            _gameScenePostOpeningUiDelayWait = CoroutineCache.GetWaitForSecondsRealtime(gameScenePostOpeningUiDelay);
        }

        if (_baseSceneBgmStopDelayWait == null || Mathf.Abs(_cachedBaseSceneBgmStopDelay - baseSceneBgmStopDelay) > 0.001f)
        {
            _cachedBaseSceneBgmStopDelay = baseSceneBgmStopDelay;
            _baseSceneBgmStopDelayWait = CoroutineCache.GetWaitForSecondsRealtime(baseSceneBgmStopDelay);
        }

        if (_gameOverLoadingScreenDelayWait == null || Mathf.Abs(_cachedGameOverLoadingScreenDelay - gameOverLoadingScreenDelay) > 0.001f)
        {
            _cachedGameOverLoadingScreenDelay = gameOverLoadingScreenDelay;
            _gameOverLoadingScreenDelayWait = CoroutineCache.GetWaitForSecondsRealtime(gameOverLoadingScreenDelay);
        }
    }

    public void LoadTitleScene()
    {
        if (_isLoading) return;
        
        if (BgmManager.Instance != null)
        {
            BgmManager.Instance.PlayTitleBgm();
        }
        
        StartCoroutine(LoadSceneSimple(titleSceneName));
    }

    public void LoadGameScene()
    {
        if (_isLoading) return;
        StartCoroutine(LoadGameplaySceneAsync(gameSceneName));
    }

    public void LoadGameScene(string sceneName)
    {
        if (_isLoading) return;

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            StartCoroutine(LoadGameplaySceneAsync(gameSceneName));
            return;
        }

        StartCoroutine(LoadGameplaySceneAsync(sceneName));
    }

    public void LoadTutorialScene()
    {
        if (_isLoading) return;
        StartCoroutine(LoadGameplaySceneAsync(tutorialSceneName));
    }

    public void LoadBaseScene(ReturnFromGameState returnState = ReturnFromGameState.None)
    {
        if (_isLoading) return;
        
        string currentScene = SceneManager.GetActiveScene().name;
        if (currentScene == baseSceneName) {
            return;
        }

        if (currentScene == gameSceneName || currentScene == tutorialSceneName) {
            _returnState = returnState;
            StartCoroutine(LoadBaseSceneFromGameAsync());
        }
        else {
            StartCoroutine(LoadSceneSimple(baseSceneName));
        }
    }

    public void CompleteBaseSceneLoad() => _waitingForContinue = false;

    private IEnumerator LoadSceneSimple(string sceneName)
    {
        _isLoading = true;
        yield return StartCoroutine(AsyncLoadRoutine(sceneName, true));
        _isLoading = false;
    }

    private IEnumerator LoadGameplaySceneAsync(string targetSceneName)
    {
        _isLoading = true;
        UpdateCachedWaits();
        
        yield return StartCoroutine(FadeRoutine(1f, fadeDuration));

        if (LoadingUIManager.Instance != null) {
            LoadingUIManager.Instance.ShowLoadingScreen();
            yield return StartCoroutine(WaitForLoadingEntry(LoadingUIManager.Instance.GetLoadingScreenComponent()));
        }

        yield return StartCoroutine(AsyncLoadRoutine(targetSceneName, true));

        while (GameManager.Instance == null || !GameManager.Instance.IsGameSceneInitialized) yield return null;

        bool isGameScene = string.Equals(SceneManager.GetActiveScene().name, gameSceneName, StringComparison.Ordinal);
        if (isGameScene && GameManager.Instance != null)
        {
            GameManager.Instance.SetMainCanvasVisible(false);
        }

        if (LoadingUIManager.Instance != null) yield return LoadingUIManager.Instance.HideLoadingScreenWithFadeAsync();
        
        yield return StartCoroutine(FadeRoutine(0f, fadeDuration));
        yield return _gameScenePostFadeDelayWait;

        GameManager.Instance?.SpawnUnitsAfterLoading();

        if (isGameScene && GameManager.Instance != null)
        {
            yield return GameManager.Instance.StartCoroutine(GameManager.Instance.RunGameSceneOpeningSequence());
            yield return _gameScenePostOpeningUiDelayWait;
            yield return GameManager.Instance.StartCoroutine(GameManager.Instance.WarmUpGameplayUiCoroutine());
            GameManager.Instance.SetMainCanvasVisible(true);
        }

        if (isGameScene)
        {
            BgmManager.Instance?.PlayGameBgm();
        }
        if (GameManager.Instance != null)
        {
            GameManager.Instance.EndGameSceneOpeningSequence();
            GameManager.IsGameplayReady = true;
        }

        _isLoading = false;
    }

    private IEnumerator LoadBaseSceneFromGameAsync()
    {
        _isLoading = true;
        _waitingForContinue = true;
        UpdateCachedWaits();

        if (LoadingUIManager.Instance != null) {
            bool isSuccess = _returnState == ReturnFromGameState.Success;

            if (isSuccess) {
                LoadingUIManager.Instance.ShowSuccessLoadingScreen();
                yield return StartCoroutine(WaitForLoadingEntry(LoadingUIManager.Instance.GetSuccessLoadingScreenComponent()));
            } else {
                LoadingUIManager.Instance.ShowGameOverLoadingScreen();
                GameOverLoadingScreen gameOverLoadingScreen = LoadingUIManager.Instance.GetGameOverLoadingScreenComponent();
                while (gameOverLoadingScreen == null || !gameOverLoadingScreen.IsEntryAnimationComplete)
                {
                    yield return null;
                    if (LoadingUIManager.Instance != null)
                    {
                        gameOverLoadingScreen = LoadingUIManager.Instance.GetGameOverLoadingScreenComponent();
                    }
                }
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.OnGameOverScreenFullyShown();
                }
            }
        }

        _baseSceneLoadOperation = SceneManager.LoadSceneAsync(baseSceneName);
        if (_baseSceneLoadOperation == null) {
            SceneManager.LoadScene(baseSceneName);
        }
        else {
            _baseSceneLoadOperation.allowSceneActivation = false;
            while (_baseSceneLoadOperation.progress < 0.9f || _waitingForContinue) yield return null;
            _baseSceneLoadOperation.allowSceneActivation = true;
            while (!_baseSceneLoadOperation.isDone) yield return null;
        }

        if (BgmManager.Instance != null) {
            if (_returnState == ReturnFromGameState.Success) BgmManager.Instance.StopSuccessLoadingBgm(baseSceneBgmFadeOutTime);
            else if (_returnState == ReturnFromGameState.Failure) BgmManager.Instance.StopFailureLoadingBgm(baseSceneBgmFadeOutTime);
            BgmManager.Instance.PlayBaseBgm();
        }

        yield return _baseSceneBgmStopDelayWait;

        LoadingUIManager.Instance?.HideLoadingScreen();
        GameObject fadeOverlay = LoadingUIManager.Instance != null
            ? LoadingUIManager.Instance.GetFadeOverlay()
            : (GameObject.Find("FadeOverlay") ?? GameObject.Find("Fade Panel"));
        if (fadeOverlay != null)
        {
            Image overlayImg = fadeOverlay.GetComponent<Image>();
            if (overlayImg != null)
            {
                overlayImg.color = new Color(0f, 0f, 0f, 1f);
                fadeOverlay.SetActive(true);
            }
        }
        yield return StartCoroutine(FadeRoutine(0f, fadeDuration));

        _returnState = ReturnFromGameState.None;
        _isLoading = false;
    }

    private IEnumerator AsyncLoadRoutine(string sceneName, bool autoActivate)
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        if (op == null) {
            Debug.LogError($"[SceneLoader] Failed to load scene '{sceneName}'. Check if scene is in Build Settings.");
            yield break;
        }
        op.allowSceneActivation = autoActivate;
        while (op.progress < 0.9f) yield return null;
        if (autoActivate) {
            while (!op.isDone) yield return null;
        }
    }

    private IEnumerator WaitForLoadingEntry(object screen)
    {
        if (screen is LoadingScreen ls) while (!ls.IsEntryAnimationComplete) yield return null;
        else if (screen is SuccessLoadingScreen sls) while (!sls.IsEntryAnimationComplete) yield return null;
    }

    private IEnumerator FadeRoutine(float targetAlpha, float duration)
    {
        GameObject fadeOverlay = LoadingUIManager.Instance != null
            ? LoadingUIManager.Instance.GetFadeOverlay()
            : (GameObject.Find("FadeOverlay") ?? GameObject.Find("Fade Panel"));
        if (fadeOverlay != null) {
            fadeOverlay.SetActive(true);
            Image img = fadeOverlay.GetComponent<Image>();
            if (img != null) yield return img.DOFade(targetAlpha, duration).SetUpdate(true).WaitForCompletion();
            if (targetAlpha <= 0) fadeOverlay.SetActive(false);
        }
    }

    public void QuitGame() => Application.Quit();
}