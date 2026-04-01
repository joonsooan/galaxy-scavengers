using UnityEngine;

public class PowerStatusWorldFollower : MonoBehaviour
{
    private Transform _target;
    private Vector3 _worldOffset;
    private string _poolReturnTag;

    public string PoolReturnTag => _poolReturnTag;

    public void Initialize(Transform target, Vector3 worldOffset, string poolReturnTag = null)
    {
        _target = target;
        _worldOffset = worldOffset;
        _poolReturnTag = poolReturnTag;
    }

    public void ClearFollowTarget()
    {
        _target = null;
    }

    private void LateUpdate()
    {
        if (_target == null) {
            if (string.IsNullOrEmpty(_poolReturnTag)) {
                Destroy(gameObject);
            }

            return;
        }

        transform.position = _target.position + _worldOffset;
    }
}
