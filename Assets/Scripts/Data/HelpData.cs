using UnityEngine;

[CreateAssetMenu(fileName = "New Help Data", menuName = "Game Data/Help Data")]
public class HelpData : ScriptableObject
{
    public string helpName;
    [TextArea(3, 10)] public string description;
    public Sprite image;
    public int order;
}
