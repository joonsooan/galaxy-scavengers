using UnityEngine;

public class PowerStatusWorldFollower : MonoBehaviour
{
    private Transform _target;
    private Vector3 _worldOffset;
    private string _poolReturnTag;
    private CanvasGroup _canvasGroup;
    private bool _revealedAfterPosition;

    public string PoolReturnTag => _poolReturnTag;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    public void Initialize(Transform target, Vector3 worldOffset, string poolReturnTag = null)
    {
        _target = target;
        _worldOffset = worldOffset;
        _poolReturnTag = poolReturnTag;
        _revealedAfterPosition = false;
        if (_canvasGroup == null) {
            _canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        }
        _canvasGroup.alpha = 0f;
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

        if (!_revealedAfterPosition && _canvasGroup != null) {
            _revealedAfterPosition = true;
            _canvasGroup.alpha = 1f;
        }
    }
}
