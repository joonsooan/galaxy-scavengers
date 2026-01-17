using UnityEngine;
using UnityEngine.UI;

public class QuestTester : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button resetQuestProgressButton;
    
    [Header("Test Settings")]
    [Tooltip("If true, all active quests will be completable regardless of requirements")]
    [SerializeField] private bool questTestMode = false;
    
    private void Awake()
    {
        if (resetQuestProgressButton != null)
        {
            resetQuestProgressButton.onClick.AddListener(OnResetQuestProgressClicked);
        }
    }
    
    private void Start()
    {
        // Update QuestDataManager test mode
        if (QuestDataManager.Instance != null)
        {
            QuestDataManager.Instance.SetTestMode(questTestMode);
        }
    }
    
    private void OnValidate()
    {
        // Update test mode when changed in inspector
        if (QuestDataManager.Instance != null)
        {
            QuestDataManager.Instance.SetTestMode(questTestMode);
        }
    }
    
    private void OnResetQuestProgressClicked()
    {
        ResetQuestProgress();
    }
    
    public void ResetQuestProgress()
    {
        if (QuestDataManager.Instance == null)
        {
            Debug.LogWarning("QuestTester: QuestDataManager.Instance is null. Cannot reset quest progress.");
            return;
        }
        
        QuestDataManager.Instance.ResetAllQuestProgress();
        Debug.Log("QuestTester: Quest progress has been reset.");
    }
    
    public void SetTestMode(bool enabled)
    {
        questTestMode = enabled;
        if (QuestDataManager.Instance != null)
        {
            QuestDataManager.Instance.SetTestMode(enabled);
        }
    }
    
    private void OnDestroy()
    {
        if (resetQuestProgressButton != null)
        {
            resetQuestProgressButton.onClick.RemoveListener(OnResetQuestProgressClicked);
        }
    }
}
