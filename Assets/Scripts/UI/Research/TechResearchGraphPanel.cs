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

    private readonly Dictionary<int, TechDataCell> _cellsByIndex = new Dictionary<int, TechDataCell>();

    private void Awake()
    {
        BuildCellRegistry();
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

        foreach (KeyValuePair<int, TechDataCell> pair in _cellsByIndex)
        {
            TechDataCell cell = pair.Value;
            int[] successorIndices = cell.TechData.successorTechIndices;
            if (successorIndices == null)
            {
                continue;
            }

            for (int i = 0; i < successorIndices.Length; i++)
            {
                TechDataCell successor;
                if (_cellsByIndex.TryGetValue(successorIndices[i], out successor))
                {
                    Vector3 start = cell.GetRightCenterWorld();
                    Vector3 end = successor.GetLeftCenterWorld();
                    DrawLine(start, end);
                }
            }
        }
    }

    private void DrawLine(Vector3 startWorld, Vector3 endWorld)
    {
        if (lineContainer == null)
        {
            return;
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
