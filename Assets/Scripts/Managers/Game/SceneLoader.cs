using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    [SerializeField] private string titleSceneName = "TitleScene";
    [SerializeField] private string baseSceneName = "BaseScene";
    [SerializeField] private string gameSceneName = "GameScene";
    
    [Header("Game Scene Loading Settings")]
    [SerializeField] private float gameScenePostInitDelay = 1f;
    [SerializeField] private float gameScenePostFadeDelay = 1f;
    
    private bool _isLoading = false;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void LoadTitleScene()
    {
        if (_isLoading) return;
        StartCoroutine(LoadSceneAsync(titleSceneName));
    }

    public void LoadBaseScene()
    {
        if (_isLoading) return;
        
        string currentScene = SceneManager.GetActiveScene().name;
        
        if (currentScene == gameSceneName)
        {
            if (UnitManager.Instance != null)
            {
                UnitManager.Instance.RemoveAllUnits();
            }
        }
        
        if (currentScene == titleSceneName)
        {
            SceneManager.LoadScene(baseSceneName);
        }
        else
        {
            StartCoroutine(LoadSceneAsync(baseSceneName));
        }
    }

    public void LoadGameScene()
    {
        if (_isLoading) return;
        StartCoroutine(LoadGameSceneAsync());
    }
    
    private IEnumerator LoadGameSceneAsync()
    {
        _isLoading = true;

        if (LoadingUIManager.Instance != null)
        {
            LoadingUIManager.Instance.ShowLoadingScreen();
        }

        if (LoadingUIManager.Instance != null)
        {
            LoadingScreen loadingScreen = LoadingUIManager.Instance.GetLoadingScreenComponent();
            while (loadingScreen == null || !loadingScreen.IsEntryAnimationComplete)
            {
                yield return null;
                if (LoadingUIManager.Instance != null)
                {
                    loadingScreen = LoadingUIManager.Instance.GetLoadingScreenComponent();
                }
                else
                {
                    break;
                }
            }
        }

        yield return null;

        if (LoadingUIManager.Instance != null)
        {
            LoadingScreen loadingScreen = LoadingUIManager.Instance.GetLoadingScreenComponent();
            while (loadingScreen == null || !loadingScreen.IsEntryAnimationComplete)
            {
                yield return null;
                if (LoadingUIManager.Instance != null)
                {
                    loadingScreen = LoadingUIManager.Instance.GetLoadingScreenComponent();
                }
            }
        }

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(gameSceneName);
        asyncLoad.allowSceneActivation = false;

        while (asyncLoad.progress < 0.9f)
        {
            yield return null;
        }

        yield return new WaitForSecondsRealtime(0.1f);

        asyncLoad.allowSceneActivation = true;

        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        yield return StartCoroutine(WaitForGameSceneInitialization());

        Time.timeScale = 0f;

        if (LoadingUIManager.Instance != null)
        {
            yield return StartCoroutine(WaitForFadeCompleteAndDelay());
        }

        _isLoading = false;
    }
    
    private IEnumerator WaitForGameSceneInitialization()
    {
        while (GameManager.Instance == null || !GameManager.Instance.IsGameSceneInitialized)
        {
            yield return null;
        }
    }
    
    private IEnumerator WaitForFadeCompleteAndDelay()
    {
        if (LoadingUIManager.Instance != null)
        {
            yield return LoadingUIManager.Instance.HideLoadingScreenWithFadeAsync();
        }
        
        yield return new WaitForSecondsRealtime(gameScenePostFadeDelay);
        
        if (BgmManager.Instance != null)
        {
            BgmManager.Instance.PlayGameBgm();
        }
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SpawnUnitsAfterLoading();
        }
        
        if (GameManager.Instance != null)
        {
            GameManager.IsGameplayReady = true;
            foreach (Unit_Miner unit in FindObjectsByType<Unit_Miner>(FindObjectsSortMode.None))
            {
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

        while (asyncLoad.progress < 0.9f)
        {
            yield return null;
        }

        yield return new WaitForSeconds(0.1f);

        asyncLoad.allowSceneActivation = true;

        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        yield return new WaitForSeconds(0.1f);

        _isLoading = false;
    }
    
    public void QuitGame()
    {
        Application.Quit();
    }
}