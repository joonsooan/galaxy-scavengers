using FMOD.Studio;
using UnityEngine;
using UnityEngine.EventSystems;
using FMODUnity;

public class FMODUIButton : MonoBehaviour, IPointerEnterHandler, IPointerUpHandler
{
    [Header("FMOD Events")]
    [SerializeField] private EventReference hoverSound;
    [SerializeField] private EventReference clickSound;
    private string _currentClickState = "Default";

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!hoverSound.IsNull)
        {
            PlaySoundWithParameter(hoverSound, "ClickType", _currentClickState);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!clickSound.IsNull)
        {
            PlaySoundWithParameter(clickSound, "ClickType", _currentClickState);
        }
    }

    private void PlaySoundWithParameter(EventReference sound, string paramName, string paramValue)
    {
        EventInstance instance = RuntimeManager.CreateInstance(sound);

        instance.setParameterByNameWithLabel(paramName, paramValue);
        instance.start();
        instance.release(); 
    }

    public void SetClickState(string clickState)
    {
        _currentClickState = clickState;
    }
}