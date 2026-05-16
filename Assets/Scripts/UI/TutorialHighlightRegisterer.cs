using UnityEngine;

public class TutorialHighlightRegisterer : MonoBehaviour
{
    [SerializeField] private string tutorialID;
    [SerializeField] private Material glowMaterial;

    private void Start()
    {
        if (TutorialManager.Instance != null) {
            TutorialManager.Instance.RegisterRuntimeUI(tutorialID, gameObject, glowMaterial);
        }
    }
}
