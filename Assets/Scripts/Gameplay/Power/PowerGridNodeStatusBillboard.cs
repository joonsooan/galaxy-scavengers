using UnityEngine;

public class PowerGridNodeStatusBillboard : MonoBehaviour
{
    private Battery _battery;
    private PowerReceiver _receiver;
    private PowerStatusWorldFollower _follower;
    private PowerFeedVisualState _iconShownFor = PowerFeedVisualState.Ok;

    [SerializeField] private float iconHeightVsBuilding = 0.35f;
    [SerializeField] private Vector3 powerStatusIconWorldOffset;

    private void Awake()
    {
        _battery = GetComponent<Battery>();
        _receiver = GetComponent<PowerReceiver>();
    }

    private void LateUpdate()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsDragging()) {
            ReleaseFollower();
            _iconShownFor = PowerFeedVisualState.Ok;
            return;
        }
        ElectricityConsumptionManager mgr = ElectricityConsumptionManager.Instance;
        if (mgr == null) {
            return;
        }
        PowerFeedVisualState st;
        if (_battery != null) {
            st = mgr.GetBatteryVisualState(_battery);
        }
        else if (_receiver != null) {
            st = mgr.GetPowerReceiverVisualState(_receiver);
        }
        else {
            return;
        }
        if (st == PowerFeedVisualState.Ok) {
            ReleaseFollower();
            _iconShownFor = PowerFeedVisualState.Ok;
            return;
        }
        if (_follower != null && _iconShownFor == st) {
            return;
        }
        ReleaseFollower();
        _follower = mgr.SpawnPowerFloatingIcon(st, transform, powerStatusIconWorldOffset);
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
