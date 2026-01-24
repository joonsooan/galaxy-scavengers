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
        if (tutorialPanel != null) {
            tutorialPanel.SetActive(false);
        }

        if (progressBarContainer != null) {
            progressBarContainer.SetActive(false);
        }
    }
}
