using System.Collections;
using FMODUnity;
using UnityEngine;
using UnityEngine.UI;
using Systems.Jobs;

public class LoadingUIManager : MonoBehaviour
{
    public static LoadingUIManager Instance { get; private set; }

    [Header("Loading UI References")]
    [SerializeField] private GameObject loadingScreenPrefab;
    [SerializeField] private GameObject successLoadingScreenPrefab;
    [SerializeField] private GameObject gameOverLoadingScreenPrefab;
    
    private GameObject _currentLoadingScreen;
    private Canvas _loadingCanvas;
    private GameObject _fadeOverlay;

    public GameObject GetFadeOverlay()
    {
        if (_loadingCanvas == null) InitializeCanvas();
        if (_loadingCanvas != null)
        {
            RemoveDuplicateFadeOverlays();
            if (_fadeOverlay != null) return _fadeOverlay;
            Transform child = _loadingCanvas.transform.Find("FadeOverlay");
            if (child != null)
            {
                _fadeOverlay = child.gameObject;
                EnsureFadeOverlayState(_fadeOverlay, 0f, false);
                return _fadeOverlay;
            }
            _fadeOverlay = new GameObject("FadeOverlay");
            _fadeOverlay.transform.SetParent(_loadingCanvas.transform, false);
            RectTransform rect = _fadeOverlay.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            Image img = _fadeOverlay.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
            _fadeOverlay.SetActive(false);
        }
        return _fadeOverlay;
    }

    private void RemoveDuplicateFadeOverlays()
    {
        if (_loadingCanvas == null) return;
        Transform[] all = _loadingCanvas.GetComponentsInChildren<Transform>(true);
        GameObject toKeep = null;
        int count = 0;
        foreach (Transform t in all)
        {
            if (t.name != "FadeOverlay") continue;
            count++;
            if (toKeep == null)
            {
                toKeep = t.gameObject;
            }
            else if (t.gameObject != toKeep)
            {
                Destroy(t.gameObject);
            }
        }
        if (toKeep != null)
        {
            _fadeOverlay = toKeep;
            if (count > 1) EnsureFadeOverlayState(toKeep, 0f, false);
        }
    }

    private void EnsureFadeOverlayState(GameObject overlay, float alpha, bool active)
    {
        if (overlay == null) return;
        Image img = overlay.GetComponent<Image>();
        if (img != null) img.color = new Color(0f, 0f, 0f, alpha);
        overlay.SetActive(active);
    }

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

    public bool IsAnyLoadingScreenActive()
    {
        return _currentLoadingScreen != null && _currentLoadingScreen.activeInHierarchy;
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

    public void ShowSuccessLoadingScreen()
    {
        if (_currentLoadingScreen != null)
        {
            return;
        }

        if (_loadingCanvas == null)
        {
            InitializeCanvas();
        }

        if (successLoadingScreenPrefab != null)
        {
            _currentLoadingScreen = Instantiate(successLoadingScreenPrefab, _loadingCanvas.transform);
            _currentLoadingScreen.SetActive(true);
        }
    }

    public void ShowGameOverLoadingScreen()
    {
        if (_currentLoadingScreen != null)
        {
            return;
        }

        if (_loadingCanvas == null)
        {
            InitializeCanvas();
        }

        if (gameOverLoadingScreenPrefab != null)
        {
            _currentLoadingScreen = Instantiate(gameOverLoadingScreenPrefab, _loadingCanvas.transform);
            _currentLoadingScreen.SetActive(true);
        }
    }

    public SuccessLoadingScreen GetSuccessLoadingScreenComponent()
    {
        if (_currentLoadingScreen != null)
        {
            return _currentLoadingScreen.GetComponent<SuccessLoadingScreen>();
        }
        return null;
    }

    public GameOverLoadingScreen GetGameOverLoadingScreenComponent()
    {
        if (_currentLoadingScreen != null)
        {
            return _currentLoadingScreen.GetComponent<GameOverLoadingScreen>();
        }
        return null;
    }

    private void OnApplicationQuit()
    {
        if (_currentLoadingScreen != null)
        {
            DestroyImmediate(_currentLoadingScreen);
            _currentLoadingScreen = null;
        }
        if (_loadingCanvas != null && _loadingCanvas.gameObject != null)
        {
            DestroyImmediate(_loadingCanvas.gameObject);
            _loadingCanvas = null;
        }
        if (Instance == this)
            Instance = null;
    }

    private void OnDestroy()
    {
        if (_currentLoadingScreen != null)
        {
            Destroy(_currentLoadingScreen);
            _currentLoadingScreen = null;
        }
        if (_loadingCanvas != null && _loadingCanvas.gameObject != null)
        {
            Destroy(_loadingCanvas.gameObject);
            _loadingCanvas = null;
        }
        if (Instance == this)
            Instance = null;
    }
}
