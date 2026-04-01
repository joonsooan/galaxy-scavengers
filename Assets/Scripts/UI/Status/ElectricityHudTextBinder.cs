using TMPro;
using UnityEngine;

public class ElectricityHudTextBinder : MonoBehaviour
{
    [SerializeField] private TMP_Text storedElectricityText;
    [SerializeField] private TMP_Text productionConsumptionPerSecondText;

    private void LateUpdate()
    {
        ElectricityConsumptionManager mgr = ElectricityConsumptionManager.Instance;
        if (mgr == null) {
            return;
        }

        if (storedElectricityText != null) {
            storedElectricityText.text = mgr.GetTotalElectricityAmount().ToString();
        }

        if (productionConsumptionPerSecondText != null) {
            float prod = mgr.GetEffectiveElectricityProductionPerSecond();
            float cons = mgr.GetTotalElectricityConsumptionPerSecond();
            productionConsumptionPerSecondText.text = $"{prod:0.#} / {cons:0.#}";
        }
    }
}
