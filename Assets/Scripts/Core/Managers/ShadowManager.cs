using System;
using UnityEngine;
using UnityEngine.Tilemaps;

public class ShadowManager : MonoBehaviour
{
    public static ShadowManager Instance { get; private set; }

    [SerializeField] private float maxShear = 0.6f;
    [SerializeField] private float shadowLength = 0.5f;
    [SerializeField] private Color dayShadowColor = new Color(0f, 0f, 0f, 0.35f);
    [SerializeField] [Range(0.1f, 5f)] private float fadeExponent = 1.0f;

    [Header("Tilemap Settings")]
    [SerializeField] private Tilemap lowWallTilemap;
    [SerializeField] private Tilemap highWallTilemap;
    [SerializeField] private Tilemap resourceTilemap;
    [SerializeField] private Material shadowMaterial;
    [SerializeField] private float maxFlatOffset = 0.2f;
    [SerializeField] private float flatOffsetY = 0.15f;

    private Tilemap _lowWallShadowTilemap;
    private Tilemap _highWallShadowTilemap;
    private Tilemap _resourceShadowTilemap;

    public Tilemap ResourceShadowTilemap => _resourceShadowTilemap;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        GameManager.OnGameSceneInitialized += InitializeTilemapShadows;
    }

    private void OnDisable()
    {
        GameManager.OnGameSceneInitialized -= InitializeTilemapShadows;
    }

    private void Update()
    {
        if (DayNightCycleManager.Instance == null) return;

        float currentTime = DayNightCycleManager.Instance.GetTime();
        float dayStartHour = (DayNightCycleManager.Instance.DayStartPercent / 100f) * 24f;
        float nightStartHour = (DayNightCycleManager.Instance.NightStartPercent / 100f) * 24f;

        float shearX = 0f;
        float alpha = 0f;
        float flatOffsetX = 0f;

        if (currentTime >= dayStartHour && currentTime < nightStartHour)
        {
            float dayProgress = (currentTime - dayStartHour) / (nightStartHour - dayStartHour);
            shearX = Mathf.Lerp(maxShear, -maxShear, dayProgress);
            flatOffsetX = Mathf.Lerp(maxFlatOffset, -maxFlatOffset, dayProgress);
            float heightFactor = Mathf.Pow(Mathf.Sin(dayProgress * Mathf.PI), fadeExponent);
            alpha = dayShadowColor.a * heightFactor;
        }
        else
        {
            shearX = 0f;
            flatOffsetX = 0f;
            alpha = 0f;
        }

        Shader.SetGlobalFloat("_GlobalShadowShearX", shearX);
        Shader.SetGlobalFloat("_GlobalShadowLengthY", shadowLength);
        Shader.SetGlobalColor("_GlobalShadowColor", new Color(0f, 0f, 0f, alpha));

        Vector3 flatOffset = new Vector3(flatOffsetX, flatOffsetY, 0f);
        if (_lowWallShadowTilemap != null)
        {
            _lowWallShadowTilemap.transform.localPosition = flatOffset;
        }
        if (_highWallShadowTilemap != null)
        {
            _highWallShadowTilemap.transform.localPosition = flatOffset;
        }
        if (_resourceShadowTilemap != null)
        {
            _resourceShadowTilemap.transform.localPosition = flatOffset;
        }
    }

    private void InitializeTilemapShadows()
    {
        _lowWallShadowTilemap = CreateTilemapShadow(lowWallTilemap, shadowMaterial);
        _highWallShadowTilemap = CreateTilemapShadow(highWallTilemap, shadowMaterial);
        _resourceShadowTilemap = CreateTilemapShadow(resourceTilemap, shadowMaterial);
    }

    public Tilemap CreateTilemapShadow(Tilemap sourceTilemap, Material shadowMaterial)
    {
        if (sourceTilemap == null) return null;

        GameObject shadowObj = new GameObject(sourceTilemap.name + "_Shadow");
        shadowObj.transform.SetParent(sourceTilemap.transform);
        shadowObj.transform.localPosition = Vector3.zero;
        shadowObj.transform.localRotation = Quaternion.identity;
        shadowObj.transform.localScale = Vector3.one;

        Tilemap shadowTilemap = shadowObj.AddComponent<Tilemap>();
        TilemapRenderer shadowRenderer = shadowObj.AddComponent<TilemapRenderer>();

        TilemapRenderer sourceRenderer = sourceTilemap.GetComponent<TilemapRenderer>();
        if (sourceRenderer != null)
        {
            shadowRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            shadowRenderer.sortingOrder = sourceRenderer.sortingOrder - 1;
        }

        shadowRenderer.material = shadowMaterial;

        BoundsInt bounds = sourceTilemap.cellBounds;
        TileBase[] allTiles = sourceTilemap.GetTilesBlock(bounds);
        shadowTilemap.SetTilesBlock(bounds, allTiles);

        return shadowTilemap;
    }
}
