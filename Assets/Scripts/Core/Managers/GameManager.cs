using System;
using System.Collections;
using DG.Tweening;
using FMODUnity;
using Systems.Jobs;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public MapGenerator mapGenerator;
    public UIManager uiManager;
    public CardDragger cardDragger;


    [Header("Debug")]
    [SerializeField] private bool skipProceduralGenerationWhenLoadingGameScene;

    [Header("Audio")]
    [SerializeField] private EventReference pauseSound;
    [SerializeField] private EventReference resumeSound;

    [Header("Game Scene Opening Sequence")]
    [SerializeField][Min(0f)] private float openingPanDuration = 0.35f;
    [SerializeField][Min(0f)] private float openingCenterDwellSeconds = 0.5f;
    [SerializeField][Min(0f)] private float openingRuinsDwellSeconds = 2.5f;

    [HideInInspector] public UnityEvent<DisplayableData> onStartDrag;
    [HideInInspector] public UnityEvent onEndDrag;

    private DisplayableData _activeCardData;
    private float _lastDragEndUnscaledTime = -999f;

    private float _savedTimeScale = 1f;
    private static readonly WaitForSeconds _wait05 = CoroutineCache.GetWaitForSeconds(0.5f);
    public static GameManager Instance { get; private set; }
    public bool IsPaused { get; private set; }

    public bool IsGameSceneInitialized { get; private set; }

    public static bool IsGameplayReady { get; set; } = true;
    private bool _isGameOverProcessing;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
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
        Damageable.OnAnyDamageTaken += HandleAnyDamageTaken;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Damageable.OnAnyDamageTaken -= HandleAnyDamageTaken;
    }

    public static event Action OnGameSceneInitialized;
    public static event Action<bool> OnPauseStateChanged;

    public float GetTimeScale()
    {
        return IsPaused ? _savedTimeScale : Time.timeScale;
    }

    private void HandleAnyDamageTaken(Damageable damageable)
    {
    }

    private void HandleGameInput()
    {
        if (!IsGameSceneInitialized) return;

        if (!IsGameplayReady) return;

        if (IsLoadingScreenActive()) return;

#if UNITY_EDITOR
        HandleDebugTimeScale();
#endif

        if (GameMenuManager.Instance != null && GameMenuManager.Instance.IsMenuOpen())
        {
            return;
        }

        LaunchUIController launchUIController = FindFirstObjectByType<LaunchUIController>(FindObjectsInactive.Include);
        bool isPauseInputLocked = launchUIController != null && launchUIController.IsPauseInputLocked();
        bool isLaunchMenuInputBlocked = launchUIController != null && launchUIController.IsMenuInputBlocked();
        bool isCountdownSequenceActive = launchUIController != null && launchUIController.IsCountdownSequenceActive();

        if (!isPauseInputLocked && !isCountdownSequenceActive && Input.GetKeyDown(KeyCode.Space))
        {
            TogglePause();
        }

        if (IsPaused) return;

#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.F2))
        {
            if (FogOfWarManager.Instance != null)
            {
                FogOfWarManager.Instance.ToggleFogVisibility();
            }
        }
        if (Input.GetKeyDown(KeyCode.G))
        {
            UnitData constructUnit = Resources.Load<UnitData>("Unit Data/Unit_Construct");
            if (constructUnit != null && StartingUnitsManager.Instance != null)
            {
                StartingUnitsManager.Instance.SpawnUnitsForEditor(constructUnit, 1);
            }
        }
#endif
    }

#if UNITY_EDITOR
    private void HandleDebugTimeScale()
    {
        float targetScale = -1f;
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            targetScale = 1f;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            targetScale = 2f;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            targetScale = 4f;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            targetScale = 10f;
        }

        if (targetScale < 0f)
        {
            return;
        }

        _savedTimeScale = targetScale;
        if (!IsPaused)
        {
            Time.timeScale = targetScale;
        }
    }
#endif

    public void TogglePause()
    {
        IsPaused = !IsPaused;

        if (IsPaused)
        {
            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            if (!pauseSound.IsNull)
            {
                RuntimeManager.PlayOneShot(pauseSound);
            }
        }
        else
        {
            Time.timeScale = _savedTimeScale;
            if (!resumeSound.IsNull)
            {
                RuntimeManager.PlayOneShot(resumeSound);
            }
        }

        if (uiManager != null)
        {
            uiManager.SetPausePanelActive(IsPaused);
        }

        OnPauseStateChanged?.Invoke(IsPaused);
    }

    public void GameOver(Transform gameOverFocusTarget = null)
    {
        if (_isGameOverProcessing) return;
        _isGameOverProcessing = true;
        FocusCameraOnGameOverTarget(gameOverFocusTarget);
        IsGameplayReady = false;
        HideAllGameplayUiForGameOver();

        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadBaseScene(SceneLoader.ReturnFromGameState.Failure);
        }
    }

    public void OnGameOverScreenFullyShown()
    {
        Time.timeScale = 1f;
    }

    private void FocusCameraOnGameOverTarget(Transform explicitTarget)
    {
        CameraTargetController cameraTargetController = FindFirstObjectByType<CameraTargetController>(FindObjectsInactive.Include);
        if (cameraTargetController == null) return;

        Transform target = explicitTarget;
        if (target == null)
        {
            MainStructure mainStructure = FindFirstObjectByType<MainStructure>(FindObjectsInactive.Include);
            if (mainStructure != null)
            {
                target = mainStructure.transform;
            }
        }

        if (target != null)
        {
            cameraTargetController.SetFollowTarget(target);
        }
    }

    private void HideAllGameplayUiForGameOver()
    {
        if (uiManager != null)
        {
            uiManager.UnpinAndHideAllPanels();
            uiManager.SetPausePanelLock(false);
            uiManager.SetPausePanelActive(false);
        }

        MainControlPanel mainControlPanel = FindFirstObjectByType<MainControlPanel>(FindObjectsInactive.Include);
        if (mainControlPanel != null)
        {
            mainControlPanel.HideAllPanels();
        }

        SetMainCanvasVisible(false);
    }

    public void SetMainCanvasVisible(bool visible)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        Canvas[] sceneCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas canvas in sceneCanvases)
        {
            if (canvas == null || canvas.gameObject == null) continue;
            if (canvas.gameObject.scene != activeScene) continue;
            if (canvas.gameObject.name == "Main Canvas")
            {
                canvas.gameObject.SetActive(visible);
                break;
            }
        }
    }

    public IEnumerator RunGameSceneOpeningSequence()
    {
        CameraTargetController ctc = FindFirstObjectByType<CameraTargetController>();
        if (ctc == null)
        {
            yield break;
        }

        if (mapGenerator == null)
        {
            mapGenerator = FindFirstObjectByType<MapGenerator>();
        }

        ctc.BeginOpeningSequence();
        bool skipRequested = false;

        Vector3 startFocus = ctc.transform.position;
        bool hasRuins = mapGenerator != null && mapGenerator.AncientRuinsCells != null && mapGenerator.AncientRuinsCells.Count > 0;
        Vector3 ruins = hasRuins ? mapGenerator.GetAncientRuinsFocusWorldPosition(startFocus) : startFocus;
        float distanceToRuins = Vector2.Distance(new Vector2(startFocus.x, startFocus.y), new Vector2(ruins.x, ruins.y));

        if (openingCenterDwellSeconds > 0f)
        {
            float elapsed = 0f;
            while (elapsed < openingCenterDwellSeconds)
            {
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    skipRequested = true;
                    break;
                }
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        bool shouldMoveToRuins = hasRuins && distanceToRuins > 0.05f;
        if (!skipRequested && shouldMoveToRuins)
        {
            Tween toRuins = ctc.TweenRigToWorldXY(ruins, openingPanDuration);
            while (toRuins.IsActive() && toRuins.IsPlaying())
            {
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    skipRequested = true;
                    toRuins.Kill();
                    break;
                }
                yield return null;
            }
        }

        if (!skipRequested && openingRuinsDwellSeconds > 0f)
        {
            float elapsed = 0f;
            while (elapsed < openingRuinsDwellSeconds)
            {
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    skipRequested = true;
                    break;
                }
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        Unit_Player mainUnit = FindFirstObjectByType<Unit_Player>(FindObjectsInactive.Include);
        if (mainUnit != null)
        {
            if (skipRequested)
            {
                ctc.SnapRigToWorldXY(mainUnit.transform.position);
            }
            else
            {
                Tween toMainUnit = ctc.TweenRigToWorldXY(mainUnit.transform.position, openingPanDuration);
                while (toMainUnit.IsActive() && toMainUnit.IsPlaying())
                {
                    if (Input.GetKeyDown(KeyCode.Space))
                    {
                        skipRequested = true;
                        toMainUnit.Kill();
                        ctc.SnapRigToWorldXY(mainUnit.transform.position);
                        break;
                    }
                    yield return null;
                }
            }
            ctc.SetFollowTargetImmediate(mainUnit.transform);
        }
    }

    public void EndGameSceneOpeningSequence()
    {
        CameraTargetController ctc = FindFirstObjectByType<CameraTargetController>();
        if (ctc != null)
        {
            ctc.EndOpeningSequence();
        }
    }

    public IEnumerator WarmUpGameplayUiCoroutine()
    {
        yield return null;
        if (BuildingInfoPanel.Instance != null)
        {
            BuildingInfoPanel.Instance.WarmupFirstUse();
        }
        if (ResourceInfoPanel.Instance != null)
        {
            ResourceInfoPanel.Instance.WarmupFirstUse();
        }
        if (UnitInfoPanel.Instance != null)
        {
            UnitInfoPanel.Instance.WarmupFirstUse();
        }
        yield return null;
        Canvas.ForceUpdateCanvases();
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
        if (uiManager != null)
        {
            uiManager.UnpinAndHideAllPanels();
        }

        if (cardDragger != null)
        {
            cardDragger.EndDrag();
        }
        _activeCardData = null;
        _lastDragEndUnscaledTime = Time.unscaledTime;
        onEndDrag?.Invoke();
    }

    public bool WasDragEndedRecently(float duration)
    {
        return Time.unscaledTime - _lastDragEndUnscaledTime <= duration;
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
        if (scene.name == "GameScene" || scene.name == "TutorialScene")
        {
            UnitProcessResourceStatTracker.ResetAllStats();
            _isGameOverProcessing = false;
            IsPaused = false;
            _savedTimeScale = 1f;
            Time.timeScale = 1f;
            OnPauseStateChanged?.Invoke(false);
            IsGameSceneInitialized = false;
            IsGameplayReady = false;
            if (BuildingManager.Instance != null)
            {
                BuildingManager.Instance.ClearWalkableCellCache();
            }
            InitializeGameScene();
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
        while (LoadingUIManager.Instance == null)
        {
            yield return null;
        }

        LoadingScreen loadingScreen = LoadingUIManager.Instance.GetLoadingScreenComponent();
        while (loadingScreen == null || !loadingScreen.IsEntryAnimationComplete)
        {
            yield return null;
            if (LoadingUIManager.Instance != null)
            {
                loadingScreen = LoadingUIManager.Instance.GetLoadingScreenComponent();
            }
        }

        yield return _wait05;

        IInitializationProgress progress = GetInitializationProgress();
        yield return StartCoroutine(InitializeGameSceneAsync(progress));
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
        if (ObjectPooler.Instance != null)
        {
            ObjectPooler.Instance.InitializePools();
        }

        yield return null;

        if (mapGenerator != null)
        {
            mapGenerator.PrepareStaticSceneMapMetadata();
            if (!skipProceduralGenerationWhenLoadingGameScene)
            {
                yield return StartCoroutine(mapGenerator.GenerateMapAsync(progress));
            }
        }

        if (BuildingManager.Instance != null && mapGenerator != null)
        {
            BuildingManager.Instance.InitializeWalkableCellCache(mapGenerator.GetMapBounds());
        }

        yield return StartCoroutine(InitializeSpawnersAndUnitsAsync(progress, skipProceduralGenerationWhenLoadingGameScene));
        yield return StartCoroutine(WaitForFogOfWarInitializationAsync(progress));

        if (progress != null)
        {
            progress.UpdateProgress(
                0.0f,
                GameLocalization.GetOrDefault("UI_Common", "loading.stage.lockLandingCoordinates", "착륙 좌표 고정 중..."),
                "loading.stage.lockLandingCoordinates");
            yield return _wait05;
        }

        IsGameSceneInitialized = true;
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

    private IEnumerator InitializeSpawnersAndUnitsAsync(IInitializationProgress progress = null, bool skipProceduralResourceSpawn = false)
    {
        foreach (BuildingSpawner spawner in FindObjectsByType<BuildingSpawner>(FindObjectsSortMode.None))
        {
            spawner.SpawnBuildings();
            if (spawner.BuildingTilemap != null) spawner.BuildingTilemap.gameObject.SetActive(false);
        }

        RegisterPrePlacedMainStructure();
        RegisterPrePlacedBuildings();

        MapObjectSpawner proceduralSpawner = FindFirstObjectByType<MapObjectSpawner>();
        if (proceduralSpawner != null && !skipProceduralResourceSpawn)
        {
            yield return StartCoroutine(proceduralSpawner.SpawnResourcesAsync(progress));
        }

        yield return null;
        yield return null;

        if (mapGenerator != null)
        {
            mapGenerator.GenerateEnemyTerritoryRadiusValues();
        }

        if (mapGenerator != null)
        {
            mapGenerator.DrawEnemyTerritoryTiles();
            mapGenerator.SpawnAncientRuins();
        }
    }

    private void RegisterPrePlacedMainStructure()
    {
        MainStructure[] existingMainStructures = FindObjectsByType<MainStructure>(FindObjectsSortMode.None);

        foreach (MainStructure mainStructure in existingMainStructures)
        {
            if (ResourceManager.Instance != null)
            {
                bool alreadyRegistered = false;
                foreach (IStorage storage in ResourceManager.Instance.GetAllStorages())
                {
                    if ((MainStructure)storage == mainStructure)
                    {
                        alreadyRegistered = true;
                        break;
                    }
                }

                if (!alreadyRegistered)
                {
                    ResourceManager.Instance.RegisterMainStructure(mainStructure);
                    ResourceManager.Instance.AddStorage(mainStructure);
                }
            }

            if (BuildingManager.Instance != null && mainStructure.transform != null && BuildingManager.Instance.grid != null)
            {
                Vector3Int cellPos = BuildingManager.Instance.grid.WorldToCell(mainStructure.transform.position);
                Vector3Int anchorCell = cellPos - new Vector3Int(1, 1, 0);
                BuildingManager.Instance.RegisterMainStructure(anchorCell, new Vector2Int(3, 3), mainStructure);
            }

            if (TargetManager.Instance != null)
            {
                TargetManager.Instance.RegisterTarget(mainStructure);
            }
        }
    }

    private void RegisterPrePlacedBuildings()
    {
        BuildingPiece[] existingBuildings = FindObjectsByType<BuildingPiece>(FindObjectsSortMode.None);

        if (BuildingManager.Instance == null)
        {
            Debug.LogWarning("[GameManager] BuildingManager.Instance is null, cannot register pre-placed buildings.");
            return;
        }

        foreach (BuildingPiece buildingPiece in existingBuildings)
        {
            if (buildingPiece.GetComponent<MainStructure>() != null)
            {
                continue;
            }

            BuildingManager.Instance.RegisterPrePlacedBuilding(buildingPiece);
        }
    }

    public void SpawnUnitsAfterLoading()
    {
        StartingUnitsManager startingUnitsManager = FindFirstObjectByType<StartingUnitsManager>();
        if (startingUnitsManager != null)
        {
            startingUnitsManager.SpawnStartingUnits();
        }
    }

    private bool IsLoadingScreenActive()
    {
        if (LoadingUIManager.Instance == null)
        {
            return false;
        }

        return LoadingUIManager.Instance.IsAnyLoadingScreenActive();
    }
}
