using UnityEngine;

public abstract class DisplayableData : ScriptableObject
{
    [Header("Common Display Info")]
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;
}