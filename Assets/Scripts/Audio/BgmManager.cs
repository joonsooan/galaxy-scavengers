using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using STOP_MODE = FMOD.Studio.STOP_MODE;

public class BgmManager : MonoBehaviour
{
    [SerializeField] private EventReference titleBgm;
    [SerializeField] private EventReference baseBgm;
    [SerializeField] private EventReference gameBgm;
    [SerializeField] private float defaultFadeOutTime = 0.5f;

    private EventInstance _currentInstance;
    private bool _hasInstance;
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
        PlayBgm(titleBgm);
    }

    public void PlayBaseBgm()
    {
        PlayBgm(baseBgm);
    }

    public void PlayGameBgm()
    {
        PlayBgm(gameBgm);
    }

    public void PlayBgm(EventReference bgm, float fadeOutTime = -1f)
    {
        if (bgm.IsNull) {
            return;
        }

        float fadeTime = fadeOutTime >= 0f ? fadeOutTime : defaultFadeOutTime;
        StopCurrent(fadeTime <= 0f);

        _currentInstance = RuntimeManager.CreateInstance(bgm);
        _hasInstance = true;
        _currentInstance.start();
    }

    public void StopBgm(bool immediate = false)
    {
        StopCurrent(immediate);
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
        }
    }
}
