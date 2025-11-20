public class Radar : Damageable
{
    protected override void OnEnable()
    {
        base.OnEnable();
        ActivateComboCard();
    }

    private void ActivateComboCard()
    {
        // GameManager.Instance.expansionPanel.TogglePanelVisibility();
    }
}