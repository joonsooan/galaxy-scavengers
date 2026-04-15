using UnityEngine;
using UnityEngine.UI;

public class QuestTester : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button resetQuestProgressButton;
    
    [Header("Test Settings")]
    [Tooltip("If true, all active quests will be completable regardless of requirements")]
    [SerializeField] private bool questTestMode = false;
    
    private static QuestTester _instance;
    
    public static bool IsTestModeEnabled
    {
        get
        {
            if (_instance != null)
            {
                return _instance.questTestMode;
            }
            return false;
        }
    }
    
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        
        if (resetQuestProgressButton != null)
        {
            resetQuestProgressButton.onClick.AddListener(OnResetQuestProgressClicked);
        }
    }
    
    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
        
        if (resetQuestProgressButton != null)
        {
            resetQuestProgressButton.onClick.RemoveListener(OnResetQuestProgressClicked);
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
        
        Debug.Log("QuestTester: Quest progress and all PlayerPrefs have been reset.");
    }
    
    public void SetTestMode(bool enabled)
    {
        questTestMode = enabled;
    }
}
