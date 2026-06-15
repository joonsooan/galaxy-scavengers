using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class TechDataCell : MonoBehaviour, IPointerClickHandler
{
    [Header("Tech Data")]
    [SerializeField] private TechData techData;

    [Header("UI References")]
    [SerializeField] private Image iconImg;
    [SerializeField] private Transform costPanel;
    [SerializeField] private GameObject producableResourceImgPrefab;

    private ResearchPanelUI _researchPanelUI;
    private RectTransform _rectTransform;

    public TechData TechData => techData;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
    }

    public void Setup(ResearchPanelUI researchPanelUI)
    {
        _researchPanelUI = researchPanelUI;
        InitializeDisplay();
    }

    private void InitializeDisplay()
    {
        if (techData == null)
        {
            return;
        }

        if (iconImg != null)
        {
            iconImg.sprite = techData.GetTechIcon();
        }

        if (costPanel != null)
        {
            for (int i = costPanel.childCount - 1; i >= 0; i--)
            {
                Destroy(costPanel.GetChild(i).gameObject);
            }

            ResourceCost[] costs = techData.researchCosts;
            if (costs != null)
            {
                for (int i = 0; i < costs.Length; i++)
                {
                    if (producableResourceImgPrefab == null)
                    {
                        continue;
                    }

                    ResourceCost cost = costs[i];
                    GameObject iconObj = Instantiate(producableResourceImgPrefab, costPanel);
                    Image iconImage = GetChildImage(iconObj);
                    if (iconImage != null && ResourceManager.Instance != null)
                    {
                        iconImage.sprite = ResourceManager.Instance.GetResourceIcon(cost.resourceType);
                    }
                }
            }
        }
    }

    private static Image GetChildImage(GameObject obj)
    {
        for (int i = 0; i < obj.transform.childCount; i++)
        {
            Image img = obj.transform.GetChild(i).GetComponent<Image>();
            if (img != null)
            {
                return img;
            }
        }
        return null;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_researchPanelUI != null)
        {
            _researchPanelUI.SelectTech(techData);
        }
    }

    // Returns the world-space midpoint of the left edge of this cell's RectTransform.
    // corners layout from GetWorldCorners: [0]=bottom-left, [1]=top-left, [2]=top-right, [3]=bottom-right
    public Vector3 GetLeftCenterWorld()
    {
        Vector3[] corners = new Vector3[4];
        _rectTransform.GetWorldCorners(corners);
        return (corners[0] + corners[1]) * 0.5f;
    }

    // Returns the world-space midpoint of the right edge of this cell's RectTransform.
    public Vector3 GetRightCenterWorld()
    {
        Vector3[] corners = new Vector3[4];
        _rectTransform.GetWorldCorners(corners);
        return (corners[2] + corners[3]) * 0.5f;
    }
}
