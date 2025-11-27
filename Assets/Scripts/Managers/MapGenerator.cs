using System.Collections.Generic;
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
    [SerializeField] private TileBase wallTile;

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
    
    [Header("Map Polishing Settings")]
    [Tooltip("Enable center circle that ensures ground tiles at map center. Range: true/false. Effect: Creates a safe starting area in the center of the map.")]
    [SerializeField] private bool enableCenterCircle = true;
    
    [Tooltip("Radius of the center circle in tiles. Range: 1-100+. Increase: Larger center area becomes ground. Decrease: Smaller center area. Effect: Controls the size of the guaranteed ground area at map center.")]
    [SerializeField] private int centerCircleRadius = 10;
    
    [Tooltip("Fill disconnected areas surrounded by walls with high walls. Range: true/false. Effect: Removes isolated ground/low wall areas that are disconnected from the center.")]
    [SerializeField] private bool fillDisconnectedAreas = true;
    
    [Tooltip("Enable enemy spawn holes around the map. Range: true/false. Effect: Creates openings around the map perimeter for enemy spawning.")]
    [SerializeField] private bool enableEnemySpawnHoles = true;
    
    [Tooltip("Number of enemy spawn holes to create. Range: 1-10. Effect: Controls how many spawn areas are created around the map.")]
    [SerializeField] private int enemySpawnHoleCount = 3;
    
    [Tooltip("Radius of each enemy spawn hole in tiles. Range: 1-50+. Increase: Larger spawn areas. Decrease: Smaller spawn areas. Effect: Controls the size of each enemy spawn opening.")]
    [SerializeField] private int enemySpawnHoleRadius = 5;
    
    [Tooltip("Distance from map center to spawn holes as a ratio (0.0-1.0). Range: 0.3-0.9. Increase: Holes further from center, closer to edges. Decrease: Holes closer to center. Effect: Controls where around the map the spawn holes appear. 0.7-0.8 recommended for edge placement.")]
    [Range(0.3f, 0.9f)]
    [SerializeField] private float enemySpawnHoleDistanceRatio = 0.7f;

    private int _mapCenterXOffset;
    private int _mapCenterYOffset;
    private float[,] _noiseMap;
    private float[,] _gradientMap;

    public void GenerateMap()
    {
        CalculateMapDimensions();

        if (randomSeed)
        {
            seed = Random.Range(int.MinValue, int.MaxValue);
        }

        if (useProceduralTerrain)
        {
            GenerateNoiseMap();
        }

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
        
        PolishMap();
        
        // Copy terrain tiles to BuildingManager's groundTilemap so buildings can be placed and fog works correctly
        // Do this after PolishMap() so all modifications are included
        CopyTilesToBuildingManagerGroundTilemap(mapBounds);
        
        tilemap.CompressBounds();
        SetupCameraController();
        
        if (FogOfWarManager.Instance != null)
        {
            FogOfWarManager.Instance.RefreshFogOfWar();
        }
    }

    private void PolishMap()
    {
        if (enableCenterCircle)
        {
            PunchCenterCircle();
        }

        if (fillDisconnectedAreas)
        {
            FillDisconnectedAreas();
        }

        if (enableEnemySpawnHoles)
        {
            PunchEnemySpawnHoles();
        }
        
        if (fillDisconnectedAreas)
        {
            FillDisconnectedAreas();
        }
        
        ConnectWallsToBorders();
    }

    private TileBase GetTileForSmoothTransition(int x, int y, float transitionFactor, TileBase originalTile)
    {
        // Create smooth transition - prevent high walls from appearing next to ground
        if (transitionFactor < 0.5f)
        {
            // Inner half of transition: always ground
            return groundTile;
        }
        else if (originalTile == highWallTile)
        {
            // Original was high wall: use low wall as intermediate for smooth transition
            return lowWallTile != null ? lowWallTile : groundTile;
        }
        else
        {
            // Original was low wall or ground: use ground to maintain smoothness
            return groundTile;
        }
    }

    private void PunchCenterCircle()
    {
        int centerX = width / 2;
        int centerY = height / 2;
        
        // Define transition zone (70% inner = full ground, 30% outer = transition)
        float transitionStartRatio = 0.7f;
        float transitionEnd = centerCircleRadius;
        float transitionStart = centerCircleRadius * transitionStartRatio;
        
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // Skip borders
                if (x == 0 || x == width - 1 || y == 0 || y == height - 1)
                    continue;
                
                float distance = Mathf.Sqrt((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY));
                
                if (distance <= centerCircleRadius)
                {
                    Vector3Int cellPos = new Vector3Int(x - _mapCenterXOffset, y - _mapCenterYOffset, 0);
                    
                    // Inner area: guaranteed ground
                    if (distance <= transitionStart)
                    {
                        tilemap.SetTile(cellPos, groundTile);
                    }
                    // Transition zone: blend between ground and surrounding terrain
                    else
                    {
                        // Get original tile type based on noise (before modifications)
                        TileBase originalTile = GetTileForPosition(x, y);
                        
                        // Create transition: closer to center = more ground, closer to edge = more original
                        float transitionFactor = (distance - transitionStart) / (transitionEnd - transitionStart);
                        
                        TileBase transitionTile = GetTileForSmoothTransition(x, y, transitionFactor, originalTile);
                        tilemap.SetTile(cellPos, transitionTile);
                    }
                }
            }
        }
    }

    private void FillDisconnectedAreas()
    {
        // Create a map to track which ground tiles are connected to center
        bool[,] connectedToCenter = new bool[width, height];
        
        // Find center ground tile position
        int centerX = width / 2;
        int centerY = height / 2;
        
        // Flood fill from center to find all connected ground tiles
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        
        // Start from center - only if it's a ground tile
        Vector3Int centerCellPos = new Vector3Int(centerX - _mapCenterXOffset, centerY - _mapCenterYOffset, 0);
        TileBase centerTile = tilemap.GetTile(centerCellPos);
        
        // Only start flood fill if center is actually a ground tile
        if (centerTile == groundTile)
        {
            queue.Enqueue(new Vector2Int(centerX, centerY));
            connectedToCenter[centerX, centerY] = true;
        }
        
        // Flood fill through ground tiles only (walls act as barriers)
        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            int x = current.x;
            int y = current.y;
            
            // Check all four neighbors
            Vector2Int[] neighbors = new Vector2Int[]
            {
                new Vector2Int(x + 1, y),
                new Vector2Int(x - 1, y),
                new Vector2Int(x, y + 1),
                new Vector2Int(x, y - 1)
            };
            
            foreach (Vector2Int neighbor in neighbors)
            {
                int nx = neighbor.x;
                int ny = neighbor.y;
                
                // Skip if out of bounds
                if (nx < 1 || nx >= width - 1 || ny < 1 || ny >= height - 1)
                    continue;
                
                // Skip if already connected
                if (connectedToCenter[nx, ny])
                    continue;
                
                // Check if neighbor tile is a ground tile (can only pass through ground tiles)
                Vector3Int neighborCellPos = new Vector3Int(nx - _mapCenterXOffset, ny - _mapCenterYOffset, 0);
                TileBase neighborTile = tilemap.GetTile(neighborCellPos);
                
                // Only flood fill through ground tiles (walls block the path)
                if (neighborTile == groundTile)
                {
                    connectedToCenter[nx, ny] = true;
                    queue.Enqueue(neighbor);
                }
            }
        }
        
        // Scan every tile and convert isolated ground tiles to low wall tiles
        for (int x = 1; x < width - 1; x++) // Skip borders
        {
            for (int y = 1; y < height - 1; y++) // Skip borders
            {
                Vector3Int cellPos = new Vector3Int(x - _mapCenterXOffset, y - _mapCenterYOffset, 0);
                TileBase currentTile = tilemap.GetTile(cellPos);
                
                // If this is a ground tile that is NOT connected to center, it's isolated
                if (currentTile == groundTile && !connectedToCenter[x, y])
                {
                    // Change isolated ground tile to low wall tile
                    tilemap.SetTile(cellPos, lowWallTile != null ? lowWallTile : groundTile);
                }
            }
        }
    }

    private void PunchEnemySpawnHoles()
    {
        int centerX = width / 2;
        int centerY = height / 2;
        
        // Calculate the distance from center to spawn holes
        float maxDistance = Mathf.Min(width, height) * 0.5f * enemySpawnHoleDistanceRatio;
        
        // Generate spawn hole positions evenly distributed around the map
        for (int i = 0; i < enemySpawnHoleCount; i++)
        {
            // Calculate angle for evenly distributed holes
            float angle = (i / (float)enemySpawnHoleCount) * 360f * Mathf.Deg2Rad;
            
            // Calculate position based on angle and distance
            int holeCenterX = centerX + Mathf.RoundToInt(Mathf.Cos(angle) * maxDistance);
            int holeCenterY = centerY + Mathf.RoundToInt(Mathf.Sin(angle) * maxDistance);
            
            // Clamp to valid map bounds (with margin for radius)
            holeCenterX = Mathf.Clamp(holeCenterX, enemySpawnHoleRadius + 1, width - enemySpawnHoleRadius - 2);
            holeCenterY = Mathf.Clamp(holeCenterY, enemySpawnHoleRadius + 1, height - enemySpawnHoleRadius - 2);
            
            // Punch the hole, following threshold rules
            PunchHoleWithThreshold(holeCenterX, holeCenterY, enemySpawnHoleRadius);
            
            // Create a path from enemy hole to center to ensure connectivity
            CreatePathToCenter(holeCenterX, holeCenterY, centerX, centerY);
        }
    }
    
    private void CreatePathToCenter(int startX, int startY, int endX, int endY)
    {
        // Create a simple path using Bresenham-like line algorithm
        int dx = Mathf.Abs(endX - startX);
        int dy = Mathf.Abs(endY - startY);
        int sx = startX < endX ? 1 : -1;
        int sy = startY < endY ? 1 : -1;
        int err = dx - dy;
        
        int currentX = startX;
        int currentY = startY;
        int pathWidth = 2; // Width of the path to ensure connectivity
        
        // Create path tiles along the line from start to end
        while (true)
        {
            // Create a small area at each point for path width
            for (int offsetX = -pathWidth; offsetX <= pathWidth; offsetX++)
            {
                for (int offsetY = -pathWidth; offsetY <= pathWidth; offsetY++)
                {
                    int pathX = currentX + offsetX;
                    int pathY = currentY + offsetY;
                    
                    // Skip if out of bounds
                    if (pathX < 1 || pathX >= width - 1 || pathY < 1 || pathY >= height - 1)
                        continue;
                    
                    // Only create path if within distance (creates path of width 2-3 tiles)
                    float dist = Mathf.Sqrt(offsetX * offsetX + offsetY * offsetY);
                    if (dist <= pathWidth)
                    {
                        Vector3Int cellPos = new Vector3Int(pathX - _mapCenterXOffset, pathY - _mapCenterYOffset, 0);
                        TileBase currentTile = tilemap.GetTile(cellPos);
                        
                        // Break through walls to create path, prefer ground for center of path
                        if (dist <= 1)
                        {
                            // Center of path: use ground tiles
                            tilemap.SetTile(cellPos, groundTile);
                        }
                        else if (currentTile == highWallTile)
                        {
                            // Convert high walls to low walls on path edges
                            tilemap.SetTile(cellPos, lowWallTile != null ? lowWallTile : groundTile);
                        }
                        else if (currentTile == lowWallTile)
                        {
                            // Convert low walls to ground if close to center
                            tilemap.SetTile(cellPos, groundTile);
                        }
                    }
                }
            }
            
            // Check if we've reached the destination (with some tolerance)
            if (Mathf.Abs(currentX - endX) <= 1 && Mathf.Abs(currentY - endY) <= 1)
                break;
            
            // Move along the line
            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                currentX += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                currentY += sy;
            }
            
            // Safety check to prevent infinite loops
            if (Mathf.Abs(currentX - startX) > width * 2 || Mathf.Abs(currentY - startY) > height * 2)
                break;
        }
    }

    private void ConnectWallsToBorders()
    {
        // Check left border (x = 0)
        for (int y = 0; y < height; y++)
        {
            int innerX = 1;
            if (innerX < width)
            {
                Vector3Int innerCellPos = new Vector3Int(innerX - _mapCenterXOffset, y - _mapCenterYOffset, 0);
                TileBase innerTile = tilemap.GetTile(innerCellPos);
                
                if (innerTile == highWallTile)
                {
                    Vector3Int borderCellPos = new Vector3Int(-_mapCenterXOffset, y - _mapCenterYOffset, 0);
                    tilemap.SetTile(borderCellPos, highWallTile);
                }
            }
        }
        
        // Check right border (x = width - 1)
        for (int y = 0; y < height; y++)
        {
            int innerX = width - 2;
            if (innerX >= 0)
            {
                Vector3Int innerCellPos = new Vector3Int(innerX - _mapCenterXOffset, y - _mapCenterYOffset, 0);
                TileBase innerTile = tilemap.GetTile(innerCellPos);
                
                if (innerTile == highWallTile)
                {
                    Vector3Int borderCellPos = new Vector3Int((width - 1) - _mapCenterXOffset, y - _mapCenterYOffset, 0);
                    tilemap.SetTile(borderCellPos, highWallTile);
                }
            }
        }
        
        // Check bottom border (y = 0)
        for (int x = 0; x < width; x++)
        {
            int innerY = 1;
            if (innerY < height)
            {
                Vector3Int innerCellPos = new Vector3Int(x - _mapCenterXOffset, innerY - _mapCenterYOffset, 0);
                TileBase innerTile = tilemap.GetTile(innerCellPos);
                
                if (innerTile == highWallTile)
                {
                    Vector3Int borderCellPos = new Vector3Int(x - _mapCenterXOffset, -_mapCenterYOffset, 0);
                    tilemap.SetTile(borderCellPos, highWallTile);
                }
            }
        }
        
        // Check top border (y = height - 1)
        for (int x = 0; x < width; x++)
        {
            int innerY = height - 2;
            if (innerY >= 0)
            {
                Vector3Int innerCellPos = new Vector3Int(x - _mapCenterXOffset, innerY - _mapCenterYOffset, 0);
                TileBase innerTile = tilemap.GetTile(innerCellPos);
                
                if (innerTile == highWallTile)
                {
                    Vector3Int borderCellPos = new Vector3Int(x - _mapCenterXOffset, (height - 1) - _mapCenterYOffset, 0);
                    tilemap.SetTile(borderCellPos, highWallTile);
                }
            }
        }
    }

    private void PunchHoleWithThreshold(int centerX, int centerY, int radius)
    {
        // Define transition zone (70% inner = full ground, 30% outer = transition)
        float transitionStartRatio = 0.7f;
        float transitionEnd = radius;
        float transitionStart = radius * transitionStartRatio;
        
        // Punch circle with smooth transitions
        for (int x = centerX - radius; x <= centerX + radius; x++)
        {
            for (int y = centerY - radius; y <= centerY + radius; y++)
            {
                if (x < 1 || x >= width - 1 || y < 1 || y >= height - 1)
                    continue;
                
                float distance = Mathf.Sqrt((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY));
                
                if (distance <= radius)
                {
                    Vector3Int cellPos = new Vector3Int(x - _mapCenterXOffset, y - _mapCenterYOffset, 0);
                    
                    // Inner area: guaranteed ground
                    if (distance <= transitionStart)
                    {
                        tilemap.SetTile(cellPos, groundTile);
                    }
                    // Transition zone: blend between ground and surrounding terrain
                    else
                    {
                        // Get original tile type based on noise (before modifications)
                        TileBase originalTile = GetTileForPosition(x, y);
                        
                        // Create transition: closer to center = more ground, closer to edge = more original
                        float transitionFactor = (distance - transitionStart) / (transitionEnd - transitionStart);
                        
                        TileBase transitionTile = GetTileForSmoothTransition(x, y, transitionFactor, originalTile);
                        tilemap.SetTile(cellPos, transitionTile);
                    }
                }
            }
        }
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
    
    /// <summary>
    /// Checks if a tile is a terrain tile (wall, low wall, or high wall).
    /// Terrain tiles block building placement and unit movement.
    /// </summary>
    public bool IsTerrainTile(TileBase tile)
    {
        if (tile == null) return false;
        return tile == wallTile || tile == lowWallTile || tile == highWallTile;
    }
    
    /// <summary>
    /// Checks if a cell position contains terrain (wall, low wall, or high wall).
    /// </summary>
    public bool IsTerrainCell(Vector3Int cellPosition)
    {
        if (tilemap == null) return false;
        TileBase tile = tilemap.GetTile(cellPosition);
        return IsTerrainTile(tile);
    }
    
    private void CopyTilesToBuildingManagerGroundTilemap(BoundsInt mapBounds)
    {
        if (BuildingManager.Instance == null)
        {
            Debug.LogWarning("[MapGenerator] BuildingManager.Instance is null. Cannot copy tiles to groundTilemap.");
            return;
        }
        
        Tilemap groundTilemap = BuildingManager.Instance.GroundTilemap;
        if (groundTilemap == null)
        {
            Debug.LogWarning("[MapGenerator] BuildingManager.GroundTilemap is null. Cannot copy tiles to groundTilemap.");
            return;
        }
        
        // Copy all tiles from MapGenerator's tilemap to BuildingManager's groundTilemap
        // This ensures buildings can be placed and fog of war works correctly
        // Read tiles from tilemap after PolishMap() has modified them
        TileBase[] tiles = new TileBase[mapBounds.size.x * mapBounds.size.y];
        int index = 0;
        foreach (Vector3Int pos in mapBounds.allPositionsWithin)
        {
            tiles[index++] = tilemap.GetTile(pos);
        }
        
        groundTilemap.SetTilesBlock(mapBounds, tiles);
        groundTilemap.CompressBounds();
    }
}