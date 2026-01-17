using UnityEngine;
using UnityEngine.UI;

public interface IQuestUIProvider
{
    Button GetQuestButton();
    Button GetShopButton();
    GameObject GetQuestGridPanel();
    RectTransform GetQuestGridParent();
    GameObject GetQuestCellPrefab();
    QuestDetailPanel GetQuestDetailPanel();
    QuestProvider GetQuestProvider();
    GameObject GetShopUIContainer();
    void ShowShopUI();
    void HideShopUI();
    void ClearDetailPanel();
    GameObject GetNewQuestIndicator();
}
