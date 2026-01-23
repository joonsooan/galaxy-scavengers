using System;
using UnityEngine;
using DG.Tweening;

public class LoadingStagePanelAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform panelPrefab;

    [Header("Animation")]
    [SerializeField] private float entryDuration = 0.5f;
    [SerializeField] private float startYOffset = -200f;
    [SerializeField] private Ease entryEase = Ease.OutCubic;

    private RectTransform _selfRect;
    private RectTransform _panelParent;

    private void Awake()
    {
        _selfRect = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        OnLoadingStageChanged += HandleLoadingStageChanged;
    }

    private void OnDisable()
    {
        OnLoadingStageChanged -= HandleLoadingStageChanged;
    }

    private void HandleLoadingStageChanged(string stage)
    {
        if (!string.IsNullOrEmpty(stage))
        {
            PlayNextStagePanel();
        }
    }

    public void SetPanelParent(RectTransform parent)
    {
        _panelParent = parent;
    }

    private void PlayNextStagePanel()
    {
        if (panelPrefab == null)
        {
            return;
        }

        RectTransform parent = _panelParent != null
            ? _panelParent
            : (_selfRect != null ? _selfRect : transform as RectTransform);
        if (parent == null)
        {
            return;
        }

        RectTransform newPanel = Instantiate(panelPrefab, parent);
        newPanel.gameObject.SetActive(true);

        CanvasGroup canvasGroup = newPanel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = newPanel.gameObject.AddComponent<CanvasGroup>();
        }
        canvasGroup.alpha = 1f;

        Vector2 endPos = new Vector2(0f, 0f);
        Vector2 startPos = endPos + new Vector2(0f, startYOffset);

        newPanel.anchoredPosition = startPos;
        newPanel.DOAnchorPos(endPos, entryDuration)
            .SetEase(entryEase)
            .SetUpdate(true);
    }

    public static event Action<string> OnLoadingStageChanged;

    public static void InvokeLoadingStageChanged(string stage)
    {
        OnLoadingStageChanged?.Invoke(stage);
    }
}

