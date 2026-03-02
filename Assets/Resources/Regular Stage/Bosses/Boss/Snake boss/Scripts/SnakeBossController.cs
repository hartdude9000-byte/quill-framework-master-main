using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controller for the Snake Boss in the Rootway Tunnels zone.
/// Cycles through 3 attacks: Fireball Eruption, Descending Weave, and Body Slam.
/// Extends the Quill Framework Boss base class.
/// Author - pj - Velocity Runner
/// </summary>
public class SnakeBossController : Boss
{
    /// <summary>
    /// Hides Boss.Awake() which does GetComponent&lt;SpriteRenderer&gt;() on the root.
    /// We point spriteRenderer at the Head child's renderer instead, and disable
    /// any SpriteRenderer on the root so it doesn't render a ghost sprite.
    /// </summary>
    private void Awake()
    {
        this.FixSpriteRendererReference();
    }

    // ===== Attack State =====
    [Tooltip("The current attack phase of the snake boss"), FirstFoldOutItem("Snake Attack Info"), SerializeField]
    private SnakeAttackPhase attackPhase = SnakeAttackPhase.FireballEruption;

    [Tooltip("Total hits to defeat the snake boss"), SerializeField]
    private int snakeHealthPoints = 3;

    [Tooltip("Delay in steps between attacks"), SerializeField]
    private float transitionDelay = 60f;

    [Tooltip("Timer used for tracking attack progress"), SerializeField, LastFoldoutItem()]
    private float actionTimer = 0f;

    // ===== Entry =====
    [Tooltip("How far above the arena floor the snake rises during the intro"), FirstFoldOutItem("Entry Settings"), SerializeField]
    private float entryRiseHeight = 48f;

    [Tooltip("Speed the snake rises during the entry intro"), SerializeField, LastFoldoutItem()]
    private float entryRiseSpeed = 3f;

    // ===== General Positioning =====
    [Tooltip("How far below the arena floor the snake hides (used between attacks and for eruption start)"), FirstFoldOutItem("General Positioning"), SerializeField]
    private float undergroundDepth = 48f;

    [Tooltip("How far offscreen the snake starts/ends when entering from the sides (weave attack)"), SerializeField, LastFoldoutItem()]
    private float offscreenBuffer = 48f;

    // ===== Attack 1: Fireball Eruption =====
    [Tooltip("Speed the snake rises/descends during eruption"), FirstFoldOutItem("Attack 1 - Fireball Eruption"), SerializeField]
    private float eruptionRiseSpeed = 2f;

    [Tooltip("How high the snake rises above the arena floor"), SerializeField]
    private float eruptionMaxHeight = 100f;

    [Tooltip("Height at which fireballs are fired"), SerializeField]
    private float eruptionFireHeight = 80f;

    [Tooltip("The X position offset for the left eruption point"), SerializeField]
    private float eruptionLeftX = -150f;

    [Tooltip("The X position offset for the right eruption point"), SerializeField]
    private float eruptionRightX = 150f;

    [Tooltip("Delay in steps between the two fireball bursts"), SerializeField]
    private float fireballBurstDelay = 30f;

    [Tooltip("Pause in steps after firing before descending"), SerializeField]
    private float fireballPostFireDelay = 20f;

    [Tooltip("The spread angle in degrees for the 3 fireballs"), SerializeField]
    private float fireballSpreadAngle = 30f;

    [Tooltip("Speed of the fireball projectile"), SerializeField]
    private float fireballSpeed = 4f;

    [Tooltip("Fireball prefab to spawn"), SerializeField]
    private GameObject fireballPrefab;

    [Tooltip("Which side the snake erupts from next (true = left)"), SerializeField, LastFoldoutItem()]
    private bool eruptFromLeft = true;

    // ===== Attack 2: Descending Weave =====
    [Tooltip("Speed the snake moves horizontally during weave"), FirstFoldOutItem("Attack 2 - Descending Weave"), SerializeField]
    private float weaveSpeed = 4f;

    [Tooltip("How much the snake descends with each pass"), SerializeField]
    private float weaveDescentPerPass = 25f;

    [Tooltip("Starting height for the weave"), SerializeField]
    private float weaveStartHeight = 100f;

    [Tooltip("Total number of horizontal passes"), SerializeField]
    private int weaveTotalPasses = 4;

    [Tooltip("Current pass number"), SerializeField, LastFoldoutItem()]
    private int weaveCurrentPass = 0;

    // ===== Attack 3: Trap Door Slam =====
    [Tooltip("Speed of the pivot rotation in degrees per second"), FirstFoldOutItem("Attack 3 - Trap Door Slam"), SerializeField]
    private float slamPivotSpeed = 180f;

    [Tooltip("Height of the head (hinge point) above the arena floor"), SerializeField]
    private float slamHingeHeight = 0f;

    [Tooltip("How far offscreen the head sits during the slam"), SerializeField]
    private float slamHeadOffscreenOffset = 20f;

    [Tooltip("Total number of slams"), SerializeField]
    private int bodySlamTotalSlams = 3;

    [Tooltip("Current slam number"), SerializeField]
    private int bodySlamCurrentSlam = 0;

    [Tooltip("Duration of the warning before each slam in steps"), SerializeField]
    private float bodySlamWarningDuration = 30f;

    [Tooltip("Pause in steps after slam hits the ground (0 = no pause)"), SerializeField]
    private float bodySlamGroundPause = 0f;

    [Tooltip("Delay between slams in steps"), SerializeField]
    private float bodySlamDelay = 45f;

    [Tooltip("Speed the snake exits horizontally after the slam"), SerializeField]
    private float slamExitSpeed = 6f;

    [Tooltip("Which side the next slam comes from (true = left)"), SerializeField]
    private bool slamLeftSide = true;

    [Tooltip("The warning indicator GameObject (shadow/rumble effect)"), SerializeField]
    private GameObject warningIndicator;

    [Tooltip("Offset from the hinge point to position the warning indicator"), SerializeField, LastFoldoutItem()]
    private Vector2 warningIndicatorOffset = Vector2.zero;

    // ===== Boundaries =====
    [Tooltip("The left boundary of the arena"), FirstFoldOutItem("Arena Boundaries"), SerializeField]
    private Vector2 leftBoundary;

    [Tooltip("The right boundary of the arena"), SerializeField, LastFoldoutItem()]
    private Vector2 rightBoundary;

    // ===== Snake Body =====
    [Tooltip("Reference to the SnakeBody component that manages segments"), SerializeField]
    private SnakeBody snakeBody;

    // ===== Audio =====
    [Tooltip("Sound played when the snake fires a fireball burst"), SerializeField]
    private AudioClip fireballSound;

    // ===== Internal State =====
    private float arenaFloorY;
    private float arenaCenterX;
    private int attackSubState = 0;
    private int burstsFired = 0;
    private Vector2 startPosition;
    private bool weaveHitFleeing = false;

    // =========================================================================
    // UNITY LIFECYCLE
    // =========================================================================

    protected override void Start()
    {
        base.Start();
        this.startPosition = this.transform.position;

        // Belt-and-suspenders: after base.Start(), make sure spriteRenderer
        // points at the Head child (not the root) and root renderer is off.
        // Boss.Start() may have re-assigned spriteRenderer to root's.
        this.FixSpriteRendererReference();

        // Assign boss flash material to all body segments so the entire snake flashes
        if (this.snakeBody != null && this.bossMaterial != null)
        {
            this.snakeBody.SetAllMaterials(this.bossMaterial);
        }
    }

    /// <summary>
    /// Ensures spriteRenderer points at the Head child's SpriteRenderer
    /// and disables any SpriteRenderer on the root to prevent ghost sprites.
    /// </summary>
    private void FixSpriteRendererReference()
    {
        // Disable root renderer
        SpriteRenderer rootRenderer = this.GetComponent<SpriteRenderer>();
        if (rootRenderer != null)
        {
            rootRenderer.sprite = null;
            rootRenderer.enabled = false;
        }

        // Point at Head child's renderer and assign boss material
        Transform headChild = this.transform.Find("Head");
        if (headChild != null)
        {
            SpriteRenderer headRenderer = headChild.GetComponent<SpriteRenderer>();
            if (headRenderer != null)
            {
                this.spriteRenderer = headRenderer;

                // Assign the boss flash material so HurtFlash works
                if (this.bossMaterial != null)
                {
                    headRenderer.material = this.bossMaterial;
                }
            }
        }
    }

    private void FixedUpdate()
    {
        switch (this.bossPhase)
        {
            case BossPhase.Idle:
                break;
            case BossPhase.Entry:
                this.BossEntryMovement();
                break;
            case BossPhase.Attacking:
                this.HandleAttackPhase();
                break;
            case BossPhase.Exploding:
                this.ExplosionSequence();
                break;
            case BossPhase.Exit:
                this.ExitMovement();
                break;
            default:
                break;
        }

        this.Move(this.velocity);
    }

    public override bool CheckIfHitPlayer(Player player)
    {
        return base.CheckIfHitPlayer(player);
    }

    public override void TakeDamage()
    {
        base.TakeDamage();

        // If killed, let the death sequence handle everything
        if (this.healthPoints <= 0) return;

        // If hit during the weave, start the flee sequence
        if (this.attackPhase == SnakeAttackPhase.DescendingWeave && !this.weaveHitFleeing)
        {
            this.weaveHitFleeing = true;
            this.StartCoroutine(this.WeaveHitFlee());
        }
    }

    /// <summary>
    /// When hit during Attack 2: freeze briefly, speed offscreen, then go to Attack 3.
    /// </summary>
    private IEnumerator WeaveHitFlee()
    {
        // Freeze in place for 0.5 seconds
        this.velocity = Vector2.zero;
        yield return new WaitForSeconds(0.5f);

        // Speed offscreen in the current travel direction
        float fleeSpeed = this.weaveSpeed * 3f;
        float snakeLength = this.GetSnakeBodyLength();

        float exitX = this.currentDirection == 1
            ? this.arenaCenterX + this.rightBoundary.x + this.offscreenBuffer + snakeLength
            : this.arenaCenterX + this.leftBoundary.x - this.offscreenBuffer - snakeLength;

        // Move until fully offscreen
        while ((this.currentDirection == 1 && this.transform.position.x < exitX) ||
               (this.currentDirection == -1 && this.transform.position.x > exitX))
        {
            this.velocity.x = fleeSpeed * this.currentDirection;
            this.velocity.y = 0f;
            yield return null;
        }

        // Fully offscreen — stop and transition to Attack 3
        this.velocity = Vector2.zero;
        this.weaveHitFleeing = false;

        if (this.snakeBody != null)
        {
            this.snakeBody.SetBodyVisible(false);
        }

        this.attackPhase = SnakeAttackPhase.BodySlam;
        this.InitBodySlam();
    }

    // =========================================================================
    // ACTIVATION & ENTRY
    // =========================================================================

    public override void OnActivation(BossTrigger bossTrigger)
    {
        base.OnActivation(bossTrigger);
        this.healthPoints = this.snakeHealthPoints;
        GMStageManager.Instance().SetBoss(this);

        this.arenaCenterX = this.bossTrigger.GetBossFightBounds().GetCenterPosition().x;
        this.arenaFloorY = this.startPosition.y;

        // Skip entry — go straight to Attack 1
        this.SetCurrentBossPhase(BossPhase.Attacking);
        this.eruptFromLeft = true;
        this.attackPhase = SnakeAttackPhase.FireballEruption;
        this.InitFireballEruption();
    }

    private void BossEntryMovement()
    {
        this.velocity.y = this.entryRiseSpeed;

        if (this.transform.position.y >= this.arenaFloorY + this.entryRiseHeight)
        {
            this.velocity = Vector2.zero;
            this.SetCurrentBossPhase(BossPhase.Attacking);
            this.attackPhase = SnakeAttackPhase.Transitioning;
            this.actionTimer = this.transitionDelay;
            this.bossHealthStatus = HealthStatus.Invulnerable;

            this.StartCoroutine(this.HideBeforeFirstAttack());
        }
    }

    private IEnumerator HideBeforeFirstAttack()
    {
        yield return new WaitForSeconds(General.StepsToSeconds(this.transitionDelay / 2f));

        this.velocity = Vector2.zero;
        this.transform.position = new Vector3(this.arenaCenterX, this.arenaFloorY - this.undergroundDepth, 0f);

        if (this.snakeBody != null)
        {
            this.snakeBody.SetBodyVisible(false);
        }
    }

    // =========================================================================
    // ATTACK PHASE HANDLER
    // =========================================================================

    private void HandleAttackPhase()
    {
        switch (this.attackPhase)
        {
            case SnakeAttackPhase.Transitioning:
                this.HandleTransition();
                break;
            case SnakeAttackPhase.FireballEruption:
                this.ExecuteFireballEruption();
                break;
            case SnakeAttackPhase.DescendingWeave:
                this.ExecuteDescendingWeave();
                break;
            case SnakeAttackPhase.BodySlam:
                this.ExecuteBodySlam();
                break;
            default:
                break;
        }
    }

    private void HandleTransition()
    {
        this.velocity = Vector2.zero;
        this.actionTimer -= Time.deltaTime;

        if (this.actionTimer <= 0f)
        {
            this.BeginNextAttack();
        }
    }

    private void BeginNextAttack()
    {
        this.attackSubState = 0;

        switch (this.attackPhase)
        {
            case SnakeAttackPhase.Transitioning:
                this.attackPhase = SnakeAttackPhase.FireballEruption;
                this.InitFireballEruption();
                break;
            case SnakeAttackPhase.FireballEruption:
                this.attackPhase = SnakeAttackPhase.DescendingWeave;
                this.InitDescendingWeave();
                break;
            case SnakeAttackPhase.DescendingWeave:
                this.attackPhase = SnakeAttackPhase.BodySlam;
                this.InitBodySlam();
                break;
            case SnakeAttackPhase.BodySlam:
                this.attackPhase = SnakeAttackPhase.FireballEruption;
                this.InitFireballEruption();
                break;
        }
    }

    private void TransitionToNextAttack(SnakeAttackPhase completedAttack)
    {
        this.velocity = Vector2.zero;
        this.bossHealthStatus = HealthStatus.Invulnerable;
        this.actionTimer = General.StepsToSeconds(this.transitionDelay);
        this.attackPhase = SnakeAttackPhase.Transitioning;

        this.transform.position = new Vector3(this.arenaCenterX, this.arenaFloorY - this.undergroundDepth, 0f);

        if (this.snakeBody != null)
        {
            this.snakeBody.SetBodyVisible(false);
        }

        this.StartCoroutine(this.TransitionAfterDelay(completedAttack));
    }

    private IEnumerator TransitionAfterDelay(SnakeAttackPhase completedAttack)
    {
        yield return new WaitForSeconds(General.StepsToSeconds(this.transitionDelay));

        this.attackSubState = 0;

        switch (completedAttack)
        {
            case SnakeAttackPhase.FireballEruption:
                this.attackPhase = SnakeAttackPhase.DescendingWeave;
                this.InitDescendingWeave();
                break;
            case SnakeAttackPhase.DescendingWeave:
                this.attackPhase = SnakeAttackPhase.BodySlam;
                this.InitBodySlam();
                break;
            case SnakeAttackPhase.BodySlam:
                this.attackPhase = SnakeAttackPhase.FireballEruption;
                this.InitFireballEruption();
                break;
        }
    }

    // =========================================================================
    // ATTACK 1: FIREBALL ERUPTION
    // =========================================================================

    private void InitFireballEruption()
    {
        this.burstsFired = 0;
        this.attackSubState = 0;

        float eruptX = this.eruptFromLeft
            ? this.arenaCenterX + this.eruptionLeftX
            : this.arenaCenterX + this.eruptionRightX;

        this.transform.position = new Vector3(eruptX, this.arenaFloorY - this.undergroundDepth, 0f);

        if (this.snakeBody != null)
        {
            this.snakeBody.SetDirection(Vector2.up);
            this.snakeBody.SnapToHead();
            this.snakeBody.SetBodyVisible(true);
        }

        this.bossHealthStatus = HealthStatus.Vulnerable;
    }

    private void ExecuteFireballEruption()
    {
        switch (this.attackSubState)
        {
            // RISING
            case 0:
                this.velocity.y = this.eruptionRiseSpeed;

                // Tell body we're going up
                if (this.snakeBody != null) this.snakeBody.SetDirection(Vector2.up);

                if (this.transform.position.y >= this.arenaFloorY + this.eruptionFireHeight)
                {
                    this.velocity.y = 0f;
                    this.attackSubState = 1;
                    this.StartCoroutine(this.FireBursts());
                }
                break;

            // FIRING
            case 1:
                this.velocity = Vector2.zero;
                break;

            // DESCENDING
            case 2:
                this.velocity.y = -this.eruptionRiseSpeed;

                // Tell body we're going down
                if (this.snakeBody != null) this.snakeBody.SetDirection(Vector2.down);

                // Wait until the TAIL is underground, not just the head.
                float snakeLength = this.GetSnakeBodyLength();
                float fullyHiddenY = this.arenaFloorY - this.undergroundDepth - snakeLength;

                if (this.transform.position.y <= fullyHiddenY)
                {
                    this.velocity = Vector2.zero;
                    this.eruptFromLeft = !this.eruptFromLeft;
                    this.TransitionToNextAttack(SnakeAttackPhase.FireballEruption);
                }
                break;
        }
    }

    private IEnumerator FireBursts()
    {
        for (int burst = 0; burst < 2; burst++)
        {
            this.FireSpreadShot();
            this.burstsFired++;

            if (burst < 1)
            {
                yield return new WaitForSeconds(General.StepsToSeconds(this.fireballBurstDelay));
            }
        }

        yield return new WaitForSeconds(General.StepsToSeconds(this.fireballPostFireDelay));
        this.attackSubState = 2;
        this.bossHealthStatus = HealthStatus.Vulnerable;
    }

    private void FireSpreadShot()
    {
        if (this.fireballPrefab == null)
        {
            Debug.LogError("[SnakeBoss] Fireball prefab not assigned!");
            return;
        }

        Player player = GMStageManager.Instance().GetPlayer();
        if (player == null) return;

        Vector2 directionToPlayer = ((Vector2)player.transform.position - (Vector2)this.transform.position).normalized;
        float baseAngle = Mathf.Atan2(directionToPlayer.y, directionToPlayer.x) * Mathf.Rad2Deg;

        for (int i = 0; i < 3; i++)
        {
            float angleOffset = (i - 1) * this.fireballSpreadAngle;
            float finalAngle = baseAngle + angleOffset;
            Vector2 fireDirection = new Vector2(
                Mathf.Cos(finalAngle * Mathf.Deg2Rad),
                Mathf.Sin(finalAngle * Mathf.Deg2Rad)
            );

            GameObject fireball = Instantiate(this.fireballPrefab, this.transform.position, Quaternion.identity);
            SnakeFireball fireballScript = fireball.GetComponent<SnakeFireball>();
            if (fireballScript != null)
            {
                fireballScript.Launch(fireDirection, this.fireballSpeed);
            }
        }

        GMAudioManager.Instance().PlayOneShot(this.fireballSound);
    }

    // =========================================================================
    // ATTACK 2: DESCENDING WEAVE
    // =========================================================================

    private void InitDescendingWeave()
    {
        this.weaveCurrentPass = 0;
        this.attackSubState = 0;
        this.weaveHitFleeing = false;

        // Start far enough offscreen that the entire body trail is hidden too
        float snakeLength = this.GetSnakeBodyLength();
        float startX = this.arenaCenterX + this.leftBoundary.x - this.offscreenBuffer - snakeLength;
        this.transform.position = new Vector3(startX, this.arenaFloorY + this.weaveStartHeight, 0f);

        this.currentDirection = 1;
        this.bossHealthStatus = HealthStatus.Vulnerable;

        if (this.snakeBody != null)
        {
            this.snakeBody.SetDirection(Vector2.right);
            this.snakeBody.SnapToHead();
            this.snakeBody.SetBodyVisible(true);
        }
    }

    private void ExecuteDescendingWeave()
    {
        // Flee coroutine handles movement when hit
        if (this.weaveHitFleeing) return;

        this.velocity.x = this.weaveSpeed * this.currentDirection;
        this.velocity.y = 0f;

        // Tell body which way we're going
        if (this.snakeBody != null)
        {
            this.snakeBody.SetDirection(this.currentDirection == 1 ? Vector2.right : Vector2.left);
        }

        // Wait until the entire body has cleared offscreen (head + body length)
        float snakeLength = this.GetSnakeBodyLength();
        bool passedRight = this.currentDirection == 1 && this.transform.position.x > this.arenaCenterX + this.rightBoundary.x + this.offscreenBuffer + snakeLength;
        bool passedLeft = this.currentDirection == -1 && this.transform.position.x < this.arenaCenterX + this.leftBoundary.x - this.offscreenBuffer - snakeLength;

        if (passedRight || passedLeft)
        {
            this.weaveCurrentPass++;

            if (this.weaveCurrentPass >= this.weaveTotalPasses)
            {
                this.velocity = Vector2.zero;
                this.bossHealthStatus = HealthStatus.Invulnerable;
                this.TransitionToNextAttack(SnakeAttackPhase.DescendingWeave);
                return;
            }

            this.currentDirection *= -1;

            float newY = this.arenaFloorY + this.weaveStartHeight - (this.weaveDescentPerPass * this.weaveCurrentPass);

            // Start new pass far enough offscreen that body trail is hidden
            float newX = this.currentDirection == 1
                ? this.arenaCenterX + this.leftBoundary.x - this.offscreenBuffer - snakeLength
                : this.arenaCenterX + this.rightBoundary.x + this.offscreenBuffer + snakeLength;

            this.transform.position = new Vector3(newX, newY, 0f);

            if (this.snakeBody != null)
            {
                this.snakeBody.SetDirection(this.currentDirection == 1 ? Vector2.right : Vector2.left);
                this.snakeBody.SnapToHead();
            }

            if (this.weaveCurrentPass == this.weaveTotalPasses - 1)
            {
                this.bossHealthStatus = HealthStatus.Vulnerable;
            }
        }
    }

    // =========================================================================
    // ATTACK 3: BODY SLAM
    // =========================================================================

    private void InitBodySlam()
    {
        this.bodySlamCurrentSlam = 0;
        this.slamLeftSide = true;
        this.bossHealthStatus = HealthStatus.Vulnerable;
        this.attackSubState = 0;
        this.velocity = Vector2.zero;

        // Ensure warning indicator starts hidden
        if (this.warningIndicator != null)
        {
            this.warningIndicator.SetActive(false);
        }

        if (this.snakeBody != null)
        {
            this.snakeBody.SetBodyVisible(true);
        }

        this.StartCoroutine(this.BodySlamSequence());
    }

    private void ExecuteBodySlam()
    {
        // Movement handled by coroutine
    }

    /// <summary>
    /// Trap door slam sequence:
    /// 1. Position head just offscreen on one side at hinge height, body rigid pointing UP
    /// 2. Warning pause
    /// 3. Pivot body from vertical (90°) down to horizontal (0° or 180°) — trap door slam
    /// 4. Optional ground pause
    /// 5. Exit horizontally offscreen (head moves in body direction, rigid body follows)
    /// 6. Alternate side, repeat
    /// </summary>
    private IEnumerator BodySlamSequence()
    {
        for (int slam = 0; slam < this.bodySlamTotalSlams; slam++)
        {
            this.bodySlamCurrentSlam = slam;

            // Determine slam direction
            // From LEFT: head offscreen left, body slams to the RIGHT (90° → 0°)
            // From RIGHT: head offscreen right, body slams to the LEFT (90° → 180°)
            float headX;
            float startAngle = 90f;  // Always starts vertical (body pointing UP)
            float endAngle;
            int exitDirection;

            if (this.slamLeftSide)
            {
                headX = this.arenaCenterX + this.leftBoundary.x - this.slamHeadOffscreenOffset;
                endAngle = 0f;   // Body points RIGHT when slammed
                exitDirection = -1; // Exit back to the LEFT
            }
            else
            {
                headX = this.arenaCenterX + this.rightBoundary.x + this.slamHeadOffscreenOffset;
                endAngle = 180f; // Body points LEFT when slammed
                exitDirection = 1;  // Exit back to the RIGHT
            }

            float headY = this.arenaFloorY + this.slamHingeHeight;

            // === POSITION & ENABLE RIGID MODE ===
            this.transform.position = new Vector3(headX, headY, 0f);
            this.velocity = Vector2.zero;

            if (this.snakeBody != null)
            {
                this.snakeBody.SetRigidAngle(startAngle);
                this.snakeBody.SetRigidMode(true);
                this.snakeBody.SetBodyVisible(true);
            }

            // === WARNING ===
            if (this.warningIndicator != null)
            {
                float indicatorXOffset = this.slamLeftSide ? this.warningIndicatorOffset.x : -this.warningIndicatorOffset.x;
                Vector3 indicatorPos = new Vector3(
                    headX + indicatorXOffset,
                    headY + this.warningIndicatorOffset.y,
                    0f
                );
                this.warningIndicator.transform.position = indicatorPos;
                this.warningIndicator.SetActive(true);
            }

            yield return new WaitForSeconds(General.StepsToSeconds(this.bodySlamWarningDuration));

            // === PIVOT SLAM ===
            float currentAngle = startAngle;

            // Determine rotation direction
            // Left side: 90° → 0° (rotate clockwise, angle decreases)
            // Right side: 90° → 180° (rotate counter-clockwise, angle increases)
            float rotationSign = this.slamLeftSide ? -1f : 1f;

            while ((this.slamLeftSide && currentAngle > endAngle) ||
                   (!this.slamLeftSide && currentAngle < endAngle))
            {
                currentAngle += rotationSign * this.slamPivotSpeed * Time.deltaTime;

                // Clamp to end angle
                if (this.slamLeftSide)
                {
                    currentAngle = Mathf.Max(currentAngle, endAngle);
                }
                else
                {
                    currentAngle = Mathf.Min(currentAngle, endAngle);
                }

                if (this.snakeBody != null)
                {
                    this.snakeBody.SetRigidAngle(currentAngle);
                }

                yield return null;
            }

            // Snap to final angle
            if (this.snakeBody != null)
            {
                this.snakeBody.SetRigidAngle(endAngle);
            }

            if (this.warningIndicator != null)
            {
                this.warningIndicator.SetActive(false);
            }

            // TODO: Camera shake on impact

            // === GROUND PAUSE ===
            if (this.bodySlamGroundPause > 0f)
            {
                yield return new WaitForSeconds(General.StepsToSeconds(this.bodySlamGroundPause));
            }

            // === EXIT HORIZONTALLY ===
            // Move head in the exit direction until entire body is offscreen
            float snakeLength = this.GetSnakeBodyLength();
            float exitTargetX = exitDirection == 1
                ? this.arenaCenterX + this.rightBoundary.x + this.offscreenBuffer + snakeLength
                : this.arenaCenterX + this.leftBoundary.x - this.offscreenBuffer - snakeLength;

            while ((exitDirection == 1 && this.transform.position.x < exitTargetX) ||
                   (exitDirection == -1 && this.transform.position.x > exitTargetX))
            {
                this.velocity.x = this.slamExitSpeed * exitDirection;
                this.velocity.y = 0f;
                yield return null;
            }

            this.velocity = Vector2.zero;

            // === CLEANUP & ALTERNATE ===
            if (this.snakeBody != null)
            {
                this.snakeBody.SetRigidMode(false);
                this.snakeBody.SetBodyVisible(false);
            }

            this.slamLeftSide = !this.slamLeftSide;

            if (slam < this.bodySlamTotalSlams - 1)
            {
                yield return new WaitForSeconds(General.StepsToSeconds(this.bodySlamDelay));
            }
        }

        this.velocity = Vector2.zero;
        this.TransitionToNextAttack(SnakeAttackPhase.BodySlam);
    }

    // =========================================================================
    // MOVEMENT
    // =========================================================================

    private void Move(Vector2 velocity)
    {
        this.transform.position += GMStageManager.Instance().GetPhysicsMultiplier() * Time.deltaTime * new Vector3(velocity.x, velocity.y, 0f);
    }

    /// <summary>
    /// Returns the total length of the snake body trail (all segments * spacing).
    /// Used to ensure the entire snake clears offscreen before teleporting.
    /// </summary>
    private float GetSnakeBodyLength()
    {
        if (this.snakeBody == null) return 0f;

        int segmentCount = this.snakeBody.GetAllSegments().Count; // head + body + tail
        return segmentCount * this.snakeBody.GetSegmentDistance();
    }

    // =========================================================================
    // DAMAGE & DESTRUCTION
    // =========================================================================

    [Tooltip("Delay between each segment explosion in seconds"), FirstFoldOutItem("Death Sequence"), SerializeField]
    private float segmentExplosionDelay = 0.3f;

    [Tooltip("Pause after all segments explode before act clear in seconds"), SerializeField]
    private float postExplosionPause = 1.0f;

    [Tooltip("Optional cutscene to play after act clear (leave empty to skip cutscene)"), SerializeField, LastFoldoutItem()]
    private CutsceneController postBossCutscene;

    public override void OnBossDestruction()
    {
        base.OnBossDestruction();
        this.StopAllCoroutines();
        this.velocity = Vector2.zero;
        this.bossHealthStatus = HealthStatus.Death;
        this.SetCurrentBossPhase(BossPhase.Exploding);

        // Disable body segment colliders so they can't hurt the player during death
        if (this.snakeBody != null)
        {
            foreach (Transform seg in this.snakeBody.GetAllSegments())
            {
                BoxCollider2D col = seg.GetComponent<BoxCollider2D>();
                if (col != null) col.enabled = false;
            }
        }

        this.StartCoroutine(this.SequentialExplosion());
    }

    /// <summary>
    /// Explodes each segment one by one from tail to head, then triggers act clear.
    /// </summary>
    private IEnumerator SequentialExplosion()
    {
        // Brief pause before explosions start
        yield return new WaitForSeconds(0.5f);

        if (this.snakeBody != null)
        {
            List<Transform> segments = this.snakeBody.GetSegmentsTailFirst();

            for (int i = 0; i < segments.Count; i++)
            {
                Transform segment = segments[i];
                if (segment == null) continue;

                bool isHead = (i == segments.Count - 1);

                // Spawn explosion at segment position
                GMSpawnManager.Instance().SpawnGameObject(ObjectToSpawn.BossExplosion, segment.position);
                GMAudioManager.Instance().PlayOneShot(this.bossExplosionSound);

                if (isHead)
                {
                    // Head gets 3 extra explosions before disappearing
                    for (int e = 0; e < 3; e++)
                    {
                        yield return new WaitForSeconds(this.segmentExplosionDelay);
                        GMSpawnManager.Instance().SpawnGameObject(ObjectToSpawn.BossExplosion, segment.position);
                        GMAudioManager.Instance().PlayOneShot(this.bossExplosionSound);
                    }
                }

                // Hide the segment
                SpriteRenderer renderer = segment.GetComponent<SpriteRenderer>();
                if (renderer != null) renderer.enabled = false;

                yield return new WaitForSeconds(this.segmentExplosionDelay);
            }
        }

        // Pause after final explosion
        yield return new WaitForSeconds(this.postExplosionPause);

        this.SetCurrentBossPhase(BossPhase.Exit);

        // Stop boss music
        GMAudioManager.Instance().ClearQueue();
        GMAudioManager.Instance().StopBGM(BGMToPlay.BossTheme);

        // Disable boss trigger
        if (this.bossTrigger != null)
        {
            this.bossTrigger.gameObject.SetActive(false);
        }

        // Register cutscene if one is assigned
        if (this.postBossCutscene != null)
        {
            GMCutsceneManager.Instance().SetActClearCutscene(this.postBossCutscene);
        }

        // Set camera to end level mode targeting the boss position
        HedgehogCamera.Instance().SetCameraMode(CameraMode.EndLevel);

        // Revert super form
        GMStageManager.Instance().GetPlayer().GetHedgePowerUpManager().RevertSuperForm();

        // Trigger act clear
        GMStageManager.Instance().SetStageState(RegularStageState.ActClear);
        GMStageHUDManager.Instance().SetActClearUIActive(true);

        this.gameObject.SetActive(false);
    }

    private void ExplosionSequence()
    {
        // Handled by SequentialExplosion coroutine
    }

    private void ExitMovement()
    {
        // Handled by SequentialExplosion coroutine
    }

    public override void OnBossFightEnd()
    {
        HedgehogCamera.Instance().SetCameraMode(CameraMode.FollowTarget);
        GMAudioManager.Instance().ClearQueue();
        GMAudioManager.Instance().StopBGM(BGMToPlay.BossTheme);
        GMStageManager.Instance().GetPlayer().GetHedgePowerUpManager().RevertSuperForm();
        this.bossTrigger.gameObject.SetActive(false);
        // NOTE: We intentionally do NOT set stage state here —
        // the explosion coroutine sets it to ActClear after this.
    }

    // =========================================================================
    // GIZMOS
    // =========================================================================

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        Vector3 center;
        float floorY;

        if (this.bossTrigger != null)
        {
            center = this.bossTrigger.GetBossFightBounds().GetCenterPosition();
            floorY = this.startPosition.y != 0 ? this.startPosition.y : this.transform.position.y;
        }
        else
        {
            center = this.transform.position;
            floorY = this.transform.position.y;
        }

        float gizmoHeight = 200f; // Tall enough to see arena boundaries

        // ===== RED: Arena Boundaries =====
        Gizmos.color = Color.red;
        Vector3 leftBoundBottom = new Vector3(center.x + this.leftBoundary.x, floorY - this.undergroundDepth, 0f);
        Vector3 leftBoundTop = new Vector3(center.x + this.leftBoundary.x, floorY + gizmoHeight, 0f);
        Gizmos.DrawLine(leftBoundBottom, leftBoundTop);
        for (float y = floorY; y < floorY + gizmoHeight; y += 20f)
        {
            Gizmos.DrawLine(new Vector3(center.x + this.leftBoundary.x - 3f, y, 0f),
                            new Vector3(center.x + this.leftBoundary.x + 3f, y, 0f));
        }

        Vector3 rightBoundBottom = new Vector3(center.x + this.rightBoundary.x, floorY - this.undergroundDepth, 0f);
        Vector3 rightBoundTop = new Vector3(center.x + this.rightBoundary.x, floorY + gizmoHeight, 0f);
        Gizmos.DrawLine(rightBoundBottom, rightBoundTop);
        for (float y = floorY; y < floorY + gizmoHeight; y += 20f)
        {
            Gizmos.DrawLine(new Vector3(center.x + this.rightBoundary.x - 3f, y, 0f),
                            new Vector3(center.x + this.rightBoundary.x + 3f, y, 0f));
        }

        // ===== CYAN: Eruption Points =====
        Gizmos.color = Color.cyan;
        Vector3 eruptLeftBottom = new Vector3(center.x + this.eruptionLeftX, floorY - this.undergroundDepth, 0f);
        Vector3 eruptLeftTop = new Vector3(center.x + this.eruptionLeftX, floorY + this.eruptionMaxHeight + 10f, 0f);
        Gizmos.DrawLine(eruptLeftBottom, eruptLeftTop);
        float fireY = floorY + this.eruptionFireHeight;
        float diamondSize = 5f;
        Gizmos.DrawLine(new Vector3(center.x + this.eruptionLeftX - diamondSize, fireY, 0f),
                        new Vector3(center.x + this.eruptionLeftX, fireY + diamondSize, 0f));
        Gizmos.DrawLine(new Vector3(center.x + this.eruptionLeftX, fireY + diamondSize, 0f),
                        new Vector3(center.x + this.eruptionLeftX + diamondSize, fireY, 0f));
        Gizmos.DrawLine(new Vector3(center.x + this.eruptionLeftX + diamondSize, fireY, 0f),
                        new Vector3(center.x + this.eruptionLeftX, fireY - diamondSize, 0f));
        Gizmos.DrawLine(new Vector3(center.x + this.eruptionLeftX, fireY - diamondSize, 0f),
                        new Vector3(center.x + this.eruptionLeftX - diamondSize, fireY, 0f));

        Vector3 eruptRightBottom = new Vector3(center.x + this.eruptionRightX, floorY - this.undergroundDepth, 0f);
        Vector3 eruptRightTop = new Vector3(center.x + this.eruptionRightX, floorY + this.eruptionMaxHeight + 10f, 0f);
        Gizmos.DrawLine(eruptRightBottom, eruptRightTop);
        Gizmos.DrawLine(new Vector3(center.x + this.eruptionRightX - diamondSize, fireY, 0f),
                        new Vector3(center.x + this.eruptionRightX, fireY + diamondSize, 0f));
        Gizmos.DrawLine(new Vector3(center.x + this.eruptionRightX, fireY + diamondSize, 0f),
                        new Vector3(center.x + this.eruptionRightX + diamondSize, fireY, 0f));
        Gizmos.DrawLine(new Vector3(center.x + this.eruptionRightX + diamondSize, fireY, 0f),
                        new Vector3(center.x + this.eruptionRightX, fireY - diamondSize, 0f));
        Gizmos.DrawLine(new Vector3(center.x + this.eruptionRightX, fireY - diamondSize, 0f),
                        new Vector3(center.x + this.eruptionRightX - diamondSize, fireY, 0f));

        // ===== YELLOW: Arena Floor =====
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(new Vector3(center.x + this.leftBoundary.x - 10f, floorY, 0f),
                        new Vector3(center.x + this.rightBoundary.x + 10f, floorY, 0f));

        // ===== WHITE: Underground Depth =====
        Gizmos.color = new Color(1f, 1f, 1f, 0.3f);
        Gizmos.DrawLine(new Vector3(center.x + this.leftBoundary.x, floorY - this.undergroundDepth, 0f),
                        new Vector3(center.x + this.rightBoundary.x, floorY - this.undergroundDepth, 0f));

        // ===== GREEN: Weave Heights =====
        Gizmos.color = Color.green;
        Gizmos.DrawLine(new Vector3(center.x + this.leftBoundary.x, floorY + this.weaveStartHeight, 0f),
                        new Vector3(center.x + this.rightBoundary.x, floorY + this.weaveStartHeight, 0f));
        Gizmos.color = new Color(0f, 1f, 0f, 0.4f);
        for (int pass = 1; pass < this.weaveTotalPasses; pass++)
        {
            float passY = floorY + this.weaveStartHeight - (this.weaveDescentPerPass * pass);
            Gizmos.DrawLine(new Vector3(center.x + this.leftBoundary.x, passY, 0f),
                            new Vector3(center.x + this.rightBoundary.x, passY, 0f));
        }

        // ===== MAGENTA: Trap Door Hinge Points =====
        Gizmos.color = new Color(1f, 0f, 1f, 0.5f);
        float hingeLeftX = center.x + this.leftBoundary.x - this.slamHeadOffscreenOffset;
        float hingeRightX = center.x + this.rightBoundary.x + this.slamHeadOffscreenOffset;
        float hingeY = floorY + this.slamHingeHeight;
        Gizmos.DrawWireSphere(new Vector3(hingeLeftX, hingeY, 0f), 5f);
        Gizmos.DrawWireSphere(new Vector3(hingeRightX, hingeY, 0f), 5f);
        // Draw pivot arc hint
        Gizmos.DrawLine(new Vector3(hingeLeftX, hingeY, 0f), new Vector3(hingeLeftX, hingeY + 60f, 0f));
        Gizmos.DrawLine(new Vector3(hingeLeftX, hingeY, 0f), new Vector3(hingeLeftX + 60f, hingeY, 0f));
        Gizmos.DrawLine(new Vector3(hingeRightX, hingeY, 0f), new Vector3(hingeRightX, hingeY + 60f, 0f));
        Gizmos.DrawLine(new Vector3(hingeRightX, hingeY, 0f), new Vector3(hingeRightX - 60f, hingeY, 0f));

        // ===== YELLOW: Warning Indicator Positions =====
        Gizmos.color = Color.yellow;
        Vector3 warningLeft = new Vector3(hingeLeftX + this.warningIndicatorOffset.x, hingeY + this.warningIndicatorOffset.y, 0f);
        Vector3 warningRight = new Vector3(hingeRightX - this.warningIndicatorOffset.x, hingeY + this.warningIndicatorOffset.y, 0f);
        Gizmos.DrawSphere(warningLeft, 8f);
        Gizmos.DrawSphere(warningRight, 8f);
        Gizmos.DrawWireSphere(warningLeft, 12f);
        Gizmos.DrawWireSphere(warningRight, 12f);
    }
}