using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class ResourceCircleGenerator
{
    private readonly int _numberOfCircles;
    private readonly int _borderPadding;
    private readonly float _minCircleRadius;
    private readonly float _maxCircleRadius;
    private readonly float _circleSpawnPercentage;
    private readonly int _maxCircles;
    private readonly int _starterRingOuterRadius;
    private readonly List<ResourceSpawnSettings> _resourceSettings;
    
    private Vector2Int _mapCenter;
    private int _mapWidth;
    private int _mapHeight;
    private float _maxMapRadius;
    private float _radiusStep;
    private readonly List<float> _divisionRadii;
    
    public ResourceCircleGenerator(
        int numberOfCircles,
        int borderPadding,
        float minCircleRadius,
        float maxCircleRadius,
        float circleSpawnPercentage,
        int maxCircles,
        int starterRingOuterRadius,
        List<ResourceSpawnSettings> resourceSettings)
    {
        _numberOfCircles = numberOfCircles;
        _borderPadding = borderPadding;
        _minCircleRadius = minCircleRadius;
        _maxCircleRadius = maxCircleRadius;
        _circleSpawnPercentage = circleSpawnPercentage;
        _maxCircles = maxCircles;
        _starterRingOuterRadius = starterRingOuterRadius;
        _resourceSettings = resourceSettings;
        
        _divisionRadii = new List<float>();
    }
    
    public void Initialize(int mapWidth, int mapHeight, Vector2Int mapCenter)
    {
        _mapWidth = mapWidth;
        _mapHeight = mapHeight;
        _mapCenter = mapCenter;
        
        float halfWidth = (_mapWidth / 2f) - _borderPadding;
        float halfHeight = (_mapHeight / 2f) - _borderPadding;
        _maxMapRadius = Mathf.Min(halfWidth, halfHeight);
        
        DivideMapIntoConcentricCircles();
    }
    
    public void DivideMapIntoConcentricCircles()
    {
        _divisionRadii.Clear();
        
        _radiusStep = _maxMapRadius / _numberOfCircles;
        
        for (int i = 0; i <= _numberOfCircles; i++)
        {
            float radius = i * _radiusStep;
            _divisionRadii.Add(radius);
        }
    }
    
    public List<ResourceCircle> GenerateResourceCircles()
    {
        List<ResourceCircle> circles = new List<ResourceCircle>();
        
        float mapArea = (_mapWidth - _borderPadding * 2) * (_mapHeight - _borderPadding * 2);
        float avgCircleArea = Mathf.PI * ((_minCircleRadius + _maxCircleRadius) / 2f) * ((_minCircleRadius + _maxCircleRadius) / 2f);
        int maxPossibleCircles = Mathf.RoundToInt(mapArea / avgCircleArea);
        int circlesToSpawn = Mathf.RoundToInt(maxPossibleCircles * (_circleSpawnPercentage / 100f));
        
        circlesToSpawn = Mathf.Min(circlesToSpawn, _maxCircles);
        circlesToSpawn = Mathf.Max(circlesToSpawn, 10);
        
        int attempts = 0;
        int maxAttempts = circlesToSpawn * 30;
        
        while (circles.Count < circlesToSpawn && attempts < maxAttempts)
        {
            attempts++;
            
            Vector2Int center = GenerateCircleCenter();
            float radius = Random.Range(_minCircleRadius, _maxCircleRadius);
            
            if (ValidateCirclePosition(center, radius, circles))
            {
                ResourceType? selectedType = SelectResourceTypeForCircle(center);
                if (selectedType.HasValue)
                {
                    circles.Add(new ResourceCircle
                    {
                        center = center,
                        radius = radius,
                        resourceType = selectedType.Value
                    });
                }
            }
        }
        
        return circles;
    }
    
    public List<float> GetDivisionRadii() => _divisionRadii;
    
    private Vector2Int GenerateCircleCenter()
    {
        int centerX = Random.Range(-(_mapWidth / 2) + _borderPadding, (_mapWidth / 2) - _borderPadding);
        int centerY = Random.Range(-(_mapHeight / 2) + _borderPadding, (_mapHeight / 2) - _borderPadding);
        Vector2Int center = new Vector2Int(centerX, centerY);
        
        float distanceFromCenter = Vector2.Distance(center, _mapCenter);
        float minRequiredDistance = _starterRingOuterRadius + _maxCircleRadius;
        if (distanceFromCenter < minRequiredDistance)
        {
            if (distanceFromCenter > 0)
            {
                float scale = minRequiredDistance / distanceFromCenter;
                center = new Vector2Int(
                    Mathf.RoundToInt(center.x * scale),
                    Mathf.RoundToInt(center.y * scale)
                );
            }
            else
            {
                center = new Vector2Int(Mathf.CeilToInt(minRequiredDistance), 0);
            }
            
            if (center.x < -(_mapWidth / 2) + _borderPadding || center.x >= (_mapWidth / 2) - _borderPadding ||
                center.y < -(_mapHeight / 2) + _borderPadding || center.y >= (_mapHeight / 2) - _borderPadding)
            {
                return Vector2Int.zero;
            }
        }
        
        return center;
    }
    
    private bool ValidateCirclePosition(Vector2Int center, float radius, List<ResourceCircle> existingCircles, float minDistanceMultiplier = 0.5f)
    {
        if (BuildingManager.Instance == null) return false;
        
        if (CircleOverlapsTerrain(center, radius))
        {
            return false;
        }
        
        foreach (var existingCircle in existingCircles)
        {
            float distance = Vector2.Distance(center, existingCircle.center);
            float minDistance = (radius + existingCircle.radius) * minDistanceMultiplier;
            if (distance < minDistance)
            {
                return false;
            }
        }
        
        return true;
    }
    
    private bool CircleOverlapsTerrain(Vector2Int center, float radius)
    {
        if (BuildingManager.Instance == null) return false;
        
        int samplePoints = Mathf.Min(Mathf.CeilToInt(radius * 2f), 16);
        
        for (int i = 0; i < samplePoints; i++)
        {
            float angle = (i / (float)samplePoints) * Mathf.PI * 2f;
            int x = Mathf.RoundToInt(center.x + Mathf.Cos(angle) * radius);
            int y = Mathf.RoundToInt(center.y + Mathf.Sin(angle) * radius);
            
            Vector3Int cellPos = new Vector3Int(x, y, 0);
            
            if (BuildingManager.Instance.IsTerrainCell(cellPos))
            {
                return true;
            }
        }
        
        Vector3Int centerCell = new Vector3Int(center.x, center.y, 0);
        if (BuildingManager.Instance.IsTerrainCell(centerCell))
        {
            return true;
        }
        
        for (int i = 0; i < 4; i++)
        {
            float angle = (i / 4f) * Mathf.PI * 2f;
            int x = Mathf.RoundToInt(center.x + Mathf.Cos(angle) * radius * 0.5f);
            int y = Mathf.RoundToInt(center.y + Mathf.Sin(angle) * radius * 0.5f);
            Vector3Int cellPos = new Vector3Int(x, y, 0);
            if (BuildingManager.Instance.IsTerrainCell(cellPos))
            {
                return true;
            }
        }
        
        return false;
    }
    
    private ResourceType? SelectResourceTypeForCircle(Vector2Int center)
    {
        float distanceFromCenter = Vector2.Distance(center, _mapCenter);
        
        int sectorIndex = 0;
        for (int i = 0; i < _divisionRadii.Count - 1; i++)
        {
            if (distanceFromCenter >= _divisionRadii[i] && distanceFromCenter < _divisionRadii[i + 1])
            {
                sectorIndex = i;
                break;
            }
        }
        
        List<ResourceSpawnSettings> validSettings = new List<ResourceSpawnSettings>();
        foreach (var settings in _resourceSettings)
        {
            if (settings.resourcePrefab == null || settings.resourceRuleTile == null) continue;
            if (Random.Range(0f, 100f) <= settings.spawnFrequencyPercent)
            {
                validSettings.Add(settings);
            }
        }
        
        if (validSettings.Count == 0) return null;
        
        ResourceSpawnSettings selected = validSettings[Random.Range(0, validSettings.Count)];
        return selected.resourceType;
    }
}
