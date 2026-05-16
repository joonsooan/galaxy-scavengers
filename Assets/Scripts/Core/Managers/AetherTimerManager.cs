using System;
using System.Collections;
using UnityEngine;

public class AetherTimerManager : MonoBehaviour
{
    public static event Action OnPowerTick;
    
    private static readonly WaitForSeconds _tickWait = CoroutineCache.GetWaitForSeconds(1f);
    private Coroutine _tickCoroutine;
    
    private void Start()
    {
        _tickCoroutine = StartCoroutine(PowerTickCoroutine());
    }

    private void OnDisable()
    {
        if (_tickCoroutine != null)
        {
            StopCoroutine(_tickCoroutine);
            _tickCoroutine = null;
        }
    }
    
    private IEnumerator PowerTickCoroutine()
    {
        while (true)
        {
            yield return _tickWait;
            OnPowerTick?.Invoke();
        }
    }
}
