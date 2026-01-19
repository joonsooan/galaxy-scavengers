using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CreditDisplayUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text creditText;
    
    private void OnEnable()
    {
        if (CreditManager.Instance != null)
        {
            CreditManager.Instance.OnCreditsChanged += UpdateCreditDisplay;
            UpdateCreditDisplay(CreditManager.Instance.GetCredits());
        }
    }
    
    private void OnDisable()
    {
        if (CreditManager.Instance != null)
        {
            CreditManager.Instance.OnCreditsChanged -= UpdateCreditDisplay;
        }
    }
    
    private void Start()
    {
        if (CreditManager.Instance != null)
        {
            UpdateCreditDisplay(CreditManager.Instance.GetCredits());
        }
    }
    
    private void UpdateCreditDisplay(int credits)
    {
        if (creditText != null)
        {
            creditText.text = $"{credits}";
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
    }
}
