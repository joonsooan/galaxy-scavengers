using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;

    private Rigidbody2D _rb;
    private Vector2 _moveInput;
    private Grid _grid;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        _grid = BuildingManager.Instance.grid;
    }

    private void Update()
    {
        _moveInput.x = Input.GetAxisRaw("Horizontal");
        _moveInput.y = Input.GetAxisRaw("Vertical");
        _moveInput.Normalize();
    }

    private void FixedUpdate()
    {
        if (_grid == null || BuildingManager.Instance == null)
        {
            _rb.linearVelocity = _moveInput * moveSpeed;
            return;
        }

        Vector2 desiredVelocity = _moveInput * moveSpeed;
        Vector2 filteredVelocity = FilterVelocity(desiredVelocity);
        _rb.linearVelocity = filteredVelocity;
    }

    private Vector2 FilterVelocity(Vector2 desiredVelocity)
    {
        if (desiredVelocity.sqrMagnitude < 0.01f)
        {
            return Vector2.zero;
        }

        Vector3 currentPos = transform.position;
        Vector3Int currentCell = _grid.WorldToCell(currentPos);
        Vector3 nextPos = currentPos + (Vector3)desiredVelocity * Time.fixedDeltaTime;
        Vector3Int nextCell = _grid.WorldToCell(nextPos);

        if (currentCell == nextCell)
        {
            return desiredVelocity;
        }

        Vector2 filteredVelocity = desiredVelocity;

        if (desiredVelocity.x != 0f)
        {
            Vector3Int testCellX = new Vector3Int(nextCell.x, currentCell.y, 0);
            if (!IsCellWalkable(testCellX))
            {
                filteredVelocity.x = 0f;
            }
        }

        if (desiredVelocity.y != 0f)
        {
            Vector3Int testCellY = new Vector3Int(currentCell.x, nextCell.y, 0);
            if (!IsCellWalkable(testCellY))
            {
                filteredVelocity.y = 0f;
            }
        }

        if (filteredVelocity.x != 0f && filteredVelocity.y != 0f && !IsCellWalkable(nextCell))
        {
            if (Mathf.Abs(desiredVelocity.x) > Mathf.Abs(desiredVelocity.y))
            {
                filteredVelocity.y = 0f;
            }
            else
            {
                filteredVelocity.x = 0f;
            }
        }

        return filteredVelocity;
    }

    private bool IsCellWalkable(Vector3Int cell)
    {
        if (BuildingManager.Instance.IsTerrainCell(cell) ||
            BuildingManager.Instance.IsResourceTile(cell))
        {
            return false;
        }
        
        if (BuildingManager.Instance.IsMainStructureCell(cell) || 
            BuildingManager.Instance.GetBuildingAt(cell, out _))
        {
            return false;
        }
        
        return true;
    }

    public Vector2 GetMoveDirection()
    {
        return _moveInput;
    }
}
