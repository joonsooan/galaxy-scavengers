using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UnitUnlockManager : MonoBehaviour
{
    private static bool _isQuitting = false;
    private static UnitUnlockManager _instance;
    public static UnitUnlockManager Instance
    {
        get
        {
            if (_isQuitting) return null;

            if (_instance == null)
            {
                _instance = FindFirstObjectByType<UnitUnlockManager>();
                if (_instance == null && !_isQuitting)
                {
                    GameObject go = new GameObject("UnitUnlockManager");
                    _instance = go.AddComponent<UnitUnlockManager>();
                }
            }
            return _instance;
        }
    }

    public static bool HasInstance => !_isQuitting && _instance != null;

    private readonly HashSet<UnitData> _unlockedUnits = new HashSet<UnitData>();

    public event Action<UnitData> OnUnitUnlocked;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
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

    private void OnApplicationQuit()
    {
        _isQuitting = true;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "GameScene" || scene.name == "TutorialScene")
        {
            _unlockedUnits.Clear();
        }
    }

    public bool IsUnitUnlocked(UnitData unit)
    {
        if (unit == null)
        {
            return false;
        }

        return _unlockedUnits.Contains(unit);
    }

    public void UnlockUnit(UnitData unit)
    {
        if (unit == null)
        {
            return;
        }

        if (_unlockedUnits.Contains(unit))
        {
            return;
        }

        _unlockedUnits.Add(unit);
        OnUnitUnlocked?.Invoke(unit);
    }
}
