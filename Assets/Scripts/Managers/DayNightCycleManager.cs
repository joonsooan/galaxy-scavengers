using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[ExecuteAlways]
public class DayNightCycleManager : MonoBehaviour
{
    [Header("Time Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float currentTime = 0.5f;
    [SerializeField] private float timeSpeed = 0.1f;
    [SerializeField] private bool autoAdvanceTime = true;

    [Header("Ambient Light (Global Light 2D)")]
    [SerializeField] private Light2D globalLight2D; 
    
    [Tooltip("하루(0.0~1.0) 동안의 빛 색상 변화")]
    [SerializeField] private Gradient ambientLightGradient = new ();
    [SerializeField] private bool controlRenderSettings = true;

    [Header("Post-Processing Volume Blending")]
    [SerializeField] private Volume volumeA;
    [SerializeField] private Volume volumeB;
    [SerializeField] private List<TimeProfile> timeProfiles = new ();

    [Serializable]
    public class TimeProfile
    {
        [Range(0f, 1f)] public float timeOfDay;
        public VolumeProfile profile;
    }

    private void Update()
    {
        if (autoAdvanceTime && Application.isPlaying)
        {
            currentTime += timeSpeed * Time.deltaTime;
            if (currentTime >= 1f) currentTime -= 1f;
        }
        else
        {
            currentTime = Mathf.Clamp01(currentTime);
        }

        UpdateAmbientLight();
        UpdateVolumeBlending();
    }

    private void UpdateAmbientLight()
    {
        if (ambientLightGradient == null) return;

        Color targetColor = ambientLightGradient.Evaluate(currentTime);
        
        if (globalLight2D != null)
        {
            globalLight2D.color = targetColor;
        }

        if (controlRenderSettings)
        {
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

        if (volumeA.profile == fromProfile)
        {
            if (volumeB.profile != toProfile) volumeB.profile = toProfile;

            volumeA.weight = 1f - smoothFactor;
            volumeB.weight = smoothFactor;
        }
        else if (volumeB.profile == fromProfile)
        {
            if (volumeA.profile != toProfile) volumeA.profile = toProfile;

            volumeB.weight = 1f - smoothFactor;
            volumeA.weight = smoothFactor;
        }
        else
        {
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

        for (int i = 0; i < timeProfiles.Count; i++)
        {
            int next = (i + 1) % timeProfiles.Count;
            float t1 = timeProfiles[i].timeOfDay;
            float t2 = timeProfiles[next].timeOfDay;

            bool isCurrentSection;
            if (t1 <= t2) isCurrentSection = (currentTime >= t1 && currentTime <= t2);
            else isCurrentSection = (currentTime >= t1 || currentTime <= t2);

            if (isCurrentSection)
            {
                currentIdx = i;
                nextIdx = next;
                
                float duration;
                float timePassed;

                if (t1 <= t2)
                {
                    duration = t2 - t1;
                    timePassed = currentTime - t1;
                }
                else
                {
                    duration = (1f - t1) + t2;
                    if (currentTime >= t1) timePassed = currentTime - t1;
                    else timePassed = (1f - t1) + currentTime;
                }

                if (duration > 0) blendFactor = timePassed / duration;
                return;
            }
        }
    }

    public void SetTime(float time)
    {
        currentTime = Mathf.Clamp01(time);
    }

    public float GetTime()
    {
        return currentTime;
    }
}
