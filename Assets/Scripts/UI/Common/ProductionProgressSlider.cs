using UnityEngine;
using UnityEngine.UI;

public class ProductionProgressSlider : MonoBehaviour
{
    [SerializeField] private Slider progressSlider;
    [SerializeField] private Image fillImage;
    [SerializeField] private float yOffset = 2.0f;
    
    private Transform _targetTransform;
    private const string ObjectUICanvasName = "ObjectUI Canvas";

    public void Initialize(Transform target)
    {
        _targetTransform = target;
        FindAndSetParentCanvas();
        transform.position = _targetTransform.position + new Vector3(0f, yOffset, 0f);
        gameObject.SetActive(false);
    }
    
    private void FindAndSetParentCanvas()
    {
        GameObject canvasObj = GameObject.Find(ObjectUICanvasName);
        if (canvasObj != null)
        {
            transform.SetParent(canvasObj.transform, false);
        }
    }
    
    public void SetProgress(float value)
    {
        if (progressSlider != null)
        {
            progressSlider.value = Mathf.Clamp01(value);
        }
    }
}
