using UnityEngine;

public class UnitShadowController : MonoBehaviour
{
    private SpriteRenderer _parentRenderer;
    private SpriteRenderer _shadowRenderer;

    private void Awake()
    {
        _shadowRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        FindParentRenderer();
    }

    private void FindParentRenderer()
    {
        Transform parent = transform.parent;
        if (parent == null) return;

        UnitSpriteController spriteController = parent.GetComponentInChildren<UnitSpriteController>();
        if (spriteController != null)
        {
            _parentRenderer = spriteController.GetComponent<SpriteRenderer>();
            return;
        }

        SpriteRenderer[] renderers = parent.GetComponentsInChildren<SpriteRenderer>();
        foreach (SpriteRenderer sr in renderers)
        {
            if (sr != _shadowRenderer)
            {
                _parentRenderer = sr;
                break;
            }
        }
    }

    private void LateUpdate()
    {
        if (_parentRenderer != null && _shadowRenderer != null)
        {
            _shadowRenderer.sprite = _parentRenderer.sprite;
            _shadowRenderer.flipX = _parentRenderer.flipX;
            _shadowRenderer.flipY = _parentRenderer.flipY;
        }
        else if (_parentRenderer == null)
        {
            FindParentRenderer();
        }
    }
}
