using System.Collections;
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Systems.Jobs;

public class GameManager : MonoBehaviour
{
    public MapGenerator mapGenerator;
    public UIManager uiManager;
    public CardDragger cardDragger;

    [Header("UI Elements")]
    [SerializeField] private GameObject pausePanel;

    [HideInInspector] public UnityEvent<DisplayableData> onStartDrag;
    [HideInInspector] public UnityEvent onEndDrag;
    
    public static event Action OnGameSceneInitialized;
    
    private DisplayableData _activeCardData;

    private bool _isPaused;
    private float _savedTimeScale = 1f;
    private bool _isGameSceneInitialized = false;
    public static GameManager Instance { get; private set; }
    public bool IsPaused => _isPaused;
    public bool IsGameSceneInitialized => _isGameSceneInitialized;
    
    public float GetTimeScale()
    {
        return _isPaused ? _savedTimeScale : Time.timeScale;
    }

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

        if (Input.GetKeyDown(KeyCode.Alpha1)) {
            _savedTimeScale = 1f;
            if (!_isPaused) Time.timeScale = 1f;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2)) {
            _savedTimeScale = 2f;
            if (!_isPaused) Time.timeScale = 2f;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3)) {
            _savedTimeScale = 3f;
            if (!_isPaused) Time.timeScale = 3f;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4)) {
            _savedTimeScale = 4f;
            if (!_isPaused) Time.timeScale = 4f;
        }

        if (_isPaused) return;

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
        
        if (_isPaused) {
            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }
        else {
            Time.timeScale = _savedTimeScale;
        }

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
            _savedTimeScale = 1f;
            Time.timeScale = 1f;
            _isGameSceneInitialized = false;
            InitializeGameScene();
        }
        if (scene.name == "LightTestScene") {
            _isPaused = false;
            _savedTimeScale = 1f;
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

        // Get progress tracker from LoadingScreen
        IInitializationProgress progress = GetInitializationProgress();
        
        StartCoroutine(InitializeGameSceneAsync(progress));
    }
    
    private IInitializationProgress GetInitializationProgress()
    {
        if (LoadingUIManager.Instance != null)
        {
            return LoadingUIManager.Instance.GetProgressTracker();
        }
        return null;
    }
    
    private IEnumerator InitializeGameSceneAsync(IInitializationProgress progress = null)
    {
        yield return null;
        
        // Step 1: Generate Map
        if (mapGenerator != null)
        {
            yield return StartCoroutine(mapGenerator.GenerateMapAsync(progress));
        }
        
        CameraTargetController cameraController = FindFirstObjectByType<CameraTargetController>();
        if (cameraController != null)
        {
            cameraController.RefreshMapBounds();
        }
        
        yield return StartCoroutine(InitializeSpawnersAndUnitsAsync(progress));
        
        // Step 3: Wait for Fog of War
        yield return StartCoroutine(WaitForFogOfWarInitializationAsync(progress));
        
        UpdateEnemyVisibility();
        
        if (progress != null)
        {
            progress.UpdateProgress(0.0f, "착륙 좌표 고정 중...");
            yield return new WaitForSeconds(1.0f);
        }
        
        _isGameSceneInitialized = true;
        OnGameSceneInitialized?.Invoke();
        
        if (LoadingUIManager.Instance != null)
        {
            LoadingScreen loadingScreen = LoadingUIManager.Instance.GetLoadingScreenComponent();
            if (loadingScreen != null)
            {
                loadingScreen.SetInitializationComplete();
            }
        }
    }
    
    private class ProgressTracker : IInitializationProgress
    {
        private readonly IInitializationProgress _parent;
        private readonly float _startProgress;
        private readonly float _progressRange;
        
        public ProgressTracker(IInitializationProgress parent, float startProgress, float progressRange)
        {
            _parent = parent;
            _startProgress = startProgress;
            _progressRange = progressRange;
        }
        
        public void UpdateProgress(float progress, string stage)
        {
            if (_parent != null)
            {
                float mappedProgress = _startProgress + (progress * _progressRange);
                _parent.UpdateProgress(mappedProgress, stage);
            }
        }
    }

    private IEnumerator DelayedInitialization()
    {
        yield return null;

        yield return StartCoroutine(InitializeSpawnersAndUnits());
        
        yield return StartCoroutine(WaitForFogOfWarInitialization());
        
        UpdateEnemyVisibility();
        
        _isGameSceneInitialized = true;
        OnGameSceneInitialized?.Invoke();
    }
    
    private IEnumerator WaitForFogOfWarInitializationAsync(IInitializationProgress progress = null)
    {
        if (FogOfWarManager.Instance != null)
        {
            IInitializationProgress fogProgress = progress;
            FogOfWarManager.Instance.StartFogInitializationWithProgress(fogProgress);
        }
        
        while (FogOfWarManager.Instance == null || !FogOfWarManager.Instance.IsInitialized)
        {
            yield return null;
        }
        
        yield return null;
    }
    
    private IEnumerator WaitForFogOfWarInitialization()
    {
        while (FogOfWarManager.Instance == null || !FogOfWarManager.Instance.IsInitialized)
        {
            yield return null;
        }
        
        yield return null;
    }
    
    private void UpdateEnemyVisibility()
    {
        if (FogOfWarManager.Instance == null || !FogOfWarManager.Instance.IsInitialized)
        {
            return;
        }
        
        if (UnitManager.Instance != null && UnitManager.Instance.EnemyUnits != null)
        {
            foreach (UnitBase enemyUnit in UnitManager.Instance.EnemyUnits)
            {
                if (enemyUnit != null)
                {
                    VisibilityController controller = enemyUnit.GetComponent<VisibilityController>();
                    if (controller == null)
                    {
                        controller = enemyUnit.GetComponentInChildren<VisibilityController>();
                    }
                    
                    if (controller != null)
                    {
                        controller.ForceUpdateVisibility();
                    }
                }
            }
        }
        
        VisibilityController[] allVisibilityControllers = FindObjectsByType<VisibilityController>(FindObjectsSortMode.None);
        
        foreach (VisibilityController controller in allVisibilityControllers)
        {
            if (controller != null)
            {
                EnemyUnitBase enemyUnit = controller.GetComponent<EnemyUnitBase>();
                if (enemyUnit == null)
                {
                    enemyUnit = controller.GetComponentInParent<EnemyUnitBase>();
                }
                
                if (enemyUnit != null)
                {
                    controller.ForceUpdateVisibility();
                }
            }
        }
    }

    private IEnumerator InitializeSpawnersAndUnitsAsync(IInitializationProgress progress = null)
    {
        foreach (BuildingSpawner spawner in FindObjectsByType<BuildingSpawner>(FindObjectsSortMode.None)) {
            spawner.SpawnBuildings();
            if (spawner.BuildingTilemap != null) spawner.BuildingTilemap.gameObject.SetActive(false);
        }

        RegisterPrePlacedMainStructure();
        RegisterPrePlacedBuildings();
        
        // Spawn starting units after MainStructure is registered
        yield return null;
        
        StartingUnitsManager startingUnitsManager = FindFirstObjectByType<StartingUnitsManager>();
        if (startingUnitsManager != null)
        {
            startingUnitsManager.SpawnStartingUnits();
        }
        
        MapObjectSpawner proceduralSpawner = FindFirstObjectByType<MapObjectSpawner>();
        if (proceduralSpawner != null) {
            yield return StartCoroutine(proceduralSpawner.SpawnResourcesAsync(progress));
        }

        yield return null;
        yield return null;
        
        if (mapGenerator != null)
        {
            mapGenerator.GenerateEnemyTerritoryRadiusValues();
        }
        
        foreach (EnemySpawner enemySpawner in FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None))
        {
            enemySpawner.SpawnEnemies();
        }
        
        if (mapGenerator != null)
        {
            mapGenerator.DrawEnemyTerritoryTiles();
        }
    }
    
    private IEnumerator InitializeSpawnersAndUnits()
    {
        foreach (BuildingSpawner spawner in FindObjectsByType<BuildingSpawner>(FindObjectsSortMode.None)) {
            spawner.SpawnBuildings();
            if (spawner.BuildingTilemap != null) spawner.BuildingTilemap.gameObject.SetActive(false);
        }

        RegisterPrePlacedMainStructure();
        RegisterPrePlacedBuildings();
        
        // Spawn starting units after MainStructure is registered
        yield return null;
        StartingUnitsManager startingUnitsManager = FindFirstObjectByType<StartingUnitsManager>();
        if (startingUnitsManager != null)
        {
            startingUnitsManager.SpawnStartingUnits();
        }

        MapObjectSpawner proceduralSpawner = FindFirstObjectByType<MapObjectSpawner>();
        if (proceduralSpawner != null) {
            proceduralSpawner.SpawnResources();
        }

        yield return null;
        yield return null;
        
        if (mapGenerator != null)
        {
            mapGenerator.GenerateEnemyTerritoryRadiusValues();
        }
        
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
