using UnityEngine;

public enum HoverType
{
    ResourcePanel,
    RecipeBtn,
    CardBtn,
    SolanaRequiredPanel,
    RemainTimePanel,
    MineTypePanel,
    UnitMakeBtn
}

public class MouseHoverableObject : MonoBehaviour
{
    public HoverType hoverType;

    private void OnMouseEnter()
    {
        switch (hoverType)
        {
            case HoverType.ResourcePanel:
                GameManager.Instance.uiManager.DisplayResourcePanel();
                break;
            case HoverType.RecipeBtn:
                GameManager.Instance.uiManager.DisplayRecipeBtn();
                break;
            case HoverType.CardBtn:
                GameManager.Instance.uiManager.DisplayCardBtn();
                break;
            case HoverType.SolanaRequiredPanel:
                GameManager.Instance.uiManager.DisplaySolanaRequiredPanel();
                break;
            case HoverType.RemainTimePanel:
                GameManager.Instance.uiManager.DisplayRemainTimePanel();
                break;
            case HoverType.MineTypePanel:
                GameManager.Instance.uiManager.DisplayMineTypePanel();
                break;
            case HoverType.UnitMakeBtn:
                GameManager.Instance.uiManager.DisplayUnitMakeBtn();
                break;
        }
    }

    private void OnMouseExit()
    {
        switch (hoverType)
        {
            case HoverType.ResourcePanel:
                GameManager.Instance.uiManager.HideResourcePanel();
                break;
            case HoverType.RecipeBtn:
                GameManager.Instance.uiManager.HideRecipeBtn();
                break;
            case HoverType.CardBtn:
                GameManager.Instance.uiManager.HideCardBtn();
                break;
            case HoverType.SolanaRequiredPanel:
                GameManager.Instance.uiManager.HideSolanaRequiredPanel();
                break;
            case HoverType.RemainTimePanel:
                GameManager.Instance.uiManager.HideRemainTimePanel();
                break;
            case HoverType.MineTypePanel:
                GameManager.Instance.uiManager.HideMineTypePanel();
                break;
            case HoverType.UnitMakeBtn:
                GameManager.Instance.uiManager.HideUnitMakeBtn();
                break;
        }
    }
}