using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class TechResearchGraphPanel : MonoBehaviour, IPointerClickHandler
{
    [Header("References")]
    [SerializeField] private ResearchPanelUI researchPanelUI;
    [SerializeField] private Transform lineContainer;

    [Header("Line Settings")]
    [SerializeField] private float lineThickness = 2f;
    [SerializeField] private Color lineColor = Color.white;

    [Header("Selected Line")]
    [SerializeField] private Color selectedLineColor = Color.yellow;
    [SerializeField] private float selectedLineThickness = 4f;

    [Header("Focus Mode")]
    [SerializeField] private bool focusOnSelected;
    [SerializeField] private float dimmedAlpha = 0.3f;

    private readonly Dictionary<int, TechDataCell> _cellsByIndex = new Dictionary<int, TechDataCell>();
    private readonly Dictionary<int, CanvasGroup> _cellCanvasGroups = new Dictionary<int, CanvasGroup>();
    private readonly Dictionary<(int from, int to), Image> _lineImages = new Dictionary<(int, int), Image>();
    private int _selectedTechIndex = -1;

    private void Awake()
    {
        BuildCellRegistry();
    }

    private void OnEnable()
    {
        TechDataCell.OnCellSelected += OnCellSelectedHandler;
        ResearchPanelUI.OnSelectionCleared += RestoreAllVisibility;
    }

    private void OnDisable()
    {
        TechDataCell.OnCellSelected -= OnCellSelectedHandler;
        ResearchPanelUI.OnSelectionCleared -= RestoreAllVisibility;
        RestoreAllVisibility();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (researchPanelUI != null)
            researchPanelUI.ClearSelection();
    }

    private void Start()
    {
        foreach (KeyValuePair<int, TechDataCell> pair in _cellsByIndex)
        {
            pair.Value.Setup(researchPanelUI);
        }

        StartCoroutine(DrawLinesAfterLayout());
    }

    private IEnumerator DrawLinesAfterLayout()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        DrawAllLines();
    }

    public void RedrawLines()
    {
        if (lineContainer != null)
        {
            for (int i = lineContainer.childCount - 1; i >= 0; i--)
                Destroy(lineContainer.GetChild(i).gameObject);
        }

        _lineImages.Clear();
        _cellsByIndex.Clear();
        _cellCanvasGroups.Clear();
        BuildCellRegistry();
        StartCoroutine(DrawLinesAfterLayout());
    }

    private void BuildCellRegistry()
    {
        TechDataCell[] cells = GetComponentsInChildren<TechDataCell>(true);
        for (int i = 0; i < cells.Length; i++)
        {
            TechDataCell cell = cells[i];
            if (cell.TechData == null)
            {
                continue;
            }
            int idx = cell.TechData.techIndex;
            _cellsByIndex[idx] = cell;
            CanvasGroup cg = cell.GetComponent<CanvasGroup>();
            if (cg == null)
                cg = cell.gameObject.AddComponent<CanvasGroup>();
            _cellCanvasGroups[idx] = cg;
        }
    }

    private void DrawAllLines()
    {
        if (lineContainer == null)
        {
            return;
        }

        _lineImages.Clear();

        foreach (KeyValuePair<int, TechDataCell> pair in _cellsByIndex)
        {
            TechDataCell cell = pair.Value;
            int fromIndex = pair.Key;
            int[] successorIndices = cell.TechData.successorTechIndices;
            if (successorIndices == null)
            {
                continue;
            }

            for (int i = 0; i < successorIndices.Length; i++)
            {
                int toIndex = successorIndices[i];
                TechDataCell successor;
                if (_cellsByIndex.TryGetValue(toIndex, out successor))
                {
                    Vector3 start = cell.GetRightCenterWorld();
                    Vector3 end = successor.GetLeftCenterWorld();
                    Image lineImg = DrawLine(start, end);
                    _lineImages[(fromIndex, toIndex)] = lineImg;
                }
            }
        }
    }

    private Image DrawLine(Vector3 startWorld, Vector3 endWorld)
    {
        if (lineContainer == null)
        {
            return null;
        }

        Vector3 startLocal = lineContainer.InverseTransformPoint(startWorld);
        Vector3 endLocal = lineContainer.InverseTransformPoint(endWorld);

        Vector2 dir = (Vector2)endLocal - (Vector2)startLocal;
        float distance = dir.magnitude;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        Vector3 midLocal = (startLocal + endLocal) * 0.5f;

        GameObject lineObj = new GameObject("Line", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        lineObj.transform.SetParent(lineContainer, false);

        lineObj.GetComponent<LayoutElement>().ignoreLayout = true;

        Image img = lineObj.GetComponent<Image>();
        img.color = lineColor;

        RectTransform rt = lineObj.GetComponent<RectTransform>();
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(distance, lineThickness);
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);
        rt.localPosition = new Vector3(midLocal.x, midLocal.y, 0f);

        return img;
    }

    private void OnCellSelectedHandler(int techIndex)
    {
        foreach (KeyValuePair<(int from, int to), Image> pair in _lineImages)
        {
            Image img = pair.Value;
            if (img == null)
            {
                continue;
            }

            img.color = lineColor;
            RectTransform rt = img.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, lineThickness);
        }

        foreach (KeyValuePair<(int from, int to), Image> pair in _lineImages)
        {
            if (pair.Key.from != techIndex && pair.Key.to != techIndex)
            {
                continue;
            }

            Image img = pair.Value;
            if (img == null)
            {
                continue;
            }

            img.color = selectedLineColor;
            RectTransform rt = img.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, selectedLineThickness);
        }

        if (focusOnSelected)
        {
            _selectedTechIndex = techIndex;
            ApplyFocusVisibility(techIndex);
        }
    }

    private void ApplyFocusVisibility(int techIndex)
    {
        System.Collections.Generic.HashSet<int> visible = new System.Collections.Generic.HashSet<int>();
        visible.Add(techIndex);

        TechDataCell selectedCell;
        if (_cellsByIndex.TryGetValue(techIndex, out selectedCell))
        {
            int[] successors = selectedCell.TechData.successorTechIndices;
            if (successors != null)
            {
                for (int i = 0; i < successors.Length; i++)
                    visible.Add(successors[i]);
            }
        }

        foreach (KeyValuePair<int, TechDataCell> pair in _cellsByIndex)
        {
            int[] successors = pair.Value.TechData.successorTechIndices;
            if (successors == null)
                continue;
            for (int i = 0; i < successors.Length; i++)
            {
                if (successors[i] == techIndex)
                {
                    visible.Add(pair.Key);
                    break;
                }
            }
        }

        foreach (KeyValuePair<int, CanvasGroup> pair in _cellCanvasGroups)
            pair.Value.alpha = visible.Contains(pair.Key) ? 1f : dimmedAlpha;

        foreach (KeyValuePair<(int from, int to), Image> pair in _lineImages)
        {
            if (pair.Value == null)
                continue;
            bool connected = pair.Key.from == techIndex || pair.Key.to == techIndex;
            Color c = pair.Value.color;
            pair.Value.color = new Color(c.r, c.g, c.b, connected ? c.a : dimmedAlpha);
        }
    }

    private void RestoreAllVisibility()
    {
        _selectedTechIndex = -1;

        foreach (KeyValuePair<int, CanvasGroup> pair in _cellCanvasGroups)
            pair.Value.alpha = 1f;

        foreach (KeyValuePair<(int from, int to), Image> pair in _lineImages)
        {
            if (pair.Value == null)
                continue;
            pair.Value.color = lineColor;
            RectTransform rt = pair.Value.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, lineThickness);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        TechDataCell[] cells = GetComponentsInChildren<TechDataCell>(true);
        Dictionary<int, TechDataCell> cellMap = new Dictionary<int, TechDataCell>();
        for (int i = 0; i < cells.Length; i++)
        {
            TechDataCell cell = cells[i];
            if (cell.TechData != null)
                cellMap[cell.TechData.techIndex] = cell;
        }

        Gizmos.color = lineColor;
        foreach (KeyValuePair<int, TechDataCell> pair in cellMap)
        {
            int[] successors = pair.Value.TechData.successorTechIndices;
            if (successors == null)
                continue;
            for (int i = 0; i < successors.Length; i++)
            {
                TechDataCell successor;
                if (cellMap.TryGetValue(successors[i], out successor))
                {
                    Gizmos.DrawLine(pair.Value.GetRightCenterWorld(), successor.GetLeftCenterWorld());
                }
            }
        }
    }
#endif
}
