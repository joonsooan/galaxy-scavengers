using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class UnitSpriteController : MonoBehaviour
{
    [Header("Sprites")]
    public Sprite spriteUp;
    public Sprite spriteDown;
    public Sprite spriteRight;

    private SpriteRenderer _sr;

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
    }

    public void UpdateSpriteDirection(Vector2 direction)
    {
        if (direction != Vector2.zero) {
            if (Mathf.Abs(direction.x) + 0.1 > Mathf.Abs(direction.y)) {
                _sr.flipX = direction.x < 0;
                _sr.sprite = spriteRight;
            }
            else if (direction.y > 0) {
                _sr.sprite = spriteUp;
            }
            else if (direction.y < 0) {
                _sr.sprite = spriteDown;
            }
        }
    }
}
