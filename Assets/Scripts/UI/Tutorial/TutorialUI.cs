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
    [SerializeField] private Image panelBackgroundImage;

    [Header("Flash Settings")]
    [SerializeField] private float flashDuration = 0.3f;
    [SerializeField] private float fadeBackDuration = 0.5f;

    private Color _originalColor;
    private Coroutine _flashCoroutine;

    private void Awake()
    {
        if (tutorialPanel != null) {
            tutorialPanel.SetActive(false);
        }

        if (progressBarContainer != null) {
            progressBarContainer.SetActive(false);
        }

        if (panelBackgroundImage == null && tutorialPanel != null) {
            panelBackgroundImage = tutorialPanel.GetComponent<Image>();
        }

        if (panelBackgroundImage != null) {
            _originalColor = panelBackgroundImage.color;
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

        if (panelBackgroundImage != null) {
            if (_flashCoroutine != null) {
                StopCoroutine(_flashCoroutine);
            }
            _flashCoroutine = StartCoroutine(FlashPanelCoroutine());
        }
    }

    private IEnumerator FlashPanelCoroutine()
    {
        if (panelBackgroundImage == null) {
            yield break;
        }

        panelBackgroundImage.color = Color.white;

        yield return new WaitForSeconds(flashDuration);

        float elapsedTime = 0f;
        while (elapsedTime < fadeBackDuration) {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / fadeBackDuration;
            panelBackgroundImage.color = Color.Lerp(Color.white, _originalColor, t);
            yield return null;
        }

        panelBackgroundImage.color = _originalColor;
        _flashCoroutine = null;
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
