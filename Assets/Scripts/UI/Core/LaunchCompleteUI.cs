using UnityEngine;
using UnityEngine.UI;
using FMODUnity;

public class LaunchCompleteUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TMPro.TextMeshProUGUI titleText;
    [SerializeField] private TMPro.TextMeshProUGUI messageText;
    [SerializeField] private Button continueButton;

    [Header("Settings")]
    [SerializeField] private string titleString = "발사 완료!";
    [SerializeField] private string messageString = "발사가 성공적으로 완료되었습니다.";
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.95f);
    [SerializeField] private float autoHideDelay = 3f;

    [Header("Audio")]
    [SerializeField] private EventReference buttonClickSound;

    private void Awake()
    {
        LoadStringsFromLocalization();
        if (backgroundImage != null)
        {
            backgroundImage.color = backgroundColor;
        }

        ApplyTextsToTmp();

        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OnContinueClicked);
        }

        if (autoHideDelay > 0f)
        {
            Invoke(nameof(OnContinueClicked), autoHideDelay);
        }
    }

    public void ApplyPassiveLocaleRefresh()
    {
        LoadStringsFromLocalization();
        ApplyTextsToTmp();
    }

    private void LoadStringsFromLocalization()
    {
        titleString = GameLocalization.GetOrDefault("UI_Common", "launchComplete.title", titleString);
        messageString = GameLocalization.GetOrDefault("UI_Common", "launchComplete.message", messageString);
    }

    private void ApplyTextsToTmp()
    {
        if (titleText != null)
        {
            titleText.text = titleString;
        }

        if (messageText != null)
        {
            messageText.text = messageString;
        }
    }

    private void Start()
    {
        RectTransform rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;
        }
    }

    private void OnContinueClicked()
    {
        if (!FMODUIButton.HasPlayedClickSoundThisFrame && !buttonClickSound.IsNull)
        {
            RuntimeManager.PlayOneShot(buttonClickSound);
        }

        Hide();
    }

    public void Show()
    {
        gameObject.SetActive(true);

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvas.sortingOrder = 10000;
        }
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void SetTitle(string title)
    {
        if (titleText != null)
        {
            titleText.text = title;
        }
    }

    public void SetMessage(string message)
    {
        if (messageText != null)
        {
            messageText.text = message;
        }
    }

    private void OnDestroy()
    {
        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(OnContinueClicked);
        }
    }
}
