using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PowerCoveragePreviewOverlay : MonoBehaviour
{
    public static PowerCoveragePreviewOverlay Instance { get; private set; }

    [SerializeField] private Tilemap previewTilemap;
    [SerializeField] private TileBase previewTile;
    [SerializeField] private int previewSortingOrder = 18;

    private readonly HashSet<Vector3Int> _paintedCells = new HashSet<Vector3Int>();
    private readonly List<IPowerGridNode> _nodeBuffer = new List<IPowerGridNode>();
    private static Tile _runtimeWhiteTile;

    public bool IsShowing { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        BuildingManager.OnBuildingConstructed += OnAnyBuildingConstructed;
    }

    private void OnDisable()
    {
        BuildingManager.OnBuildingConstructed -= OnAnyBuildingConstructed;
    }

    private void OnAnyBuildingConstructed(BuildingData data)
    {
        if (!IsShowing || data == null) {
            return;
        }
        if (data.buildingType != BuildingType.Generator && data.buildingType != BuildingType.Battery &&
            data.buildingType != BuildingType.PowerReceiver) {
            return;
        }
        Show();
    }

    private void Start()
    {
        EnsurePreviewTilemap();
    }

    private void EnsurePreviewTilemap()
    {
        if (previewTilemap != null) {
            return;
        }
        BuildingManager bm = BuildingManager.Instance;
        if (bm != null && bm.grid != null) {
            GameObject go = new GameObject("PowerCoveragePreviewTilemap");
            go.transform.SetParent(bm.grid.transform, false);
            previewTilemap = go.AddComponent<Tilemap>();
            TilemapRenderer tr = go.AddComponent<TilemapRenderer>();
            AlignPreviewRendererAboveSiblings(tr, bm.grid.transform);
        }
    }

    private static int GetSortingLayerListIndex(int layerId)
    {
        SortingLayer[] layers = SortingLayer.layers;
        for (int i = 0; i < layers.Length; i++) {
            if (layers[i].id == layerId) {
                return i;
            }
        }
        return -1;
    }

    private void AlignPreviewRendererAboveSiblings(TilemapRenderer previewRenderer, Transform gridTransform)
    {
        TilemapRenderer[] renderers = gridTransform.GetComponentsInChildren<TilemapRenderer>(true);
        int bestLayerListIndex = -1;
        int bestOrder = int.MinValue;
        TilemapRenderer topRef = null;
        foreach (TilemapRenderer r in renderers) {
            if (r == previewRenderer) {
                continue;
            }
            int li = GetSortingLayerListIndex(r.sortingLayerID);
            if (li > bestLayerListIndex || (li == bestLayerListIndex && r.sortingOrder > bestOrder)) {
                bestLayerListIndex = li;
                bestOrder = r.sortingOrder;
                topRef = r;
            }
        }
        if (topRef != null) {
            previewRenderer.sortingLayerID = topRef.sortingLayerID;
            previewRenderer.sortingOrder = topRef.sortingOrder + previewSortingOrder;
            return;
        }
        Tilemap ground = BuildingManager.Instance != null ? BuildingManager.Instance.GroundTilemap : null;
        if (ground != null) {
            TilemapRenderer gr = ground.GetComponent<TilemapRenderer>();
            if (gr != null) {
                previewRenderer.sortingLayerID = gr.sortingLayerID;
                previewRenderer.sortingOrder = gr.sortingOrder + previewSortingOrder;
            }
        }
    }

    private void OnDestroy()
    {
        IsShowing = false;
        ClearInternal();
        if (Instance == this) {
            Instance = null;
        }
    }

    public void Show()
    {
        EnsurePreviewTilemap();
        IsShowing = true;
        ClearInternal();
        if (previewTilemap == null) {
            return;
        }
        ElectricityConsumptionManager mgr = ElectricityConsumptionManager.Instance;
        BuildingManager bm = BuildingManager.Instance;
        if (mgr == null || bm == null) {
            return;
        }

        TileBase tile = previewTile;
        if (tile == null) {
            tile = GetOrCreateRuntimeWhiteTile();
        }

        mgr.FillActivePowerNodesForPreview(_nodeBuffer);
        Tilemap ground = bm.GroundTilemap;

        foreach (IPowerGridNode node in _nodeBuffer) {
            if (node == null) {
                continue;
            }
            BoundsInt bounds = node.GetPowerCoverageBounds();
            foreach (Vector3Int cell in bounds.allPositionsWithin) {
                if (ground == null || !ground.HasTile(cell) || bm.IsTerrainCell(cell)) {
                    continue;
                }
                if (_paintedCells.Add(cell)) {
                    previewTilemap.SetTile(cell, tile);
                }
            }
        }

        previewTilemap.RefreshAllTiles();
    }

    public void Clear()
    {
        IsShowing = false;
        ClearInternal();
    }

    private void ClearInternal()
    {
        if (previewTilemap == null) {
            return;
        }
        foreach (Vector3Int c in _paintedCells) {
            previewTilemap.SetTile(c, null);
        }
        _paintedCells.Clear();
        previewTilemap.RefreshAllTiles();
    }

    private static TileBase GetOrCreateRuntimeWhiteTile()
    {
        if (_runtimeWhiteTile != null) {
            return _runtimeWhiteTile;
        }
        Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        for (int x = 0; x < 4; x++) {
            for (int y = 0; y < 4; y++) {
                tex.SetPixel(x, y, Color.white);
            }
        }
        tex.Apply();
        tex.hideFlags = HideFlags.HideAndDontSave;
        Sprite spr = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
        _runtimeWhiteTile = ScriptableObject.CreateInstance<Tile>();
        _runtimeWhiteTile.sprite = spr;
        _runtimeWhiteTile.color = new Color(1f, 1f, 1f, 0.75f);
        _runtimeWhiteTile.hideFlags = HideFlags.HideAndDontSave;
        return _runtimeWhiteTile;
    }
}
