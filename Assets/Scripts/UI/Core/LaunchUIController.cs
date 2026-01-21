using System.Collections;
using TMPro;
using UnityEngine;

public class LaunchUIController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject launchPanel;
    [SerializeField] private GameObject countdownPanel;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private TMP_Text neededAetherText;
    [SerializeField] private LaunchCompleteUI launchCompleteUI;

    [Header("Launch Settings")]
    [SerializeField] private float countdownDurationSeconds = 10f;
    [SerializeField] private int neededAetherPerCell = 10;
    [SerializeField] private int freeLaunchCells = 0;
    [SerializeField] private float launchCompleteDisplayDuration = 2f;

    private bool _isCountingDown;
    private Coroutine _countdownCoroutine;

    private void Awake()
    {
        if (launchPanel != null)
        {
            launchPanel.SetActive(false);
        }

        if (countdownPanel != null)
        {
            countdownPanel.SetActive(false);
        }
    }

    public void ShowLaunchPanel()
    {
        if (_isCountingDown)
        {
            return;
        }

        if (GameManager.Instance != null && GameManager.Instance.uiManager != null)
        {
            GameManager.Instance.uiManager.UnpinAndHideAllPanels();
        }

        MainControlPanel mainControl = FindFirstObjectByType<MainControlPanel>();
        if (mainControl != null)
        {
            mainControl.HideAllPanels();
        }

        if (launchPanel != null)
        {
            launchPanel.SetActive(true);
        }

        if (countdownPanel != null)
        {
            countdownPanel.SetActive(false);
        }
    }

    public void OnCancelLaunch()
    {
        if (launchPanel != null)
        {
            launchPanel.SetActive(false);
        }
    }

    public void OnConfirmLaunch()
    {
        if (_isCountingDown)
        {
            return;
        }

        MainStructure mainStructure = FindFirstObjectByType<MainStructure>();
        InventorySystem inventorySystem = mainStructure.GetComponent<InventorySystem>();

        int occupiedCells = inventorySystem.GetOccupiedCellCount();
        int cellsRequiringAether = Mathf.Max(0, occupiedCells - freeLaunchCells);
        int neededAether = neededAetherPerCell * cellsRequiringAether;

        if (ResourceManager.Instance != null)
        {
            int currentAether = ResourceManager.Instance.GetResourceAmount(ResourceType.Aether);
            if (currentAether < neededAether)
            {
                Debug.LogWarning($"LaunchUIController: Not enough aether! Need {neededAether}, have {currentAether}");
                return;
            }

            if (neededAether > 0)
            {
                ResourceManager.Instance.RemoveResource(ResourceType.Aether, neededAether);
            }
        }

        inventorySystem.SetTransferEnabled(false);

        if (launchPanel != null)
        {
            launchPanel.SetActive(false);
        }

        if (countdownPanel != null)
        {
            countdownPanel.SetActive(true);
        }

        _isCountingDown = true;

        if (_countdownCoroutine != null)
        {
            StopCoroutine(_countdownCoroutine);
        }
        _countdownCoroutine = StartCoroutine(CountdownCoroutine());
    }

    private IEnumerator CountdownCoroutine()
    {
        float remaining = Mathf.Max(0f, countdownDurationSeconds);

        while (remaining > 0f)
        {
            UpdateCountdownText(remaining);
            yield return null;
            remaining -= Time.deltaTime;
        }

        UpdateCountdownText(0f);

        if (countdownPanel != null)
        {
            countdownPanel.SetActive(false);
        }

        MainStructure mainStructure = FindFirstObjectByType<MainStructure>();
        if (mainStructure != null)
        {
            InventorySystem inventorySystem = mainStructure.GetComponent<InventorySystem>();
            if (inventorySystem != null)
            {
                inventorySystem.TransferAllToBaseInventory();
            }
        }

        if (launchCompleteUI != null)
        {
            launchCompleteUI.Show();
            yield return new WaitForSeconds(launchCompleteDisplayDuration);
        }

        _isCountingDown = false;
        _countdownCoroutine = null;

        SceneLoader.Instance.LoadBaseScene();
    }

    private void UpdateCountdownText(float remainingSeconds)
    {
        if (countdownText == null)
        {
            return;
        }

        int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(remainingSeconds));
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        countdownText.text = $"{minutes:00} : {seconds:00}";
    }
}


