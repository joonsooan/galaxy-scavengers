using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using FMOD;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using STOP_MODE = FMOD.Studio.STOP_MODE;

public partial class BgmManager : MonoBehaviour
{
    [SerializeField] private EventReference titleBgm;
    [SerializeField] private EventReference baseBgm;
    [SerializeField] private EventReference[] gameBgms;
    [SerializeField] private EventReference tutorialBgm;
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
    private Coroutine _gameBgmEndFadeCoroutine;
    private Coroutine _loadingFadeInCoroutine;
    private Coroutine _loadingFadeOutCoroutine;
    private Coroutine _successFadeInCoroutine;
    private Coroutine _successFadeOutCoroutine;
    private Coroutine _failureFadeInCoroutine;
    private Coroutine _failureFadeOutCoroutine;
    private Coroutine _playSuccessTransitionCoroutine;
    private Coroutine _playFailureTransitionCoroutine;
    private bool _isGameBgmCooldownActive;
    private WaitForSecondsRealtime _gameBgmCooldownWait;
    private static readonly EVENT_CALLBACK CountdownBeatCallback = OnCountdownBeatCallback;
    private int _pendingCountdownBeatTicks;
    private int _countdownBeatSyncActive;
    private int _latestCountdownTempoMilliBpm;
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
            StopTransitionCoroutines();
            StopCurrent(true);
            StopLoadingBgmImmediate();
            StopSuccessLoadingBgmImmediate();
            StopFailureLoadingBgmImmediate();
            Instance = null;
        }
    }

    public void PlayTitleBgm()
    {
        StopGameBgmCooldown();
        
        if (_playGameBgmCoroutine != null)
        {
            StopCoroutine(_playGameBgmCoroutine);
            _playGameBgmCoroutine = null;
        }
        
        PlayBgm(titleBgm, true);
    }

    public void PlayBaseBgm()
    {
        PlayBgm(titleBgm, false);
    }

    public void PlayTutorialBgm()
    {
        if (tutorialBgm.IsNull)
        {
            PlayGameBgm();
            return;
        }

        StopGameBgmCooldown();

        if (_playGameBgmCoroutine != null)
        {
            StopCoroutine(_playGameBgmCoroutine);
            _playGameBgmCoroutine = null;
        }

        if (_hasInstance)
        {
            StopCurrent(false);
        }

        _lastGameBgm = tutorialBgm;
        _playGameBgmCoroutine = StartCoroutine(PlayGameBgmWithFade(tutorialBgm));
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

        EventReference selectedBgm = availableBgms[UnityEngine.Random.Range(0, availableBgms.Count)];
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
        if (_gameBgmEndFadeCoroutine != null)
        {
            StopCoroutine(_gameBgmEndFadeCoroutine);
            _gameBgmEndFadeCoroutine = null;
        }
        _gameBgmEndFadeCoroutine = StartCoroutine(GameBgmEndFadeOut());
        _gameBgmCooldownCoroutine = StartCoroutine(GameBgmCooldown());
    }

    private IEnumerator GameBgmEndFadeOut()
    {
        EventDescription desc;
        RESULT descResult = _currentInstance.getDescription(out desc);
        if (descResult != RESULT.OK)
        {
            _gameBgmEndFadeCoroutine = null;
            yield break;
        }
        int lengthMs;
        if (desc.getLength(out lengthMs) != RESULT.OK || lengthMs <= 0)
        {
            _gameBgmEndFadeCoroutine = null;
            yield break;
        }
        int fadeOutMs = Mathf.Max(0, (int)(gameBgmFadeOutTime * 1000f));
        int startFadeAtMs = Mathf.Max(0, lengthMs - fadeOutMs);
        while (_hasInstance)
        {
            PLAYBACK_STATE state;
            _currentInstance.getPlaybackState(out state);
            if (state == PLAYBACK_STATE.STOPPED || state == PLAYBACK_STATE.STOPPING)
            {
                _gameBgmEndFadeCoroutine = null;
                yield break;
            }
            int posMs;
            if (_currentInstance.getTimelinePosition(out posMs) == RESULT.OK && posMs >= startFadeAtMs)
            {
                yield return StartCoroutine(FadeOutBgm(gameBgmFadeOutTime, false));
                _gameBgmEndFadeCoroutine = null;
                yield break;
            }
            yield return null;
        }
        _gameBgmEndFadeCoroutine = null;
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

        if (_hasInstance)
        {
            StopCurrentInstanceOnly();
        }

        if (_gameBgmEndFadeCoroutine != null)
        {
            StopCoroutine(_gameBgmEndFadeCoroutine);
            _gameBgmEndFadeCoroutine = null;
        }

        yield return _gameBgmCooldownWait;

        _isGameBgmCooldownActive = false;
        StartCoroutine(DeferredPlayNextGameBgm());
    }

    private IEnumerator DeferredPlayNextGameBgm()
    {
        yield return null;
        PlayGameBgm();
    }

    public void PlayBgm(EventReference bgm, float fadeOutTime = -1f)
    {
        PlayBgm(bgm, true, fadeOutTime);
    }

    public bool EnableCountdownBeatSync()
    {
        if (!_hasInstance)
        {
            return false;
        }

        DisableCountdownBeatSync();

        RESULT callbackResult = _currentInstance.setCallback(CountdownBeatCallback, EVENT_CALLBACK_TYPE.TIMELINE_BEAT | EVENT_CALLBACK_TYPE.STOPPED);

        if (callbackResult != RESULT.OK)
        {
            DisableCountdownBeatSync();
            return false;
        }

        Interlocked.Exchange(ref _pendingCountdownBeatTicks, 0);
        Interlocked.Exchange(ref _countdownBeatSyncActive, 1);
        Interlocked.Exchange(ref _latestCountdownTempoMilliBpm, 0);
        return true;
    }

    public void DisableCountdownBeatSync()
    {
        ResetCountdownBeatSyncState(true);
    }

    public int ConsumeCountdownBeatTicks()
    {
        if (Interlocked.CompareExchange(ref _countdownBeatSyncActive, 0, 0) == 0)
        {
            return 0;
        }

        int consumed = Interlocked.Exchange(ref _pendingCountdownBeatTicks, 0);
        return Mathf.Max(0, consumed);
    }

    public float GetCountdownTempoBpm()
    {
        int milliBpm = Interlocked.CompareExchange(ref _latestCountdownTempoMilliBpm, 0, 0);
        if (milliBpm <= 0)
        {
            return 0f;
        }

        return milliBpm / 1000f;
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
        if (_hasInstance)
        {
            if (stopCurrent)
            {
                StopCurrent(fadeTime <= 0f);
            }
            else
            {
                StopCurrent(true, false);
            }
        }

        _currentBgm = bgm;
        _currentInstance = RuntimeManager.CreateInstance(bgm);
        _hasInstance = true;
        _currentInstance.start();
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

    private IEnumerator FadeOutBgm(float duration, bool stopGameBgmFlow = true)
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

        StopCurrent(true, stopGameBgmFlow);
    }

    private void StopCurrent(bool immediate, bool stopGameBgmFlow = true)
    {
        if (!_hasInstance) {
            ResetCountdownBeatSyncState(false);
            return;
        }

        ResetCountdownBeatSyncState(true);

        if (stopGameBgmFlow)
        {
            StopGameBgmCooldown();
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

    private void StopCurrentInstanceOnly()
    {
        if (!_hasInstance) {
            ResetCountdownBeatSyncState(false);
            return;
        }

        ResetCountdownBeatSyncState(true);

        try {
            _currentInstance.stop(STOP_MODE.IMMEDIATE);
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
        if (_gameBgmEndFadeCoroutine != null)
        {
            StopCoroutine(_gameBgmEndFadeCoroutine);
            _gameBgmEndFadeCoroutine = null;
        }
        _isGameBgmCooldownActive = false;
    }

    private void ResetCountdownBeatSyncState(bool detachFromCurrentInstance)
    {
        if (detachFromCurrentInstance && _hasInstance)
        {
            _currentInstance.setCallback(null);
        }

        Interlocked.Exchange(ref _pendingCountdownBeatTicks, 0);
        Interlocked.Exchange(ref _countdownBeatSyncActive, 0);
        Interlocked.Exchange(ref _latestCountdownTempoMilliBpm, 0);
    }

    [AOT.MonoPInvokeCallback(typeof(EVENT_CALLBACK))]
    private static RESULT OnCountdownBeatCallback(EVENT_CALLBACK_TYPE type, IntPtr eventInstancePtr, IntPtr parameterPtr)
    {
        if (type != EVENT_CALLBACK_TYPE.TIMELINE_BEAT && type != EVENT_CALLBACK_TYPE.STOPPED)
        {
            return RESULT.OK;
        }

        BgmManager manager = Instance;
        if (manager == null)
        {
            return RESULT.OK;
        }

        if (Interlocked.CompareExchange(ref manager._countdownBeatSyncActive, 0, 0) == 0)
        {
            return RESULT.OK;
        }

        if (manager._currentInstance.handle != eventInstancePtr)
        {
            return RESULT.OK;
        }

        if (type == EVENT_CALLBACK_TYPE.TIMELINE_BEAT)
        {
            if (parameterPtr != IntPtr.Zero)
            {
                TIMELINE_BEAT_PROPERTIES beat = Marshal.PtrToStructure<TIMELINE_BEAT_PROPERTIES>(parameterPtr);
                int milliTempo = Mathf.Max(0, Mathf.RoundToInt(beat.tempo * 1000f));
                Interlocked.Exchange(ref manager._latestCountdownTempoMilliBpm, milliTempo);
            }

            Interlocked.Increment(ref manager._pendingCountdownBeatTicks);
        }
        else if (type == EVENT_CALLBACK_TYPE.STOPPED)
        {
            Interlocked.Exchange(ref manager._countdownBeatSyncActive, 0);
        }

        return RESULT.OK;
    }
}
