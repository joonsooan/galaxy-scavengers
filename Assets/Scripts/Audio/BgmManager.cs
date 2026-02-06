using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using STOP_MODE = FMOD.Studio.STOP_MODE;

public class BgmManager : MonoBehaviour
{
    [SerializeField] private EventReference titleBgm;
    [SerializeField] private EventReference baseBgm;
    [SerializeField] private EventReference[] gameBgms;
    [SerializeField] private EventReference loadingBgm;
    [SerializeField] private EventReference successLoadingBgm;
    [SerializeField] private EventReference failureLoadingBgm;
    [SerializeField] private float defaultFadeOutTime = 0.5f;
    [SerializeField] private float defaultFadeInTime = 1.0f;
    [Header("Game BGM Settings")]
    [SerializeField] private float gameBgmFadeInTime = 1.0f;
    [SerializeField] private float gameBgmFadeOutTime = 1.0f;
    [SerializeField] private float gameBgmCooldownTime = 30f;

    private EventInstance _currentInstance;
    private EventInstance _loadingInstance;
    private EventInstance _successLoadingInstance;
    private EventInstance _failureLoadingInstance;
    private bool _hasInstance;
    private bool _hasLoadingInstance;
    private bool _hasSuccessLoadingInstance;
    private bool _hasFailureLoadingInstance;
    private EventReference _currentBgm;
    private EventReference _lastGameBgm;
    private Coroutine _gameBgmCooldownCoroutine;
    private Coroutine _playGameBgmCoroutine;
    private bool _isGameBgmCooldownActive;
    private WaitForSecondsRealtime _gameBgmCooldownWait;
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

    private void Start()
    {
        _gameBgmCooldownWait = CoroutineCache.GetWaitForSecondsRealtime(gameBgmCooldownTime);
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
        if (gameBgms == null || gameBgms.Length == 0)
        {
            return;
        }

        if (_isGameBgmCooldownActive)
        {
            return;
        }

        if (_gameBgmCooldownCoroutine != null)
        {
            StopCoroutine(_gameBgmCooldownCoroutine);
        }

        List<EventReference> availableBgms = new List<EventReference>();
        foreach (EventReference bgm in gameBgms)
        {
            if (!bgm.IsNull && (!_lastGameBgm.IsNull && bgm.Guid != _lastGameBgm.Guid || _lastGameBgm.IsNull))
            {
                availableBgms.Add(bgm);
            }
        }

        if (availableBgms.Count == 0)
        {
            availableBgms.AddRange(gameBgms.Where(bgm => !bgm.IsNull));
        }

        if (availableBgms.Count == 0)
        {
            return;
        }

        EventReference selectedBgm = availableBgms[Random.Range(0, availableBgms.Count)];
        _lastGameBgm = selectedBgm;

        if (_playGameBgmCoroutine != null)
        {
            StopCoroutine(_playGameBgmCoroutine);
        }
        _playGameBgmCoroutine = StartCoroutine(PlayGameBgmWithFade(selectedBgm));
    }

    private IEnumerator PlayGameBgmWithFade(EventReference bgm)
    {
        if (_hasInstance)
        {
            yield return StartCoroutine(FadeOutBgm(gameBgmFadeOutTime));
        }

        _currentBgm = bgm;
        _currentInstance = RuntimeManager.CreateInstance(bgm);
        _hasInstance = true;
        _currentInstance.setVolume(0f);
        _currentInstance.start();

        yield return StartCoroutine(FadeInGameBgm(gameBgmFadeInTime));

        _playGameBgmCoroutine = null;
        _gameBgmCooldownCoroutine = StartCoroutine(GameBgmCooldown());
    }

    private IEnumerator FadeInGameBgm(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && _hasInstance)
        {
            elapsed += Time.unscaledDeltaTime;
            float volume = Mathf.Clamp01(elapsed / duration);
            _currentInstance.setVolume(volume);
            yield return null;
        }

        if (_hasInstance)
        {
            _currentInstance.setVolume(1f);
        }
    }

    private IEnumerator GameBgmCooldown()
    {
        _isGameBgmCooldownActive = true;

        while (_hasInstance)
        {
            PLAYBACK_STATE state;
            _currentInstance.getPlaybackState(out state);
            if (state == PLAYBACK_STATE.STOPPED || state == PLAYBACK_STATE.STOPPING)
            {
                break;
            }
            yield return null;
        }

        if (_hasInstance && gameBgmFadeOutTime > 0f)
        {
            yield return StartCoroutine(FadeOutBgm(gameBgmFadeOutTime));
        }

        yield return _gameBgmCooldownWait;

        _isGameBgmCooldownActive = false;

        PlayGameBgm();
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

        StopGameBgmCooldown();

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
        StopGameBgmCooldown();

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

        StopGameBgmCooldown();

        try {
            _currentInstance.stop(immediate ? STOP_MODE.IMMEDIATE : STOP_MODE.ALLOWFADEOUT);
            _currentInstance.release();
        }
        finally {
            _hasInstance = false;
            _currentBgm = default;
        }
    }

    private void StopGameBgmCooldown()
    {
        if (_playGameBgmCoroutine != null)
        {
            StopCoroutine(_playGameBgmCoroutine);
            _playGameBgmCoroutine = null;
        }
        if (_gameBgmCooldownCoroutine != null)
        {
            StopCoroutine(_gameBgmCooldownCoroutine);
            _gameBgmCooldownCoroutine = null;
        }
        _isGameBgmCooldownActive = false;
    }

    public void PlaySuccessLoadingBgm(float fadeInTime = -1f)
    {
        if (successLoadingBgm.IsNull) {
            return;
        }

        float fadeTime = fadeInTime >= 0f ? fadeInTime : defaultFadeInTime;
        StopSuccessLoadingBgm(fadeTime);

        if (_hasInstance)
        {
            StartCoroutine(FadeOutGameBgmAndPlaySuccess(fadeTime));
        }
        else
        {
            _successLoadingInstance = RuntimeManager.CreateInstance(successLoadingBgm);
            _hasSuccessLoadingInstance = true;
            _successLoadingInstance.setVolume(0f);
            _successLoadingInstance.start();
            
            StartCoroutine(FadeInSuccessLoadingBgm(fadeTime));
        }
    }

    private IEnumerator FadeOutGameBgmAndPlaySuccess(float fadeInTime)
    {
        yield return StartCoroutine(FadeOutBgm(gameBgmFadeOutTime));

        _successLoadingInstance = RuntimeManager.CreateInstance(successLoadingBgm);
        _hasSuccessLoadingInstance = true;
        _successLoadingInstance.setVolume(0f);
        _successLoadingInstance.start();
        
        yield return StartCoroutine(FadeInSuccessLoadingBgm(fadeInTime));
    }

    public void StopSuccessLoadingBgm(float fadeOutTime = -1f)
    {
        if (!_hasSuccessLoadingInstance) {
            return;
        }

        float fadeTime = fadeOutTime >= 0f ? fadeOutTime : defaultFadeOutTime;
        StartCoroutine(FadeOutSuccessLoadingBgm(fadeTime));
    }

    private IEnumerator FadeInSuccessLoadingBgm(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && _hasSuccessLoadingInstance)
        {
            elapsed += Time.unscaledDeltaTime;
            float volume = Mathf.Clamp01(elapsed / duration);
            _successLoadingInstance.setVolume(volume);
            yield return null;
        }
        
        if (_hasSuccessLoadingInstance) {
            _successLoadingInstance.setVolume(1f);
        }
    }

    private IEnumerator FadeOutSuccessLoadingBgm(float duration)
    {
        if (!_hasSuccessLoadingInstance) {
            yield break;
        }

        float startVolume = 1f;
        _successLoadingInstance.getVolume(out startVolume);

        float elapsed = 0f;
        while (elapsed < duration && _hasSuccessLoadingInstance)
        {
            elapsed += Time.unscaledDeltaTime;
            float volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
            _successLoadingInstance.setVolume(volume);
            yield return null;
        }

        StopSuccessLoadingBgmImmediate();
    }

    private void StopSuccessLoadingBgmImmediate()
    {
        if (!_hasSuccessLoadingInstance) {
            return;
        }

        try {
            _successLoadingInstance.stop(STOP_MODE.IMMEDIATE);
            _successLoadingInstance.release();
        }
        finally {
            _hasSuccessLoadingInstance = false;
        }
    }

    public void PlayFailureLoadingBgm(float fadeInTime = -1f)
    {
        if (failureLoadingBgm.IsNull) {
            return;
        }

        float fadeTime = fadeInTime >= 0f ? fadeInTime : defaultFadeInTime;
        StopFailureLoadingBgm(fadeTime);

        if (_hasInstance)
        {
            StartCoroutine(FadeOutGameBgmAndPlayFailure(fadeTime));
        }
        else
        {
            _failureLoadingInstance = RuntimeManager.CreateInstance(failureLoadingBgm);
            _hasFailureLoadingInstance = true;
            _failureLoadingInstance.setVolume(0f);
            _failureLoadingInstance.start();
            
            StartCoroutine(FadeInFailureLoadingBgm(fadeTime));
        }
    }

    private IEnumerator FadeOutGameBgmAndPlayFailure(float fadeInTime)
    {
        yield return StartCoroutine(FadeOutBgm(gameBgmFadeOutTime));

        _failureLoadingInstance = RuntimeManager.CreateInstance(failureLoadingBgm);
        _hasFailureLoadingInstance = true;
        _failureLoadingInstance.setVolume(0f);
        _failureLoadingInstance.start();
        
        yield return StartCoroutine(FadeInFailureLoadingBgm(fadeInTime));
    }

    public void StopFailureLoadingBgm(float fadeOutTime = -1f)
    {
        if (!_hasFailureLoadingInstance) {
            return;
        }

        float fadeTime = fadeOutTime >= 0f ? fadeOutTime : defaultFadeOutTime;
        StartCoroutine(FadeOutFailureLoadingBgm(fadeTime));
    }

    private IEnumerator FadeInFailureLoadingBgm(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && _hasFailureLoadingInstance)
        {
            elapsed += Time.unscaledDeltaTime;
            float volume = Mathf.Clamp01(elapsed / duration);
            _failureLoadingInstance.setVolume(volume);
            yield return null;
        }
        
        if (_hasFailureLoadingInstance) {
            _failureLoadingInstance.setVolume(1f);
        }
    }

    private IEnumerator FadeOutFailureLoadingBgm(float duration)
    {
        if (!_hasFailureLoadingInstance) {
            yield break;
        }

        float startVolume = 1f;
        _failureLoadingInstance.getVolume(out startVolume);

        float elapsed = 0f;
        while (elapsed < duration && _hasFailureLoadingInstance)
        {
            elapsed += Time.unscaledDeltaTime;
            float volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
            _failureLoadingInstance.setVolume(volume);
            yield return null;
        }

        StopFailureLoadingBgmImmediate();
    }

    private void StopFailureLoadingBgmImmediate()
    {
        if (!_hasFailureLoadingInstance) {
            return;
        }

        try {
            _failureLoadingInstance.stop(STOP_MODE.IMMEDIATE);
            _failureLoadingInstance.release();
        }
        finally {
            _hasFailureLoadingInstance = false;
        }
    }
}
