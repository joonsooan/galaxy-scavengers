using UnityEngine;

public class TargetBracketEffect : MonoBehaviour
{
    [Header("Sprite")]
    [SerializeField] private Sprite bracketSprite;
    [SerializeField] private string sortingLayerName = "Objects";
    [SerializeField] private int sortingOrder = 100;

    [Header("Positioning")]
    [SerializeField] private float padding = 0.15f;

    [Header("Heartbeat Animation")]
    [SerializeField] private float heartbeatDuration = 0.8f;
    [SerializeField] private float maxBounceOffset = 0.08f;
    [SerializeField] private float bounceUpPeak = 1.3f;
    [SerializeField] private float bounceDownPeak = 0.9f;
    [SerializeField] private float bounceUp2Peak = 1.15f;

    private static TargetBracketEffect _instance;

    private static readonly Vector2[] BounceDirections =
    {
        new Vector2(-1f, 1f).normalized,
        new Vector2(1f, 1f).normalized,
        new Vector2(-1f, -1f).normalized,
        new Vector2(1f, -1f).normalized
    };

    private SpriteRenderer[] _brackets;
    private Vector3[] _basePositions;
    private Transform _currentTarget;
    private SpriteRenderer _targetRenderer;

    public static void Show(Transform target)
    {
        if (target == null || _instance == null) return;
        _instance.ShowInternal(target);
    }

    public static void Hide()
    {
        if (_instance != null)
            _instance.HideInternal();
    }

    private void Awake()
    {
        _instance = this;
        InitializeBrackets();
    }

    private void InitializeBrackets()
    {
        _brackets = new SpriteRenderer[4];
        _basePositions = new Vector3[4];

        bool[] flipX = { false, true, false, true };
        bool[] flipY = { false, false, true, true };

        for (int i = 0; i < 4; i++)
        {
            GameObject child = new GameObject($"Bracket_{i}");
            child.transform.SetParent(transform);
            SpriteRenderer sr = child.AddComponent<SpriteRenderer>();
            sr.sprite = bracketSprite;
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = sortingOrder;
            sr.flipX = flipX[i];
            sr.flipY = flipY[i];
            child.SetActive(false);
            _brackets[i] = sr;
        }
    }

    private void ShowInternal(Transform target)
    {
        _currentTarget = target;
        _targetRenderer = target.GetComponentInChildren<SpriteRenderer>();
        if (_targetRenderer == null)
        {
            HideInternal();
            return;
        }

        RecalculateBasePositions();
        for (int i = 0; i < 4; i++)
            _brackets[i].gameObject.SetActive(true);
    }

    private void HideInternal()
    {
        _currentTarget = null;
        _targetRenderer = null;
        if (_brackets == null) return;
        for (int i = 0; i < 4; i++)
        {
            if (_brackets[i] != null)
                _brackets[i].gameObject.SetActive(false);
        }
    }

    private void RecalculateBasePositions()
    {
        if (_targetRenderer == null) return;

        Bounds bounds = _targetRenderer.bounds;
        Vector3 center = bounds.center;
        float halfW = bounds.extents.x + padding;
        float halfH = bounds.extents.y + padding;

        _basePositions[0] = new Vector3(center.x - halfW, center.y + halfH, center.z);
        _basePositions[1] = new Vector3(center.x + halfW, center.y + halfH, center.z);
        _basePositions[2] = new Vector3(center.x - halfW, center.y - halfH, center.z);
        _basePositions[3] = new Vector3(center.x + halfW, center.y - halfH, center.z);
    }

    private void LateUpdate()
    {
        if (_currentTarget == null || _targetRenderer == null)
        {
            HideInternal();
            return;
        }

        RecalculateBasePositions();

        float phase = (Time.unscaledTime % heartbeatDuration) / heartbeatDuration;
        float bounceOffset = GetBounceOffset(phase);

        for (int i = 0; i < 4; i++)
        {
            Vector3 pos = _basePositions[i];
            pos.x += BounceDirections[i].x * bounceOffset;
            pos.y += BounceDirections[i].y * bounceOffset;
            _brackets[i].transform.position = pos;
        }
    }

    private float GetBounceOffset(float phase)
    {
        float scaleMultiplier;
        if (phase < 0.25f)
        {
            float t = phase / 0.25f;
            t = 1f - (1f - t) * (1f - t);
            scaleMultiplier = Mathf.Lerp(1f, bounceUpPeak, t);
        }
        else if (phase < 0.4f)
        {
            float t = (phase - 0.25f) / 0.15f;
            t = t * t;
            scaleMultiplier = Mathf.Lerp(bounceUpPeak, bounceDownPeak, t);
        }
        else if (phase < 0.6f)
        {
            float t = (phase - 0.4f) / 0.2f;
            t = 1f - (1f - t) * (1f - t);
            scaleMultiplier = Mathf.Lerp(bounceDownPeak, bounceUp2Peak, t);
        }
        else
        {
            float t = (phase - 0.6f) / 0.4f;
            t = 1f - (1f - t) * (1f - t);
            scaleMultiplier = Mathf.Lerp(bounceUp2Peak, 1f, t);
        }

        return (scaleMultiplier - 1f) / (bounceUpPeak - 1f) * maxBounceOffset;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
}
