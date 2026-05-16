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
    private Dictionary<string, GameObject> _prefabMap;
    private Dictionary<string, Transform> _poolParentCache;
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
        if (_prefabMap == null)
        {
            _prefabMap = new Dictionary<string, GameObject>();
        }
        if (_poolParentCache == null)
        {
            _poolParentCache = new Dictionary<string, Transform>();
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
            _prefabMap[poolTag] = prefab;
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
        if (_poolParentCache != null && _poolParentCache.TryGetValue(poolTag, out Transform cached) && cached != null)
            return cached;
        Transform poolParent = transform.Find(poolTag);
        if (poolParent == null)
        {
            GameObject parentObj = new GameObject(poolTag);
            parentObj.transform.SetParent(transform);
            parentObj.SetActive(true);
            poolParent = parentObj.transform;
        }
        if (_poolParentCache != null)
            _poolParentCache[poolTag] = poolParent;
        return poolParent;
    }

    public GameObject SpawnFromPool(string objTag, Vector3 position, Quaternion rotation)
    {
        if (!_poolDictionary.ContainsKey(objTag))
        {
            Debug.LogWarning("Pool with tag " + objTag + " doesn't exist.");
            return null;
        }

        Queue<GameObject> pool = _poolDictionary[objTag];
        GameObject objectToSpawn = null;
        int count = pool.Count;
        for (int i = 0; i < count; i++)
        {
            GameObject candidate = pool.Dequeue();
            pool.Enqueue(candidate);
            if (!candidate.activeSelf)
            {
                objectToSpawn = candidate;
                break;
            }
        }

        if (objectToSpawn == null)
        {
            GameObject prefab = null;
            if (_prefabMap != null && _prefabMap.TryGetValue(objTag, out prefab) && prefab != null)
            {
                Transform poolParent = GetOrCreatePoolParent(objTag);
                objectToSpawn = Instantiate(prefab, poolParent);
                pool.Enqueue(objectToSpawn);
            }
            else
            {
                Pool poolConfig = pools?.Find(p => p != null && p.tag == objTag);
                if (poolConfig != null && poolConfig.prefab != null)
                {
                    Transform poolParent = GetOrCreatePoolParent(objTag);
                    objectToSpawn = Instantiate(poolConfig.prefab, poolParent);
                    pool.Enqueue(objectToSpawn);
                }
                else
                {
                    objectToSpawn = pool.Dequeue();
                    pool.Enqueue(objectToSpawn);
                }
            }
        }

        if (objectToSpawn.transform.parent != null && !objectToSpawn.transform.parent.gameObject.activeSelf)
        {
            objectToSpawn.transform.parent.gameObject.SetActive(true);
        }

        objectToSpawn.transform.position = position;
        objectToSpawn.transform.rotation = rotation;
        Rigidbody2D rb = objectToSpawn.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.position = position;
            rb.linearVelocity = Vector2.zero;
        }
        objectToSpawn.SetActive(true);

        if (ModuleEffectManager.Instance != null)
        {
            ModuleEffectManager.Instance.OnObjectCreated(objectToSpawn);
        }

        return objectToSpawn;
    }

    public bool HasUIPool(string tag)
    {
        return _poolDictionary != null && !string.IsNullOrEmpty(tag) && _poolDictionary.ContainsKey(tag);
    }

    public void EnsureUIPool(string tag, GameObject prefab, int count)
    {
        if (prefab == null || string.IsNullOrEmpty(tag) || count <= 0) {
            return;
        }

        if (_poolDictionary == null) {
            _poolDictionary = new Dictionary<string, Queue<GameObject>>();
        }

        if (_prefabMap == null) {
            _prefabMap = new Dictionary<string, GameObject>();
        }

        if (_poolParentCache == null) {
            _poolParentCache = new Dictionary<string, Transform>();
        }

        if (_poolDictionary.ContainsKey(tag)) {
            return;
        }

        string parentName = "UIPool_" + tag;
        Transform poolParent = GetOrCreatePoolParent(parentName);
        Queue<GameObject> q = new Queue<GameObject>();
        for (int i = 0; i < count; i++) {
            GameObject obj = Instantiate(prefab, poolParent);
            obj.SetActive(false);
            q.Enqueue(obj);
        }

        _poolDictionary[tag] = q;
        _prefabMap[tag] = prefab;
    }

    public GameObject SpawnUIPooled(string tag, Transform parent)
    {
        if (_poolDictionary == null || string.IsNullOrEmpty(tag) || !_poolDictionary.ContainsKey(tag) || parent == null) {
            return null;
        }

        Queue<GameObject> pool = _poolDictionary[tag];
        GameObject objectToSpawn = null;
        int n = pool.Count;
        for (int i = 0; i < n; i++) {
            GameObject candidate = pool.Dequeue();
            pool.Enqueue(candidate);
            if (!candidate.activeSelf) {
                objectToSpawn = candidate;
                break;
            }
        }

        if (objectToSpawn == null) {
            GameObject prefab = null;
            if (_prefabMap != null && _prefabMap.TryGetValue(tag, out prefab) && prefab != null) {
                Transform poolParent = GetOrCreatePoolParent("UIPool_" + tag);
                objectToSpawn = Instantiate(prefab, poolParent);
                pool.Enqueue(objectToSpawn);
            }
        }

        if (objectToSpawn == null) {
            return null;
        }

        objectToSpawn.transform.SetParent(parent, false);
        RectTransform rt = objectToSpawn.GetComponent<RectTransform>();
        if (rt != null) {
            rt.localPosition = Vector3.zero;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
        }

        objectToSpawn.SetActive(true);

        if (ModuleEffectManager.Instance != null) {
            ModuleEffectManager.Instance.OnObjectCreated(objectToSpawn);
        }

        return objectToSpawn;
    }

    public void ReturnUIPooled(string tag, GameObject obj)
    {
        if (obj == null || _poolDictionary == null || string.IsNullOrEmpty(tag) || !_poolDictionary.ContainsKey(tag)) {
            return;
        }

        obj.SetActive(false);
        Transform poolParent = GetOrCreatePoolParent("UIPool_" + tag);
        obj.transform.SetParent(poolParent, false);
    }
}
