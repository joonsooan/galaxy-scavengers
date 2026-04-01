using UnityEngine;

public class ElectricityConsumerStatusBillboard : MonoBehaviour
{
    private IElectricityConsumer _consumer;
    private PowerStatusWorldFollower _follower;
    private PowerFeedVisualState _iconShownFor = PowerFeedVisualState.Ok;

    [SerializeField] private float iconHeightVsBuilding = 0.35f;

    private void Awake()
    {
        _consumer = GetComponent<IElectricityConsumer>();
    }

    private void LateUpdate()
    {
        if (_consumer == null) {
            return;
        }
        ElectricityConsumptionManager mgr = ElectricityConsumptionManager.Instance;
        if (mgr == null) {
            return;
        }
        PowerFeedVisualState st = mgr.GetConsumerVisualState(_consumer);
        if (st == PowerFeedVisualState.Ok) {
            ReleaseFollower();
            _iconShownFor = PowerFeedVisualState.Ok;
            return;
        }
        if (_follower != null && _iconShownFor == st) {
            return;
        }
        ReleaseFollower();
        _follower = mgr.SpawnPowerFloatingIcon(st, transform);
        _iconShownFor = st;
        if (_follower != null) {
            RectTransform rt = _follower.GetComponent<RectTransform>();
            if (rt == null) {
                rt = _follower.GetComponentInChildren<RectTransform>(true);
            }
            if (rt != null) {
                PowerStatusBillboardAlign.ApplyCanvasIconWorldHeight(transform, rt, iconHeightVsBuilding);
            }
        }
    }

    private void OnDestroy()
    {
        ReleaseFollower();
    }

    private void ReleaseFollower()
    {
        if (_follower != null) {
            ElectricityConsumptionManager.Instance?.ReleasePowerFloatingIcon(_follower);
            _follower = null;
        }
    }
}
