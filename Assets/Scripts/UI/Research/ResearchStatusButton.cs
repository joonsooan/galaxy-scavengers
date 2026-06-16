using UnityEngine;
using UnityEngine.UI;

public class ResearchStatusButton : MonoBehaviour
{
    [SerializeField] private Image targetImage;
    [SerializeField] private Sprite lockedSprite;
    [SerializeField] private Sprite availableSprite;
    [SerializeField] private Sprite inProgressSprite;
    [SerializeField] private Sprite completedSprite;

    public void SetState(TechResearchState state)
    {
        if (targetImage == null) return;
        switch (state)
        {
            case TechResearchState.Locked:
                if (lockedSprite != null) targetImage.sprite = lockedSprite;
                break;
            case TechResearchState.Available:
                if (availableSprite != null) targetImage.sprite = availableSprite;
                break;
            case TechResearchState.InProgress:
                if (inProgressSprite != null) targetImage.sprite = inProgressSprite;
                break;
            case TechResearchState.Completed:
                if (completedSprite != null) targetImage.sprite = completedSprite;
                break;
        }
    }
}
