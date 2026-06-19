using System;
using FMODUnity;
using UnityEngine;

public class SeedCore : MonoBehaviour, IClickable
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color hoverTint = new Color(0.7f, 0.7f, 0.7f, 1f);
    [SerializeField] private EventReference clickSound;
    [SerializeField] private EventReference hoverSound;

    private Color _originalColor;
    private bool _isHovered;

    public static event Action<SeedCore> OnSeedCoreClicked;

    private void Awake()
    {
        if (spriteRenderer != null)
        {
            _originalColor = spriteRenderer.color;
        }
    }

    private void Update()
    {
        bool hovered = IsMouseOver();
        if (hovered != _isHovered)
        {
            _isHovered = hovered;
            ApplyHoverVisual(_isHovered);
        }
    }

    private bool IsMouseOver()
    {
        if (UIUtils.IsPointerOverUI()) return false;
        Vector2 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(mouseWorld, Vector2.zero);
        if (hit.collider == null) return false;
        return hit.collider.GetComponentInParent<SeedCore>() == this;
    }

    private void ApplyHoverVisual(bool hovered)
    {
        if (spriteRenderer == null) return;
        spriteRenderer.color = hovered ? hoverTint : _originalColor;
        if (hovered && !hoverSound.IsNull)
        {
            RuntimeManager.PlayOneShot(hoverSound);
        }
    }

    public void OnClicked()
    {
        if (UIUtils.IsPointerOverUI())
        {
            return;
        }

        if (!clickSound.IsNull)
        {
            RuntimeManager.PlayOneShot(clickSound);
        }
        OnSeedCoreClicked?.Invoke(this);
    }
}
