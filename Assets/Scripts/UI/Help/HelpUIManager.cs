using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HelpUIManager : MonoBehaviour
{
    [Header("Panel References")]
    [SerializeField] private GameObject helpPanel;
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;

    [Header("Cell List")]
    [SerializeField] private GameObject cellPrefab;
    [SerializeField] private Transform cellContainer;

    [Header("Detail Panel")]
    [SerializeField] private RectTransform detailPanelRect;
    [SerializeField] private TMP_Text detailTitle;
    [SerializeField] private Image detailImage;
    [SerializeField] private TMP_Text detailDescription;

    private readonly List<HelpCell> _cells = new List<HelpCell>();
    private HelpData[] _helpEntries;
    private bool _isOpen;

    public static HelpUIManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (openButton != null)
        {
            openButton.onClick.AddListener(OpenHelp);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseHelp);
        }

        LoadHelpData();
        CreateCells();

        if (helpPanel != null)
        {
            helpPanel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (openButton != null)
        {
            openButton.onClick.RemoveAllListeners();
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
        }
    }

    private void LoadHelpData()
    {
        _helpEntries = Resources.LoadAll<HelpData>("Help Data");

        if (_helpEntries != null && _helpEntries.Length > 0)
        {
            _helpEntries = _helpEntries.OrderBy(h => h.order).ToArray();
        }
    }

    private void CreateCells()
    {
        if (_helpEntries == null || cellPrefab == null || cellContainer == null) return;

        foreach (HelpData entry in _helpEntries)
        {
            GameObject cellObj = Instantiate(cellPrefab, cellContainer);
            HelpCell cell = cellObj.GetComponent<HelpCell>();
            if (cell != null)
            {
                cell.Initialize(entry, this);
                _cells.Add(cell);
            }
        }
    }

    public void OpenHelp()
    {
        if (_isOpen) return;
        if (helpPanel == null) return;

        _isOpen = true;
        helpPanel.SetActive(true);

        if (GameManager.Instance != null && !GameManager.Instance.IsPaused)
        {
            GameManager.Instance.TogglePause();
        }

        if (_helpEntries != null && _helpEntries.Length > 0)
        {
            SelectEntry(_helpEntries[0]);
        }
    }

    public void CloseHelp()
    {
        if (!_isOpen) return;

        _isOpen = false;

        if (helpPanel != null)
        {
            helpPanel.SetActive(false);
        }

        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
        {
            GameManager.Instance.TogglePause();
        }
    }

    public void SelectEntry(HelpData data)
    {
        if (data == null) return;

        if (detailTitle != null)
        {
            detailTitle.text = data.helpName;
        }

        if (detailImage != null)
        {
            detailImage.sprite = data.image;
            detailImage.gameObject.SetActive(data.image != null);
            if (data.image != null)
            {
                RectTransform imageRect = detailImage.rectTransform;
                float currentWidth = imageRect.rect.width;
                float nativeWidth = data.image.rect.width;
                float nativeHeight = data.image.rect.height;
                float scaledHeight = currentWidth * (nativeHeight / nativeWidth);
                imageRect.sizeDelta = new Vector2(currentWidth, scaledHeight);
            }
        }

        if (detailDescription != null)
        {
            detailDescription.text = data.description;
        }

        StartCoroutine(RebuildDetailLayout());
    }

    private IEnumerator RebuildDetailLayout()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();

        if (detailTitle != null)
        {
            detailTitle.ForceMeshUpdate(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(detailTitle.rectTransform);
        }

        if (detailDescription != null)
        {
            detailDescription.ForceMeshUpdate(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(detailDescription.rectTransform);
        }

        if (detailPanelRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(detailPanelRect);
            if (detailPanelRect.parent is RectTransform parentRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
        }

        Canvas.ForceUpdateCanvases();
    }

    public bool IsHelpOpen()
    {
        return _isOpen;
    }
}
