using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class Unit_Scout : UnitBase
{
    [Header("Scout Settings")]
    [SerializeField] private UnitMovement unitMovement;
    [SerializeField] private float arrivalTolerance = 0.2f;

    [Header("Hover Animation")]
    [SerializeField] private float hoverHeight = 0.2f;
    [SerializeField] private float hoverDuration = 1.5f;

    private Beacon _assignedBeacon;
    private BeaconWaypointGroup _assignedWaypointGroup;
    private Coroutine _beaconCoroutine;
    private Vector3 _homePosition;
    private bool _isReturningHome;
    private MainStructure _mainStructure;
    private UnitSpriteController _spriteController;
    private Tween _hoverTween;
    private Vector3 _baseHoverLocalPosition;
    private Transform _spriteTransform;
    
    public static event Action<Vector3> OnScoutEnteredLocation;

    public bool IsAssignedToBeacon {
        get {
            return _assignedBeacon != null || _assignedWaypointGroup != null;
        }
    }

    protected override void Awake()
    {
        base.Awake();
        unitMovement = GetComponent<UnitMovement>();
    }

    protected void Start()
    {
        _mainStructure = FindFirstObjectByType<MainStructure>();
        _spriteController = GetComponentInChildren<UnitSpriteController>();
        if (_spriteController != null) {
            _spriteTransform = _spriteController.transform;
            _baseHoverLocalPosition = _spriteTransform.localPosition;
        }
    }

    private void Update()
    {
        if (currentState == UnitState.Moving) {
            CheckBeaconArrival();
        }

        UpdateAnimationState();
        UpdateHoverAnimation();
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (_beaconCoroutine != null) {
            StopCoroutine(_beaconCoroutine);
            _beaconCoroutine = null;
        }
    }

    protected override void OnDestroy()
    {
        StopHover();
        
        if (BeaconManager.Instance != null) {
            BeaconManager.Instance.OnScoutDestroyed(this);
        }

        if (_assignedBeacon != null) {
            _assignedBeacon.UnassignUnit();
        }

        if (_assignedWaypointGroup != null) {
            _assignedWaypointGroup.UnassignUnit();
        }
    }

    private void UpdateAnimationState()
    {
        if (_spriteController == null) {
            return;
        }

        _spriteController.UpdateAnimationState(currentState);

        if (currentState == UnitState.Moving && unitMovement != null) {
            Vector3 moveDir = unitMovement.GetMoveDirection();
            _spriteController.UpdateSpriteDirection(moveDir);
        }
    }

    public void AssignToBeacon(Beacon beacon)
    {
        if (beacon == null) return;

        ClearAssignments();

        _assignedBeacon = beacon;
        _isReturningHome = false;
        currentState = UnitState.Moving;

        if (unitMovement != null) {
            unitMovement.SetNewTarget(beacon.Position);
        }
    }

    public void AssignToWaypointGroup(BeaconWaypointGroup group)
    {
        if (group == null || group.Waypoints.Count == 0) return;

        ClearAssignments();

        _assignedWaypointGroup = group;
        _isReturningHome = false;
        currentState = UnitState.Moving;

        Beacon firstWaypoint = group.Waypoints[0];
        if (unitMovement != null) {
            unitMovement.SetNewTarget(firstWaypoint.Position);
        }
    }

    private void ClearAssignments()
    {
        if (_assignedBeacon != null) {
            _assignedBeacon.UnassignUnit();
            _assignedBeacon = null;
        }

        if (_assignedWaypointGroup != null) {
            _assignedWaypointGroup.UnassignUnit();
            _assignedWaypointGroup = null;
        }
    }

    private void CheckBeaconArrival()
    {
        if (unitMovement == null) return;

        Vector3 targetPosition = Vector3.zero;
        bool shouldCheckArrival = false;

        if (_assignedBeacon != null && !_isReturningHome) {
            targetPosition = _assignedBeacon.Position;
            shouldCheckArrival = true;
        }
        else if (_assignedWaypointGroup != null && !_isReturningHome) {
            Beacon currentWaypoint = _assignedWaypointGroup.CurrentWaypoint;
            if (currentWaypoint != null) {
                targetPosition = currentWaypoint.Position;
                shouldCheckArrival = true;
            }
        }
        else if (_isReturningHome) {
            targetPosition = _homePosition;
            shouldCheckArrival = true;
        }

        if (shouldCheckArrival && Vector3.Distance(transform.position, targetPosition) <= arrivalTolerance) {
            OnArrivedAtTarget();
        }
    }

    private void OnArrivedAtTarget()
    {
        Vector3 currentPosition = transform.position;
        
        if (_isReturningHome) {
            currentState = UnitState.Idle;
            _isReturningHome = false;
            ClearAssignments();
            return;
        }

        // Fire event when scout enters a location
        OnScoutEnteredLocation?.Invoke(currentPosition);

        if (_assignedBeacon != null) {
            if (_beaconCoroutine != null) {
                StopCoroutine(_beaconCoroutine);
            }
            _beaconCoroutine = StartCoroutine(StayAtBeacon(_assignedBeacon));
        }
        else if (_assignedWaypointGroup != null) {
            Beacon currentWaypoint = _assignedWaypointGroup.CurrentWaypoint;
            if (currentWaypoint != null) {
                if (_beaconCoroutine != null) {
                    StopCoroutine(_beaconCoroutine);
                }
                _beaconCoroutine = StartCoroutine(StayAtWaypoint(currentWaypoint));
            }
            else {
                ReturnHome();
            }
        }
    }

    private IEnumerator StayAtBeacon(Beacon beacon)
    {
        currentState = UnitState.Idle;
        unitMovement?.StopMovement();
        
        // Show progress bar while staying at beacon
        ShowProgressBar();
        float elapsedTime = 0f;
        float stayInterval = beacon.StayInterval;
        
        while (elapsedTime < stayInterval)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / stayInterval;
            UpdateProgressBar(progress);
            yield return null;
        }
        
        HideProgressBar();

        if (BeaconManager.Instance != null) {
            Beacon availableBeacon = BeaconManager.Instance.FindAvailableBeacon();
            if (availableBeacon != null) {
                if (_assignedBeacon != null) {
                    _assignedBeacon.UnassignUnit();
                    _assignedBeacon.DestroyBeacon();
                    _assignedBeacon = null;
                }

                _assignedBeacon = availableBeacon;
                _assignedBeacon.AssignUnit(this);
                currentState = UnitState.Moving;

                if (unitMovement != null) {
                    unitMovement.SetNewTarget(availableBeacon.Position);
                }

                _beaconCoroutine = null;
                yield break;
            }
        }

        ReturnHome();
    }

    private IEnumerator StayAtWaypoint(Beacon waypoint)
    {
        currentState = UnitState.Idle;
        unitMovement?.StopMovement();
        
        // Show progress bar while staying at waypoint
        ShowProgressBar();
        float elapsedTime = 0f;
        float stayInterval = waypoint.StayInterval;
        
        while (elapsedTime < stayInterval)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / stayInterval;
            UpdateProgressBar(progress);
            yield return null;
        }
        
        HideProgressBar();

        if (waypoint != null) {
            waypoint.UnassignUnit();
            waypoint.DestroyBeacon();
        }

        if (_assignedWaypointGroup != null && _assignedWaypointGroup.HasMoreWaypoints()) {
            Beacon nextWaypoint = _assignedWaypointGroup.GetNextWaypoint();
            if (nextWaypoint != null && unitMovement != null) {
                nextWaypoint.AssignUnit(this);
                currentState = UnitState.Moving;
                unitMovement.SetNewTarget(nextWaypoint.Position);
            }
            else {
                ReturnHome();
            }
        }
        else {
            if (BeaconManager.Instance != null) {
                BeaconManager.Instance.CompleteWaypointGroup(_assignedWaypointGroup);
            }
            ReturnHome();
        }
    }

    private void ReturnHome()
    {
        _isReturningHome = true;
        currentState = UnitState.Moving;

        if (_assignedBeacon != null) {
            _assignedBeacon.UnassignUnit();
            _assignedBeacon.DestroyBeacon();
            _assignedBeacon = null;
        }

        if (_assignedWaypointGroup != null) {
            Beacon currentWaypoint = _assignedWaypointGroup.CurrentWaypoint;
            if (currentWaypoint != null) {
                currentWaypoint.UnassignUnit();
            }
            _assignedWaypointGroup.UnassignUnit();
            _assignedWaypointGroup = null;
        }

        if (unitMovement != null) {
            _homePosition = FindHomeInteractionPosition(_mainStructure);
            unitMovement.SetNewTarget(_homePosition);
        }
    }

    private Vector3 FindHomeInteractionPosition(MainStructure mainStructure)
    {
        if (mainStructure == null) {
            return transform.position;
        }

        if (BuildingManager.Instance == null || BuildingManager.Instance.grid == null) {
            return mainStructure.transform.position;
        }

        Grid grid = BuildingManager.Instance.grid;
        Vector3Int centerCell = grid.WorldToCell(mainStructure.transform.position);

        // MainStructure is a 3x3 centered on centerCell (see BuildingManager.RegisterMainStructure)
        Vector3Int anchorCell = centerCell - new Vector3Int(1, 1, 0);

        Vector3Int[] cardinalOffsets = {
            new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0)
        };

        List<Vector3Int> candidateCells = new List<Vector3Int>();

        // Collect neighbor cells around each main-structure tile
        for (int x = 0; x < 3; x++) {
            for (int y = 0; y < 3; y++) {
                Vector3Int mainCell = anchorCell + new Vector3Int(x, y, 0);

                foreach (Vector3Int offset in cardinalOffsets) {
                    Vector3Int neighbor = mainCell + offset;

                    // Skip cells inside the 3x3 main structure footprint
                    bool insideMain =
                        neighbor.x >= anchorCell.x && neighbor.x <= anchorCell.x + 2 &&
                        neighbor.y >= anchorCell.y && neighbor.y <= anchorCell.y + 2;
                    if (insideMain) {
                        continue;
                    }

                    if (BuildingManager.Instance.CanPlaceBuilding(neighbor) &&
                        !candidateCells.Contains(neighbor)) {
                        candidateCells.Add(neighbor);
                    }
                }
            }
        }

        if (candidateCells.Count == 0) {
            // Fallback to center position if no walkable neighbors found
            return mainStructure.transform.position;
        }

        // Choose the candidate closest to the scout's current position
        Vector3 bestWorldPos = grid.GetCellCenterWorld(candidateCells[0]);
        float bestDistance = Vector3.Distance(transform.position, bestWorldPos);

        for (int i = 1; i < candidateCells.Count; i++) {
            Vector3 worldPos = grid.GetCellCenterWorld(candidateCells[i]);
            float distance = Vector3.Distance(transform.position, worldPos);
            if (distance < bestDistance) {
                bestDistance = distance;
                bestWorldPos = worldPos;
            }
        }

        return bestWorldPos;
    }

    public void OnBeaconDestroyed(Beacon beacon)
    {
        if (_assignedBeacon == beacon) {
            _assignedBeacon = null;
            ReturnHome();
        }

        if (_assignedWaypointGroup != null) {
            bool wasInGroup = false;
            foreach (Beacon waypoint in _assignedWaypointGroup.Waypoints) {
                if (waypoint == beacon) {
                    wasInGroup = true;
                    break;
                }
            }

            if (wasInGroup) {
                Beacon currentWaypoint = _assignedWaypointGroup.CurrentWaypoint;
                if (beacon == currentWaypoint ||
                    _assignedWaypointGroup.Waypoints.IndexOf(beacon) > _assignedWaypointGroup.Waypoints.IndexOf(currentWaypoint)) {
                    _assignedWaypointGroup.UnassignUnit();
                    _assignedWaypointGroup = null;
                    ReturnHome();
                }
            }
        }
    }

    private void UpdateHoverAnimation()
    {
        bool shouldHover = _spriteTransform != null && 
                          (currentState == UnitState.Idle || currentState == UnitState.Moving);
        
        if (shouldHover) {
            if (_hoverTween == null || !_hoverTween.IsActive()) {
                StartHover();
            }
        }
        else {
            if (_hoverTween != null && _hoverTween.IsActive()) {
                StopHover();
            }
        }
    }

    private void StartHover()
    {
        StopHover();
        if (_spriteTransform == null) return;
        
        _baseHoverLocalPosition = _spriteTransform.localPosition;
        _hoverTween = _spriteTransform.DOLocalMoveY(_baseHoverLocalPosition.y + hoverHeight, hoverDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    private void StopHover()
    {
        if (_hoverTween != null && _hoverTween.IsActive()) {
            _hoverTween.Kill();
            _hoverTween = null;
        }

        if (_spriteTransform != null) {
            float currentY = _spriteTransform.localPosition.y;
            float baseY = _baseHoverLocalPosition.y;
            if (Mathf.Abs(currentY - baseY) > 0.01f) {
                _spriteTransform.DOLocalMoveY(baseY, 0.2f).SetEase(Ease.OutQuad);
            }
        }
    }
}
