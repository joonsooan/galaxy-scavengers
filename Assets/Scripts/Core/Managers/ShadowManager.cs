using System;
using UnityEngine;
using UnityEngine.Tilemaps;

public class ShadowManager : MonoBehaviour
{
    public static ShadowManager Instance { get; private set; }

    [SerializeField] private float maxShear = 0.6f;
    [SerializeField] private float shadowLength = 0.5f;
    [SerializeField] [Range(0.1f, 5f)] private float fadeExponent = 1.0f;

    [Header("Day Shadow settings")]
    [Range(0f, 100f)] [SerializeField] private float dayShadowStartPercent = 15f;
    [Range(0f, 100f)] [SerializeField] private float dayShadowEndPercent = 85f;
    [SerializeField] private Color dayShadowColor = new Color(0f, 0f, 0f, 0.35f);

    [Header("Night Shadow settings")]
    [Range(0f, 100f)] [SerializeField] private float nightShadowStartPercent = 85f;
    [Range(0f, 100f)] [SerializeField] private float nightShadowEndPercent = 15f;
    [SerializeField] private Color nightShadowColor = new Color(0f, 0f, 0f, 0.20f);

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

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        Shader.SetGlobalFloat("_GlobalShadowShearX", 0f);
        Shader.SetGlobalFloat("_GlobalShadowLengthY", 0f);
        Shader.SetGlobalColor("_GlobalShadowColor", Color.clear);
    }

    private void Update()
    {
        if (DayNightCycleManager.Instance == null) return;

        float timePercent = DayNightCycleManager.Instance.GetTimePercent();

        float progress = -1f;
        Color baseColor = Color.clear;

        if (IsTimeInInterval(timePercent, dayShadowStartPercent, dayShadowEndPercent))
        {
            progress = GetIntervalProgress(timePercent, dayShadowStartPercent, dayShadowEndPercent);
            baseColor = dayShadowColor;
        }
        else if (IsTimeInInterval(timePercent, nightShadowStartPercent, nightShadowEndPercent))
        {
            progress = GetIntervalProgress(timePercent, nightShadowStartPercent, nightShadowEndPercent);
            baseColor = nightShadowColor;
        }

        float shearX = 0f;
        float alpha = 0f;
        float flatOffsetX = 0f;

        if (progress >= 0f)
        {
            shearX = Mathf.Lerp(maxShear, -maxShear, progress);
            flatOffsetX = Mathf.Lerp(maxFlatOffset, -maxFlatOffset, progress);
            float heightFactor = Mathf.Pow(Mathf.Sin(progress * Mathf.PI), fadeExponent);
            alpha = baseColor.a * heightFactor;
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

    private bool IsTimeInInterval(float t, float start, float end)
    {
        if (start < end)
        {
            return t >= start && t < end;
        }
        else
        {
            return t >= start || t < end;
        }
    }

    private float GetIntervalProgress(float t, float start, float end)
    {
        if (start < end)
        {
            float duration = end - start;
            if (duration <= 0f) return 0f;
            return Mathf.Clamp01((t - start) / duration);
        }
        else
        {
            float duration = (100f - start) + end;
            if (duration <= 0f) return 0f;
            float elapsed = (t >= start) ? (t - start) : ((100f - start) + t);
            return Mathf.Clamp01(elapsed / duration);
        }
    }

    private void InitializeTilemapShadows()
    {
        _lowWallShadowTilemap = CreateTilemapShadow(lowWallTilemap, shadowMaterial);
        _highWallShadowTilemap = CreateTilemapShadow(highWallTilemap, shadowMaterial);
        _resourceShadowTilemap = CreateTilemapShadow(resourceTilemap, shadowMaterial);
        RegisterShadowTilemapsWithFog();
    }

    private void RegisterShadowTilemapsWithFog()
    {
        if (FogOfWarManager.Instance == null) return;
        if (_lowWallShadowTilemap != null)
            FogOfWarManager.Instance.RegisterShadowTilemap(_lowWallShadowTilemap);
        if (_highWallShadowTilemap != null)
            FogOfWarManager.Instance.RegisterShadowTilemap(_highWallShadowTilemap);
        if (_resourceShadowTilemap != null)
            FogOfWarManager.Instance.RegisterShadowTilemap(_resourceShadowTilemap);
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
