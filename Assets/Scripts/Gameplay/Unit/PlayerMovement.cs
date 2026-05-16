using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    private Vector2 _moveInput;
    private Rigidbody2D _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (GameManager.Instance != null && !GameManager.IsGameplayReady)
        {
            _moveInput = Vector2.zero;
            return;
        }

        if (IsLoadingScreenActive())
        {
            _moveInput = Vector2.zero;
            return;
        }

        _moveInput.x = Input.GetAxisRaw("Horizontal");
        _moveInput.y = Input.GetAxisRaw("Vertical");
        _moveInput.Normalize();
    }

    private void FixedUpdate()
    {
        if (GameManager.Instance != null && !GameManager.IsGameplayReady)
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        if (IsLoadingScreenActive())
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        _rb.linearVelocity = _moveInput * moveSpeed;
    }

    public Vector2 GetMoveDirection()
    {
        return _moveInput;
    }

    private bool IsLoadingScreenActive()
    {
        if (LoadingUIManager.Instance == null)
        {
            return false;
        }

        LoadingScreen loadingScreen = LoadingUIManager.Instance.GetLoadingScreenComponent();
        if (loadingScreen == null)
        {
            return false;
        }

        return loadingScreen.gameObject.activeSelf;
    }
}
