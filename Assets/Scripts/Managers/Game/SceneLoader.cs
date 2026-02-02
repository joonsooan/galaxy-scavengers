using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public enum ReturnFromGameState
    {
        None,
        Success,
        Failure
    }

    [SerializeField] private string titleSceneName = "TitleScene";
    [SerializeField] private string baseSceneName = "BaseScene";
    [SerializeField] private string gameSceneName = "GameScene";

    [Header("Game Scene Loading Settings")]
    [SerializeField] private float gameScenePostFadeDelay = 1f;

    private static readonly WaitForSecondsRealtime _wait01Realtime = CoroutineCache.GetWaitForSecondsRealtime(0.1f);
    private static readonly WaitForSeconds _wait01 = CoroutineCache.GetWaitForSeconds(0.1f);
    private WaitForSecondsRealtime _postFadeDelayWait;
    private float _cachedPostFadeDelay;

    private bool _isLoading;
    private ReturnFromGameState _returnState = ReturnFromGameState.None;
    private AsyncOperation _baseSceneLoadOperation;
    private bool _waitingForContinue;
    public static SceneLoader Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else {
            Destroy(gameObject);
        }
    }

    private WaitForSecondsRealtime GetPostFadeDelayWait()
    {
        if (_postFadeDelayWait == null || Mathf.Abs(_cachedPostFadeDelay - gameScenePostFadeDelay) > 0.001f)
        {
            _cachedPostFadeDelay = gameScenePostFadeDelay;
            _postFadeDelayWait = CoroutineCache.GetWaitForSecondsRealtime(gameScenePostFadeDelay);
        }
        return _postFadeDelayWait;
    }

    public void LoadTitleScene()
    {
        if (_isLoading) return;
        StartCoroutine(LoadSceneAsync(titleSceneName));
    }

    public void LoadBaseScene(ReturnFromGameState returnState = ReturnFromGameState.None)
    {
        if (_isLoading) return;

        string currentScene = SceneManager.GetActiveScene().name;

        if (currentScene == gameSceneName) {
            _returnState = returnState;
            if (UnitManager.Instance != null) {
                UnitManager.Instance.RemoveAllUnits();
            }
            StartCoroutine(LoadBaseSceneFromGameAsync());
        }
        else if (currentScene == titleSceneName) {
            SceneManager.LoadScene(baseSceneName);
        }
        else {
            StartCoroutine(LoadSceneAsync(baseSceneName));
        }
    }

    private IEnumerator LoadBaseSceneFromGameAsync()
    {
        _isLoading = true;
        _waitingForContinue = true;

        if (_returnState == ReturnFromGameState.Success)
        {
            if (QuestDataManager.Instance != null)
            {
                foreach (int questId in QuestDataManager.Instance.GetActiveQuestIds())
                {
                    QuestDataManager.Instance.MarkQuestReturnedSuccessfully(questId);
                }
            }

            if (LoadingUIManager.Instance != null)
            {
                LoadingUIManager.Instance.ShowSuccessLoadingScreen();
            }

            if (LoadingUIManager.Instance != null)
            {
                SuccessLoadingScreen successScreen = LoadingUIManager.Instance.GetSuccessLoadingScreenComponent();
                while (successScreen == null || !successScreen.IsEntryAnimationComplete)
                {
                    yield return null;
                    if (LoadingUIManager.Instance != null)
                    {
                        successScreen = LoadingUIManager.Instance.GetSuccessLoadingScreenComponent();
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }
        else if (_returnState == ReturnFromGameState.Failure)
        {
            if (QuestDataManager.Instance != null)
            {
                foreach (int questId in QuestDataManager.Instance.GetActiveQuestIds())
                {
                    QuestDataManager.Instance.MarkQuestReturnedWithFailure(questId);
                }
            }

            if (LoadingUIManager.Instance != null)
            {
                LoadingUIManager.Instance.ShowGameOverLoadingScreen();
            }

            yield return new WaitForSecondsRealtime(0.5f);
        }

        yield return null;

        _baseSceneLoadOperation = SceneManager.LoadSceneAsync(baseSceneName);
        _baseSceneLoadOperation.allowSceneActivation = false;

        while (_baseSceneLoadOperation.progress < 0.9f)
        {
            yield return null;
        }

        while (_waitingForContinue)
        {
            yield return null;
        }

        yield return _wait01Realtime;

        _baseSceneLoadOperation.allowSceneActivation = true;

        while (!_baseSceneLoadOperation.isDone)
        {
            yield return null;
        }

        yield return StartCoroutine(WaitForBaseSceneInitialization());

        if (BgmManager.Instance != null)
        {
            if (_returnState == ReturnFromGameState.Success)
            {
                BgmManager.Instance.StopSuccessLoadingBgm(1.0f);
            }
            else if (_returnState == ReturnFromGameState.Failure)
            {
                BgmManager.Instance.StopFailureLoadingBgm(1.0f);
            }
        }

        yield return new WaitForSecondsRealtime(0.5f);

        if (BgmManager.Instance != null)
        {
            BgmManager.Instance.PlayBaseBgm();
        }

        if (_returnState == ReturnFromGameState.Success)
        {
            SuccessLoadingScreen successScreen = null;
            if (LoadingUIManager.Instance != null)
            {
                successScreen = LoadingUIManager.Instance.GetSuccessLoadingScreenComponent();
            }

            if (successScreen != null)
            {
                yield return StartCoroutine(FadeOutSuccessLoadingBackground(successScreen));
            }
        }

        if (LoadingUIManager.Instance != null)
        {
            LoadingUIManager.Instance.HideLoadingScreen();
        }

        GameObject fadeOverlay = GameObject.Find("FadeOverlay");
        if (fadeOverlay != null)
        {
            Image fadeImage = fadeOverlay.GetComponent<Image>();
            if (fadeImage != null)
            {
                fadeImage.DOFade(0f, 0.5f).SetUpdate(true).OnComplete(() => {
                    fadeOverlay.SetActive(false);
                });
            }
            else
            {
                fadeOverlay.SetActive(false);
            }
        }

        _returnState = ReturnFromGameState.None;
        _isLoading = false;
    }

    private IEnumerator FadeOutSuccessLoadingBackground(SuccessLoadingScreen successScreen)
    {
        Image backgroundImage = successScreen.GetBackgroundImage();
        float fadeOutDuration = successScreen.GetFadeOutDuration();

        if (backgroundImage != null && fadeOutDuration > 0f)
        {
            yield return backgroundImage.DOFade(0f, fadeOutDuration).SetUpdate(true).WaitForCompletion();
        }
    }

    public void CompleteBaseSceneLoad()
    {
        _waitingForContinue = false;
    }

    private IEnumerator WaitForBaseSceneInitialization()
    {
        yield return null;
        yield return null;
    }

    public void LoadGameScene()
    {
        if (_isLoading) return;
        StartCoroutine(LoadGameSceneAsync());
    }

    private IEnumerator LoadGameSceneAsync()
    {
        _isLoading = true;

        GameObject fadePanel = FindFadePanel();
        if (fadePanel != null)
        {
            fadePanel.SetActive(true);
            Image fadeImage = fadePanel.GetComponent<Image>();
            if (fadeImage != null)
            {
                Color currentColor = fadeImage.color;
                if (currentColor.a < 1f)
                {
                    fadeImage.DOFade(1f, 0.5f).SetUpdate(true);
                    yield return new WaitForSecondsRealtime(0.5f);
                }
            }
        }

        if (LoadingUIManager.Instance != null) {
            LoadingUIManager.Instance.ShowLoadingScreen();
        }

        if (LoadingUIManager.Instance != null) {
            LoadingScreen loadingScreen = LoadingUIManager.Instance.GetLoadingScreenComponent();
            while (loadingScreen == null || !loadingScreen.IsEntryAnimationComplete) {
                yield return null;
                if (LoadingUIManager.Instance != null) {
                    loadingScreen = LoadingUIManager.Instance.GetLoadingScreenComponent();
                }
                else {
                    break;
                }
            }
        }

        yield return null;

        if (LoadingUIManager.Instance != null) {
            LoadingScreen loadingScreen = LoadingUIManager.Instance.GetLoadingScreenComponent();
            while (loadingScreen == null || !loadingScreen.IsEntryAnimationComplete) {
                yield return null;
                if (LoadingUIManager.Instance != null) {
                    loadingScreen = LoadingUIManager.Instance.GetLoadingScreenComponent();
                }
            }
        }

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(gameSceneName);
        asyncLoad.allowSceneActivation = false;

        while (asyncLoad.progress < 0.9f) {
            yield return null;
        }

        yield return _wait01Realtime;

        asyncLoad.allowSceneActivation = true;

        while (!asyncLoad.isDone) {
            yield return null;
        }

        yield return StartCoroutine(WaitForGameSceneInitialization());

        Time.timeScale = 0f;

        if (LoadingUIManager.Instance != null) {
            yield return StartCoroutine(WaitForFadeCompleteAndDelay());
        }

        _isLoading = false;
    }

    private IEnumerator WaitForGameSceneInitialization()
    {
        while (GameManager.Instance == null || !GameManager.Instance.IsGameSceneInitialized) {
            yield return null;
        }
    }

    private IEnumerator WaitForFadeCompleteAndDelay()
    {
        if (LoadingUIManager.Instance != null) {
            yield return LoadingUIManager.Instance.HideLoadingScreenWithFadeAsync();
        }

        yield return GetPostFadeDelayWait();

        if (BgmManager.Instance != null) {
            BgmManager.Instance.PlayGameBgm();
        }

        if (GameManager.Instance != null) {
            GameManager.Instance.SpawnUnitsAfterLoading();
        }

        if (GameManager.Instance != null) {
            GameManager.IsGameplayReady = true;
            foreach (Unit_Miner unit in FindObjectsByType<Unit_Miner>(FindObjectsSortMode.None)) {
                unit.TryStartActions();
            }
        }

        Time.timeScale = 1f;
    }

    private IEnumerator LoadSceneAsync(string sceneName)
    {
        _isLoading = true;

        yield return null;

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        asyncLoad.allowSceneActivation = false;

        while (asyncLoad.progress < 0.9f) {
            yield return null;
        }

        yield return _wait01;

        asyncLoad.allowSceneActivation = true;

        while (!asyncLoad.isDone) {
            yield return null;
        }

        yield return _wait01;

        _isLoading = false;
    }

    private GameObject FindFadePanel()
    {
        Transform[] allTransforms = FindObjectsOfType<Transform>(true);
        foreach (Transform t in allTransforms)
        {
            if (t.name == "Fade Panel")
            {
                return t.gameObject;
            }
        }
        return null;
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
