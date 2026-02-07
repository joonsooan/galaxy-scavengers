using UnityEngine;
using UnityEngine.UI;
using FMODUnity;

public class GameMenuManager : MonoBehaviour
{
    [Header("Menu UI References")]
    [SerializeField] private Button menuOpenButton;
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button returnToTitleButton;
    [SerializeField] private Button quitGameButton;

    [Header("Volume Sliders")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;

    [Header("Audio")]
    [SerializeField] private EventReference menuOpenSound;
    [SerializeField] private EventReference menuCloseSound;
    [SerializeField] private EventReference buttonClickSound;

    private FMODVolumeController _volumeController;
    private bool _isMenuOpen;

    private void Start()
    {
        _volumeController = FMODVolumeController.Instance;
        
        if (mainPanel != null)
        {
            mainPanel.SetActive(false);
        }

        SetupButtons();
        SetupSliders();
    }

    private void SetupButtons()
    {
        if (menuOpenButton != null)
        {
            menuOpenButton.onClick.RemoveAllListeners();
            menuOpenButton.onClick.AddListener(OpenMenu);
        }

        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(CloseMenu);
        }

        if (returnToTitleButton != null)
        {
            returnToTitleButton.onClick.RemoveAllListeners();
            returnToTitleButton.onClick.AddListener(ReturnToTitle);
        }

        if (quitGameButton != null)
        {
            quitGameButton.onClick.RemoveAllListeners();
            quitGameButton.onClick.AddListener(QuitGame);
        }
    }

    private void SetupSliders()
    {
        if (_volumeController == null) return;

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.value = _volumeController.GetMasterVolume();
            masterVolumeSlider.onValueChanged.RemoveAllListeners();
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.value = _volumeController.GetSFXVolume();
            sfxVolumeSlider.onValueChanged.RemoveAllListeners();
            sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.value = _volumeController.GetMusicVolume();
            musicVolumeSlider.onValueChanged.RemoveAllListeners();
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
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

        _isMenuOpen = true;

        if (mainPanel != null)
        {
            mainPanel.SetActive(true);
        }

        if (GameManager.Instance != null && !GameManager.Instance.IsPaused)
        {
            GameManager.Instance.TogglePause();
        }

        if (!menuOpenSound.IsNull)
        {
            RuntimeManager.PlayOneShot(menuOpenSound);
        }
    }

    public void CloseMenu()
    {
        if (!_isMenuOpen) return;

        _isMenuOpen = false;

        if (mainPanel != null)
        {
            mainPanel.SetActive(false);
        }

        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
        {
            GameManager.Instance.TogglePause();
        }

        if (!menuCloseSound.IsNull)
        {
            RuntimeManager.PlayOneShot(menuCloseSound);
        }
    }

    private void OnMasterVolumeChanged(float value)
    {
        if (_volumeController != null)
        {
            _volumeController.SetMasterVolume(value);
        }
        PlayButtonSound();
    }

    private void OnSFXVolumeChanged(float value)
    {
        if (_volumeController != null)
        {
            _volumeController.SetSFXVolume(value);
        }
        PlayButtonSound();
    }

    private void OnMusicVolumeChanged(float value)
    {
        if (_volumeController != null)
        {
            _volumeController.SetMusicVolume(value);
        }
        PlayButtonSound();
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
        if (!buttonClickSound.IsNull)
        {
            RuntimeManager.PlayOneShot(buttonClickSound);
        }
    }

    public bool IsMenuOpen()
    {
        return _isMenuOpen;
    }

    private void OnDestroy()
    {
        if (menuOpenButton != null)
        {
            menuOpenButton.onClick.RemoveAllListeners();
        }

        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
        }

        if (returnToTitleButton != null)
        {
            returnToTitleButton.onClick.RemoveAllListeners();
        }

        if (quitGameButton != null)
        {
            quitGameButton.onClick.RemoveAllListeners();
        }

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.RemoveAllListeners();
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.RemoveAllListeners();
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.RemoveAllListeners();
        }
    }
}
