using UnityEngine;
using UnityEngine.EventSystems;

public abstract class InfoDisplayTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private bool _isUIPinned = false;
    
    protected abstract DisplayableData GetData(); 
    protected abstract void ShowInfo();
    protected abstract void HideInfo();

    protected virtual void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.onStartDrag.AddListener(OnDragStart);
            GameManager.Instance.onEndDrag.AddListener(OnDragEnd);
        }
    }
    
    protected virtual void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.onStartDrag.RemoveListener(OnDragStart);
            GameManager.Instance.onEndDrag.RemoveListener(OnDragEnd);
        }
    }
    
    private void OnDragStart(DisplayableData activeData)
    {
        if (activeData != null && activeData == GetData())
        {
            _isUIPinned = true;
        }
    }
    
    private void OnDragEnd()
    {
        _isUIPinned = false;
        HideInfo();
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!GameManager.Instance.IsDragging())
        {
            ShowInfo();
        }
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        if (!_isUIPinned)
        {
            HideInfo();
        }
    }
}