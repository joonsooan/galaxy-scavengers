using System.Collections;
using FMODUnity;
using UnityEngine;
using UnityEngine.UI;

public class BtnManager_Base : MonoBehaviour
{
    [SerializeField] private Button coreLaunchButton;
    [SerializeField] private Button titleButton;
    [SerializeField] private EventReference coreLaunchSound;
    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField] private float fadeDuration = 1f;

    private bool _isLaunching;

    private void Awake()
    {
        if (coreLaunchButton != null) {
            coreLaunchButton.onClick.AddListener(OnCoreLaunchClicked);
        }

        if (titleButton != null) {
            titleButton.onClick.AddListener(BackToTitle);
        }

        if (fadeCanvasGroup != null) {
            fadeCanvasGroup.alpha = 0f;
        }
    }

    private void OnDestroy()
    {
        if (coreLaunchButton != null) {
            coreLaunchButton.onClick.RemoveListener(OnCoreLaunchClicked);
        }
        if (titleButton != null) {
            titleButton.onClick.RemoveListener(BackToTitle);
        }
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
        if (fadeCanvasGroup != null && fadeDuration > 0f) {
            float elapsed = 0f;
            while (elapsed < fadeDuration) {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                fadeCanvasGroup.alpha = t;
                yield return null;
            }
            fadeCanvasGroup.alpha = 1f;
        }
        else if (fadeCanvasGroup != null) {
            fadeCanvasGroup.alpha = 1f;
        }

        SceneLoader.Instance.LoadGameScene();
    }

    private void BackToTitle()
    {
        SceneLoader.Instance.LoadTitleScene();
    }
}
