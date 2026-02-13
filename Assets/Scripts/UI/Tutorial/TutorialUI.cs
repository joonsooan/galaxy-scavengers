using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TutorialUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject tutorialPanel;
    [SerializeField] private TextMeshProUGUI tutorialText;
    [SerializeField] private Slider progressSlider;
    [SerializeField] private GameObject progressBarContainer;
    [SerializeField] private Graphic flashTarget;
    [SerializeField] private float flashFadeOutTime = 0.5f;

    private Coroutine _flashRoutine;

    private void Awake()
    {
        if (tutorialPanel != null) {
            tutorialPanel.SetActive(false);
        }

        if (progressBarContainer != null) {
            progressBarContainer.SetActive(false);
        }
    }

    public void ShowTutorialStep(TutorialStepData step)
    {
        if (tutorialPanel != null) {
            tutorialPanel.SetActive(true);
        }

        if (flashTarget != null) {
            if (_flashRoutine != null) {
                StopCoroutine(_flashRoutine);
            }
            _flashRoutine = StartCoroutine(FlashRoutine());
        }

        if (tutorialText != null) {
            tutorialText.text = step.text;
            LayoutRebuilder.ForceRebuildLayoutImmediate(tutorialText.rectTransform);
        }

        if (progressBarContainer != null) {
            progressBarContainer.SetActive(step.showProgressBar);
        }

        if (progressSlider != null) {
            progressSlider.gameObject.SetActive(step.showProgressBar);
            if (step.showProgressBar) {
                progressSlider.value = 0f;
            }
        }
    }

    public void UpdateProgress(float progress)
    {
        if (progressSlider != null) {
            progressSlider.value = Mathf.Clamp01(progress);
        }
    }

    public void HideTutorial()
    {
        if (_flashRoutine != null) {
            StopCoroutine(_flashRoutine);
            _flashRoutine = null;
        }

        if (tutorialPanel != null) {
            tutorialPanel.SetActive(false);
        }

        if (progressBarContainer != null) {
            progressBarContainer.SetActive(false);
        }
    }

    private IEnumerator FlashRoutine()
    {
        Material mat = flashTarget.material;
        mat.SetFloat("_FlashIntensity", 1f);
        float currentFlash = 1.0f;

        while (currentFlash > 0) {
            currentFlash -= Time.deltaTime / flashFadeOutTime;
            mat.SetFloat("_FlashIntensity", Mathf.Max(0, currentFlash));
            yield return null;
        }

        mat.SetFloat("_FlashIntensity", 0f);
        _flashRoutine = null;
    }
}
