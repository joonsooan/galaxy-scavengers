using FMOD.Studio;
using UnityEngine;
using UnityEngine.EventSystems;
using FMODUnity;

public class FMODUIButton : MonoBehaviour, IPointerEnterHandler, IPointerUpHandler
{
    [Header("FMOD Events")]
    [SerializeField] private EventReference hoverSound;
    [SerializeField] private EventReference clickSound;
    [SerializeField] private string clickType = "Default";
    private static int _lastClickSoundFrame = -1;

    public static bool HasPlayedClickSoundThisFrame => _lastClickSoundFrame == Time.frameCount;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!hoverSound.IsNull)
        {
            PlaySoundWithParameter(hoverSound, "ClickType", clickType);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!clickSound.IsNull)
        {
            PlaySoundWithParameter(clickSound, "ClickType", clickType);
            _lastClickSoundFrame = Time.frameCount;
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
        SetClickType(clickState);
    }

    public void SetClickType(string newClickType)
    {
        clickType = newClickType;
    }
}