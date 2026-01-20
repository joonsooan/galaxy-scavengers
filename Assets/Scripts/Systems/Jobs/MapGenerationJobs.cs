using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using UnityEngine;

namespace Systems.Jobs
{
    [BurstCompile]
    public struct GenerateNoiseMapJob : IJobParallelFor
    {
        [WriteOnly]
        public NativeArray<float> noiseMap;
        
        public int width;
        public int height;
        public float noiseScale;
        public float offsetX;
        public float offsetY;

        public void Execute(int index)
        {
            int x = index % width;
            int y = index / width;
            
            float sampleX = (x / noiseScale) + offsetX;
            float sampleY = (y / noiseScale) + offsetY;

            float perlinValue = Mathf.PerlinNoise(sampleX, sampleY);
            
            noiseMap[index] = perlinValue;
        }
    }

    [BurstCompile]
    public struct GenerateGradientMapJob : IJobParallelFor
    {
        [WriteOnly]
        public NativeArray<float> gradientMap;
        
        public int width;
        public int height;
        public Vector2 gradientCenter;
        public float falloffExponent;

        public void Execute(int index)
        {
            int x = index % width;
            int y = index / height;
            
            float normalizedX = (float)x / width;
            float normalizedY = (float)y / height;

            float dx = normalizedX - gradientCenter.x;
            float dy = normalizedY - gradientCenter.y;
            float distance = Mathf.Sqrt(dx * dx + dy * dy);

            float maxDistance = Mathf.Sqrt(0.5f * 0.5f + 0.5f * 0.5f);
            float normalizedDistance = Mathf.Clamp01(distance / maxDistance);

            float gradientValue = Mathf.Pow(normalizedDistance, falloffExponent);
            
            gradientMap[index] = 1f - gradientValue;
        }
    }

    [BurstCompile]
    public struct CalculateTerrainTilesJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<float> noiseMap;
        
        [ReadOnly]
        public NativeArray<float> gradientMap;
        
        [WriteOnly]
        public NativeArray<byte> tileTypes; // 0: ground, 1: lowWall, 2: highWall, 3: borderWall
        
        public int width;
        public int height;
        public float lowWallThreshold;
        public float highWallThreshold;
        public bool useGradientMap;
        public float gradientStrength;

        public void Execute(int index)
        {
            int x = index % width;
            int y = index / width;
            
            bool isBorder = x == 0 || x == width - 1 || y == 0 || y == height - 1;
            if (isBorder)
            {
                tileTypes[index] = 3; // borderWall
                return;
            }
            
            float noiseVal = noiseMap[index];
            
            if (useGradientMap && gradientMap.Length > 0)
            {
                float gradientValue = gradientMap[index];
                float combinedValue = noiseVal + (gradientValue - 0.5f) * gradientStrength;
                noiseVal = Mathf.Clamp01(combinedValue);
            }
            
            noiseVal = 1f - noiseVal;
            
            if (noiseVal >= highWallThreshold)
            {
                tileTypes[index] = 2; // highWall
            }
            else if (noiseVal >= lowWallThreshold)
            {
                tileTypes[index] = 1; // lowWall
            }
            else
            {
                tileTypes[index] = 0; // ground
            }
        }
    }

    [BurstCompile]
    public struct CollectCenterCircleTilesJob : IJob
    {
        [ReadOnly]
        public NativeArray<byte> tileTypes;
        
        [WriteOnly]
        public NativeList<int> affectedIndices;
        
        public int width;
        public int height;
        public int centerX;
        public int centerY;
        public int centerCircleRadius;
        public float transitionStartRatio;

        public void Execute()
        {
            float transitionEnd = centerCircleRadius;
            float transitionStart = centerCircleRadius * transitionStartRatio;
            
            for (int x = 1; x < width - 1; x++)
            {
                for (int y = 1; y < height - 1; y++)
                {
                    float distance = Mathf.Sqrt((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY));
                    
                    if (distance <= centerCircleRadius)
                    {
                        int index = x + y * width;
                        affectedIndices.Add(index);
                    }
                }
            }
        }
    }

    [BurstCompile]
    public struct FloodFillConnectivityJob : IJob
    {
        [ReadOnly]
        public NativeArray<byte> tileTypes;
        
        [WriteOnly]
        public NativeArray<bool> connectedToCenter;
        
        public int width;
        public int height;
        public int centerX;
        public int centerY;

        public void Execute()
        {
            NativeQueue<int> queue = new NativeQueue<int>(Allocator.Temp);
            
            int centerIndex = centerX + centerY * width;
            byte centerTileType = tileTypes[centerIndex];
            
            // IsTerrainTile: type 1, 2, or 3
            bool isTerrain = centerTileType >= 1 && centerTileType <= 3;
            
            if (!isTerrain)
            {
                queue.Enqueue(centerIndex);
                connectedToCenter[centerIndex] = true;
            }
            
            int[] dx = { 1, -1, 0, 0 };
            int[] dy = { 0, 0, 1, -1 };
            
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                int x = current % width;
                int y = current / width;
                
                for (int i = 0; i < 4; i++)
                {
                    int nx = x + dx[i];
                    int ny = y + dy[i];
                    
                    if (nx < 1 || nx >= width - 1 || ny < 1 || ny >= height - 1)
                        continue;
                    
                    int neighborIndex = nx + ny * width;
                    
                    if (connectedToCenter[neighborIndex])
                        continue;
                    
                    byte neighborTileType = tileTypes[neighborIndex];
                    bool isNeighborTerrain = neighborTileType >= 1 && neighborTileType <= 3;
                    
                    if (!isNeighborTerrain)
                    {
                        connectedToCenter[neighborIndex] = true;
                        queue.Enqueue(neighborIndex);
                    }
                }
            }
            
            queue.Dispose();
        }
    }

    [BurstCompile]
    public struct FindDisconnectedTilesJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<bool> connectedToCenter;
        
        [ReadOnly]
        public NativeArray<byte> tileTypes;
        
        [WriteOnly]
        public NativeArray<bool> shouldFill;
        
        public int width;

        public void Execute(int index)
        {
            int x = index % width;
            int y = index / width;
            
            if (x < 1 || x >= width - 1 || y < 1 || y >= (connectedToCenter.Length / width) - 1)
                return;
            
            if (!connectedToCenter[index])
            {
                byte tileType = tileTypes[index];
                bool isTerrain = tileType >= 1 && tileType <= 3;
                
                if (!isTerrain)
                {
                    shouldFill[index] = true;
                }
            }
        }
    }
}
