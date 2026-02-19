using UnityEngine;

public class UnitHoverTrigger : MonoBehaviour
{
    private UnitBase _unitBase;
    private BoxCollider2D[] _boxColliders;
    private bool _isInitialized;

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }
        _unitBase = GetComponent<UnitBase>();
        _boxColliders = GetComponentsInChildren<BoxCollider2D>();
        _isInitialized = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("MouseHoverDetector"))
        {
            return;
        }
        Initialize();
        if (_unitBase == null)
        {
            return;
        }
        if (!IsTouchingAnyCollider(other))
        {
            return;
        }
        if (BuildingHoverManager.Instance != null)
        {
            BuildingHoverManager.Instance.OnUnitEnter(_unitBase);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("MouseHoverDetector"))
        {
            return;
        }
        if (_unitBase == null)
        {
            return;
        }
        if (IsTouchingAnyCollider(other))
        {
            return;
        }
        if (BuildingHoverManager.Instance != null)
        {
            BuildingHoverManager.Instance.OnUnitExit(_unitBase);
        }
    }

    private bool IsTouchingAnyCollider(Collider2D other)
    {
        if (_boxColliders == null)
        {
            _boxColliders = GetComponentsInChildren<BoxCollider2D>();
        }
        Vector2 otherPosition = other.bounds.center;
        foreach (BoxCollider2D boxCollider in _boxColliders)
        {
            if (boxCollider != null && boxCollider.enabled)
            {
                if (boxCollider.isTrigger)
                {
                    if (boxCollider.IsTouching(other))
                    {
                        return true;
                    }
                }
                else
                {
                    if (boxCollider.bounds.Contains(otherPosition))
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }
}
