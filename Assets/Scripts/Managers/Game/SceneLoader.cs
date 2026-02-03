using System.Collections;
using System.Collections.Generic;
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

    [Header("Fade Settings")]
    [SerializeField] private float fadeDuration = 0.5f;

    [Header("Game Scene Loading Settings")]
    [SerializeField] private float gameScenePostFadeDelay = 1f;

    [Header("Base Scene Loading Settings")]
    [SerializeField] private float baseSceneBgmStopDelay = 0.5f;
    [SerializeField] private float baseSceneBgmFadeOutTime = 1.0f;
    [SerializeField] private float gameOverLoadingScreenDelay = 0.5f;

    private static readonly WaitForSecondsRealtime _wait01Realtime = CoroutineCache.GetWaitForSecondsRealtime(0.1f);
    private WaitForSecondsRealtime _gameScenePostFadeDelayWait;
    private WaitForSecondsRealtime _baseSceneBgmStopDelayWait;
    private WaitForSecondsRealtime _gameOverLoadingScreenDelayWait;
    private float _cachedGameScenePostFadeDelay;
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
        StartCoroutine(LoadSceneSimple(titleSceneName));
    }

    public void LoadGameScene()
    {
        if (_isLoading) return;
        StartCoroutine(LoadGameSceneAsync());
    }

    public void LoadBaseScene(ReturnFromGameState returnState = ReturnFromGameState.None)
    {
        if (_isLoading) return;
        
        string currentScene = SceneManager.GetActiveScene().name;
        if (currentScene == gameSceneName) {
            _returnState = returnState;
            UnitManager.Instance?.RemoveAllUnits();
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

    private IEnumerator LoadGameSceneAsync()
    {
        _isLoading = true;
        UpdateCachedWaits();
        
        yield return StartCoroutine(FadeRoutine(1f, fadeDuration));

        if (LoadingUIManager.Instance != null) {
            LoadingUIManager.Instance.ShowLoadingScreen();
            yield return StartCoroutine(WaitForLoadingEntry(LoadingUIManager.Instance.GetLoadingScreenComponent()));
        }

        yield return StartCoroutine(AsyncLoadRoutine(gameSceneName, true));

        while (GameManager.Instance == null || !GameManager.Instance.IsGameSceneInitialized) yield return null;

        Time.timeScale = 0f;

        if (LoadingUIManager.Instance != null) yield return LoadingUIManager.Instance.HideLoadingScreenWithFadeAsync();
        
        yield return _gameScenePostFadeDelayWait;
        
        BgmManager.Instance?.PlayGameBgm();
        GameManager.Instance?.SpawnUnitsAfterLoading();
        if (GameManager.Instance != null) GameManager.IsGameplayReady = true;

        Time.timeScale = 1f;
        _isLoading = false;
    }

    private IEnumerator LoadBaseSceneFromGameAsync()
    {
        _isLoading = true;
        _waitingForContinue = true;
        UpdateCachedWaits();

        if (LoadingUIManager.Instance != null) {
            bool isSuccess = _returnState == ReturnFromGameState.Success;
            var questIds = (QuestDataManager.Instance != null) 
                ? QuestDataManager.Instance.GetActiveQuestIds() 
                : new HashSet<int>();

            foreach (int id in questIds) {
                if (isSuccess) QuestDataManager.Instance.MarkQuestReturnedSuccessfully(id);
                else QuestDataManager.Instance.MarkQuestReturnedWithFailure(id);
            }

            if (isSuccess) {
                LoadingUIManager.Instance.ShowSuccessLoadingScreen();
                yield return StartCoroutine(WaitForLoadingEntry(LoadingUIManager.Instance.GetSuccessLoadingScreenComponent()));
            } else {
                LoadingUIManager.Instance.ShowGameOverLoadingScreen();
                yield return _gameOverLoadingScreenDelayWait;
            }
        }

        _baseSceneLoadOperation = SceneManager.LoadSceneAsync(baseSceneName);
        _baseSceneLoadOperation.allowSceneActivation = false;

        while (_baseSceneLoadOperation.progress < 0.9f || _waitingForContinue) yield return null;

        _baseSceneLoadOperation.allowSceneActivation = true;
        while (!_baseSceneLoadOperation.isDone) yield return null;

        if (BgmManager.Instance != null) {
            if (_returnState == ReturnFromGameState.Success) BgmManager.Instance.StopSuccessLoadingBgm(baseSceneBgmFadeOutTime);
            else if (_returnState == ReturnFromGameState.Failure) BgmManager.Instance.StopFailureLoadingBgm(baseSceneBgmFadeOutTime);
            BgmManager.Instance.PlayBaseBgm();
        }

        yield return _baseSceneBgmStopDelayWait;

        if (_returnState == ReturnFromGameState.Success && LoadingUIManager.Instance != null) {
            var successScreen = LoadingUIManager.Instance.GetSuccessLoadingScreenComponent();
            if (successScreen != null) {
                yield return successScreen.GetBackgroundImage().DOFade(0f, successScreen.GetFadeOutDuration()).SetUpdate(true).WaitForCompletion();
            }
        }

        LoadingUIManager.Instance?.HideLoadingScreen();
        yield return StartCoroutine(FadeRoutine(0f, fadeDuration));

        _returnState = ReturnFromGameState.None;
        _isLoading = false;
    }

    private IEnumerator AsyncLoadRoutine(string sceneName, bool autoActivate)
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
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
        GameObject fadeOverlay = GameObject.Find("FadeOverlay") ?? GameObject.Find("Fade Panel");
        if (fadeOverlay != null) {
            fadeOverlay.SetActive(true);
            Image img = fadeOverlay.GetComponent<Image>();
            if (img != null) yield return img.DOFade(targetAlpha, duration).SetUpdate(true).WaitForCompletion();
            if (targetAlpha <= 0) fadeOverlay.SetActive(false);
        }
    }

    public void QuitGame() => Application.Quit();
}