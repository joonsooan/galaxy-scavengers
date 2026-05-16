using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;
using System;

namespace Systems.Jobs
{
    public struct ResourceCircleData
    {
        public float2 center;
        public float radius;
        public byte resourceType;
    }

    [BurstCompile]
    public struct CalculateValidSpawnPositionsJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<ResourceCircleData> circles;
        
        [ReadOnly]
        public NativeArray<byte> terrainTileTypes; // 0: ground, 1+: terrain
        
        [WriteOnly]
        public NativeList<ValidSpawnPosition>.ParallelWriter validPositions;
        
        public int width;
        public int height;
        public int mapCenterXOffset;
        public int mapCenterYOffset;
        public float circleRadius;

        public void Execute(int index)
        {
            ResourceCircleData circle = circles[index];
            
            int radiusInt = (int)math.ceil(circle.radius);
            int minX = (int)math.floor(circle.center.x - radiusInt);
            int maxX = (int)math.ceil(circle.center.x + radiusInt);
            int minY = (int)math.floor(circle.center.y - radiusInt);
            int maxY = (int)math.ceil(circle.center.y + radiusInt);
            
            Vector2Int mapSize = new Vector2Int(width, height);
            int mapMinX = -mapSize.x / 2;
            int mapMaxX = mapSize.x / 2 - 1;
            int mapMinY = -mapSize.y / 2;
            int mapMaxY = mapSize.y / 2 - 1;
            
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    float distanceFromCircleCenter = math.distance(new float2(x, y), circle.center);
                    if (distanceFromCircleCenter > circle.radius)
                        continue;
                    
                    if (x < mapMinX || x > mapMaxX || y < mapMinY || y > mapMaxY)
                        continue;
                    
                    // Convert to map tilemap coordinates
                    int tileX = x + mapCenterXOffset;
                    int tileY = y + mapCenterYOffset;
                    
                    if (tileX < 0 || tileX >= width || tileY < 0 || tileY >= height)
                        continue;
                    
                    int tileIndex = tileX + tileY * width;
                    if (tileIndex >= terrainTileTypes.Length)
                        continue;
                    
                    byte tileType = terrainTileTypes[tileIndex];
                    
                    // Only spawn on ground tiles (type 0)
                    if (tileType == 0)
                    {
                        var pos = new ValidSpawnPosition
                        {
                            cellX = x,
                            cellY = y,
                            circleIndex = index,
                            resourceType = circle.resourceType
                        };
                        validPositions.AddNoResize(pos);
                    }
                }
            }
        }
    }

    public struct ValidSpawnPosition : IEquatable<ValidSpawnPosition>
    {
        public int cellX;
        public int cellY;
        public int circleIndex;
        public byte resourceType;

        public bool Equals(ValidSpawnPosition other)
        {
            return cellX == other.cellX && cellY == other.cellY;
        }

        public override int GetHashCode()
        {
            return cellX * 10000 + cellY;
        }
    }
}
