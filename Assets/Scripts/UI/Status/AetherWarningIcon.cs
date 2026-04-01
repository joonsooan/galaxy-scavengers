using UnityEngine;
using UnityEngine.UI;

public class AetherWarningIcon : MonoBehaviour
{
    [SerializeField] private GameObject disconnectedIconObject;
    [SerializeField] private GameObject insufficientIconObject;
    [SerializeField] private Image disconnectedImage;
    [SerializeField] private Image insufficientImage;
    [SerializeField] private GameObject iconObject;
    [SerializeField] private Image iconImage;

    private IElectricityConsumer _electricityConsumer;

    private void Awake()
    {
        _electricityConsumer = GetComponentInParent<IElectricityConsumer>();

        if (disconnectedIconObject != null) {
            disconnectedIconObject.SetActive(false);
        }
        if (insufficientIconObject != null) {
            insufficientIconObject.SetActive(false);
        }
        if (iconObject != null) {
            iconObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (_electricityConsumer == null) {
            return;
        }

        ElectricityConsumptionManager mgr = ElectricityConsumptionManager.Instance;
        PowerFeedVisualState st = mgr != null
            ? mgr.GetConsumerVisualState(_electricityConsumer)
            : (_electricityConsumer.IsOperational ? PowerFeedVisualState.Ok : PowerFeedVisualState.Disconnected);

        bool showDisconnected = st == PowerFeedVisualState.Disconnected;
        bool showInsufficient = st == PowerFeedVisualState.InsufficientPool;
        bool anyProblem = showDisconnected || showInsufficient;

        if (disconnectedIconObject != null && insufficientIconObject != null) {
            disconnectedIconObject.SetActive(showDisconnected);
            insufficientIconObject.SetActive(showInsufficient);
            return;
        }

        if (iconObject != null) {
            iconObject.SetActive(anyProblem);
            return;
        }

        if (disconnectedImage != null && insufficientImage != null) {
            disconnectedImage.enabled = showDisconnected;
            insufficientImage.enabled = showInsufficient;
        }
        else if (iconImage != null) {
            iconImage.enabled = anyProblem;
        }
    }
}
