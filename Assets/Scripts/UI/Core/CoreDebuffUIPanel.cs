using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CoreDebuffUIPanel : MonoBehaviour
{
    private static readonly CorePart[] CorePartDisplayOrder = {
        CorePart.Engine,
        CorePart.Barrier,
        CorePart.Controller,
        CorePart.Repeater,
        CorePart.Storage,
    };

    [Header("UI References")]
    [SerializeField] private GameObject debuffContainer;
    [SerializeField] private GameObject debuffImagePrefab;
    [SerializeField] private GameObject debuffInfoPanel;
    [SerializeField] private GameObject debuffInfoText;

    private readonly List<GameObject> _debuffIcons = new List<GameObject>();
    private Coroutine _debuffInfoRebuildCoroutine;

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
        HideDebuffInfo();
    }

    private void UpdateDebuffDisplay()
    {
        if (CoreRepairManager.Instance == null || debuffContainer == null) return;

        foreach (GameObject iconObj in _debuffIcons)
        {
            if (iconObj != null)
            {
                Destroy(iconObj);
            }
        }
        _debuffIcons.Clear();

        if (debuffImagePrefab == null) return;

        foreach (CorePart part in CorePartDisplayOrder)
        {
            if (!CoreRepairManager.Instance.IsPartRepaired(part))
            {
                string debuffDesc = CoreRepairManager.Instance.GetDebuffDescription(part);
                if (!string.IsNullOrEmpty(debuffDesc))
                {
                    CorePartData partData = CoreRepairManager.Instance.GetPartData(part);
                    GameObject iconObj = Instantiate(debuffImagePrefab, debuffContainer.transform);

                    Image image = iconObj.GetComponent<Image>();
                    if (image == null)
                        image = iconObj.GetComponentInChildren<Image>();
                    if (image != null && partData != null && partData.debuffIcon != null)
                        image.sprite = partData.debuffIcon;

                    DebuffIconHover hover = iconObj.GetComponent<DebuffIconHover>();
                    if (hover == null)
                        hover = iconObj.GetComponentInChildren<DebuffIconHover>();
                    if (hover != null)
                        hover.Initialize(part, this);

                    _debuffIcons.Add(iconObj);
                }
            }
        }
    }

    public void ShowDebuffInfo(CorePart part)
    {
        if (CoreRepairManager.Instance == null) return;
        if (debuffInfoText != null)
        {
            TMP_Text textComponent = debuffInfoText.GetComponent<TMP_Text>();
            if (textComponent == null)
                textComponent = debuffInfoText.GetComponentInChildren<TMP_Text>();
            if (textComponent != null)
                textComponent.text = CoreRepairManager.Instance.GetDebuffDescription(part);
        }
        if (debuffInfoPanel != null)
        {
            debuffInfoPanel.SetActive(true);
            RebuildDebuffInfoLayout();
        }
    }

    private void RebuildDebuffInfoLayout()
    {
        Canvas.ForceUpdateCanvases();

        TMP_Text tmpText = debuffInfoText != null ? debuffInfoText.GetComponentInChildren<TMP_Text>() : null;
        RectTransform textRect = tmpText != null ? tmpText.rectTransform : (debuffInfoText != null ? debuffInfoText.GetComponent<RectTransform>() : null);
        RectTransform panelRect = debuffInfoPanel != null ? debuffInfoPanel.GetComponent<RectTransform>() : null;

        if (textRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(textRect);
        if (panelRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
        if (panelRect != null && panelRect.parent is RectTransform parentRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);

        if (_debuffInfoRebuildCoroutine != null)
            StopCoroutine(_debuffInfoRebuildCoroutine);
        _debuffInfoRebuildCoroutine = StartCoroutine(DelayedRebuildDebuffInfoLayout());
    }

    private IEnumerator DelayedRebuildDebuffInfoLayout()
    {
        yield return new WaitForEndOfFrame();

        Canvas.ForceUpdateCanvases();

        TMP_Text tmpText = debuffInfoText != null ? debuffInfoText.GetComponentInChildren<TMP_Text>() : null;
        RectTransform textRect = tmpText != null ? tmpText.rectTransform : (debuffInfoText != null ? debuffInfoText.GetComponent<RectTransform>() : null);
        RectTransform panelRect = debuffInfoPanel != null ? debuffInfoPanel.GetComponent<RectTransform>() : null;

        if (textRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(textRect);
        if (panelRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
        if (panelRect != null && panelRect.parent is RectTransform parentRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);

        _debuffInfoRebuildCoroutine = null;
    }

    public void HideDebuffInfo()
    {
        if (debuffInfoPanel != null)
            debuffInfoPanel.SetActive(false);
    }
}
