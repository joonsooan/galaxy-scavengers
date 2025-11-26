using UnityEngine;
using UnityEngine.Tilemaps;

public enum GradientMode
{
    Texture,
    Procedural
}

public class MapGenerator : MonoBehaviour
{
    public Vector2Int MapSize => new Vector2Int(width, height);
    public Tilemap Tilemap => tilemap;
    
    [Header("Tiles")]
    [SerializeField] private Tilemap tilemap;
    [SerializeField] private TileBase groundTile;
    [SerializeField] private TileBase lowWallTile;
    [SerializeField] private TileBase highWallTile;
    [SerializeField] private TileBase wallTile; // Border walls

    [Header("Map Generation Settings")]
    [SerializeField] private int width = 250;
    [SerializeField] private int height = 150;

    [Header("Procedural Terrain Settings")]
    [SerializeField] private bool useProceduralTerrain = true;
    [SerializeField] private int seed = 0;
    [SerializeField] private bool randomSeed = true;
    
    [Header("Perlin Noise Settings")]
    [Tooltip("Scale of the Perlin noise. Controls the size of terrain features. Range: 1-500+ (typically 10-200). Increase: Larger, smoother terrain features, more gradual transitions. Decrease: Smaller, more detailed features, more chaotic/varied terrain. Lower values create more frequent changes between tile types.")]
    [SerializeField] private float noiseScale = 50f;
    
    [Tooltip("Offset applied to the noise sampling coordinates. Shifts the entire terrain pattern. Range: Any Vector2 (typically -1000 to 1000). Increase X: Shifts pattern right. Increase Y: Shifts pattern up. Effect: Allows fine-tuning terrain placement without changing seed.")]
    [SerializeField] private Vector2 noiseOffset = Vector2.zero;
    
    [Header("Gradient Map Settings")]
    [Tooltip("Enable gradient map to add variation to terrain in specific areas. Range: true/false. Effect: When enabled, gradient values are combined with noise values to create more varied terrain patterns.")]
    [SerializeField] private bool useGradientMap = false;
    
    [Tooltip("Method for generating gradient map. Texture: Uses a gradient texture for precise control. Procedural: Generates gradient procedurally (radial from center/edges). Range: Texture/Procedural. Effect: Choose between texture-based or algorithm-based gradient generation.")]
    [SerializeField] private GradientMode gradientMode = GradientMode.Procedural;
    
    [Tooltip("Gradient texture used for gradient map generation. The grayscale values from this texture are sampled and combined with noise. Range: Any Texture2D (grayscale recommended). Effect: Black areas reduce terrain height, white areas increase it. Used when gradientMode is set to Texture.")]
    [SerializeField] private Texture2D gradientTexture;
    
    [Tooltip("Strength of gradient map influence on final terrain. Range: 0.0-1.0. Increase: Stronger gradient influence, more variation. Decrease: Weaker gradient influence, more noise-driven. Effect: Controls how much the gradient map affects terrain vs noise map.")]
    [Range(0f, 1f)]
    [SerializeField] private float gradientStrength = 0.5f;
    
    [Tooltip("Procedural gradient center point (0-1 coordinates). Range: 0.0-1.0 for X and Y. Effect: Determines where the gradient originates from when using Procedural mode. (0.5, 0.5) = center, (0,0) = corner.")]
    [SerializeField] private Vector2 gradientCenter = new Vector2(0.5f, 0.5f);
    
    [Tooltip("Falloff curve exponent for procedural gradient. Range: 1.0-10.0+. Increase: Sharper falloff, more concentrated gradient effect. Decrease: Gentler falloff, smoother gradient transition. Effect: Controls how quickly the gradient effect decreases from center.")]
    [Range(1f, 10f)]
    [SerializeField] private float falloffExponent = 2f;
    
    [Header("Terrain Thresholds")]
    [Tooltip("Noise value threshold for spawning low wall tiles. Terrain with noise values between this and highWallThreshold becomes low walls. Range: 0.0-1.0 (must be less than highWallThreshold). Increase: More low walls, less ground. Decrease: Less low walls, more ground. Controls distribution between ground and low walls.")]
    [Range(0f, 1f)]
    [SerializeField] private float lowWallThreshold = 0.5f;
    
    [Tooltip("Noise value threshold for spawning high wall tiles. Terrain with noise values above this becomes high walls. Range: 0.0-1.0 (must be greater than lowWallThreshold). Increase: Less high walls, more low walls/ground. Decrease: More high walls, less low walls/ground. Controls how common high elevation areas are.")]
    [Range(0f, 1f)]
    [SerializeField] private float highWallThreshold = 0.75f;

    private int _mapCenterXOffset;
    private int _mapCenterYOffset;
    private float[,] _noiseMap;
    private float[,] _gradientMap;

    public void GenerateMap()
    {
        CalculateMapDimensions();

        // Generate seed if random
        if (randomSeed)
        {
            seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        }

        // Generate noise map if using procedural terrain
        if (useProceduralTerrain)
        {
            GenerateNoiseMap();
        }

        // Generate gradient map if enabled
        if (useGradientMap)
        {
            GenerateGradientMap();
        }

        Vector3Int mapOrigin = new Vector3Int(-_mapCenterXOffset, -_mapCenterYOffset, 0);
        BoundsInt mapBounds = new BoundsInt(mapOrigin, new Vector3Int(width, height, 1));
        
        TileBase[] tiles = new TileBase[width * height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                tiles[x + y * width] = GetTileForPosition(x, y);
            }
        }
        
        tilemap.SetTilesBlock(mapBounds, tiles);
        tilemap.CompressBounds();
        SetupCameraController();
    }

    private void GenerateNoiseMap()
    {
        _noiseMap = new float[width, height];
        
        // Use seed for consistent generation
        System.Random prng = new System.Random(seed);
        float offsetX = prng.Next(-100000, 100000) + noiseOffset.x;
        float offsetY = prng.Next(-100000, 100000) + noiseOffset.y;

        // Generate Perlin noise values
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // Calculate sample coordinates for Perlin noise
                float sampleX = (x / noiseScale) + offsetX;
                float sampleY = (y / noiseScale) + offsetY;

                // Generate Perlin noise value (0 to 1)
                float perlinValue = Mathf.PerlinNoise(sampleX, sampleY);
                
                _noiseMap[x, y] = perlinValue;
            }
        }
    }

    private void GenerateGradientMap()
    {
        _gradientMap = new float[width, height];

        if (gradientMode == GradientMode.Texture && gradientTexture != null)
        {
            // Generate gradient map from texture
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    // Sample texture at corresponding coordinates
                    int texX = Mathf.RoundToInt(x * (float)gradientTexture.width / width);
                    int texY = Mathf.RoundToInt(y * (float)gradientTexture.height / height);
                    
                    // Clamp to texture bounds
                    texX = Mathf.Clamp(texX, 0, gradientTexture.width - 1);
                    texY = Mathf.Clamp(texY, 0, gradientTexture.height - 1);
                    
                    // Get pixel color and convert to grayscale (0-1)
                    Color pixelColor = gradientTexture.GetPixel(texX, texY);
                    _gradientMap[x, y] = pixelColor.grayscale;
                }
            }
        }
        else if (gradientMode == GradientMode.Procedural)
        {
            // Generate procedural gradient (radial falloff from center)
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    // Normalize coordinates to 0-1 range
                    float normalizedX = (float)x / width;
                    float normalizedY = (float)y / height;

                    // Calculate distance from gradient center
                    float dx = normalizedX - gradientCenter.x;
                    float dy = normalizedY - gradientCenter.y;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    // Apply falloff curve (max distance is from corner to center: ~0.707)
                    float maxDistance = Mathf.Sqrt(0.5f * 0.5f + 0.5f * 0.5f);
                    float normalizedDistance = Mathf.Clamp01(distance / maxDistance);

                    // Apply exponent for falloff curve
                    float gradientValue = Mathf.Pow(normalizedDistance, falloffExponent);
                    
                    // Invert so center has higher values (white) and edges have lower (black)
                    _gradientMap[x, y] = 1f - gradientValue;
                }
            }
        }
        else
        {
            // Fallback: create flat gradient map
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    _gradientMap[x, y] = 0.5f;
                }
            }
        }
    }

    private void CalculateMapDimensions()
    {
        _mapCenterXOffset = width / 2;
        _mapCenterYOffset = height / 2;
    }
    
    private void SetupCameraController()
    {
        Bounds localBounds = tilemap.localBounds;
        Vector3 worldCenter = tilemap.transform.TransformPoint(localBounds.center);
        Vector3 worldSize = Vector3.Scale(localBounds.size, tilemap.transform.lossyScale);
        Bounds mapWorldBounds = new Bounds(worldCenter, worldSize);

        if (Camera.main != null && Camera.main.TryGetComponent<CameraController>(out var cameraController))
        {
            cameraController.SetBounds(mapWorldBounds);
        }
    }
    
    private TileBase GetTileForPosition(int x, int y)
    {
        // Always use wall tile for map borders
        bool isBorder = x == 0 || x == width - 1 || y == 0 || y == height - 1;
        if (isBorder)
        {
            return wallTile;
        }

        // Use procedural terrain if enabled and noise map is generated
        if (useProceduralTerrain && _noiseMap != null)
        {
            float noiseValue = _noiseMap[x, y];
            
            // Combine with gradient map if enabled
            if (useGradientMap && _gradientMap != null)
            {
                float gradientValue = _gradientMap[x, y];
                
                // Combine noise and gradient values
                // Gradient strength controls how much gradient affects the final value
                float combinedValue = noiseValue + (gradientValue - 0.5f) * gradientStrength;
                
                // Normalize to 0-1 range
                combinedValue = Mathf.Clamp01(combinedValue);
                
                // Use combined value for tile selection
                noiseValue = combinedValue;
            }
            
            // Invert noise value to swap ground and high wall tiles
            // Low noise values become high (high walls), high noise values become low (ground)
            noiseValue = 1f - noiseValue;
            
            // Map noise values to tile types based on thresholds
            if (noiseValue >= highWallThreshold)
            {
                return highWallTile != null ? highWallTile : groundTile;
            }
            else if (noiseValue >= lowWallThreshold)
            {
                return lowWallTile != null ? lowWallTile : groundTile;
            }
            else
            {
                return groundTile;
            }
        }

        // Fallback to ground tile
        return groundTile;
    }

    public bool IsPositionInBounds(Vector3 worldPosition)
    {
        Vector3 localPos = tilemap.transform.InverseTransformPoint(worldPosition);

        int cellX = Mathf.FloorToInt(localPos.x + _mapCenterXOffset);
        int cellY = Mathf.FloorToInt(localPos.y + _mapCenterYOffset);

        return cellX >= 0 && cellX < width && cellY >= 0 && cellY < height;
    }
    
}