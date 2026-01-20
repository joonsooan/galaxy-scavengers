using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Systems.Jobs;

public class LoadingUIManager : MonoBehaviour
{
    public static LoadingUIManager Instance { get; private set; }

    [Header("Loading UI References")]
    [SerializeField] private GameObject loadingScreenPrefab;
    [SerializeField] private Canvas loadingCanvasPrefab;
    
    private GameObject _currentLoadingScreen;
    private Canvas _loadingCanvas;
    
    public IInitializationProgress GetProgressTracker()
    {
        if (_currentLoadingScreen != null)
        {
            LoadingScreen loadingScreen = _currentLoadingScreen.GetComponent<LoadingScreen>();
            if (loadingScreen != null && loadingScreen is Systems.Jobs.IInitializationProgress)
            {
                return loadingScreen as Systems.Jobs.IInitializationProgress;
            }
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
        // Create a dedicated canvas for loading UI that persists across scenes
        GameObject canvasObj = new GameObject("LoadingCanvas");
        _loadingCanvas = canvasObj.AddComponent<Canvas>();
        _loadingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _loadingCanvas.sortingOrder = 9999; // Ensure it's on top of everything
        
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
        else
        {
            // Fallback: Create a simple loading screen if prefab is not assigned
            CreateFallbackLoadingScreen();
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

    private IEnumerator HideLoadingScreenWithFadeCoroutine(LoadingScreen loadingScreen)
    {
        yield return loadingScreen.FadeOutSequence();
        
        if (_currentLoadingScreen != null)
        {
            Destroy(_currentLoadingScreen);
            _currentLoadingScreen = null;
        }
    }

    private void CreateFallbackLoadingScreen()
    {
        GameObject loadingObj = new GameObject("LoadingScreen");
        RectTransform rectTransform = loadingObj.AddComponent<RectTransform>();
        rectTransform.SetParent(_loadingCanvas.transform, false);
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;

        Image bg = loadingObj.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.9f);

        GameObject textObj = new GameObject("LoadingText");
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.SetParent(loadingObj.transform, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        TMPro.TextMeshProUGUI text = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        text.text = "로딩 중...";
        text.fontSize = 48;
        text.alignment = TMPro.TextAlignmentOptions.Center;
        text.color = Color.white;

        _currentLoadingScreen = loadingObj;
    }
}
