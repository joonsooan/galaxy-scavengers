using UnityEngine;
using System.Collections;

public class Combo_1 : Damageable
{
    [Header("Values")]
    [SerializeField] private float generationInterval = 5f;
    [SerializeField] private int resourceAmount = 1;
    [SerializeField] private ResourceType resourceType;
    
    [Header("VFX")]
    [SerializeField] private string canvasName = "FloatingText Canvas";
    
    private Canvas _canvas;
    private Coroutine _productionCoroutine;

    protected override void OnEnable()
    {
        base.OnEnable();
        
        GameObject canvasObject = GameObject.Find(canvasName);
        
        if (canvasObject != null)
        {
            _canvas = canvasObject.GetComponent<Canvas>();
        }
        
        ActivateComboCard();
    }

    private void ActivateComboCard()
    {
        if (_productionCoroutine != null)
        {
            StopCoroutine(_productionCoroutine);
        }
        _productionCoroutine = StartCoroutine(ProduceResource());
    }

    private IEnumerator ProduceResource()
    {
        while (true)
        {
            yield return new WaitForSeconds(generationInterval);
            
            GenerateResource();
        }
    }

    private void GenerateResource()
    {
        ResourceManager.Instance.AddResource(resourceType, resourceAmount);
        ShowResourceText(resourceAmount);
    }
    
    private void ShowResourceText(int amount)
    {
        GameObject textObj = ObjectPooler.Instance.SpawnFromPool(
            "ResourceText", transform.position, Quaternion.identity);

        if (textObj != null)
        {
            FloatingNumText floatingText = textObj.GetComponent<FloatingNumText>();
            if (floatingText != null)
            {
                floatingText.Play($"+{amount}", Color.white);
            }
        }
    }
}