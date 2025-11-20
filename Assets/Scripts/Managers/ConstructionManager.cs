using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ConstructionManager : MonoBehaviour
{
    private readonly List<ConstructionSite> _constructionSites = new List<ConstructionSite>();
    private readonly List<Unit_Construct> _constructDrones = new List<Unit_Construct>();
    
    public static ConstructionManager Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("[ConstructionManager] Instance initialized");
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        // Ensure all existing construct drones are registered
        // This handles cases where drones were created before ConstructionManager
        Unit_Construct[] existingDrones = FindObjectsByType<Unit_Construct>(FindObjectsSortMode.None);
        foreach (var drone in existingDrones)
        {
            if (!_constructDrones.Contains(drone))
            {
                RegisterConstructDrone(drone);
            }
        }
        
        // Try to assign drones to any existing construction sites
        if (_constructionSites.Count > 0)
        {
            AssignDronesToSites();
        }
    }
    
    public void RegisterConstructionSite(ConstructionSite site)
    {
        if (site == null)
        {
            Debug.LogWarning("[ConstructionManager] Attempted to register null construction site");
            return;
        }
        
        if (!_constructionSites.Contains(site))
        {
            _constructionSites.Add(site);
            Debug.Log($"[ConstructionManager] Registered construction site at {site.cellPosition}. Total sites: {_constructionSites.Count}, Available drones: {_constructDrones.Count}");
            
            // Try to assign available construct drones
            AssignDronesToSites();
        }
        else
        {
            Debug.LogWarning($"[ConstructionManager] Construction site at {site.cellPosition} already registered");
        }
    }
    
    public void UnregisterConstructionSite(ConstructionSite site)
    {
        _constructionSites.Remove(site);
    }
    
    public void RegisterConstructDrone(Unit_Construct drone)
    {
        if (drone == null)
        {
            Debug.LogWarning("[ConstructionManager] Attempted to register null construct drone");
            return;
        }
        
        if (!_constructDrones.Contains(drone))
        {
            _constructDrones.Add(drone);
            Debug.Log($"[ConstructionManager] Registered construct drone: {drone.name}. Total drones: {_constructDrones.Count}, Active sites: {_constructionSites.Count}");
            
            // Try to assign this drone to a site
            AssignDroneToSite(drone);
        }
        else
        {
            Debug.LogWarning($"[ConstructionManager] Construct drone {drone.name} already registered");
        }
    }
    
    public void UnregisterConstructDrone(Unit_Construct drone)
    {
        _constructDrones.Remove(drone);
    }
    
    public void RequestTask(Unit_Construct drone, ConstructionSite prioritySite = null)
    {
        if (drone == null)
        {
            Debug.LogWarning("[ConstructionManager] RequestTask called with null drone");
            return;
        }
        
        // if (_constructionSites.Count == 0)
        // {
        //     Debug.Log($"[ConstructionManager] No construction sites available for drone '{drone.name}'");
        //     return;
        // }
        
        // If priority site is provided, check for pieces ready to construct
        if (prioritySite != null && prioritySite.comboCardData != null && !prioritySite.AreAllPiecesConstructed())
        {
            Vector3Int? nextPiece = prioritySite.GetNextPieceToConstruct();
            if (nextPiece.HasValue)
            {
                drone.SetTask_ConstructPiece(prioritySite, nextPiece.Value);
                Debug.Log($"[ConstructionManager] Assigned drone '{drone.name}' to construct piece at {nextPiece.Value} for '{prioritySite.comboCardData.displayName}' (priority)");
                return;
            }
        }
        
        // Find pieces that have all their resources and are ready to construct
        // Prioritize sites that have pieces ready for construction
        foreach (var site in _constructionSites)
        {
            if (site == null) continue;
            if (site.comboCardData == null || site.AreAllPiecesConstructed()) continue;
            
            Vector3Int? nextPiece = site.GetNextPieceToConstruct();
            if (nextPiece.HasValue)
            {
                // Try to assign this drone to the piece
                if (site.AssignDroneToPiece(nextPiece.Value, drone))
                {
                    drone.SetTask_ConstructPiece(site, nextPiece.Value);
                    Debug.Log($"[ConstructionManager] Assigned drone '{drone.name}' to construct piece at {nextPiece.Value} for '{site.comboCardData.displayName}'");
                    return;
                }
                else
                {
                    // Another drone is already working on this piece, try next piece
                    continue;
                }
            }
        }
        
        // Check if priority site still needs resources
        if (prioritySite != null && !prioritySite.IsComplete)
        {
            ConstructionSite.ConstructionRequest request = prioritySite.GetNextResourceRequest(drone.carryCapacity);
            if (request != null)
            {
                request.assignedDrone = drone;
                drone.SetTask_FetchResource(request, prioritySite);
                Debug.Log($"[ConstructionManager] Assigned drone '{drone.name}' to fetch {request.amount} {request.type} for site at {prioritySite.cellPosition} (priority)");
                return;
            }
        }
        
        // Then find sites that need resources
        foreach (var site in _constructionSites)
        {
            if (site == null) continue;
            if (site.IsComplete) continue;
            
            ConstructionSite.ConstructionRequest request = site.GetNextResourceRequest(drone.carryCapacity);
            if (request != null)
            {
                request.assignedDrone = drone;
                drone.SetTask_FetchResource(request, site);
                Debug.Log($"[ConstructionManager] Assigned drone '{drone.name}' to fetch {request.amount} {request.type} for site at {site.cellPosition}");
                return;
            }
        }
        
        // No work available
        // Debug.Log($"[ConstructionManager] No tasks available for drone '{drone.name}'. Sites: {_constructionSites.Count}, Drones: {_constructDrones.Count}");
    }
    
    private void AssignDronesToSites()
    {
        foreach (var drone in _constructDrones)
        {
            if (drone.IsIdle())
            {
                AssignDroneToSite(drone);
            }
        }
    }
    
    private void AssignDroneToSite(Unit_Construct drone)
    {
        if (drone == null || !drone.IsIdle()) return;
        
        RequestTask(drone);
    }
    
    public void OnSiteResourceDelivered(ConstructionSite site, Unit_Construct deliveringDrone = null)
    {
        // When resources are delivered, check if any pieces now have all their resources
        // If so, prioritize construction tasks for those pieces
        if (site.comboCardData != null && !site.AreAllPiecesConstructed())
        {
            // If the delivering drone just finished, try to assign it to construct immediately
            if (deliveringDrone != null && deliveringDrone.IsIdle())
            {
                RequestTask(deliveringDrone, site);
            }
            
            // Also check other idle drones for pieces ready to construct
            foreach (var drone in _constructDrones)
            {
                if (drone != deliveringDrone && drone.IsIdle())
                {
                    RequestTask(drone, site);
                }
            }
        }
    }
    
    public void OnPieceConstructed(ConstructionSite site)
    {
        // When a piece is constructed, check if site is complete or assign next piece
        if (site.AreAllPiecesConstructed())
        {
            // All pieces done, spawn combo prefab (no tiles)
            if (site.comboCardData != null && BuildingManager.Instance != null)
            {
                BuildingManager.Instance.CreateComboBuildingFromConstruction(site.cellPosition, site.comboCardData);
                Debug.Log($"[ConstructionManager] All pieces constructed for '{site.comboCardData.displayName}', spawning combo prefab");
                // Site will be destroyed after combo building is created
            }
        }
        else
        {
            // More pieces to construct, assign drones to next pieces
            AssignDronesToSites();
        }
    }
}

