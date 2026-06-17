using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ResourceUnlockManager : MonoBehaviour
{
    private static ResourceUnlockManager _instance;
    public static ResourceUnlockManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<ResourceUnlockManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("ResourceUnlockManager");
                    _instance = go.AddComponent<ResourceUnlockManager>();
                }
            }
            return _instance;
        }
    }

    public static bool HasInstance => _instance != null;

    private readonly HashSet<ResourceType> _unlockedResources = new HashSet<ResourceType>();

    public event Action<ResourceType> OnResourceUnlocked;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        ResetToDefaults();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "GameScene" || scene.name == "TutorialScene")
        {
            ResetToDefaults();
        }
    }

    private void ResetToDefaults()
    {
        _unlockedResources.Clear();
        _unlockedResources.Add(ResourceType.Ferrite);
        _unlockedResources.Add(ResourceType.Aether);
        _unlockedResources.Add(ResourceType.Biomass);
        _unlockedResources.Add(ResourceType.CryoCrystal);
    }

    public bool IsResourceUnlocked(ResourceType resourceType)
    {
        return _unlockedResources.Contains(resourceType);
    }

    public void UnlockResource(ResourceType resourceType)
    {
        if (_unlockedResources.Contains(resourceType))
        {
            return;
        }

        _unlockedResources.Add(resourceType);
        OnResourceUnlocked?.Invoke(resourceType);
    }
}
