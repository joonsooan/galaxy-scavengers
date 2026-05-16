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
    private TutorialStepData _currentTutorialStepForText;

    private void Awake()
    {
        if (tutorialPanel != null) {
            tutorialPanel.SetActive(false);
        }

        if (progressBarContainer != null) {
            progressBarContainer.SetActive(false);
        }
    }

    public void ApplyPassiveLocaleRefresh()
    {
        if (_currentTutorialStepForText == null || tutorialText == null) {
            return;
        }

        ApplyTutorialStepBody(_currentTutorialStepForText);
    }

    public void ShowTutorialStep(TutorialStepData step)
    {
        if (step == null) {
            return;
        }

        _currentTutorialStepForText = step;

        if (!gameObject.activeSelf) {
            gameObject.SetActive(true);
        }

        if (tutorialPanel != null) {
            tutorialPanel.SetActive(true);
        }

        if (flashTarget != null) {
            if (_flashRoutine != null) {
                StopCoroutine(_flashRoutine);
            }
            if (isActiveAndEnabled) {
                _flashRoutine = StartCoroutine(FlashRoutine());
            }
            else {
                Material mat = flashTarget.material;
                mat.SetFloat("_FlashIntensity", 0f);
                _flashRoutine = null;
            }
        }

        if (tutorialText != null) {
            ApplyTutorialStepBody(step);
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

    private void ApplyTutorialStepBody(TutorialStepData step)
    {
        if (tutorialText == null || step == null) {
            return;
        }

        tutorialText.text = step.GetResolvedTutorialBody();
        LayoutRebuilder.ForceRebuildLayoutImmediate(tutorialText.rectTransform);
    }

    public void UpdateProgress(float progress)
    {
        if (progressSlider != null) {
            progressSlider.value = Mathf.Clamp01(progress);
        }
    }

    public void HideTutorial()
    {
        _currentTutorialStepForText = null;

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
            currentFlash -= Time.unscaledDeltaTime / flashFadeOutTime;
            mat.SetFloat("_FlashIntensity", Mathf.Max(0, currentFlash));
            yield return null;
        }

        mat.SetFloat("_FlashIntensity", 0f);
        _flashRoutine = null;
    }
}
