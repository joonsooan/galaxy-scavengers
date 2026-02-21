using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FMODUnity;

public class GameMenuManager : MonoBehaviour
{
    [Header("Menu UI References")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject overlayPanel;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button returnToTitleButton;
    [SerializeField] private Button quitGameButton;

    [Header("Volume Sliders")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;

    [Header("Volume Text Displays")]
    [SerializeField] private TMP_Text masterVolumeText;
    [SerializeField] private TMP_Text sfxVolumeText;
    [SerializeField] private TMP_Text musicVolumeText;

    private GameObject _currentMainPanel;
    private GameObject _currentOverlayPanel;
    private Button _currentContinueButton;
    private Button _currentReturnToTitleButton;
    private Button _currentQuitGameButton;
    private Slider _currentMasterVolumeSlider;
    private Slider _currentSFXVolumeSlider;
    private Slider _currentMusicVolumeSlider;
    private TMP_Text _currentMasterVolumeText;
    private TMP_Text _currentSFXVolumeText;
    private TMP_Text _currentMusicVolumeText;

    [Header("Audio")]
    [SerializeField] private EventReference menuOpenSound;
    [SerializeField] private EventReference menuCloseSound;
    [SerializeField] private EventReference buttonClickSound;

    private FMODVolumeController _volumeController;
    private Button _currentMenuOpenButton;
    private bool _isMenuOpen;
    private MainControlPanel _mainControlPanel;

    public static GameMenuManager Instance { get; private set; }

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
            return;
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        _volumeController = FMODVolumeController.Instance;
        InitializeUIReferences();
        FindAndSetupMenuUI();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        FindAndSetupMenuUI();
        _mainControlPanel = null;
        TryCacheMainControlPanel();
    }

    private void InitializeUIReferences()
    {
        _currentMainPanel = mainPanel;
        _currentOverlayPanel = overlayPanel;
        _currentContinueButton = continueButton;
        _currentReturnToTitleButton = returnToTitleButton;
        _currentQuitGameButton = quitGameButton;
        _currentMasterVolumeSlider = masterVolumeSlider;
        _currentSFXVolumeSlider = sfxVolumeSlider;
        _currentMusicVolumeSlider = musicVolumeSlider;
        _currentMasterVolumeText = masterVolumeText;
        _currentSFXVolumeText = sfxVolumeText;
        _currentMusicVolumeText = musicVolumeText;
    }

    private void FindAndSetupMenuUI()
    {
        FindAndSetupMenuOpenButton();
        
        MenuUIProvider provider = FindFirstObjectByType<MenuUIProvider>();
        if (provider != null)
        {
            SetMenuUI(provider);
        }
        else
        {
            InitializeUIReferences();
        }

        if (_currentMainPanel == null)
        {
            Debug.LogWarning("GameMenuManager: Main Panel이 설정되지 않았습니다. MenuUIProvider를 추가하거나 GameMenuManager의 기본 mainPanel을 설정하세요.");
        }
        else
        {
            _currentMainPanel.SetActive(false);
        }

        if (_currentOverlayPanel != null)
        {
            _currentOverlayPanel.SetActive(false);
        }

        SetupButtons();
        SetupSliders();
    }

    public void SetMenuUI(MenuUIProvider provider)
    {
        if (provider == null) return;

        if (provider.mainPanel != null)
            _currentMainPanel = provider.mainPanel;
        else
            _currentMainPanel = mainPanel;

        if (provider.overlayPanel != null)
            _currentOverlayPanel = provider.overlayPanel;
        else
            _currentOverlayPanel = overlayPanel;

        if (provider.continueButton != null)
            _currentContinueButton = provider.continueButton;
        else
            _currentContinueButton = continueButton;

        if (provider.returnToTitleButton != null)
            _currentReturnToTitleButton = provider.returnToTitleButton;
        else
            _currentReturnToTitleButton = returnToTitleButton;

        if (provider.quitGameButton != null)
            _currentQuitGameButton = provider.quitGameButton;
        else
            _currentQuitGameButton = quitGameButton;

        if (provider.masterVolumeSlider != null)
            _currentMasterVolumeSlider = provider.masterVolumeSlider;
        else
            _currentMasterVolumeSlider = masterVolumeSlider;

        if (provider.sfxVolumeSlider != null)
            _currentSFXVolumeSlider = provider.sfxVolumeSlider;
        else
            _currentSFXVolumeSlider = sfxVolumeSlider;

        if (provider.musicVolumeSlider != null)
            _currentMusicVolumeSlider = provider.musicVolumeSlider;
        else
            _currentMusicVolumeSlider = musicVolumeSlider;

        if (provider.masterVolumeText != null)
            _currentMasterVolumeText = provider.masterVolumeText;
        else
            _currentMasterVolumeText = masterVolumeText;

        if (provider.sfxVolumeText != null)
            _currentSFXVolumeText = provider.sfxVolumeText;
        else
            _currentSFXVolumeText = sfxVolumeText;

        if (provider.musicVolumeText != null)
            _currentMusicVolumeText = provider.musicVolumeText;
        else
            _currentMusicVolumeText = musicVolumeText;
    }

    public void SetMenuOpenButton(Button button)
    {
        if (_currentMenuOpenButton != null)
        {
            _currentMenuOpenButton.onClick.RemoveListener(OpenMenu);
        }

        _currentMenuOpenButton = button;

        if (_currentMenuOpenButton != null)
        {
            _currentMenuOpenButton.onClick.RemoveAllListeners();
            _currentMenuOpenButton.onClick.AddListener(OpenMenu);
        }
    }

    private void FindAndSetupMenuOpenButton()
    {
        MenuOpenButtonProvider provider = FindFirstObjectByType<MenuOpenButtonProvider>();
        if (provider != null && provider.menuOpenButton != null)
        {
            SetMenuOpenButton(provider.menuOpenButton);
        }
    }

    private void SetupButtons()
    {
        if (_currentContinueButton != null)
        {
            _currentContinueButton.onClick.RemoveAllListeners();
            _currentContinueButton.onClick.AddListener(CloseMenu);
        }

        if (_currentReturnToTitleButton != null)
        {
            _currentReturnToTitleButton.onClick.RemoveAllListeners();
            _currentReturnToTitleButton.onClick.AddListener(ReturnToTitle);
        }

        if (_currentQuitGameButton != null)
        {
            _currentQuitGameButton.onClick.RemoveAllListeners();
            _currentQuitGameButton.onClick.AddListener(QuitGame);
        }
    }

    private void SetupSliders()
    {
        if (_volumeController == null) return;

        if (_currentMasterVolumeSlider != null)
        {
            float masterVolume = _volumeController.GetMasterVolume();
            _currentMasterVolumeSlider.value = masterVolume;
            _currentMasterVolumeSlider.onValueChanged.RemoveAllListeners();
            _currentMasterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            UpdateMasterVolumeText(masterVolume);
        }

        if (_currentSFXVolumeSlider != null)
        {
            float sfxVolume = _volumeController.GetSFXVolume();
            _currentSFXVolumeSlider.value = sfxVolume;
            _currentSFXVolumeSlider.onValueChanged.RemoveAllListeners();
            _currentSFXVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
            UpdateSFXVolumeText(sfxVolume);
        }

        if (_currentMusicVolumeSlider != null)
        {
            float musicVolume = _volumeController.GetMusicVolume();
            _currentMusicVolumeSlider.value = musicVolume;
            _currentMusicVolumeSlider.onValueChanged.RemoveAllListeners();
            _currentMusicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            UpdateMusicVolumeText(musicVolume);
        }
    }

    private void Update()
    {
        if (IsLoadingScreenActive())
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (HelpUIManager.Instance != null && HelpUIManager.Instance.IsHelpOpen())
            {
                HelpUIManager.Instance.CloseHelp();
                return;
            }

            MainControlPanel mainControlPanel = TryCacheMainControlPanel();
            if (mainControlPanel != null && mainControlPanel.IsResourceStatPanelActive())
            {
                mainControlPanel.CloseResourceStatPanel();
                return;
            }

            if (_isMenuOpen)
            {
                CloseMenu();
            }
            else
            {
                OpenMenu();
            }
        }
    }

    public void OpenMenu()
    {
        if (_isMenuOpen) return;

        if (_currentMainPanel == null)
        {
            Debug.LogWarning("GameMenuManager: Main Panel이 설정되지 않았습니다. MenuUIProvider를 확인하세요.");
            return;
        }

        _isMenuOpen = true;

        _currentMainPanel.SetActive(true);

        if (_currentOverlayPanel != null)
        {
            _currentOverlayPanel.SetActive(true);
        }

        if (IsGameScene() && GameManager.Instance != null && !GameManager.Instance.IsPaused)
        {
            GameManager.Instance.TogglePause();
        }

        if (!IsGameScene() && !menuOpenSound.IsNull)
        {
            RuntimeManager.PlayOneShot(menuOpenSound);
        }
    }

    public void CloseMenu()
    {
        if (!_isMenuOpen) return;

        _isMenuOpen = false;

        if (_currentMainPanel != null)
        {
            _currentMainPanel.SetActive(false);
        }

        if (_currentOverlayPanel != null)
        {
            _currentOverlayPanel.SetActive(false);
        }

        if (IsGameScene() && GameManager.Instance != null && GameManager.Instance.IsPaused)
        {
            GameManager.Instance.TogglePause();
        }

        if (!IsGameScene() && !menuCloseSound.IsNull)
        {
            RuntimeManager.PlayOneShot(menuCloseSound);
        }
    }

    private bool IsGameScene()
    {
        return SceneManager.GetActiveScene().name == "GameScene";
    }

    private void OnMasterVolumeChanged(float value)
    {
        if (_volumeController != null)
        {
            _volumeController.SetMasterVolume(value);
        }
        UpdateMasterVolumeText(value);
    }

    private void OnSFXVolumeChanged(float value)
    {
        if (_volumeController != null)
        {
            _volumeController.SetSFXVolume(value);
        }
        UpdateSFXVolumeText(value);
    }

    private void OnMusicVolumeChanged(float value)
    {
        if (_volumeController != null)
        {
            _volumeController.SetMusicVolume(value);
        }
        UpdateMusicVolumeText(value);
    }

    private void UpdateMasterVolumeText(float value)
    {
        if (_currentMasterVolumeText != null)
        {
            int percentage = Mathf.RoundToInt(value * 100f);
            _currentMasterVolumeText.text = $"{percentage}";
        }
    }

    private void UpdateSFXVolumeText(float value)
    {
        if (_currentSFXVolumeText != null)
        {
            int percentage = Mathf.RoundToInt(value * 100f);
            _currentSFXVolumeText.text = $"{percentage}";
        }
    }

    private void UpdateMusicVolumeText(float value)
    {
        if (_currentMusicVolumeText != null)
        {
            int percentage = Mathf.RoundToInt(value * 100f);
            _currentMusicVolumeText.text = $"{percentage}";
        }
    }

    private void ReturnToTitle()
    {
        PlayButtonSound();
        
        if (SceneLoader.Instance != null)
        {
            CloseMenu();
            SceneLoader.Instance.LoadTitleScene();
        }
    }

    private void QuitGame()
    {
        PlayButtonSound();
        
        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.QuitGame();
        }
        else
        {
            Application.Quit();
        }
    }

    private void PlayButtonSound()
    {
        if (FMODUIButton.HasPlayedClickSoundThisFrame)
        {
            return;
        }

        if (!buttonClickSound.IsNull)
        {
            RuntimeManager.PlayOneShot(buttonClickSound);
        }
    }

    public bool IsMenuOpen()
    {
        return _isMenuOpen;
    }

    private MainControlPanel TryCacheMainControlPanel()
    {
        if (_mainControlPanel == null)
        {
            _mainControlPanel = FindFirstObjectByType<MainControlPanel>();
        }

        return _mainControlPanel;
    }

    private bool IsLoadingScreenActive()
    {
        if (LoadingUIManager.Instance == null)
        {
            return false;
        }

        LoadingScreen loadingScreen = LoadingUIManager.Instance.GetLoadingScreenComponent();
        if (loadingScreen == null)
        {
            return false;
        }

        return loadingScreen.gameObject.activeSelf;
    }

    private void OnDestroy()
    {
        if (_currentMenuOpenButton != null)
        {
            _currentMenuOpenButton.onClick.RemoveAllListeners();
        }

        if (_currentContinueButton != null)
        {
            _currentContinueButton.onClick.RemoveAllListeners();
        }

        if (_currentReturnToTitleButton != null)
        {
            _currentReturnToTitleButton.onClick.RemoveAllListeners();
        }

        if (_currentQuitGameButton != null)
        {
            _currentQuitGameButton.onClick.RemoveAllListeners();
        }

        if (_currentMasterVolumeSlider != null)
        {
            _currentMasterVolumeSlider.onValueChanged.RemoveAllListeners();
        }

        if (_currentSFXVolumeSlider != null)
        {
            _currentSFXVolumeSlider.onValueChanged.RemoveAllListeners();
        }

        if (_currentMusicVolumeSlider != null)
        {
            _currentMusicVolumeSlider.onValueChanged.RemoveAllListeners();
        }
    }
}
