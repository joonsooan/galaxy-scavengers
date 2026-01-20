using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Systems.Jobs;

public class LoadingUIManager : MonoBehaviour
{
    public static LoadingUIManager Instance { get; private set; }

    [Header("Loading UI References")]
    [SerializeField] private GameObject loadingScreenPrefab;
    
    private GameObject _currentLoadingScreen;
    private Canvas _loadingCanvas;
    
    public IInitializationProgress GetProgressTracker()
    {
        if (_currentLoadingScreen != null)
        {
            LoadingScreen loadingScreen = _currentLoadingScreen.GetComponent<LoadingScreen>();
            if (loadingScreen != null)
            {
                return loadingScreen;
            }
        }
        return null;
    }
    
    public LoadingScreen GetLoadingScreenComponent()
    {
        if (_currentLoadingScreen != null)
        {
            return _currentLoadingScreen.GetComponent<LoadingScreen>();
        }
        return null;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeCanvas();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeCanvas()
    {
        GameObject canvasObj = new GameObject("LoadingCanvas");
        _loadingCanvas = canvasObj.AddComponent<Canvas>();
        _loadingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _loadingCanvas.sortingOrder = 9999;
        
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        
        canvasObj.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasObj);
    }

    public void ShowLoadingScreen()
    {
        if (_currentLoadingScreen != null)
        {
            return;
        }

        if (_loadingCanvas == null)
        {
            InitializeCanvas();
        }

        if (loadingScreenPrefab != null)
        {
            _currentLoadingScreen = Instantiate(loadingScreenPrefab, _loadingCanvas.transform);
            _currentLoadingScreen.SetActive(true);
        }
    }

    public void HideLoadingScreen()
    {
        if (_currentLoadingScreen != null)
        {
            Destroy(_currentLoadingScreen);
            _currentLoadingScreen = null;
        }
    }

    public void HideLoadingScreenWithFade()
    {
        if (_currentLoadingScreen != null)
        {
            LoadingScreen loadingScreen = _currentLoadingScreen.GetComponent<LoadingScreen>();
            if (loadingScreen != null)
            {
                StartCoroutine(HideLoadingScreenWithFadeCoroutine(loadingScreen));
            }
            else
            {
                HideLoadingScreen();
            }
        }
    }
    
    public IEnumerator HideLoadingScreenWithFadeAsync()
    {
        if (_currentLoadingScreen != null)
        {
            LoadingScreen loadingScreen = _currentLoadingScreen.GetComponent<LoadingScreen>();
            if (loadingScreen != null)
            {
                yield return HideLoadingScreenWithFadeCoroutine(loadingScreen);
            }
            else
            {
                HideLoadingScreen();
            }
        }
    }

    private IEnumerator HideLoadingScreenWithFadeCoroutine(LoadingScreen loadingScreen)
    {
        yield return loadingScreen.FadeOutSequence();
        
        if (_currentLoadingScreen != null)
        {
            Destroy(_currentLoadingScreen);
            _currentLoadingScreen = null;
        }
    }
}
