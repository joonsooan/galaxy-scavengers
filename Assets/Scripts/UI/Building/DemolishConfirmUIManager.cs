using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
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
    public bool isUnit;
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
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text returnResourceText;
    [SerializeField] private TMP_Text confirmButtonText;
    [SerializeField] private TMP_Text cancelButtonText;

    private AreaBuildingDestroyer _areaBuildingDestroyer;
    private HashSet<Vector3Int> _pendingCells;
    private List<DemolishTarget> _pendingTargets;
    private Dictionary<ResourceType, int> _pendingRefund;

    private void Awake()
    {
        if (panel != null)
            panel.SetActive(false);

        ResolveStaticTextReferences();
        ApplyLocalizedStaticTexts();

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

    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
        ResolveStaticTextReferences();
        ApplyLocalizedStaticTexts();
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
    }

    private void OnSelectedLocaleChanged(Locale _)
    {
        ApplyLocalizedStaticTexts();
    }

    private void ResolveStaticTextReferences()
    {
        if (titleText == null)
            titleText = FindTextByName("Title Text");
        if (returnResourceText == null)
            returnResourceText = FindTextByName("Return Resource Text");
        if (confirmButtonText == null)
            confirmButtonText = FindTextByName("Yes Button Text");
        if (cancelButtonText == null)
            cancelButtonText = FindTextByName("No Button Text");
        if (confirmButtonText == null && confirmButton != null)
            confirmButtonText = confirmButton.GetComponentInChildren<TMP_Text>(true);
        if (cancelButtonText == null && cancelButton != null)
            cancelButtonText = cancelButton.GetComponentInChildren<TMP_Text>(true);
    }

    private TMP_Text FindTextByName(string objectName)
    {
        if (panel == null)
            return null;

        TMP_Text[] texts = panel.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].gameObject.name == objectName)
                return texts[i];
        }

        return null;
    }

    private void ApplyLocalizedStaticTexts()
    {
        if (titleText != null)
            titleText.text = GameLocalization.GetOrDefault("UI_Common", "buildingDestroy.title", "철거 확인");
        if (returnResourceText != null)
            returnResourceText.text = GameLocalization.GetOrDefault("UI_Common", "buildingDestroy.returnResource", "반환 자원");
        if (confirmButtonText != null)
            confirmButtonText.text = GameLocalization.GetOrDefault("UI_Common", "buildingDestroy.yes", "예");
        if (cancelButtonText != null)
            cancelButtonText.text = GameLocalization.GetOrDefault("UI_Common", "buildingDestroy.no", "아니오");
    }

    public void Show(HashSet<Vector3Int> selectedCells, List<DemolishTarget> targets)
    {
        if (targets == null || targets.Count == 0) return;
        if (panel == null) return;

        _pendingCells = selectedCells;
        _pendingTargets = targets;
        _pendingRefund = CalculateTotalRefund(targets);
        ApplyLocalizedStaticTexts();

        if (buildingListText != null)
        {
            Dictionary<string, int> nameCounts = new Dictionary<string, int>();
            foreach (var t in targets)
            {
                string name = GetDisplayNameForTarget(t);
                if (nameCounts.ContainsKey(name))
                    nameCounts[name]++;
                else
                    nameCounts[name] = 1;
            }
            List<string> lines = new List<string>();
            foreach (var kvp in nameCounts)
            {
                string line = "- " + kvp.Key;
                if (kvp.Value > 1)
                    line += " " + kvp.Value;
                lines.Add(line);
            }
            buildingListText.text = string.Join("\n", lines);
        }

        if (resourceGridContainer != null)
        {
            foreach (Transform child in resourceGridContainer)
                Destroy(child.gameObject);

            if (resourceInfoCellPrefab != null && _pendingRefund != null)
            {
                foreach (var kvp in _pendingRefund.OrderBy(x => (int)x.Key))
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
                Vector3 referencePos = Vector3.zero;
                if (_pendingCells.Count > 0)
                {
                    Vector3Int firstCell = _pendingCells.First();
                    if (BuildingManager.Instance != null && BuildingManager.Instance.grid != null)
                    {
                        referencePos = BuildingManager.Instance.grid.CellToWorld(firstCell);
                    }
                }

                foreach (var kvp in _pendingRefund)
                {
                    if (kvp.Value > 0)
                        ResourceManager.Instance.DistributeRefundedResource(kvp.Key, kvp.Value, referencePos);
                }
            }
            HashSet<Vector3Int> cellsToDestroy = new HashSet<Vector3Int>(_pendingCells);
            if (_pendingTargets != null)
            {
                foreach (var target in _pendingTargets)
                {
                    if (target.isConstructionSite)
                        cellsToDestroy.Add(target.anchorCell);
                }
            }
            _areaBuildingDestroyer.ExecuteDemolish(cellsToDestroy);
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
            bool isConstructionSitePiece = t.isConstructionSite;
            bool isUnit = t.isUnit;
            float ratio = (isBuildingPiece || isConstructionSitePiece) ? 1f : (isUnit ? 0.5f : refundRatio);

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
            BuildingPieceData[] all = Resources.LoadAll<BuildingPieceData>("Building Piece Data");
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

    private string GetDisplayNameForTarget(DemolishTarget target)
    {
        return target.displayName ?? "?";
    }

    private static BuildingPieceData GetBuildingPieceData(BuildingPieceType type)
    {
        if (type == BuildingPieceType.None) return null;
        BuildingPieceData[] all = Resources.LoadAll<BuildingPieceData>("Building Piece Data");
        foreach (var data in all)
        {
            if (data.buildingPieceType == type)
                return data;
        }
        return null;
    }

    private void RebuildLayout()
    {
        StartCoroutine(RebuildLayoutNextFrame());
    }

    private IEnumerator RebuildLayoutNextFrame()
    {
        yield return null;
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
    }
}
