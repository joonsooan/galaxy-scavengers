using System.Collections;
using DG.Tweening;
using FMODUnity;
using UnityEngine;
using UnityEngine.UI;

public class BaseSceneManager : MonoBehaviour
{
    [Header("UI Buttons")]
    [SerializeField] private Button titleButton;
    [SerializeField] private Button inventoryButton;
    [SerializeField] private Button moduleButton;
    [SerializeField] private Button laboratoryButton;
    [SerializeField] private Button farmButton;
    [SerializeField] private Button mapButton;
    [SerializeField] private Button coreLaunchButton;

    [Header("Audio")]
    [SerializeField] private EventReference buttonClickSound;

    [Header("UI Panels")]
    [SerializeField] private GameObject inventoryUIPanel;
    [SerializeField] private GameObject moduleUIPanel;
    [SerializeField] private GameObject laboratoryUIPanel;
    [SerializeField] private GameObject farmUIPanel;
    [SerializeField] private GameObject mapUIPanel;
    [SerializeField] private GameObject coreLaunchUIPanel;
    [SerializeField] private GameObject fadePanel;
    [SerializeField] private float fadeInDuration = 1f;
    private BaseInventorySystem _baseInventorySystem;
    private int _currentPanelIndex = -1;

    private void Awake()
    {
        titleButton.onClick.AddListener(LoadTitle);
        inventoryButton.onClick.AddListener(ToggleInventoryPanel);
        moduleButton.onClick.AddListener(() => {
            PlayButtonSound();
            OpenUIPanel(1);
        });
        laboratoryButton.onClick.AddListener(() => {
            PlayButtonSound();
            OpenUIPanel(2);
        });
        farmButton.onClick.AddListener(() => {
            PlayButtonSound();
            OpenUIPanel(3);
        });
        mapButton.onClick.AddListener(() => {
            PlayButtonSound();
            OpenUIPanel(4);
        });
        coreLaunchButton.onClick.AddListener(() => {
            PlayButtonSound();
            OpenUIPanel(5);
        });

        if (BgmManager.Instance != null) {
            // BgmManager.Instance.PlayBaseBgm();
        }
    }

    private void Start()
    {
        _baseInventorySystem = FindFirstObjectByType<BaseInventorySystem>();
        StartCoroutine(HandleSceneEntryFade());
    }


    private IEnumerator HandleSceneEntryFade()
    {
        yield return null;
        yield return null;

        GameObject fadePanelObj = GetFadePanel();
        if (fadePanelObj != null)
        {
            Image fadeImage = fadePanelObj.GetComponent<Image>();
            if (fadeImage != null)
            {
                Color currentColor = fadeImage.color;
                if (currentColor.a > 0f)
                {
                    fadePanelObj.SetActive(true);
                    fadeImage.DOFade(0f, fadeInDuration).SetUpdate(true);
                    yield return new WaitForSecondsRealtime(fadeInDuration);
                }
            }
        }

        BtnManager_Base btnManager = FindFirstObjectByType<BtnManager_Base>();
        if (btnManager != null)
        {
            btnManager.ResetFadeCanvasGroup();
        }
    }

    private GameObject GetFadePanel()
    {
        if (fadePanel != null) return fadePanel;
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Transform child = c.transform.Find("Fade Panel");
            if (child != null) return child.gameObject;
        }
        return null;
    }

    private void Update()
    {
        if (IsLoadingScreenActive())
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape)) {
            CloseUIPanel(_currentPanelIndex);
        }

        if (Input.GetKeyDown(KeyCode.Tab)) {
            ToggleInventoryPanel();
        }
    }

    private void OnDestroy()
    {
        titleButton.onClick.RemoveAllListeners();
        inventoryButton.onClick.RemoveAllListeners();
        moduleButton.onClick.RemoveAllListeners();
        laboratoryButton.onClick.RemoveAllListeners();
        farmButton.onClick.RemoveAllListeners();
        mapButton.onClick.RemoveAllListeners();
        coreLaunchButton.onClick.RemoveAllListeners();
    }

    private void PlayButtonSound()
    {
        if (FMODUIButton.HasPlayedClickSoundThisFrame) {
            return;
        }

        if (!buttonClickSound.IsNull) {
            RuntimeManager.PlayOneShot(buttonClickSound);
        }
    }

    private void LoadTitle()
    {
        SceneLoader.Instance.LoadTitleScene();
    }

    private void ToggleInventoryPanel()
    {
        if (_baseInventorySystem != null) {
            GameObject inventoryPanel = _baseInventorySystem.GetInventoryPanel();
            if (inventoryPanel != null) {
                bool isActive = inventoryPanel.activeSelf;
                if (isActive) {
                    _baseInventorySystem.ToggleInventory();
                    _currentPanelIndex = -1;
                }
                else {
                    _baseInventorySystem.ToggleInventory();
                    _currentPanelIndex = 0;
                }
            }
        }
        else if (inventoryUIPanel != null) {
            bool isActive = inventoryUIPanel.activeSelf;
            if (isActive) {
                inventoryUIPanel.SetActive(false);
                _currentPanelIndex = -1;
            }
            else {
                inventoryUIPanel.SetActive(true);
                _currentPanelIndex = 0;
            }
        }
    }

    private void OpenUIPanel(int panelIndex)
    {
        GameObject targetPanel = null;
        _currentPanelIndex = panelIndex;

        switch (panelIndex) {
        case 0:
            targetPanel = inventoryUIPanel;
            break;
        case 1:
            targetPanel = moduleUIPanel;
            break;
        case 2:
            targetPanel = laboratoryUIPanel;
            break;
        case 3:
            targetPanel = farmUIPanel;
            break;
        case 4:
            targetPanel = mapUIPanel;
            break;
        case 5:
            targetPanel = coreLaunchUIPanel;
            break;
        }

        if (targetPanel != null) {
            targetPanel.SetActive(true);
        }
    }

    private void CloseUIPanel(int panelIndex)
    {
        GameObject targetPanel = null;
        _currentPanelIndex = -1;

        switch (panelIndex) {
        case 0:
            targetPanel = inventoryUIPanel;
            break;
        case 1:
            targetPanel = moduleUIPanel;
            break;
        case 2:
            targetPanel = laboratoryUIPanel;
            break;
        case 3:
            targetPanel = farmUIPanel;
            break;
        case 4:
            targetPanel = mapUIPanel;
            break;
        case 5:
            targetPanel = coreLaunchUIPanel;
            break;
        }

        if (targetPanel != null) {
            targetPanel.SetActive(false);
        }
    }

    private void CloseAllPanels()
    {
        if (moduleUIPanel != null) moduleUIPanel.SetActive(false);
        if (laboratoryUIPanel != null) laboratoryUIPanel.SetActive(false);
        if (farmUIPanel != null) farmUIPanel.SetActive(false);
        if (mapUIPanel != null) mapUIPanel.SetActive(false);
        if (coreLaunchUIPanel != null) coreLaunchUIPanel.SetActive(false);
    }

    private bool IsLoadingScreenActive()
    {
        if (LoadingUIManager.Instance == null)
        {
            return false;
        }

        return LoadingUIManager.Instance.IsAnyLoadingScreenActive();
    }
}
