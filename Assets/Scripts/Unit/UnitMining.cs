using System;
using System.Collections;
using UnityEngine;

public class UnitMining : MonoBehaviour
{
    [Header("Gathering Stats")]
    public int mineAmountPerAction = 1; // 한번 채굴 시 캐는 양

    private Coroutine _mineCoroutine;
    private WaitForSeconds _miningDelay;
    private ResourceNode _targetResourceNode;

    public event Action<ResourceType, int> OnResourceMined;

    public void StartMining(ResourceNode target)
    {
        _targetResourceNode = target;
        
        _miningDelay = new WaitForSeconds(target.timeToMinePerUnit);
        
        if (_mineCoroutine == null) {
            _mineCoroutine = StartCoroutine(MineResourceCoroutine());
        }
    }

    public void StopMining()
    {
        if (_mineCoroutine != null) {
            StopCoroutine(_mineCoroutine);
            _mineCoroutine = null;
        }
    }

    private IEnumerator MineResourceCoroutine()
    {
        yield return _miningDelay;

        while (true) {
            if (_targetResourceNode != null && !_targetResourceNode.IsDepleted) {
                int minedAmount = _targetResourceNode.Mine(mineAmountPerAction);
                OnResourceMined?.Invoke(_targetResourceNode.resourceType, minedAmount);
            }
            else {
                StopMining();
                yield break;
            }
            yield return _miningDelay;
        }
    }
}
