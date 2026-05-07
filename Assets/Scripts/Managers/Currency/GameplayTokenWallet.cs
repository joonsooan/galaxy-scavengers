using System;
using UnityEngine;

public class GameplayTokenWallet : MonoBehaviour
{
    public static GameplayTokenWallet Instance { get; private set; }

    public static event Action OnBalanceChanged;

    private int _balance;

    public int Balance => _balance;

    public static void EnsureExists(MonoBehaviour hostIfNoGameManager)
    {
        if (Instance != null)
        {
            return;
        }

        GameplayTokenWallet existing = FindFirstObjectByType<GameplayTokenWallet>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            return;
        }

        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.GetComponent<GameplayTokenWallet>() == null)
            {
                GameManager.Instance.gameObject.AddComponent<GameplayTokenWallet>();
            }

            return;
        }

        if (hostIfNoGameManager != null && hostIfNoGameManager.GetComponent<GameplayTokenWallet>() == null)
        {
            hostIfNoGameManager.gameObject.AddComponent<GameplayTokenWallet>();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void Add(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        _balance += amount;
        OnBalanceChanged?.Invoke();
    }

    public bool TrySpend(int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        if (_balance < amount)
        {
            return false;
        }

        _balance -= amount;
        OnBalanceChanged?.Invoke();
        return true;
    }

    public bool CanAfford(int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        return _balance >= amount;
    }
}
