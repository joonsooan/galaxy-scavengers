using System;
using UnityEngine;

public class CreditManager : MonoBehaviour
{
    public static CreditManager Instance { get; private set; }
    
    private int _currentCredits;
    
    public event Action<int> OnCreditsChanged;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    private void Start()
    {
        if (PlayerPrefs.HasKey("CurrentCredits"))
        {
            _currentCredits = PlayerPrefs.GetInt("CurrentCredits");
            OnCreditsChanged?.Invoke(_currentCredits);
        }
        else
        {
            _currentCredits = 0;
        }
    }
    
    public void AddCredits(int amount)
    {
        if (amount <= 0) return;
        
        _currentCredits += amount;
        SaveCredits();
        OnCreditsChanged?.Invoke(_currentCredits);
        
        Debug.Log($"CreditManager: Added {amount} credits. Total: {_currentCredits}");
    }
    
    public bool SpendCredits(int amount)
    {
        if (amount <= 0) return true;
        
        if (_currentCredits < amount)
        {
            Debug.LogWarning($"CreditManager: Not enough credits. Required: {amount}, Available: {_currentCredits}");
            return false;
        }
        
        _currentCredits -= amount;
        SaveCredits();
        OnCreditsChanged?.Invoke(_currentCredits);
        
        Debug.Log($"CreditManager: Spent {amount} credits. Remaining: {_currentCredits}");
        return true;
    }
    
    public int GetCredits()
    {
        return _currentCredits;
    }
    
    public void ResetCredits()
    {
        _currentCredits = 0;
        SaveCredits();
        OnCreditsChanged?.Invoke(_currentCredits);
        Debug.Log("CreditManager: Credits reset to 0");
    }
    
    private void SaveCredits()
    {
        PlayerPrefs.SetInt("CurrentCredits", _currentCredits);
        PlayerPrefs.Save();
    }
}
