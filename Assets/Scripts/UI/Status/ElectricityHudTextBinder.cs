using TMPro;
using UnityEngine;

public class ElectricityHudTextBinder : MonoBehaviour
{
    [SerializeField] private TMP_Text storedElectricityText;
    [SerializeField] private TMP_Text productionConsumptionPerSecondText;

    private float _lastStoredElectricity = float.NaN;
    private float _lastProd = float.NaN;
    private float _lastCons = float.NaN;

    private void LateUpdate()
    {
        ElectricityConsumptionManager mgr = ElectricityConsumptionManager.Instance;
        if (mgr == null) {
            return;
        }

        if (storedElectricityText != null) {
            float stored = mgr.GetTotalElectricityAmount();
            if (stored != _lastStoredElectricity)
            {
                _lastStoredElectricity = stored;
                storedElectricityText.text = stored.ToString();
            }
        }

        if (productionConsumptionPerSecondText != null) {
            float prod = mgr.GetEffectiveElectricityProductionPerSecond();
            float cons = mgr.GetTotalElectricityConsumptionPerSecond();
            if (prod != _lastProd || cons != _lastCons)
            {
                _lastProd = prod;
                _lastCons = cons;
                productionConsumptionPerSecondText.text = $"{prod:0.#} / {cons:0.#}";
            }
        }
    }
}
