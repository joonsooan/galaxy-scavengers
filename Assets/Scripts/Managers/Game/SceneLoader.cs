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

        yield return null;

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(gameSceneName);
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

        yield return StartCoroutine(WaitForGameSceneInitialization());
        
        // Freeze gameplay while loading screen is fading out and for an additional delay
        Time.timeScale = 0f;

        // Wait for fade to complete and then wait additional delay
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
            // Wait for fade sequence to complete
            yield return LoadingUIManager.Instance.HideLoadingScreenWithFadeAsync();
        }
        
        yield return new WaitForSecondsRealtime(gameScenePostFadeDelay);
        
        // Spawn units after loading screen is complete
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
        
        // Resume gameplay after loading screen has disappeared and delay has passed
        Time.timeScale = 1f;
    }
    
    private IEnumerator LoadSceneAsync(string sceneName)
    {
        _isLoading = true;

        if (LoadingUIManager.Instance != null)
        {
            LoadingUIManager.Instance.ShowLoadingScreen();
        }

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
        
        if (LoadingUIManager.Instance != null)
        {
            LoadingUIManager.Instance.HideLoadingScreen();
        }

        _isLoading = false;
    }
    
    public void QuitGame()
    {
        Application.Quit();
    }
}