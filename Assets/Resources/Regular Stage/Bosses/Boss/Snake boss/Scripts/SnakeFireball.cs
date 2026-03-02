using UnityEngine;

/// <summary>
/// A fireball projectile fired by the Snake Boss during the Fireball Eruption attack.
/// Moves in a set direction, animates through frames, and damages the player on contact.
/// Place this GameObject on the Hazard Layer (layer 21) so the Quill Framework's 
/// HitBoxController automatically handles player damage.
/// Author - pj - Velocity Runner
/// </summary>
public class SnakeFireball : MonoBehaviour
{
    // ===== Movement =====
    [Header("Fireball Settings")]
    [Tooltip("The direction the fireball travels in"), SerializeField]
    private Vector2 moveDirection = Vector2.zero;

    [Tooltip("The speed of the fireball"), SerializeField]
    private float speed = 4f;

    [Tooltip("How long the fireball lives before being destroyed (in seconds)"), SerializeField]
    private float lifetime = 8f;

    // ===== Animation =====
    [Header("Animation")]
    [Tooltip("The fireball animation frames in order"), SerializeField]
    private Sprite[] animationFrames;

    [Tooltip("How fast the fireball animation plays (frames per second)"), SerializeField]
    private float animationSpeed = 10f;

    // ===== Offscreen Cleanup =====
    [Header("Cleanup")]
    [Tooltip("How far offscreen (in world units) the fireball can go before being destroyed"), SerializeField]
    private float offscreenBuffer = 300f;

    // ===== Internal =====
    private SpriteRenderer spriteRenderer;
    private float animationTimer = 0f;
    private int currentFrame = 0;
    private float lifetimeTimer = 0f;

    // =========================================================================
    // UNITY LIFECYCLE
    // =========================================================================

    private void Awake()
    {
        this.spriteRenderer = this.GetComponent<SpriteRenderer>();

        if (this.spriteRenderer == null)
        {
            this.spriteRenderer = this.gameObject.AddComponent<SpriteRenderer>();
        }
    }

    private void Start()
    {
        this.lifetimeTimer = this.lifetime;

        Rigidbody2D rb = this.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.gravityScale = 0f;
        }

        if (this.animationFrames != null && this.animationFrames.Length > 0 && this.spriteRenderer != null)
        {
            this.spriteRenderer.sprite = this.animationFrames[0];
        }

        if (this.spriteRenderer != null)
        {
            this.spriteRenderer.sortingOrder = 15;
        }
    }

    private void FixedUpdate()
    {
        this.UpdateMovement();
        this.UpdateAnimation();
        this.UpdateLifetime();
        // CheckOffscreen disabled — lifetime handles cleanup
    }

    // =========================================================================
    // MOVEMENT
    // =========================================================================

    /// <summary>
    /// Moves the fireball using the framework's physics multiplier for consistent speed.
    /// </summary>
    private void UpdateMovement()
    {
        Vector3 movement = new Vector3(this.moveDirection.x, this.moveDirection.y, 0f) * this.speed;
        this.transform.position += GMStageManager.Instance().GetPhysicsMultiplier() * Time.deltaTime * movement;
    }

    // =========================================================================
    // ANIMATION
    // =========================================================================

    /// <summary>
    /// Cycles through the fireball frames to create a flickering effect.
    /// </summary>
    private void UpdateAnimation()
    {
        if (this.animationFrames == null || this.animationFrames.Length == 0 || this.spriteRenderer == null)
        {
            return;
        }

        this.animationTimer += Time.deltaTime;
        float frameDuration = 1f / this.animationSpeed;

        if (this.animationTimer >= frameDuration)
        {
            this.animationTimer -= frameDuration;
            this.currentFrame = (this.currentFrame + 1) % this.animationFrames.Length;
            this.spriteRenderer.sprite = this.animationFrames[this.currentFrame];
        }
    }

    // =========================================================================
    // LIFETIME & CLEANUP
    // =========================================================================

    private void UpdateLifetime()
    {
        this.lifetimeTimer -= Time.deltaTime;

        if (this.lifetimeTimer <= 0f)
        {
            Destroy(this.gameObject);
        }
    }

    /// <summary>
    /// Destroys the fireball if it goes too far from the camera.
    /// Uses pixel-scale distance (arena is ~400 units wide).
    /// </summary>
    private void CheckOffscreen()
    {
        if (HedgehogCamera.Instance() == null)
        {
            return;
        }

        Vector2 cameraCenter = HedgehogCamera.Instance().transform.position;
        float distanceFromCamera = Vector2.Distance(this.transform.position, cameraCenter);

        if (distanceFromCamera > this.offscreenBuffer)
        {
            Destroy(this.gameObject);
        }
    }

    // =========================================================================
    // PUBLIC API
    // =========================================================================

    /// <summary>
    /// Sets the direction and speed, then begins moving.
    /// Called by SnakeBossController when spawning.
    /// </summary>
    public void Launch(Vector2 direction, float speed)
    {
        this.moveDirection = direction.normalized;
        this.speed = speed;
    }

    public void SetDirection(Vector2 direction)
    {
        this.moveDirection = direction.normalized;
    }

    public void SetSpeed(float speed)
    {
        this.speed = speed;
    }
}