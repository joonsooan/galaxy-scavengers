using System.Collections.Generic;
using UnityEngine;

public class ObjectPooler : MonoBehaviour
{
    [System.Serializable]
    public class Pool
    {
        public string tag;
        public int size;
        public GameObject prefab;
        public Transform parent;
    }

    public static ObjectPooler Instance;

    [Header("Pool Settings")]
    [SerializeField] private int defaultPoolSize = 10;
    public List<Pool> pools;
    private Dictionary<string, Queue<GameObject>> _poolDictionary;
    private bool _isInitialized;

    private void Awake()
    {
        Instance = this;
    }

    public void InitializePools()
    {
        if (_isInitialized)
        {
            return;
        }

        if (_poolDictionary == null)
        {
            _poolDictionary = new Dictionary<string, Queue<GameObject>>();
        }

        RegisterEnemyPoolsFromSpawners();

        foreach (Pool pool in pools)
        {
            if (pool == null || pool.prefab == null || string.IsNullOrEmpty(pool.tag))
            {
                continue;
            }

            if (_poolDictionary.ContainsKey(pool.tag))
            {
                continue;
            }

            Queue<GameObject> objectPool = new Queue<GameObject>();

            Transform poolParent = pool.parent;

            for (int i = 0; i < pool.size; i++)
            {
                GameObject obj = Instantiate(pool.prefab, poolParent);
                obj.SetActive(false);
                objectPool.Enqueue(obj);
            }

            _poolDictionary.Add(pool.tag, objectPool);
        }

        _isInitialized = true;
    }

    private void RegisterEnemyPoolsFromSpawners()
    {
        EnemySpawner[] spawners = FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None);
        if (spawners == null || spawners.Length == 0)
        {
            return;
        }
        
        HashSet<string> registeredTags = new HashSet<string>();
        Dictionary<string, GameObject> prefabMap = new Dictionary<string, GameObject>();
        Dictionary<string, int> spawnCounts = new Dictionary<string, int>();
        
        foreach (EnemySpawner spawner in spawners)
        {
            if (spawner == null)
            {
                continue;
            }
            
            List<EnemySpawnData> enemyPrefabs = spawner.GetEnemyPrefabs();
            if (enemyPrefabs == null || enemyPrefabs.Count == 0)
            {
                continue;
            }
            
            int maxEnemiesPerHole = spawner.GetMaxEnemiesPerHole();
            
            foreach (EnemySpawnData enemyData in enemyPrefabs)
            {
                if (enemyData == null || enemyData.enemyPrefab == null)
                {
                    continue;
                }
                
                string poolTag = EnemySpawner.GetPoolTagFromPrefab(enemyData.enemyPrefab);
                if (string.IsNullOrEmpty(poolTag))
                {
                    continue;
                }
                
                if (!registeredTags.Contains(poolTag))
                {
                    registeredTags.Add(poolTag);
                    prefabMap[poolTag] = enemyData.enemyPrefab;
                }
                
                if (!spawnCounts.ContainsKey(poolTag))
                {
                    spawnCounts[poolTag] = maxEnemiesPerHole * 10;
                }
                else
                {
                    spawnCounts[poolTag] = Mathf.Max(spawnCounts[poolTag], maxEnemiesPerHole * 10);
                }
            }
        }
        
        foreach (var kvp in prefabMap)
        {
            string poolTag = kvp.Key;
            GameObject prefab = kvp.Value;
            int poolSize = Mathf.Max(defaultPoolSize, spawnCounts.GetValueOrDefault(poolTag, defaultPoolSize));
            
            Transform poolParent = GetOrCreatePoolParent(poolTag);
            Queue<GameObject> objectPool = new Queue<GameObject>();
            
            for (int i = 0; i < poolSize; i++)
            {
                GameObject obj = Instantiate(prefab, poolParent);
                obj.SetActive(false);
                objectPool.Enqueue(obj);
            }
            
            _poolDictionary[poolTag] = objectPool;
        }
    }
    
    private Transform GetOrCreatePoolParent(string poolTag)
    {
        Transform poolParent = transform.Find(poolTag);
        if (poolParent == null)
        {
            GameObject parentObj = new GameObject(poolTag);
            parentObj.transform.SetParent(transform);
            parentObj.SetActive(true);
            poolParent = parentObj.transform;
        }
        return poolParent;
    }

    public GameObject SpawnFromPool(string objTag, Vector3 position, Quaternion rotation)
    {
        if (!_poolDictionary.ContainsKey(objTag))
        {
            Debug.LogWarning("Pool with tag " + objTag + " doesn't exist.");
            return null;
        }

        GameObject objectToSpawn = _poolDictionary[objTag].Dequeue();

        // Ensure parent is active so spawned objects are visible
        if (objectToSpawn.transform.parent != null && !objectToSpawn.transform.parent.gameObject.activeSelf)
        {
            objectToSpawn.transform.parent.gameObject.SetActive(true);
        }

        objectToSpawn.SetActive(true);
        objectToSpawn.transform.position = position;
        objectToSpawn.transform.rotation = rotation;

        _poolDictionary[objTag].Enqueue(objectToSpawn);
        
        if (ModuleEffectManager.Instance != null)
        {
            ModuleEffectManager.Instance.OnObjectCreated(objectToSpawn);
        }

        return objectToSpawn;
    }
}
