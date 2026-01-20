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
        StartCoroutine(LoadSceneAsync(baseSceneName));
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

        yield return new WaitForSeconds(gameScenePostInitDelay);

        if (LoadingUIManager.Instance != null)
        {
            LoadingUIManager.Instance.HideLoadingScreen();
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