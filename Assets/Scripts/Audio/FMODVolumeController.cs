using FMOD.Studio;
using FMODUnity;
using UnityEngine;

public class FMODVolumeController : MonoBehaviour
{
    [Header("VCA Paths")]
    [SerializeField] private string masterVCAPath = "vca:/Master";
    [SerializeField] private string musicVCAPath = "vca:/Music";
    [SerializeField] private string sfxVCAPath = "vca:/SFX";

    [Header("Default Volumes")]
    [SerializeField] private float defaultMasterVolume = 1.0f;
    [SerializeField] private float defaultMusicVolume = 1.0f;
    [SerializeField] private float defaultSFXVolume = 1.0f;

    private const string PrefsMasterVolume = "FMOD_MasterVolume";
    private const string PrefsMusicVolume = "FMOD_MusicVolume";
    private const string PrefsSFXVolume = "FMOD_SFXVolume";

    private VCA _masterVCA;
    private VCA _musicVCA;
    private VCA _sfxVCA;

    private float _masterVolume;
    private float _musicVolume;
    private float _sfxVolume;

    public static FMODVolumeController Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeVCAs();
            LoadVolumes();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeVCAs()
    {
        try
        {
            if (!string.IsNullOrEmpty(masterVCAPath))
            {
                _masterVCA = RuntimeManager.GetVCA(masterVCAPath);
            }
        }
        catch (VCANotFoundException)
        {
            Debug.LogWarning($"FMOD VCA not found: {masterVCAPath}");
        }

        try
        {
            if (!string.IsNullOrEmpty(musicVCAPath))
            {
                _musicVCA = RuntimeManager.GetVCA(musicVCAPath);
            }
        }
        catch (VCANotFoundException)
        {
            Debug.LogWarning($"FMOD VCA not found: {musicVCAPath}");
        }

        try
        {
            if (!string.IsNullOrEmpty(sfxVCAPath))
            {
                _sfxVCA = RuntimeManager.GetVCA(sfxVCAPath);
            }
        }
        catch (VCANotFoundException)
        {
            Debug.LogWarning($"FMOD VCA not found: {sfxVCAPath}");
        }
    }

    private void LoadVolumes()
    {
        _masterVolume = PlayerPrefs.GetFloat(PrefsMasterVolume, defaultMasterVolume);
        _musicVolume = PlayerPrefs.GetFloat(PrefsMusicVolume, defaultMusicVolume);
        _sfxVolume = PlayerPrefs.GetFloat(PrefsSFXVolume, defaultSFXVolume);

        ApplyMasterVolume(_masterVolume);
        ApplyMusicVolume(_musicVolume);
        ApplySFXVolume(_sfxVolume);
    }

    public void SetMasterVolume(float volume)
    {
        _masterVolume = Mathf.Clamp01(volume);
        ApplyMasterVolume(_masterVolume);
        PlayerPrefs.SetFloat(PrefsMasterVolume, _masterVolume);
        PlayerPrefs.Save();
    }

    public void SetMusicVolume(float volume)
    {
        _musicVolume = Mathf.Clamp01(volume);
        ApplyMusicVolume(_musicVolume);
        PlayerPrefs.SetFloat(PrefsMusicVolume, _musicVolume);
        PlayerPrefs.Save();
    }

    public void SetSFXVolume(float volume)
    {
        _sfxVolume = Mathf.Clamp01(volume);
        ApplySFXVolume(_sfxVolume);
        PlayerPrefs.SetFloat(PrefsSFXVolume, _sfxVolume);
        PlayerPrefs.Save();
    }

    private void ApplyMasterVolume(float volume)
    {
        if (_masterVCA.isValid())
        {
            _masterVCA.setVolume(volume);
        }
    }

    private void ApplyMusicVolume(float volume)
    {
        if (_musicVCA.isValid())
        {
            _musicVCA.setVolume(volume);
        }
    }

    private void ApplySFXVolume(float volume)
    {
        if (_sfxVCA.isValid())
        {
            _sfxVCA.setVolume(volume);
        }
    }

    public float GetMasterVolume()
    {
        return _masterVolume;
    }

    public float GetMusicVolume()
    {
        return _musicVolume;
    }

    public float GetSFXVolume()
    {
        return _sfxVolume;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
