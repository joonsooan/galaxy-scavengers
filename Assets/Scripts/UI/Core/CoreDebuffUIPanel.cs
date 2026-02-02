using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CoreDebuffUIPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject debuffContainer;
    [SerializeField] private GameObject debuffTextPrefab;

    private readonly List<GameObject> _debuffTexts = new List<GameObject>();

    private void OnEnable()
    {
        if (CoreRepairManager.Instance != null)
        {
            CoreRepairManager.Instance.OnRepairStatusChanged += UpdateDebuffDisplay;
            UpdateDebuffDisplay();
        }
    }

    private void OnDisable()
    {
        if (CoreRepairManager.Instance != null)
        {
            CoreRepairManager.Instance.OnRepairStatusChanged -= UpdateDebuffDisplay;
        }
    }

    private void UpdateDebuffDisplay()
    {
        if (CoreRepairManager.Instance == null || debuffContainer == null) return;

        foreach (GameObject textObj in _debuffTexts)
        {
            if (textObj != null)
            {
                Destroy(textObj);
            }
        }
        _debuffTexts.Clear();

        foreach (CorePart part in System.Enum.GetValues(typeof(CorePart)))
        {
            if (!CoreRepairManager.Instance.IsPartRepaired(part))
            {
                string debuffDesc = CoreRepairManager.Instance.GetDebuffDescription(part);
                if (!string.IsNullOrEmpty(debuffDesc))
                {
                    GameObject textObj = debuffTextPrefab != null ? 
                        Instantiate(debuffTextPrefab, debuffContainer.transform) :
                        CreateDefaultText(debuffDesc);
                    
                    if (textObj != null)
                    {
                        TMP_Text textComponent = textObj.GetComponent<TMP_Text>();
                        if (textComponent == null)
                        {
                            textComponent = textObj.GetComponentInChildren<TMP_Text>();
                        }
                        if (textComponent != null)
                        {
                            textComponent.text = debuffDesc;
                        }
                        _debuffTexts.Add(textObj);
                    }
                }
            }
        }
    }

    private GameObject CreateDefaultText(string text)
    {
        GameObject textObj = new GameObject("DebuffText");
        textObj.transform.SetParent(debuffContainer.transform);
        TMP_Text textComponent = textObj.AddComponent<TextMeshProUGUI>();
        textComponent.text = text;
        textComponent.fontSize = 14;
        return textObj;
    }
}
