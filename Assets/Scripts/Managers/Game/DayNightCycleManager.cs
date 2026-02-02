using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[ExecuteAlways]
public class DayNightCycleManager : MonoBehaviour
{
    [Header("Time Settings")]
    [SerializeField] private float RealSecondsPerInGameHour = 2;
    [Range(0f, 24f)]
    [SerializeField] private float currentTime = 12f;
    [Range(0f, 24f)]
    [SerializeField] private float gameStartTime = 12f;
    [SerializeField] private bool autoAdvanceTime = true;

    [Header("Day/Night Settings")]
    [Range(0f, 100f)]
    [SerializeField] private float dayStartPercent = 15f; // 낮 시작 퍼센트
    [Range(0f, 100f)]
    [SerializeField] private float nightStartPercent = 85f; // 밤 시작 퍼센트

    [Header("Ambient Light (Global Light 2D)")]
    [SerializeField] private Light2D globalLight2D;

    [Tooltip("하루(0.0~1.0) 동안의 빛 색상 변화")]
    [SerializeField] private Gradient ambientLightGradient = new Gradient();
    [SerializeField] private bool controlRenderSettings = true;

    [Header("Post-Processing Volume Blending")]
    [SerializeField] private Volume volumeA;
    [SerializeField] private Volume volumeB;
    [SerializeField] private List<TimeProfile> timeProfiles = new List<TimeProfile>();

    private bool _wasDay = true;

    public static DayNightCycleManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) {
            Instance = this;
        }
        else if (Instance != this) {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        autoAdvanceTime = false;
        currentTime = gameStartTime;

        StartCoroutine(WaitForLoadingComplete());
    }

    private void Update()
    {
        if (autoAdvanceTime && Application.isPlaying) {
            float hoursPerSecond = 1f / RealSecondsPerInGameHour;
            currentTime += hoursPerSecond * Time.deltaTime;
            if (currentTime >= 24f) currentTime -= 24f;
        }
        else {
            currentTime = Mathf.Clamp(currentTime, 0f, 24f);
        }

        UpdateAmbientLight();
        UpdateVolumeBlending();
        CheckDayNightTransition();
    }

    public static event Action OnNightStarted;
    public static event Action OnDayStarted;

    private IEnumerator WaitForLoadingComplete()
    {
        while (IsLoadingScreenActive()) {
            yield return null;
        }

        autoAdvanceTime = true;
        currentTime = gameStartTime;
    }

    private bool IsLoadingScreenActive()
    {
        if (LoadingUIManager.Instance == null) {
            return false;
        }

        LoadingScreen loadingScreen = LoadingUIManager.Instance.GetLoadingScreenComponent();
        if (loadingScreen == null) {
            return false;
        }

        return loadingScreen.gameObject.activeSelf;
    }

    private void CheckDayNightTransition()
    {
        bool isCurrentlyDay = IsDay();

        if (_wasDay && !isCurrentlyDay) {
            OnNightStarted?.Invoke();
        }
        else if (!_wasDay && isCurrentlyDay) {
            OnDayStarted?.Invoke();
        }

        _wasDay = isCurrentlyDay;
    }

    private void UpdateAmbientLight()
    {
        if (ambientLightGradient == null) return;

        float normalizedTime = currentTime / 24f;
        Color targetColor = ambientLightGradient.Evaluate(normalizedTime);

        if (globalLight2D != null) {
            globalLight2D.color = targetColor;
        }

        if (controlRenderSettings) {
            RenderSettings.ambientLight = targetColor;
        }
    }

    private void UpdateVolumeBlending()
    {
        if (timeProfiles == null || timeProfiles.Count == 0) return;
        if (volumeA == null || volumeB == null) return;

        FindCurrentAndNextProfiles(out int currentIdx, out int nextIdx, out float blendFactor);

        if (currentIdx == -1 || nextIdx == -1) return;

        VolumeProfile fromProfile = timeProfiles[currentIdx].profile;
        VolumeProfile toProfile = timeProfiles[nextIdx].profile;

        float smoothFactor = Mathf.SmoothStep(0f, 1f, blendFactor);

        if (volumeA.profile == fromProfile) {
            if (volumeB.profile != toProfile) volumeB.profile = toProfile;

            volumeA.weight = 1f - smoothFactor;
            volumeB.weight = smoothFactor;
        }
        else if (volumeB.profile == fromProfile) {
            if (volumeA.profile != toProfile) volumeA.profile = toProfile;

            volumeB.weight = 1f - smoothFactor;
            volumeA.weight = smoothFactor;
        }
        else {
            volumeA.profile = fromProfile;
            volumeB.profile = toProfile;
            volumeA.weight = 1f - smoothFactor;
            volumeB.weight = smoothFactor;
        }
    }

    private void FindCurrentAndNextProfiles(out int currentIdx, out int nextIdx, out float blendFactor)
    {
        currentIdx = -1;
        nextIdx = -1;
        blendFactor = 0f;

        if (timeProfiles.Count < 2) return;

        float normalizedTime = currentTime / 24f;

        for (int i = 0; i < timeProfiles.Count; i++) {
            int next = (i + 1) % timeProfiles.Count;
            float t1 = timeProfiles[i].timeOfDay;
            float t2 = timeProfiles[next].timeOfDay;

            bool isCurrentSection;
            if (t1 <= t2) isCurrentSection = normalizedTime >= t1 && normalizedTime <= t2;
            else isCurrentSection = normalizedTime >= t1 || normalizedTime <= t2;

            if (isCurrentSection) {
                currentIdx = i;
                nextIdx = next;

                float duration;
                float timePassed;

                if (t1 <= t2) {
                    duration = t2 - t1;
                    timePassed = normalizedTime - t1;
                }
                else {
                    duration = 1f - t1 + t2;
                    if (normalizedTime >= t1) timePassed = normalizedTime - t1;
                    else timePassed = 1f - t1 + normalizedTime;
                }

                if (duration > 0) blendFactor = timePassed / duration;
                return;
            }
        }
    }

    public void SetTime(float time)
    {
        currentTime = Mathf.Clamp(time, 0f, 24f);
    }

    public float GetTime()
    {
        return currentTime;
    }

    public bool IsDay()
    {
        float timePercent = currentTime / 24f * 100f;

        if (dayStartPercent < nightStartPercent) {
            return timePercent >= dayStartPercent && timePercent < nightStartPercent;
        }

        return timePercent >= dayStartPercent || timePercent < nightStartPercent;
    }

    public float GetTimePercent()
    {
        return currentTime / 24f * 100f;
    }

    public float GetDayMaxTime()
    {
        float dayDurationPercent;
        if (dayStartPercent < nightStartPercent) {
            dayDurationPercent = nightStartPercent - dayStartPercent;
        }
        else {
            dayDurationPercent = 100f - dayStartPercent + nightStartPercent;
        }
        return dayDurationPercent / 100f * 24f;
    }

    public float GetNightMaxTime()
    {
        float nightDurationPercent;
        if (dayStartPercent < nightStartPercent) {
            nightDurationPercent = dayStartPercent + (100f - nightStartPercent);
        }
        else {
            nightDurationPercent = dayStartPercent - nightStartPercent;
        }
        return nightDurationPercent / 100f * 24f;
    }

    private float GetCurrentPeriodProgress()
    {
        bool isDay = IsDay();
        float dayStartTime = dayStartPercent / 100f * 24f;
        float nightStartTime = nightStartPercent / 100f * 24f;

        if (isDay) {
            float currentTimeInDay = currentTime - dayStartTime;
            if (currentTimeInDay < 0) currentTimeInDay += 24f;
            float dayMax = GetDayMaxTime();
            return Mathf.Clamp01(currentTimeInDay / dayMax);
        }

        float currentTimeInNight;
        if (currentTime >= nightStartTime) {
            currentTimeInNight = currentTime - nightStartTime;
        }
        else {
            currentTimeInNight = 24f - nightStartTime + currentTime;
        }

        float nightMax = GetNightMaxTime();
        return Mathf.Clamp01(currentTimeInNight / nightMax);
    }

    public float GetCurrentPeriodTime()
    {
        bool isDay = IsDay();
        if (isDay) {
            return GetCurrentPeriodProgress() * GetDayMaxTime();
        }

        return GetCurrentPeriodProgress() * GetNightMaxTime();
    }

    public void SetAutoAdvanceTime(bool enable)
    {
        autoAdvanceTime = enable;
    }

    [Serializable]
    public class TimeProfile
    {
        [Range(0f, 1f)] public float timeOfDay;
        public VolumeProfile profile;
    }
}
