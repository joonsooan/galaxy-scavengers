using System;
using System.Collections;
using FMODUnity;
using Systems.Jobs;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    private const float CombatLockDuration = 5f;
    public MapGenerator mapGenerator;
    public UIManager uiManager;
    public CardDragger cardDragger;


    [Header("Audio")]
    [SerializeField] private EventReference pauseSound;
    [SerializeField] private EventReference resumeSound;

    [HideInInspector] public UnityEvent<DisplayableData> onStartDrag;
    [HideInInspector] public UnityEvent onEndDrag;

    private DisplayableData _activeCardData;
    private float _combatLockTimer;

    private bool _isCombatSpeedLockActive;

    private float _savedTimeScale = 1f;
    private static readonly WaitForSeconds _wait05 = CoroutineCache.GetWaitForSeconds(0.5f);
    public static GameManager Instance { get; private set; }
    public bool IsPaused { get; private set; }

    public bool IsGameSceneInitialized { get; private set; }

    public static bool IsGameplayReady { get; set; } = true;

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
        UpdateCombatSpeedLock();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        Damageable.OnAnyDamageTaken += HandleAnyDamageTaken;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Damageable.OnAnyDamageTaken -= HandleAnyDamageTaken;
    }

    public static event Action OnGameSceneInitialized;

    public float GetTimeScale()
    {
        return IsPaused ? _savedTimeScale : Time.timeScale;
    }

    private void HandleAnyDamageTaken(Damageable damageable)
    {
        _isCombatSpeedLockActive = true;
        _combatLockTimer = CombatLockDuration;

        if (!IsPaused && Time.timeScale != 1f) {
            Time.timeScale = 1f;
        }
    }

    private void UpdateCombatSpeedLock()
    {
        if (!_isCombatSpeedLockActive) return;

        _combatLockTimer -= Time.unscaledDeltaTime;
        if (_combatLockTimer <= 0f) {
            _isCombatSpeedLockActive = false;

            if (!IsPaused) {
                Time.timeScale = _savedTimeScale;
            }
        }
    }

    private void HandleGameInput()
    {
        if (!IsGameSceneInitialized) return;

        if (!IsGameplayReady) return;

        if (IsLoadingScreenActive()) return;

        GameMenuManager gameMenuManager = FindFirstObjectByType<GameMenuManager>();
        if (gameMenuManager != null && gameMenuManager.IsMenuOpen()) {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Space)) {
            TogglePause();
        }

        bool canChangeActualTimeScale = !_isCombatSpeedLockActive && !IsPaused;

        if (Input.GetKeyDown(KeyCode.Alpha1)) {
            _savedTimeScale = 1f;
            if (canChangeActualTimeScale) Time.timeScale = 1f;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2)) {
            _savedTimeScale = 2f;
            if (canChangeActualTimeScale) Time.timeScale = 2f;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3)) {
            _savedTimeScale = 3f;
            if (canChangeActualTimeScale) Time.timeScale = 3f;
        }

        if (IsPaused) return;

#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.F)) {
            if (FogOfWarManager.Instance != null) {
                FogOfWarManager.Instance.ToggleFogVisibility();
            }
        }
        if (Input.GetKeyDown(KeyCode.T)) {
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive()) {
                TutorialManager.Instance.SkipAllTutorials();
            }
        }
#endif
    }

    public void TogglePause()
    {
        IsPaused = !IsPaused;

        if (IsPaused) {
            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            if (!pauseSound.IsNull) {
                RuntimeManager.PlayOneShot(pauseSound);
            }
        }
        else {
            Time.timeScale = _savedTimeScale;
            if (!resumeSound.IsNull) {
                RuntimeManager.PlayOneShot(resumeSound);
            }
        }

        if (uiManager != null) {
            uiManager.SetPausePanelActive(IsPaused);
        }
    }

    public void GameOver()
    {
        Time.timeScale = 1;
        if (SceneLoader.Instance != null) {
            SceneLoader.Instance.LoadBaseScene(SceneLoader.ReturnFromGameState.Failure);
        }
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
            IsPaused = false;
            _savedTimeScale = 1f;
            Time.timeScale = 1f;
            IsGameSceneInitialized = false;
            IsGameplayReady = false;
            InitializeGameScene();
        }
        if (scene.name == "LightTestScene") {
            IsPaused = false;
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

        StartCoroutine(WaitForEntryAnimationAndInitialize());
    }

    private IEnumerator WaitForEntryAnimationAndInitialize()
    {
        while (LoadingUIManager.Instance == null) {
            yield return null;
        }

        LoadingScreen loadingScreen = LoadingUIManager.Instance.GetLoadingScreenComponent();
        while (loadingScreen == null || !loadingScreen.IsEntryAnimationComplete) {
            yield return null;
            if (LoadingUIManager.Instance != null) {
                loadingScreen = LoadingUIManager.Instance.GetLoadingScreenComponent();
            }
        }

        yield return _wait05;

        IInitializationProgress progress = GetInitializationProgress();
        yield return StartCoroutine(InitializeGameSceneAsync(progress));
    }

    private IInitializationProgress GetInitializationProgress()
    {
        if (LoadingUIManager.Instance != null) {
            return LoadingUIManager.Instance.GetProgressTracker();
        }
        return null;
    }

    private IEnumerator InitializeGameSceneAsync(IInitializationProgress progress = null)
    {
        if (ObjectPooler.Instance != null)
        {
            ObjectPooler.Instance.InitializePools();
        }

        yield return null;

        // Step 1: Generate Map
        if (mapGenerator != null) {
            yield return StartCoroutine(mapGenerator.GenerateMapAsync(progress));
        }

        CameraTargetController cameraController = FindFirstObjectByType<CameraTargetController>();
        if (cameraController != null) {
            cameraController.RefreshMapBounds();
        }

        yield return StartCoroutine(InitializeSpawnersAndUnitsAsync(progress));

        // Step 3: Wait for Fog of War
        yield return StartCoroutine(WaitForFogOfWarInitializationAsync(progress));

        if (progress != null) {
            progress.UpdateProgress(0.0f, "착륙 좌표 고정 중...");
            yield return _wait05;
        }

        if (CoreRepairManager.Instance != null) {
            CoreRepairManager.Instance.InitializeLanding();
        }

        IsGameSceneInitialized = true;
        OnGameSceneInitialized?.Invoke();

        if (LoadingUIManager.Instance != null) {
            LoadingScreen loadingScreen = LoadingUIManager.Instance.GetLoadingScreenComponent();
            if (loadingScreen != null) {
                loadingScreen.SetInitializationComplete();
            }
        }

        IsGameplayReady = true;
    }

    private IEnumerator DelayedInitialization()
    {
        yield return null;

        yield return StartCoroutine(InitializeSpawnersAndUnits());

        yield return StartCoroutine(WaitForFogOfWarInitialization());

        if (FogOfWarManager.Instance != null) {
            FogOfWarManager.Instance.RefreshFogOfWar();
        }

        IsGameSceneInitialized = true;
        OnGameSceneInitialized?.Invoke();
    }

    private IEnumerator WaitForFogOfWarInitializationAsync(IInitializationProgress progress = null)
    {
        if (FogOfWarManager.Instance != null) {
            IInitializationProgress fogProgress = progress;
            FogOfWarManager.Instance.StartFogInitializationWithProgress(fogProgress);
        }

        while (FogOfWarManager.Instance == null || !FogOfWarManager.Instance.IsInitialized) {
            yield return null;
        }

        yield return null;
    }

    private IEnumerator WaitForFogOfWarInitialization()
    {
        while (FogOfWarManager.Instance == null || !FogOfWarManager.Instance.IsInitialized) {
            yield return null;
        }

        yield return null;
    }

    private IEnumerator InitializeSpawnersAndUnitsAsync(IInitializationProgress progress = null)
    {
        foreach (BuildingSpawner spawner in FindObjectsByType<BuildingSpawner>(FindObjectsSortMode.None)) {
            spawner.SpawnBuildings();
            if (spawner.BuildingTilemap != null) spawner.BuildingTilemap.gameObject.SetActive(false);
        }

        RegisterPrePlacedMainStructure();
        RegisterPrePlacedBuildings();

        MapObjectSpawner proceduralSpawner = FindFirstObjectByType<MapObjectSpawner>();
        if (proceduralSpawner != null) {
            yield return StartCoroutine(proceduralSpawner.SpawnResourcesAsync(progress));
        }

        yield return null;
        yield return null;

        if (mapGenerator != null) {
            mapGenerator.GenerateEnemyTerritoryRadiusValues();
        }

        if (mapGenerator != null) {
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

        MapObjectSpawner proceduralSpawner = FindFirstObjectByType<MapObjectSpawner>();
        if (proceduralSpawner != null) {
            proceduralSpawner.SpawnResources();
        }

        yield return null;
        yield return null;

        if (mapGenerator != null) {
            mapGenerator.GenerateEnemyTerritoryRadiusValues();
        }

        if (mapGenerator != null) {
            mapGenerator.DrawEnemyTerritoryTiles();
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

                    // Ensure MainStructure is always registered as a valid enemy target
                    if (TargetManager.Instance != null) {
                        TargetManager.Instance.RegisterTarget(mainStructure);
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

    public void SpawnUnitsAfterLoading()
    {
        // Spawn starting units after MainStructure is registered
        StartingUnitsManager startingUnitsManager = FindFirstObjectByType<StartingUnitsManager>();
        if (startingUnitsManager != null) {
            startingUnitsManager.SpawnStartingUnits();
        }
    }

    private bool IsLoadingScreenActive()
    {
        if (LoadingUIManager.Instance == null) {
            return false;
        }

        LoadingScreen loadingScreen = LoadingUIManager.Instance.GetLoadingScreenComponent();
        if (loadingScreen == null) {
            return false;
        }

        return loadingScreen.gameObject.activeSelf;
    }
}
