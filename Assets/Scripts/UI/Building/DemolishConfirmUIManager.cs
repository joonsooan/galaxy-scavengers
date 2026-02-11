using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DemolishTarget
{
    public string displayName;
    public Vector3Int anchorCell;
    public BuildingData buildingData;
    public BuildingPieceType buildingPieceType;
    public bool isConstructionSite;
    public ConstructionSite constructionSite;
    public Dictionary<ResourceType, int> preCalculatedRefund;
}

public class DemolishConfirmUIManager : MonoBehaviour
{
    [SerializeField] [Range(0f, 1f)] private float refundRatio = 0.25f;
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text buildingListText;
    [SerializeField] private Transform resourceGridContainer;
    [SerializeField] private GameObject resourceInfoCellPrefab;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private AreaBuildingDestroyer _areaBuildingDestroyer;
    private HashSet<Vector3Int> _pendingCells;
    private List<DemolishTarget> _pendingTargets;
    private Dictionary<ResourceType, int> _pendingRefund;
    private Coroutine _rebuildCoroutine;

    private void Awake()
    {
        if (panel != null)
            panel.SetActive(false);

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(OnConfirm);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(OnCancel);
        }
    }

    private void Start()
    {
        _areaBuildingDestroyer = FindFirstObjectByType<AreaBuildingDestroyer>();
    }

    public void Show(HashSet<Vector3Int> selectedCells, List<DemolishTarget> targets)
    {
        if (targets == null || targets.Count == 0) return;
        if (panel == null) return;

        _pendingCells = selectedCells;
        _pendingTargets = targets;
        _pendingRefund = CalculateTotalRefund(targets);

        if (buildingListText != null)
        {
            List<string> lines = new List<string>();
            foreach (var t in targets)
                lines.Add("- " + (t.displayName ?? "?"));
            buildingListText.text = string.Join("\n", lines);
        }

        if (resourceGridContainer != null)
        {
            foreach (Transform child in resourceGridContainer)
                Destroy(child.gameObject);

            if (resourceInfoCellPrefab != null && _pendingRefund != null)
            {
                foreach (var kvp in _pendingRefund)
                {
                    if (kvp.Value <= 0) continue;
                    GameObject cellObj = Instantiate(resourceInfoCellPrefab, resourceGridContainer);
                    ResourceInfoCell cell = cellObj.GetComponent<ResourceInfoCell>();
                    if (cell != null)
                        cell.SetInfoDisplayOnly(kvp.Key, kvp.Value);
                }
            }
        }

        panel.SetActive(true);
        RebuildLayout();
    }

    private void OnConfirm()
    {
        if (_areaBuildingDestroyer != null && _pendingCells != null)
        {
            if (_pendingRefund != null && ResourceManager.Instance != null)
            {
                foreach (var kvp in _pendingRefund)
                {
                    if (kvp.Value > 0)
                        ResourceManager.Instance.AddResource(kvp.Key, kvp.Value);
                }
            }
            _areaBuildingDestroyer.ExecuteDemolish(_pendingCells);
        }
        Hide();
    }

    private void OnCancel()
    {
        Hide();
    }

    private void Hide()
    {
        _pendingCells = null;
        _pendingTargets = null;
        _pendingRefund = null;
        if (panel != null)
            panel.SetActive(false);
    }

    private Dictionary<ResourceType, int> CalculateTotalRefund(List<DemolishTarget> targets)
    {
        Dictionary<ResourceType, int> total = new Dictionary<ResourceType, int>();
        foreach (var t in targets)
        {
            Dictionary<ResourceType, int> cost = t.preCalculatedRefund != null ? t.preCalculatedRefund : GetConstructionCost(t);
            bool isBuildingPiece = t.buildingData == null && t.buildingPieceType != BuildingPieceType.None;
            float ratio = isBuildingPiece ? 1f : refundRatio;

            foreach (var kvp in cost)
            {
                int refund = Mathf.FloorToInt(kvp.Value * ratio);
                if (refund > 0)
                {
                    if (total.ContainsKey(kvp.Key))
                        total[kvp.Key] += refund;
                    else
                        total[kvp.Key] = refund;
                }
            }
        }
        return total;
    }

    private Dictionary<ResourceType, int> GetConstructionCost(DemolishTarget target)
    {
        Dictionary<ResourceType, int> result = new Dictionary<ResourceType, int>();
        if (target.buildingData != null && target.buildingData.recipe != null)
        {
            BuildingPieceData[] all = Resources.LoadAll<BuildingPieceData>("Building Pieces");
            Dictionary<BuildingPieceType, BuildingPieceData> map = new Dictionary<BuildingPieceType, BuildingPieceData>();
            foreach (var data in all)
            {
                if (data.buildingPieceType != BuildingPieceType.None && !map.ContainsKey(data.buildingPieceType))
                    map[data.buildingPieceType] = data;
            }
            foreach (var piece in target.buildingData.recipe)
            {
                if (map.TryGetValue(piece.buildingPieceType, out BuildingPieceData pieceData) && pieceData.costs != null)
                {
                    foreach (var cost in pieceData.costs)
                    {
                        if (result.ContainsKey(cost.resourceType))
                            result[cost.resourceType] += cost.amount;
                        else
                            result[cost.resourceType] = cost.amount;
                    }
                }
            }
        }
        else if (target.buildingPieceType != BuildingPieceType.None)
        {
            BuildingPieceData pieceData = GetBuildingPieceData(target.buildingPieceType);
            if (pieceData != null && pieceData.costs != null)
            {
                foreach (var cost in pieceData.costs)
                {
                    if (result.ContainsKey(cost.resourceType))
                        result[cost.resourceType] += cost.amount;
                    else
                        result[cost.resourceType] = cost.amount;
                }
            }
        }
        return result;
    }

    private static BuildingPieceData GetBuildingPieceData(BuildingPieceType type)
    {
        if (type == BuildingPieceType.None) return null;
        BuildingPieceData[] all = Resources.LoadAll<BuildingPieceData>("Building Pieces");
        foreach (var data in all)
        {
            if (data.buildingPieceType == type)
                return data;
        }
        return null;
    }

    private void RebuildLayout()
    {
        Canvas.ForceUpdateCanvases();

        RectTransform panelRect = panel != null ? panel.GetComponent<RectTransform>() : null;
        if (buildingListText != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(buildingListText.rectTransform);
        if (resourceGridContainer != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(resourceGridContainer.GetComponent<RectTransform>());
        if (panelRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
        if (panelRect != null && panelRect.parent is RectTransform parentRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);

        if (_rebuildCoroutine != null)
            StopCoroutine(_rebuildCoroutine);
        _rebuildCoroutine = StartCoroutine(DelayedRebuildLayout());
    }

    private IEnumerator DelayedRebuildLayout()
    {
        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();

        RectTransform panelRect = panel != null ? panel.GetComponent<RectTransform>() : null;
        if (buildingListText != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(buildingListText.rectTransform);
        if (resourceGridContainer != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(resourceGridContainer.GetComponent<RectTransform>());
        if (panelRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
        if (panelRect != null && panelRect.parent is RectTransform parentRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);

        _rebuildCoroutine = null;
    }
}
