using System.Collections;
using FMOD;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using STOP_MODE = FMOD.Studio.STOP_MODE;

public partial class BgmManager : MonoBehaviour
{
    public void PlayLoadingBgm(float fadeInTime = -1f)
    {
        if (loadingBgm.IsNull) {
            return;
        }

        float fadeTime = fadeInTime >= 0f ? fadeInTime : defaultFadeInTime;
        StopLoadingBgmImmediate();

        _loadingInstance = RuntimeManager.CreateInstance(loadingBgm);
        _hasLoadingInstance = true;
        _loadingInstance.setVolume(0f);
        _loadingInstance.start();

        if (_loadingFadeInCoroutine != null)
        {
            StopCoroutine(_loadingFadeInCoroutine);
        }
        _loadingFadeInCoroutine = StartCoroutine(FadeInLoadingBgm(_loadingInstance, fadeTime));
    }

    public void StopLoadingBgm(float fadeOutTime = -1f)
    {
        if (!_hasLoadingInstance) {
            return;
        }

        float fadeTime = fadeOutTime >= 0f ? fadeOutTime : defaultFadeOutTime;
        EventInstance targetInstance = _loadingInstance;
        if (_loadingFadeInCoroutine != null)
        {
            StopCoroutine(_loadingFadeInCoroutine);
            _loadingFadeInCoroutine = null;
        }
        if (_loadingFadeOutCoroutine != null)
        {
            StopCoroutine(_loadingFadeOutCoroutine);
        }
        _loadingFadeOutCoroutine = StartCoroutine(FadeOutLoadingBgm(targetInstance, fadeTime));
    }

    private IEnumerator FadeInLoadingBgm(EventInstance targetInstance, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && _hasLoadingInstance && _loadingInstance.handle == targetInstance.handle)
        {
            elapsed += Time.unscaledDeltaTime;
            float volume = Mathf.Clamp01(elapsed / duration);
            targetInstance.setVolume(volume);
            yield return null;
        }
        
        if (_hasLoadingInstance && _loadingInstance.handle == targetInstance.handle) {
            targetInstance.setVolume(1f);
        }

        if (_loadingFadeInCoroutine != null)
        {
            _loadingFadeInCoroutine = null;
        }
    }

    private IEnumerator FadeOutLoadingBgm(EventInstance targetInstance, float duration)
    {
        if (!_hasLoadingInstance || _loadingInstance.handle != targetInstance.handle) {
            yield break;
        }

        float startVolume = 1f;
        targetInstance.getVolume(out startVolume);

        float elapsed = 0f;
        while (elapsed < duration && _hasLoadingInstance && _loadingInstance.handle == targetInstance.handle)
        {
            elapsed += Time.unscaledDeltaTime;
            float volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
            targetInstance.setVolume(volume);
            yield return null;
        }

        StopLoadingInstance(targetInstance);
        _loadingFadeOutCoroutine = null;
    }

    private void StopLoadingBgmImmediate()
    {
        if (_loadingFadeInCoroutine != null)
        {
            StopCoroutine(_loadingFadeInCoroutine);
            _loadingFadeInCoroutine = null;
        }
        if (_loadingFadeOutCoroutine != null)
        {
            StopCoroutine(_loadingFadeOutCoroutine);
            _loadingFadeOutCoroutine = null;
        }

        if (!_hasLoadingInstance) {
            return;
        }

        StopLoadingInstance(_loadingInstance);
    }

    private void StopLoadingInstance(EventInstance targetInstance)
    {
        try {
            targetInstance.stop(STOP_MODE.IMMEDIATE);
            targetInstance.release();
        }
        finally {
            if (_hasLoadingInstance && _loadingInstance.handle == targetInstance.handle)
            {
                _hasLoadingInstance = false;
                _loadingInstance.clearHandle();
            }
        }
    }

    public void PlaySuccessLoadingBgm(float fadeInTime = -1f)
    {
        if (successLoadingBgm.IsNull) {
            return;
        }

        float fadeTime = fadeInTime >= 0f ? fadeInTime : defaultFadeInTime;
        StopTransitionCoroutines();
        StopSuccessLoadingBgmImmediate();

        if (_hasInstance)
        {
            _playSuccessTransitionCoroutine = StartCoroutine(FadeOutGameBgmAndPlaySuccess(fadeTime));
        }
        else
        {
            _successLoadingInstance = RuntimeManager.CreateInstance(successLoadingBgm);
            _hasSuccessLoadingInstance = true;
            _successLoadingInstance.setVolume(0f);
            _successLoadingInstance.start();

            if (_successFadeInCoroutine != null)
            {
                StopCoroutine(_successFadeInCoroutine);
            }
            _successFadeInCoroutine = StartCoroutine(FadeInSuccessLoadingBgm(_successLoadingInstance, fadeTime));
        }
    }

    private IEnumerator FadeOutGameBgmAndPlaySuccess(float fadeInTime)
    {
        yield return StartCoroutine(FadeOutBgm(gameBgmFadeOutTime));

        _successLoadingInstance = RuntimeManager.CreateInstance(successLoadingBgm);
        _hasSuccessLoadingInstance = true;
        _successLoadingInstance.setVolume(0f);
        _successLoadingInstance.start();

        if (_successFadeInCoroutine != null)
        {
            StopCoroutine(_successFadeInCoroutine);
        }
        _successFadeInCoroutine = StartCoroutine(FadeInSuccessLoadingBgm(_successLoadingInstance, fadeInTime));
        yield return _successFadeInCoroutine;
        _playSuccessTransitionCoroutine = null;
    }

    public void StopSuccessLoadingBgm(float fadeOutTime = -1f)
    {
        if (!_hasSuccessLoadingInstance) {
            return;
        }

        float fadeTime = fadeOutTime >= 0f ? fadeOutTime : defaultFadeOutTime;
        EventInstance targetInstance = _successLoadingInstance;
        if (_successFadeInCoroutine != null)
        {
            StopCoroutine(_successFadeInCoroutine);
            _successFadeInCoroutine = null;
        }
        if (_successFadeOutCoroutine != null)
        {
            StopCoroutine(_successFadeOutCoroutine);
        }
        _successFadeOutCoroutine = StartCoroutine(FadeOutSuccessLoadingBgm(targetInstance, fadeTime));
    }

    private IEnumerator FadeInSuccessLoadingBgm(EventInstance targetInstance, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && _hasSuccessLoadingInstance && _successLoadingInstance.handle == targetInstance.handle)
        {
            elapsed += Time.unscaledDeltaTime;
            float volume = Mathf.Clamp01(elapsed / duration);
            targetInstance.setVolume(volume);
            yield return null;
        }
        
        if (_hasSuccessLoadingInstance && _successLoadingInstance.handle == targetInstance.handle) {
            targetInstance.setVolume(1f);
        }

        if (_successFadeInCoroutine != null)
        {
            _successFadeInCoroutine = null;
        }
    }

    private IEnumerator FadeOutSuccessLoadingBgm(EventInstance targetInstance, float duration)
    {
        if (!_hasSuccessLoadingInstance || _successLoadingInstance.handle != targetInstance.handle) {
            yield break;
        }

        float startVolume = 1f;
        targetInstance.getVolume(out startVolume);

        float elapsed = 0f;
        while (elapsed < duration && _hasSuccessLoadingInstance && _successLoadingInstance.handle == targetInstance.handle)
        {
            elapsed += Time.unscaledDeltaTime;
            float volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
            targetInstance.setVolume(volume);
            yield return null;
        }

        StopSuccessLoadingInstance(targetInstance);
        _successFadeOutCoroutine = null;
    }

    private void StopSuccessLoadingBgmImmediate()
    {
        if (_playSuccessTransitionCoroutine != null)
        {
            StopCoroutine(_playSuccessTransitionCoroutine);
            _playSuccessTransitionCoroutine = null;
        }

        if (_successFadeInCoroutine != null)
        {
            StopCoroutine(_successFadeInCoroutine);
            _successFadeInCoroutine = null;
        }
        if (_successFadeOutCoroutine != null)
        {
            StopCoroutine(_successFadeOutCoroutine);
            _successFadeOutCoroutine = null;
        }

        if (!_hasSuccessLoadingInstance) {
            return;
        }

        StopSuccessLoadingInstance(_successLoadingInstance);
    }

    private void StopSuccessLoadingInstance(EventInstance targetInstance)
    {
        try {
            targetInstance.stop(STOP_MODE.IMMEDIATE);
            targetInstance.release();
        }
        finally {
            if (_hasSuccessLoadingInstance && _successLoadingInstance.handle == targetInstance.handle)
            {
                _hasSuccessLoadingInstance = false;
                _successLoadingInstance.clearHandle();
            }
        }
    }

    public void PlayFailureLoadingBgm(float fadeInTime = -1f)
    {
        if (failureLoadingBgm.IsNull) {
            return;
        }

        float fadeTime = fadeInTime >= 0f ? fadeInTime : defaultFadeInTime;
        StopTransitionCoroutines();
        StopFailureLoadingBgmImmediate();

        if (_hasInstance)
        {
            _playFailureTransitionCoroutine = StartCoroutine(FadeOutGameBgmAndPlayFailure(fadeTime));
        }
        else
        {
            _failureLoadingInstance = RuntimeManager.CreateInstance(failureLoadingBgm);
            _hasFailureLoadingInstance = true;
            _failureLoadingInstance.setVolume(0f);
            _failureLoadingInstance.start();

            if (_failureFadeInCoroutine != null)
            {
                StopCoroutine(_failureFadeInCoroutine);
            }
            _failureFadeInCoroutine = StartCoroutine(FadeInFailureLoadingBgm(_failureLoadingInstance, fadeTime));
        }
    }

    private IEnumerator FadeOutGameBgmAndPlayFailure(float fadeInTime)
    {
        yield return StartCoroutine(FadeOutBgm(gameBgmFadeOutTime));

        _failureLoadingInstance = RuntimeManager.CreateInstance(failureLoadingBgm);
        _hasFailureLoadingInstance = true;
        _failureLoadingInstance.setVolume(0f);
        _failureLoadingInstance.start();

        if (_failureFadeInCoroutine != null)
        {
            StopCoroutine(_failureFadeInCoroutine);
        }
        _failureFadeInCoroutine = StartCoroutine(FadeInFailureLoadingBgm(_failureLoadingInstance, fadeInTime));
        yield return _failureFadeInCoroutine;
        _playFailureTransitionCoroutine = null;
    }

    public void StopFailureLoadingBgm(float fadeOutTime = -1f)
    {
        if (!_hasFailureLoadingInstance) {
            return;
        }

        float fadeTime = fadeOutTime >= 0f ? fadeOutTime : defaultFadeOutTime;
        EventInstance targetInstance = _failureLoadingInstance;
        if (_failureFadeInCoroutine != null)
        {
            StopCoroutine(_failureFadeInCoroutine);
            _failureFadeInCoroutine = null;
        }
        if (_failureFadeOutCoroutine != null)
        {
            StopCoroutine(_failureFadeOutCoroutine);
        }
        _failureFadeOutCoroutine = StartCoroutine(FadeOutFailureLoadingBgm(targetInstance, fadeTime));
    }

    private IEnumerator FadeInFailureLoadingBgm(EventInstance targetInstance, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && _hasFailureLoadingInstance && _failureLoadingInstance.handle == targetInstance.handle)
        {
            elapsed += Time.unscaledDeltaTime;
            float volume = Mathf.Clamp01(elapsed / duration);
            targetInstance.setVolume(volume);
            yield return null;
        }
        
        if (_hasFailureLoadingInstance && _failureLoadingInstance.handle == targetInstance.handle) {
            targetInstance.setVolume(1f);
        }

        if (_failureFadeInCoroutine != null)
        {
            _failureFadeInCoroutine = null;
        }
    }

    private IEnumerator FadeOutFailureLoadingBgm(EventInstance targetInstance, float duration)
    {
        if (!_hasFailureLoadingInstance || _failureLoadingInstance.handle != targetInstance.handle) {
            yield break;
        }

        float startVolume = 1f;
        targetInstance.getVolume(out startVolume);

        float elapsed = 0f;
        while (elapsed < duration && _hasFailureLoadingInstance && _failureLoadingInstance.handle == targetInstance.handle)
        {
            elapsed += Time.unscaledDeltaTime;
            float volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
            targetInstance.setVolume(volume);
            yield return null;
        }

        StopFailureLoadingInstance(targetInstance);
        _failureFadeOutCoroutine = null;
    }

    private void StopFailureLoadingBgmImmediate()
    {
        if (_playFailureTransitionCoroutine != null)
        {
            StopCoroutine(_playFailureTransitionCoroutine);
            _playFailureTransitionCoroutine = null;
        }

        if (_failureFadeInCoroutine != null)
        {
            StopCoroutine(_failureFadeInCoroutine);
            _failureFadeInCoroutine = null;
        }
        if (_failureFadeOutCoroutine != null)
        {
            StopCoroutine(_failureFadeOutCoroutine);
            _failureFadeOutCoroutine = null;
        }

        if (!_hasFailureLoadingInstance) {
            return;
        }

        StopFailureLoadingInstance(_failureLoadingInstance);
    }

    private void StopFailureLoadingInstance(EventInstance targetInstance)
    {
        try {
            targetInstance.stop(STOP_MODE.IMMEDIATE);
            targetInstance.release();
        }
        finally {
            if (_hasFailureLoadingInstance && _failureLoadingInstance.handle == targetInstance.handle)
            {
                _hasFailureLoadingInstance = false;
                _failureLoadingInstance.clearHandle();
            }
        }
    }

    private void StopTransitionCoroutines()
    {
        if (_playSuccessTransitionCoroutine != null)
        {
            StopCoroutine(_playSuccessTransitionCoroutine);
            _playSuccessTransitionCoroutine = null;
        }

        if (_playFailureTransitionCoroutine != null)
        {
            StopCoroutine(_playFailureTransitionCoroutine);
            _playFailureTransitionCoroutine = null;
        }
    }
}
