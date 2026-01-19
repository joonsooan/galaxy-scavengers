using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CreditRewardCell : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image creditIcon;
    [SerializeField] private TMP_Text creditAmountText;
    
    public void SetCreditAmount(int amount)
    {
        if (creditAmountText != null)
        {
            creditAmountText.text = amount.ToString();
            creditAmountText.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning("CreditRewardCell: creditAmountText is null!");
        }
        
        if (creditIcon != null)
        {
            creditIcon.enabled = creditIcon.sprite != null;
        }
    }
}
