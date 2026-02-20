using UnityEngine;
using UnityEngine.UI;

public class ProductionProgressSlider : MonoBehaviour
{
    [SerializeField] private Slider progressSlider;
    [SerializeField] private Image fillImage;
    [SerializeField] private float yOffset = 2.0f;
    
    private Transform _targetTransform;

    public void Initialize(Transform target)
    {
        _targetTransform = target;
        FindAndSetParentCanvas();
        transform.position = _targetTransform.position + new Vector3(0f, yOffset, 0f);
        gameObject.SetActive(false);
    }
    
    private void FindAndSetParentCanvas()
    {
        Canvas canvas = GameManager.Instance?.uiManager?.GetObjectUICanvas();
        if (canvas != null)
            transform.SetParent(canvas.transform, false);
    }
    
    public void SetProgress(float value)
    {
        if (progressSlider != null)
        {
            progressSlider.value = Mathf.Clamp01(value);
        }
    }
}
