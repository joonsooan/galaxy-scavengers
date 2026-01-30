using System;
using System.Collections;
using UnityEngine;

public class AetherTimerManager : MonoBehaviour
{
    public static event Action OnAetherTick;
    
    private static readonly WaitForSeconds _tickWait = CoroutineCache.GetWaitForSeconds(1f);
    private Coroutine _tickCoroutine;
    
    private void Start()
    {
        _tickCoroutine = StartCoroutine(AetherTickCoroutine());
    }

    private void OnDisable()
    {
        if (_tickCoroutine != null)
        {
            StopCoroutine(_tickCoroutine);
            _tickCoroutine = null;
        }
    }
    
    private IEnumerator AetherTickCoroutine()
    {
        while (true)
        {
            yield return _tickWait;
            OnAetherTick?.Invoke();
        }
    }
}
