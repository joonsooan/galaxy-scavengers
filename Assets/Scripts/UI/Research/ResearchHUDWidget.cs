using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResearchHUDWidget : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image techIconImage;
    [SerializeField] private Slider progressSlider;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private MainControlPanel mainControlPanel;

    private Vector2 _iconContainerSize;

    private void Awake()
    {
        if (techIconImage != null)
            _iconContainerSize = techIconImage.rectTransform.sizeDelta;
        TechResearchManager.OnResearchStarted += OnResearchStarted;
        TechResearchManager.OnResearchProgressChanged += OnResearchProgressChanged;
        TechResearchManager.OnResearchCompleted += OnResearchCompleted;
        TechResearchManager.OnResearchStateChanged += OnResearchStateChanged;
    }

    private void Start()
    {
        RefreshFromCurrentState();
    }

    private void OnDestroy()
    {
        TechResearchManager.OnResearchStarted -= OnResearchStarted;
        TechResearchManager.OnResearchProgressChanged -= OnResearchProgressChanged;
        TechResearchManager.OnResearchCompleted -= OnResearchCompleted;
        TechResearchManager.OnResearchStateChanged -= OnResearchStateChanged;
    }

    public void OnWidgetClicked()
    {
        if (mainControlPanel != null)
            mainControlPanel.OpenResearchPanel();
    }

    private void OnResearchStarted(int techIndex)
    {
        RefreshFromCurrentState();
    }

    private void OnResearchProgressChanged()
    {
        UpdateProgress();
    }

    private void OnResearchCompleted(int techIndex)
    {
        gameObject.SetActive(false);
    }

    private void OnResearchStateChanged()
    {
        if (TechResearchManager.Instance == null || !TechResearchManager.Instance.IsResearchInProgress())
            gameObject.SetActive(false);
    }

    private void RefreshFromCurrentState()
    {
        if (TechResearchManager.Instance == null || !TechResearchManager.Instance.IsResearchInProgress())
        {
            gameObject.SetActive(false);
            return;
        }

        TechData current = TechResearchManager.Instance.GetCurrentResearch();
        if (current == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        if (techIconImage != null)
        {
            techIconImage.sprite = current.GetTechIcon();
            if (techIconImage.sprite != null)
                ApplyFitSize(techIconImage.rectTransform, techIconImage.sprite);
        }

        UpdateProgress();
    }

    private void ApplyFitSize(RectTransform iconRt, Sprite sprite)
    {
        float w = sprite.rect.width;
        float h = sprite.rect.height;
        float containerW = _iconContainerSize.x > 0 ? _iconContainerSize.x : w;
        float containerH = _iconContainerSize.y > 0 ? _iconContainerSize.y : h;
        float scale = Mathf.Min(1f, Mathf.Min(containerW / w, containerH / h));
        iconRt.sizeDelta = new Vector2(w * scale, h * scale);
    }

    private void UpdateProgress()
    {
        if (TechResearchManager.Instance == null) return;

        int progress = TechResearchManager.Instance.GetCurrentProgress();
        int max = TechResearchManager.Instance.GetMaxProgress();

        if (progressSlider != null)
        {
            progressSlider.minValue = 0;
            progressSlider.maxValue = Mathf.Max(1, max);
            progressSlider.value = progress;
        }

        if (progressText != null)
            progressText.text = $"{progress}/{max}";
    }
}
