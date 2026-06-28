using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResearchHUDWidget : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image techIconImage;
    [SerializeField] private Slider progressSlider;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private Button sliderOverlayBtn;
    [SerializeField] private TMP_Text defaultText;
    [SerializeField] private MainControlPanel mainControlPanel;

    private Vector2 _iconContainerSize;

    private void Awake()
    {
        if (techIconImage != null)
            _iconContainerSize = techIconImage.rectTransform.sizeDelta;
        if (sliderOverlayBtn != null)
            sliderOverlayBtn.onClick.AddListener(OnWidgetClicked);
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
        if (sliderOverlayBtn != null)
            sliderOverlayBtn.onClick.RemoveListener(OnWidgetClicked);
        TechResearchManager.OnResearchStarted -= OnResearchStarted;
        TechResearchManager.OnResearchProgressChanged -= OnResearchProgressChanged;
        TechResearchManager.OnResearchCompleted -= OnResearchCompleted;
        TechResearchManager.OnResearchStateChanged -= OnResearchStateChanged;
    }

    public void OnWidgetClicked()
    {
        if (mainControlPanel != null)
            mainControlPanel.ToggleResearchPanel();
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
        ShowIdleState();
    }

    private void OnResearchStateChanged()
    {
        if (TechResearchManager.Instance == null || !TechResearchManager.Instance.IsResearchInProgress())
            ShowIdleState();
    }

    private void RefreshFromCurrentState()
    {
        if (TechResearchManager.Instance == null || !TechResearchManager.Instance.IsResearchInProgress())
        {
            ShowIdleState();
            return;
        }

        TechData current = TechResearchManager.Instance.GetCurrentResearch();
        if (current == null)
        {
            ShowIdleState();
            return;
        }

        ShowResearchState(current);
    }

    private void ShowResearchState(TechData current)
    {
        if (techIconImage != null)
        {
            techIconImage.gameObject.SetActive(true);
            techIconImage.sprite = current.GetTechIcon();
            if (techIconImage.sprite != null)
                ApplyFitSize(techIconImage.rectTransform, techIconImage.sprite);
        }

        if (progressSlider != null)
            progressSlider.gameObject.SetActive(true);

        if (progressText != null)
            progressText.gameObject.SetActive(true);

        if (defaultText != null)
            defaultText.gameObject.SetActive(false);

        UpdateProgress();
    }

    private void ShowIdleState()
    {
        if (techIconImage != null)
            techIconImage.gameObject.SetActive(false);

        if (progressSlider != null)
            progressSlider.gameObject.SetActive(false);

        if (progressText != null)
            progressText.gameObject.SetActive(false);

        if (defaultText != null)
            defaultText.gameObject.SetActive(true);
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
