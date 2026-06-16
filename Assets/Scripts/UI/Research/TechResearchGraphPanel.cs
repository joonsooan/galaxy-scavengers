using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TechResearchGraphPanel : MonoBehaviour
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

    private readonly Dictionary<int, TechDataCell> _cellsByIndex = new Dictionary<int, TechDataCell>();
    private readonly Dictionary<(int from, int to), Image> _lineImages = new Dictionary<(int, int), Image>();

    private void Awake()
    {
        BuildCellRegistry();
    }

    private void OnEnable()
    {
        TechDataCell.OnCellSelected += OnCellSelectedHandler;
    }

    private void OnDisable()
    {
        TechDataCell.OnCellSelected -= OnCellSelectedHandler;
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
            _cellsByIndex[cell.TechData.techIndex] = cell;
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

        GameObject lineObj = new GameObject("Line", typeof(RectTransform), typeof(Image));
        lineObj.transform.SetParent(lineContainer, false);

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
