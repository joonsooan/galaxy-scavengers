using UnityEngine;

public class ResourceHoverTrigger : MonoBehaviour
{
    private ResourceNode _resourceNode;
    private BoxCollider2D[] _boxColliders;
    private CapsuleCollider2D[] _capsuleColliders;
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
        _resourceNode = GetComponent<ResourceNode>();
        _boxColliders = GetComponentsInChildren<BoxCollider2D>();
        _capsuleColliders = GetComponentsInChildren<CapsuleCollider2D>();
        _isInitialized = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("MouseHoverDetector"))
        {
            return;
        }
        Initialize();
        if (_resourceNode == null)
        {
            return;
        }
        if (!IsTouchingAnyCollider(other))
        {
            return;
        }
        if (BuildingHoverManager.Instance != null)
        {
            BuildingHoverManager.Instance.OnResourceEnter(_resourceNode);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("MouseHoverDetector"))
        {
            return;
        }
        if (_resourceNode == null)
        {
            return;
        }
        if (IsTouchingAnyCollider(other))
        {
            return;
        }
        if (BuildingHoverManager.Instance != null)
        {
            BuildingHoverManager.Instance.OnResourceExit(_resourceNode);
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
        if (_capsuleColliders == null)
        {
            _capsuleColliders = GetComponentsInChildren<CapsuleCollider2D>();
        }
        foreach (CapsuleCollider2D capsuleCollider in _capsuleColliders)
        {
            if (capsuleCollider != null && capsuleCollider.enabled && capsuleCollider.IsTouching(other))
            {
                return true;
            }
        }
        return false;
    }
}
