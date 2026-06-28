using UnityEngine;

public class UnitShadowController : MonoBehaviour
{
    [SerializeField] private bool useHeightOffset = false;

    private SpriteRenderer _parentRenderer;
    private SpriteRenderer _shadowRenderer;
    private float _initialSpriteLocalY;
    private bool _hasInitialY;

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

        if (spriteController != null && spriteController.transform == parent)
        {
            Transform root = parent.parent;
            if (root != null)
            {
                transform.SetParent(root);
                _parentRenderer = spriteController.GetComponent<SpriteRenderer>();
                _initialSpriteLocalY = parent.localPosition.y;
                _hasInitialY = true;
                return;
            }
        }

        if (spriteController != null)
        {
            _parentRenderer = spriteController.GetComponent<SpriteRenderer>();
            _initialSpriteLocalY = spriteController.transform.localPosition.y;
            _hasInitialY = true;
            return;
        }

        SpriteRenderer[] renderers = parent.GetComponentsInChildren<SpriteRenderer>();
        foreach (SpriteRenderer sr in renderers)
        {
            if (sr != _shadowRenderer)
            {
                _parentRenderer = sr;
                _initialSpriteLocalY = sr.transform.localPosition.y;
                _hasInitialY = true;
                break;
            }
        }
    }

    private void LateUpdate()
    {
        if (_parentRenderer != null && _shadowRenderer != null)
        {
            _shadowRenderer.enabled = _parentRenderer.enabled;
            if (_shadowRenderer.enabled)
            {
                _shadowRenderer.sprite = _parentRenderer.sprite;
                _shadowRenderer.flipX = _parentRenderer.flipX;
                _shadowRenderer.flipY = _parentRenderer.flipY;

                if (_hasInitialY)
                {
                    if (useHeightOffset)
                    {
                        float heightOffset = _parentRenderer.transform.localPosition.y - _initialSpriteLocalY;
                        float shearX = Shader.GetGlobalFloat("_GlobalShadowShearX");
                        float lengthY = Shader.GetGlobalFloat("_GlobalShadowLengthY");

                        transform.localPosition = new Vector3(heightOffset * shearX, _initialSpriteLocalY - heightOffset * lengthY, 0f);
                    }
                    else
                    {
                        transform.localPosition = new Vector3(0f, _initialSpriteLocalY, 0f);
                    }
                }
                else
                {
                    transform.localPosition = Vector3.zero;
                }
            }
        }
        else if (_parentRenderer == null)
        {
            FindParentRenderer();
        }
    }
}
