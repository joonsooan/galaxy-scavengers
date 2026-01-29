using System.Collections;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using STOP_MODE = FMOD.Studio.STOP_MODE;

public class BgmManager : MonoBehaviour
{
    [SerializeField] private EventReference titleBgm;
    [SerializeField] private EventReference baseBgm;
    [SerializeField] private EventReference gameBgm;
    [SerializeField] private EventReference loadingBgm;
    [SerializeField] private float defaultFadeOutTime = 0.5f;
    [SerializeField] private float defaultFadeInTime = 1.0f;

    private EventInstance _currentInstance;
    private EventInstance _loadingInstance;
    private bool _hasInstance;
    private bool _hasLoadingInstance;
    private EventReference _currentBgm;
    public static BgmManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) {
            StopCurrent(true);
            Instance = null;
        }
    }

    public void PlayTitleBgm()
    {
        PlayBgm(titleBgm, false);
    }

    public void PlayBaseBgm()
    {
        PlayBgm(titleBgm, false);
    }

    public void PlayGameBgm()
    {
        PlayBgm(gameBgm);
    }

    public void PlayBgm(EventReference bgm, float fadeOutTime = -1f)
    {
        PlayBgm(bgm, true, fadeOutTime);
    }

    public void PlayBgm(EventReference bgm, bool stopCurrent, float fadeOutTime = -1f)
    {
        if (bgm.IsNull) {
            return;
        }

        if (_hasInstance && _currentBgm.Guid == bgm.Guid)
        {
            return;
        }

        float fadeTime = fadeOutTime >= 0f ? fadeOutTime : defaultFadeOutTime;
        if (stopCurrent) {
            StopCurrent(fadeTime <= 0f);
        }

        _currentBgm = bgm;
        _currentInstance = RuntimeManager.CreateInstance(bgm);
        _hasInstance = true;
        _currentInstance.start();
    }

    public void PlayLoadingBgm(float fadeInTime = -1f)
    {
        if (loadingBgm.IsNull) {
            return;
        }

        float fadeTime = fadeInTime >= 0f ? fadeInTime : defaultFadeInTime;
        StopLoadingBgm(fadeTime);

        _loadingInstance = RuntimeManager.CreateInstance(loadingBgm);
        _hasLoadingInstance = true;
        _loadingInstance.setVolume(0f);
        _loadingInstance.start();
        
        StartCoroutine(FadeInLoadingBgm(fadeTime));
    }

    public void StopLoadingBgm(float fadeOutTime = -1f)
    {
        if (!_hasLoadingInstance) {
            return;
        }

        float fadeTime = fadeOutTime >= 0f ? fadeOutTime : defaultFadeOutTime;
        StartCoroutine(FadeOutLoadingBgm(fadeTime));
    }

    private IEnumerator FadeInLoadingBgm(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && _hasLoadingInstance)
        {
            elapsed += Time.unscaledDeltaTime;
            float volume = Mathf.Clamp01(elapsed / duration);
            _loadingInstance.setVolume(volume);
            yield return null;
        }
        
        if (_hasLoadingInstance) {
            _loadingInstance.setVolume(1f);
        }
    }

    private IEnumerator FadeOutLoadingBgm(float duration)
    {
        if (!_hasLoadingInstance) {
            yield break;
        }

        float startVolume = 1f;
        _loadingInstance.getVolume(out startVolume);

        float elapsed = 0f;
        while (elapsed < duration && _hasLoadingInstance)
        {
            elapsed += Time.unscaledDeltaTime;
            float volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
            _loadingInstance.setVolume(volume);
            yield return null;
        }

        StopLoadingBgmImmediate();
    }

    private void StopLoadingBgmImmediate()
    {
        if (!_hasLoadingInstance) {
            return;
        }

        try {
            _loadingInstance.stop(STOP_MODE.IMMEDIATE);
            _loadingInstance.release();
        }
        finally {
            _hasLoadingInstance = false;
        }
    }

    public void StopBgm(bool immediate = false)
    {
        StopBgm(immediate ? 0f : -1f);
    }

    public void StopBgm(float fadeOutTime)
    {
        if (fadeOutTime < 0f) {
            fadeOutTime = defaultFadeOutTime;
        }

        if (fadeOutTime <= 0f) {
            StopCurrent(true);
        }
        else {
            StartCoroutine(FadeOutBgm(fadeOutTime));
        }
    }

    private IEnumerator FadeOutBgm(float duration)
    {
        if (!_hasInstance) {
            yield break;
        }

        float startVolume = 1f;
        _currentInstance.getVolume(out startVolume);

        float elapsed = 0f;
        while (elapsed < duration && _hasInstance)
        {
            elapsed += Time.unscaledDeltaTime;
            float volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
            _currentInstance.setVolume(volume);
            yield return null;
        }

        StopCurrent(true);
    }

    private void StopCurrent(bool immediate)
    {
        if (!_hasInstance) {
            return;
        }

        try {
            _currentInstance.stop(immediate ? STOP_MODE.IMMEDIATE : STOP_MODE.ALLOWFADEOUT);
            _currentInstance.release();
        }
        finally {
            _hasInstance = false;
            _currentBgm = default;
        }
    }
}
