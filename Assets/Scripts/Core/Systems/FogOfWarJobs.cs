using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace Systems.Jobs
{
    [BurstCompile]
    public struct CollectFogTilePositionsJob : IJobParallelForBatch
    {
        [ReadOnly]
        public NativeArray<byte> terrainTileTypes; // 0: ground, 1+: terrain/wall
        
        [WriteOnly]
        public NativeList<Vector3Int>.ParallelWriter fogTilePositions;
        
        public int width;
        public int height;
        public int mapCenterXOffset;
        public int mapCenterYOffset;

        public void Execute(int startIndex, int count)
        {
            for (int i = startIndex; i < startIndex + count && i < terrainTileTypes.Length; i++)
            {
                byte tileType = terrainTileTypes[i];
                
                // Only collect positions that have tiles (ground or terrain)
                if (tileType != 255) // 255 is unused/invalid
                {
                    int x = i % width;
                    int y = i / width;
                    
                    // Convert to world cell coordinates
                    int cellX = x - mapCenterXOffset;
                    int cellY = y - mapCenterYOffset;
                    
                    fogTilePositions.AddNoResize(new Vector3Int(cellX, cellY, 0));
                }
            }
        }
    }

    public struct FogTilePosition
    {
        public int x;
        public int y;
        
        public FogTilePosition(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
        
        public override int GetHashCode()
        {
            return x * 10000 + y;
        }
        
        public bool Equals(FogTilePosition other)
        {
            return x == other.x && y == other.y;
        }
    }

    [BurstCompile]
    public struct PrepareFogTilePlacementJob : IJob
    {
        [ReadOnly]
        public NativeList<Vector3Int> fogTilePositions;
        
        [WriteOnly]
        public NativeArray<int> minX;
        
        [WriteOnly]
        public NativeArray<int> minY;
        
        [WriteOnly]
        public NativeArray<int> maxX;
        
        [WriteOnly]
        public NativeArray<int> maxY;

        public void Execute()
        {
            if (fogTilePositions.Length == 0)
            {
                minX[0] = int.MaxValue;
                minY[0] = int.MaxValue;
                maxX[0] = int.MinValue;
                maxY[0] = int.MinValue;
                return;
            }
            
            int minXVal = int.MaxValue;
            int minYVal = int.MaxValue;
            int maxXVal = int.MinValue;
            int maxYVal = int.MinValue;
            
            for (int i = 0; i < fogTilePositions.Length; i++)
            {
                Vector3Int pos = fogTilePositions[i];
                minXVal = math.min(minXVal, pos.x);
                minYVal = math.min(minYVal, pos.y);
                maxXVal = math.max(maxXVal, pos.x);
                maxYVal = math.max(maxYVal, pos.y);
            }
            
            minX[0] = minXVal;
            minY[0] = minYVal;
            maxX[0] = maxXVal;
            maxY[0] = maxYVal;
        }
    }
}
