using System.Collections;
using FMODUnity;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

public class BtnManager_Base : MonoBehaviour
{
    [SerializeField] private Button coreLaunchButton;
    [SerializeField] private Button tutorialLaunchButton;
    [SerializeField] private Button titleButton;
    [SerializeField] private EventReference coreLaunchSound;
    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField] private float fadeDuration = 1f;

    private bool _isLaunching;

    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
    }

    private void OnSelectedLocaleChanged(Locale _)
    {
        ApplyLocalizedLaunchBarLabels();
        ApplyCoreLaunchHangarTitle();
    }

    private void Awake()
    {
        if (tutorialLaunchButton == null) {
            GameObject tutorialButtonObject = GameObject.Find("Tutorial Btn");
            if (tutorialButtonObject != null) {
                tutorialLaunchButton = tutorialButtonObject.GetComponent<Button>();
            }
        }

        if (coreLaunchButton != null) {
            coreLaunchButton.onClick.AddListener(OnCoreLaunchClicked);
        }
        if (tutorialLaunchButton != null) {
            tutorialLaunchButton.onClick.AddListener(OnTutorialLaunchClicked);
        }

        if (titleButton != null) {
            titleButton.onClick.AddListener(BackToTitle);
        }

        if (fadeCanvasGroup != null) {
            fadeCanvasGroup.alpha = 0f;
        }

        ApplyCoreLaunchHangarTitle();
    }

    private void Start()
    {
        if (fadeCanvasGroup != null) {
            if (fadeCanvasGroup.gameObject != null) {
                fadeCanvasGroup.gameObject.SetActive(true);
            }
            fadeCanvasGroup.alpha = 0f;
        }
        _isLaunching = false;
        ApplyLocalizedLaunchBarLabels();
        ApplyCoreLaunchHangarTitle();
    }

    private void ApplyLocalizedLaunchBarLabels()
    {
        SetButtonLabelText(titleButton, "base.titleScreen", "\uD0C0\uC774\uD2C0 \uD654\uBA74");
        SetButtonLabelText(coreLaunchButton, "base.coreLaunch", "\uCF54\uC5B4 \uBC1C\uC0AC");
        SetButtonLabelText(tutorialLaunchButton, "base.tutorial", "\uD29C\uD1A0\uB9AC\uC5BC");
    }

    private void ApplyCoreLaunchHangarTitle()
    {
        Transform panel = FindChildRecursive(transform, "Core Launch Panel");
        if (panel == null)
        {
            return;
        }

        for (int i = 0; i < panel.childCount; i++)
        {
            TMP_Text label = panel.GetChild(i).GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = GameLocalization.GetOrDefault("UI_Common", "base.coreHangar",
                    "\uCF54\uC5B4 \uACA9\uB0A9\uACE0");
                return;
            }
        }
    }

    private static Transform FindChildRecursive(Transform root, string objectName)
    {
        if (root.name == objectName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static void SetButtonLabelText(Button button, string key, string fallback)
    {
        if (button == null)
        {
            return;
        }

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = GameLocalization.GetOrDefault("UI_Common", key, fallback);
        }
    }

    private void OnDestroy()
    {
        if (coreLaunchButton != null) {
            coreLaunchButton.onClick.RemoveListener(OnCoreLaunchClicked);
        }
        if (tutorialLaunchButton != null) {
            tutorialLaunchButton.onClick.RemoveListener(OnTutorialLaunchClicked);
        }
        if (titleButton != null) {
            titleButton.onClick.RemoveListener(BackToTitle);
        }
    }

    public void ResetFadeCanvasGroup()
    {
        if (fadeCanvasGroup != null) {
            if (fadeCanvasGroup.gameObject != null) {
                fadeCanvasGroup.gameObject.SetActive(true);
            }
            fadeCanvasGroup.alpha = 0f;
        }
        _isLaunching = false;
    }

    private void OnCoreLaunchClicked()
    {
        if (_isLaunching) {
            return;
        }

        _isLaunching = true;

        if (!coreLaunchSound.IsNull) {
            RuntimeManager.PlayOneShot(coreLaunchSound);
        }

        if (BgmManager.Instance != null) {
            BgmManager.Instance.StopBgm(fadeDuration);
        }

        StartCoroutine(CoreLaunchSequence());
    }

    private IEnumerator CoreLaunchSequence()
    {
        if (fadeCanvasGroup != null) {
            if (fadeCanvasGroup.gameObject != null) {
                fadeCanvasGroup.gameObject.SetActive(true);
            }
            
            if (fadeDuration > 0f) {
                float elapsed = 0f;
                while (elapsed < fadeDuration) {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / fadeDuration);
                    fadeCanvasGroup.alpha = t;
                    yield return null;
                }
                fadeCanvasGroup.alpha = 1f;
            }
            else {
                fadeCanvasGroup.alpha = 1f;
            }
        }

        SceneLoader.Instance.LoadGameScene(PlanetSelectionState.GetSelectedSceneName());
    }

    private void OnTutorialLaunchClicked()
    {
        if (_isLaunching) {
            return;
        }

        _isLaunching = true;

        if (!coreLaunchSound.IsNull) {
            RuntimeManager.PlayOneShot(coreLaunchSound);
        }

        if (BgmManager.Instance != null) {
            BgmManager.Instance.StopBgm(fadeDuration);
        }

        StartCoroutine(TutorialLaunchSequence());
    }

    private IEnumerator TutorialLaunchSequence()
    {
        if (fadeCanvasGroup != null) {
            if (fadeCanvasGroup.gameObject != null) {
                fadeCanvasGroup.gameObject.SetActive(true);
            }

            if (fadeDuration > 0f) {
                float elapsed = 0f;
                while (elapsed < fadeDuration) {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / fadeDuration);
                    fadeCanvasGroup.alpha = t;
                    yield return null;
                }
                fadeCanvasGroup.alpha = 1f;
            }
            else {
                fadeCanvasGroup.alpha = 1f;
            }
        }

        SceneLoader.Instance.LoadTutorialScene();
    }

    private void BackToTitle()
    {
        SceneLoader.Instance.LoadTitleScene();
    }
}
