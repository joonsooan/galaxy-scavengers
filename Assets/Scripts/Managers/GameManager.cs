using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public MapGenerator mapGenerator;
    public UIManager uiManager;
    public CardDragger cardDragger;

    [Header("UI Elements")]
    [SerializeField] private GameObject pausePanel;

    [HideInInspector] public UnityEvent<DisplayableData> onStartDrag;
    [HideInInspector] public UnityEvent onEndDrag;
    private DisplayableData _activeCardData;

    private bool _isPaused;
    public static GameManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        HandleGameInput();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void HandleGameInput()
    {
        if (SceneManager.GetActiveScene().name != "GameScene") return;

        if (Input.GetKeyDown(KeyCode.Space)) {
            TogglePause();
        }

        if (_isPaused) return;

        if (Input.GetKeyDown(KeyCode.Alpha1)) Time.timeScale = 1;
        else if (Input.GetKeyDown(KeyCode.Alpha2)) Time.timeScale = 2;
        else if (Input.GetKeyDown(KeyCode.Alpha3)) Time.timeScale = 3;
        else if (Input.GetKeyDown(KeyCode.Alpha4)) Time.timeScale = 4;

        if (Input.GetKeyDown(KeyCode.F))
        {
            if (FogOfWarManager.Instance != null)
            {
                FogOfWarManager.Instance.ToggleFogVisibility();
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape)) {
            Application.Quit();
        }
    }

    private void TogglePause()
    {
        _isPaused = !_isPaused;
        Time.timeScale = _isPaused ? 0f : 1f;

        if (pausePanel != null) {
            pausePanel.SetActive(_isPaused);
        }
    }

    public void GameOver()
    {
        Time.timeScale = 1;
        SceneLoader.Instance.LoadBaseScene();
    }

    public void StartDrag(DisplayableData data)
    {
        if (cardDragger == null) return;

        _activeCardData = data;
        cardDragger.StartDrag(_activeCardData);
        onStartDrag?.Invoke(_activeCardData);
    }

    public void EndDrag()
    {
        if (uiManager != null) {
            uiManager.UnpinAndHideAllPanels();
        }
        
        if (cardDragger != null) {
            cardDragger.EndDrag();
        }
        _activeCardData = null;
        onEndDrag?.Invoke();
    }

    public bool IsDragging()
    {
        return cardDragger != null && cardDragger.IsDragging;
    }

    public DisplayableData GetActiveData()
    {
        return _activeCardData;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "GameScene") {
            _isPaused = false;
            Time.timeScale = 1f;
            InitializeGameScene();
        }
        if (scene.name == "LightTestScene") {
            _isPaused = false;
            Time.timeScale = 1f;
            
            StartCoroutine(DelayedInitialization());
        }
    }

    private void InitializeGameScene()
    {
        mapGenerator = FindFirstObjectByType<MapGenerator>();
        uiManager = FindFirstObjectByType<UIManager>();
        cardDragger = FindFirstObjectByType<CardDragger>();

        GameObject pausePanelObject = GameObject.Find("PausePanel");
        if (pausePanelObject != null) {
            pausePanel = pausePanelObject;
            pausePanel.SetActive(false);
        }

        mapGenerator?.GenerateMap();

        StartCoroutine(DelayedInitialization());
    }

    private IEnumerator DelayedInitialization()
    {
        yield return null;

        yield return StartCoroutine(InitializeSpawnersAndUnits());
    }

    private IEnumerator InitializeSpawnersAndUnits()
    {
        foreach (BuildingSpawner spawner in FindObjectsByType<BuildingSpawner>(FindObjectsSortMode.None)) {
            spawner.SpawnBuildings();
            if (spawner.BuildingTilemap != null) spawner.BuildingTilemap.gameObject.SetActive(false);
        }

        RegisterPrePlacedMainStructure();
        RegisterPrePlacedBuildings();

        MapObjectSpawner proceduralSpawner = FindFirstObjectByType<MapObjectSpawner>();
        if (proceduralSpawner != null) {
            proceduralSpawner.SpawnResources();
        }

        yield return null;
        yield return null;
        
        foreach (EnemySpawner enemySpawner in FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None))
        {
            enemySpawner.SpawnEnemies();
        }
        
        if (mapGenerator != null)
        {
            mapGenerator.DrawEnemyTerritoryTiles();
        }

        foreach (Unit_Miner unit in FindObjectsByType<Unit_Miner>(FindObjectsSortMode.None)) {
            unit.TryStartActions();
        }
    }

    private void RegisterPrePlacedMainStructure()
    {
        MainStructure[] existingMainStructures = FindObjectsByType<MainStructure>(FindObjectsSortMode.None);

        foreach (MainStructure mainStructure in existingMainStructures) {
            if (ResourceManager.Instance != null) {
                bool alreadyRegistered = false;
                foreach (IStorage storage in ResourceManager.Instance.GetAllStorages()) {
                    if ((MainStructure)storage == mainStructure) {
                        alreadyRegistered = true;
                        break;
                    }
                }

                if (!alreadyRegistered) {
                    ResourceManager.Instance.RegisterMainStructure(mainStructure);
                    ResourceManager.Instance.AddStorage(mainStructure);

                    if (BuildingManager.Instance != null && mainStructure.transform != null) {
                        Vector3 worldPos = mainStructure.transform.position;
                        if (BuildingManager.Instance.grid != null) {
                            Vector3Int cellPos = BuildingManager.Instance.grid.WorldToCell(worldPos);
                            Vector3Int anchorCell = cellPos - new Vector3Int(1, 1, 0);
                            BuildingManager.Instance.RegisterMainStructure(anchorCell, new Vector2Int(3, 3));
                        }
                    }
                }
            }
        }
    }

    private void RegisterPrePlacedBuildings()
    {
        BuildingPiece[] existingBuildings = FindObjectsByType<BuildingPiece>(FindObjectsSortMode.None);

        if (BuildingManager.Instance == null) {
            Debug.LogWarning("[GameManager] BuildingManager.Instance is null, cannot register pre-placed buildings.");
            return;
        }

        foreach (BuildingPiece buildingPiece in existingBuildings) {
            if (buildingPiece.GetComponent<MainStructure>() != null) {
                continue;
            }

            BuildingManager.Instance.RegisterPrePlacedBuilding(buildingPiece);
        }
    }
}
