using UnityEngine;
using UnityEngine.UI;

public class LoadingScreen : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TMPro.TextMeshProUGUI loadingText;
    [SerializeField] private GameObject loadingIndicator;

    [Header("Settings")]
    [SerializeField] private string loadingTextString = "로딩 중...";
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.9f);

    private void Awake()
    {
        InitializeUI();
    }

    private void InitializeUI()
    {
        if (backgroundImage != null)
        {
            backgroundImage.color = backgroundColor;
        }

        if (loadingText != null)
        {
            loadingText.text = loadingTextString;
        }

        if (loadingIndicator != null)
        {
            loadingIndicator.SetActive(true);
        }
    }

    public void SetLoadingText(string text)
    {
        if (loadingText != null)
        {
            loadingText.text = text;
        }
    }
}
