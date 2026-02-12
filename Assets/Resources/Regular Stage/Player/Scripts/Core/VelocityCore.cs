using UnityEngine;

/// <summary>
/// Velocity Core System - Manages energy, Flow State, Air Boost, and Dive Bomb mechanics
/// 
/// CONTROLS (E key / B button):
/// 
/// GROUNDED:
/// - Moving + Tap E → Flow State (100% cost)
/// 
/// AIRBORNE:
/// - Down + Tap E → Dive Bomb → Flow State on landing (50% cost)
/// - Left/Right (no up/down) + Tap E → Air Boost Forward (50% cost)
/// - Up + Tap E → Air Boost Upward (50% cost)
/// - No Input + Tap E → Air Boost Upward (50% cost) [DEFAULT]
/// </summary>
public class VelocityCore : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private Player player;
    private InputManager inputManager;

    [Header("Charge Settings")]
    [SerializeField]
    private float currentCharge = 0f;
    public float maxCharge = 100f;
    public float chargeThreshold = 6f;
    public float chargeRate = 10f;

    [Header("Combat Settings")]
    [Tooltip("Speed required to destroy enemies on contact")]
    public float enemyKillThreshold = 11f;

    [Header("Flow State Settings")]
    [SerializeField]
    private bool isFlowStateActive = false;
    [Tooltip("Speed maintained during Flow State")]
    public float flowStateSpeed = 12f;
    [Tooltip("Minimum speed before Flow State ends")]
    public float flowStateMinVelocity = 0.5f;
    private int flowStateDirection = 1;

    [Header("Air Boost Settings")]
    [Tooltip("Cost of Air Boost (percentage of max charge)")]
    public float airBoostCost = 50f;
    [Tooltip("Velocity applied for horizontal Air Boost")]
    public float airBoostHorizontalVelocity = 10f;
    [Tooltip("Velocity applied for upward Air Boost")]
    public float airBoostUpwardVelocity = 8f;
    [Tooltip("Time to charge before launching")]
    public float airBoostChargeTime = 0.3f;
    [Tooltip("Input deadzone threshold")]
    public float inputDeadzone = 0.3f;
    [SerializeField]
    private bool isAirBoostCharging = false;
    [SerializeField]
    private bool isAirBoostActive = false;
    [SerializeField]
    private bool airBoostUsed = false;
    private float airBoostChargeTimer = 0f;
    private AirBoostDirection airBoostDirection = AirBoostDirection.Upward;

    [Header("Dive Bomb Settings")]
    [Tooltip("Cost of Dive Bomb (percentage of max charge)")]
    public float diveBombCost = 50f;
    [Tooltip("Downward velocity during Dive Bomb")]
    public float diveBombDownwardVelocity = 15f;
    [Tooltip("Slight forward velocity during Dive Bomb")]
    public float diveBombForwardVelocity = 3f;
    [SerializeField]
    private bool isDiveBombing = false;
    [SerializeField]
    private bool diveBombUsed = false;
    private int diveBombDirection = 1;

    [Header("Debug")]
    [SerializeField]
    private bool showDebugLog = false;

    // Animation state names (must match Animator Controller exactly)
    private const string ANIM_ROLL = "Roll";
    private const string ANIM_TWIRL = "Twirl";
    private const string ANIM_FALL = "Dash_Fall";
    private const string ANIM_SUPER_PEEL_OUT = "Super Peel Out";
    private const string ANIM_AIRBOOST_SIDEWAYS = "Dash_Airboost_Sideways"; // New animation for forward air boost

    /// <summary>
    /// Direction types for Air Boost
    /// </summary>
    private enum AirBoostDirection
    {
        Forward,
        Upward
    }

    private void Reset()
    {
        this.player = this.GetComponent<Player>();
        if (this.player == null)
        {
            this.player = this.GetComponentInParent<Player>();
        }
    }

    private void Start()
    {
        if (this.player == null)
        {
            this.Reset();
        }

        if (this.player == null)
        {
            Debug.LogError("VelocityCore: No Player reference found!");
        }
        else
        {
            this.inputManager = this.player.GetInputManager();
        }
    }

    private void Update()
    {
        if (this.player == null || this.inputManager == null) return;
        this.CheckVelocityCoreInput();
    }

    private void FixedUpdate()
    {
        if (this.player == null) return;

        // Reset air abilities when grounded
        if (this.player.GetGrounded())
        {
            // Check if we were dive bombing - trigger Flow State!
            if (this.isDiveBombing)
            {
                this.OnDiveBombLanded();
            }

            // Reset air boost if we were in it
            if (this.isAirBoostCharging || this.isAirBoostActive)
            {
                this.EndAirBoost("Landed");
            }

            this.airBoostUsed = false;
            this.diveBombUsed = false;
        }

        // Handle air boost charging
        if (this.isAirBoostCharging)
        {
            this.UpdateAirBoostCharging();
            return;
        }

        // Handle air boost active
        if (this.isAirBoostActive)
        {
            this.UpdateAirBoostActive();
            return;
        }

        // Handle dive bomb
        if (this.isDiveBombing)
        {
            this.UpdateDiveBomb();
            return;
        }

        // Handle Flow State or passive charging
        if (this.isFlowStateActive)
        {
            this.UpdateFlowState();
        }
        else
        {
            this.UpdatePassiveCharging();
        }
    }

    /// <summary>
    /// Passive charging based on speed
    /// </summary>
    private void UpdatePassiveCharging()
    {
        float currentSpeed = Mathf.Abs(this.player.GetHorizontalVelocity());

        if (currentSpeed >= this.chargeThreshold)
        {
            float speedMultiplier = currentSpeed / this.chargeThreshold;
            float chargeAmount = speedMultiplier * this.chargeRate * Time.fixedDeltaTime;
            this.currentCharge = Mathf.Min(this.currentCharge + chargeAmount, this.maxCharge);
        }

        if (this.showDebugLog)
        {
            Debug.Log("VelocityCore: Speed=" + currentSpeed.ToString("F1") + " | Charge=" + this.currentCharge.ToString("F1") + "/" + this.maxCharge);
        }
    }

    /// <summary>
    /// Check for Velocity Core button input and determine which action to perform
    /// </summary>
    private void CheckVelocityCoreInput()
    {
        bool flowButtonPressed = this.inputManager.GetFlowStateButton().GetButtonDown();

        if (!flowButtonPressed) return;

        Vector2 rawInput = this.inputManager.GetCurrentInput();
        
        // Apply deadzone to determine actual input direction
        bool pressingLeft = rawInput.x < -this.inputDeadzone;
        bool pressingRight = rawInput.x > this.inputDeadzone;
        bool pressingUp = rawInput.y > this.inputDeadzone;
        bool pressingDown = rawInput.y < -this.inputDeadzone;
        
        bool hasHorizontalInput = pressingLeft || pressingRight;
        bool hasVerticalInput = pressingUp || pressingDown;
        bool hasNoInput = !hasHorizontalInput && !hasVerticalInput;

        if (this.showDebugLog)
        {
            Debug.Log("VelocityCore: BUTTON PRESSED!" +
                " | RawInput=(" + rawInput.x.ToString("F2") + ", " + rawInput.y.ToString("F2") + ")" +
                " | Left=" + pressingLeft + " Right=" + pressingRight + " Up=" + pressingUp + " Down=" + pressingDown +
                " | HasHoriz=" + hasHorizontalInput + " HasVert=" + hasVerticalInput + " NoInput=" + hasNoInput +
                " | Grounded=" + this.player.GetGrounded());
        }

        // ========== AIRBORNE ACTIONS ==========
        if (!this.player.GetGrounded())
        {
            // PRIORITY 1: PRESSING DOWN = Dive Bomb
            if (pressingDown)
            {
                if (this.CanActivateDiveBomb())
                {
                    if (this.showDebugLog) Debug.Log("VelocityCore: INPUT = DOWN -> DIVE BOMB");
                    this.ActivateDiveBomb();
                    return;
                }
            }
            // PRIORITY 2: PRESSING LEFT/RIGHT (without pressing up or down) = Forward Boost
            else if (hasHorizontalInput && !pressingUp && !pressingDown)
            {
                if (this.CanActivateAirBoost())
                {
                    if (this.showDebugLog) Debug.Log("VelocityCore: INPUT = HORIZONTAL ONLY -> FORWARD BOOST");
                    this.ActivateAirBoost(AirBoostDirection.Forward);
                    return;
                }
            }
            // PRIORITY 3: PRESSING UP -OR- NO INPUT AT ALL = Upward Boost (DEFAULT)
            else if (pressingUp || hasNoInput)
            {
                if (this.CanActivateAirBoost())
                {
                    if (this.showDebugLog) Debug.Log("VelocityCore: INPUT = UP or NONE -> UPWARD BOOST (DEFAULT)");
                    this.ActivateAirBoost(AirBoostDirection.Upward);
                    return;
                }
            }
        }
        // ========== GROUNDED ACTIONS ==========
        else
        {
            // Flow State (on ground, moving)
            if (this.CanActivateFlowState())
            {
                this.ActivateFlowState();
            }
        }
    }

    #region Flow State

    /// <summary>
    /// Check if Flow State can be activated from ground
    /// </summary>
    private bool CanActivateFlowState()
    {
        if (this.isFlowStateActive) return false;
        if (this.currentCharge < this.maxCharge) return false;
        if (!this.player.GetGrounded()) return false;

        float currentSpeed = Mathf.Abs(this.player.GetHorizontalVelocity());
        if (currentSpeed < this.flowStateMinVelocity) return false;

        return true;
    }

    /// <summary>
    /// Activate Flow State from ground (costs 100%)
    /// </summary>
    private void ActivateFlowState()
    {
        this.isFlowStateActive = true;
        this.flowStateDirection = this.player.currentPlayerDirection;
        this.currentCharge = 0f; // Costs 100%

        float newVelocity = this.flowStateSpeed * this.flowStateDirection;
        this.player.groundVelocity = newVelocity;

        if (this.showDebugLog)
        {
            Debug.Log("VelocityCore: FLOW STATE ACTIVATED! (Ground - 100% cost)");
        }
    }

    /// <summary>
    /// Activate Flow State from Dive Bomb landing (already paid 50%)
    /// </summary>
    private void ActivateFlowStateFromDiveBomb()
    {
        this.isFlowStateActive = true;
        this.flowStateDirection = this.diveBombDirection;

        // Set player direction to match dive bomb direction
        this.player.currentPlayerDirection = this.diveBombDirection;

        float newVelocity = this.flowStateSpeed * this.flowStateDirection;
        this.player.groundVelocity = newVelocity;
        this.player.velocity = this.player.CalculateSlopeMovement(this.player.groundVelocity);

        // IMPORTANT: Reset all animation states and switch to running
        this.player.GetAnimatorManager().SwitchSubstate(SubState.Moving);
        this.player.GetAnimatorManager().SwitchActionSubstate(0);
        this.player.GetAnimatorManager().SwitchActionSecondarySubstate(0);

        if (this.showDebugLog)
        {
            Debug.Log("VelocityCore: FLOW STATE ACTIVATED! (Dive Bomb - already paid 50%)");
        }
    }

    /// <summary>
    /// Update Flow State each frame
    /// </summary>
    private void UpdateFlowState()
    {
        // Check end conditions
        float currentSpeed = this.player.GetGrounded() ? 
            Mathf.Abs(this.player.groundVelocity) : Mathf.Abs(this.player.velocity.x);

        // End if stopped
        if (currentSpeed < this.flowStateMinVelocity)
        {
            this.DeactivateFlowState("Stopped moving");
            return;
        }

        // End if direction changed (turned around)
        int currentDirection = this.player.currentPlayerDirection;
        if (currentDirection != this.flowStateDirection)
        {
            this.DeactivateFlowState("Changed direction");
            return;
        }

        // Maintain speed while grounded
        if (this.player.GetGrounded())
        {
            // Maintain flow state speed - ignore uphill gravity
            float targetSpeed = this.flowStateSpeed * this.flowStateDirection;
            
            // If going downhill, allow faster speed
            if (Mathf.Abs(this.player.groundVelocity) > this.flowStateSpeed)
            {
                // Keep the faster speed from downhill
            }
            else
            {
                // Maintain flow state speed
                this.player.groundVelocity = targetSpeed;
            }
        }

        if (this.showDebugLog)
        {
            float displaySpeed = this.player.GetGrounded() ? 
                Mathf.Abs(this.player.groundVelocity) : Mathf.Abs(this.player.velocity.x);
            Debug.Log("VelocityCore: FLOW STATE ACTIVE | Speed=" + displaySpeed.ToString("F1") + " | Grounded=" + this.player.GetGrounded());
        }
    }

    /// <summary>
    /// Deactivate Flow State with optional reason for debug
    /// </summary>
    public void DeactivateFlowState(string reason = "")
    {
        if (!this.isFlowStateActive) return;

        this.isFlowStateActive = false;

        if (this.showDebugLog)
        {
            Debug.Log("VelocityCore: FLOW STATE ENDED - " + reason);
        }
    }

    #endregion

    #region Air Boost

    /// <summary>
    /// Check if Air Boost can be activated
    /// </summary>
    private bool CanActivateAirBoost()
    {
        if (this.isAirBoostCharging) return false;
        if (this.isAirBoostActive) return false;
        if (this.airBoostUsed) return false;
        if (this.isDiveBombing) return false;
        if (this.currentCharge < this.airBoostCost) return false;
        if (this.player.GetGrounded()) return false;

        return true;
    }

    /// <summary>
    /// Activate Air Boost in specified direction
    /// </summary>
    private void ActivateAirBoost(AirBoostDirection direction)
    {
        this.isAirBoostCharging = true;
        this.airBoostUsed = true;
        this.airBoostDirection = direction;
        this.airBoostChargeTimer = 0f;

        // Consume 50% charge
        this.currentCharge = Mathf.Max(0f, this.currentCharge - this.airBoostCost);

        // Stop all movement during charge
        this.player.velocity.x = 0f;
        this.player.velocity.y = 0f;
        this.player.SetBothHorizontalVelocities(0f);

        // Play Roll animation (ball curl) for charging
        this.player.GetAnimatorManager().PlayAnimation(ANIM_ROLL);

        if (this.showDebugLog)
        {
            Debug.Log("VelocityCore: AIR BOOST CHARGING! Direction=" + direction + " | Cost=50%");
        }
    }

    /// <summary>
    /// Update Air Boost charging state
    /// </summary>
    private void UpdateAirBoostCharging()
    {
        // Keep player frozen in place
        this.player.velocity.x = 0f;
        this.player.velocity.y = 0f;
        this.player.SetBothHorizontalVelocities(0f);

        // Increment timer
        this.airBoostChargeTimer += Time.fixedDeltaTime;

        // Check if charge time complete
        if (this.airBoostChargeTimer >= this.airBoostChargeTime)
        {
            this.LaunchAirBoost();
        }
    }

    /// <summary>
    /// Launch Air Boost after charging complete
    /// </summary>
    private void LaunchAirBoost()
    {
        this.isAirBoostCharging = false;
        this.isAirBoostActive = true;

        int playerDirection = this.player.currentPlayerDirection;

        switch (this.airBoostDirection)
        {
            case AirBoostDirection.Upward:
                // Boost straight up
                this.player.velocity.y = this.airBoostUpwardVelocity;
                this.player.velocity.x = 0f;
                this.player.SetBothHorizontalVelocities(0f);
                // Play Twirl animation for upward boost
                this.player.GetAnimatorManager().PlayAnimation(ANIM_TWIRL);
                
                if (this.showDebugLog)
                {
                    Debug.Log("VelocityCore: AIR BOOST LAUNCHED UPWARD! Velocity=(0, " + this.airBoostUpwardVelocity + ")");
                }
                break;

            case AirBoostDirection.Forward:
            default:
                // Boost horizontally in facing direction
                this.player.velocity.x = this.airBoostHorizontalVelocity * playerDirection;
                this.player.velocity.y = 0f; // Neutral vertical - will fall naturally
                this.player.SetBothHorizontalVelocities(this.airBoostHorizontalVelocity * playerDirection);
                // Play sideways air boost animation for forward boost
                this.player.GetAnimatorManager().PlayAnimation(ANIM_AIRBOOST_SIDEWAYS);
                
                if (this.showDebugLog)
                {
                    Debug.Log("VelocityCore: AIR BOOST LAUNCHED FORWARD! Velocity=(" + (this.airBoostHorizontalVelocity * playerDirection) + ", 0)");
                }
                break;
        }
    }

    /// <summary>
    /// Update Air Boost active state
    /// </summary>
    private void UpdateAirBoostActive()
    {
        // Air boost ends when player lands (handled in FixedUpdate grounded check)
        if (this.player.GetGrounded())
        {
            this.EndAirBoost("Landed");
            return;
        }

        // For upward boost, when player starts falling, switch to fall animation but stay active
        if (this.airBoostDirection == AirBoostDirection.Upward && this.player.velocity.y <= 0)
        {
            // Keep playing Dash_Fall animation continuously to override framework's Air Walk
            if (!this.player.GetAnimatorManager().AnimationIsPlaying(ANIM_FALL))
            {
                this.player.GetAnimatorManager().PlayAnimation(ANIM_FALL);
            }
        }
    }

    /// <summary>
    /// End air boost and reset animations properly
    /// </summary>
    private void EndAirBoost(string reason)
    {
        bool wasActive = this.isAirBoostCharging || this.isAirBoostActive;
        
        this.isAirBoostCharging = false;
        this.isAirBoostActive = false;
        this.airBoostChargeTimer = 0f;

        if (wasActive)
        {
            // Reset animation states - let framework take over
            this.player.GetAnimatorManager().SwitchActionSubstate(0);
            this.player.GetAnimatorManager().SwitchActionSecondarySubstate(0);
            
            if (this.player.GetGrounded())
            {
                this.player.GetAnimatorManager().SwitchSubstate(SubState.Moving);
            }

            if (this.showDebugLog)
            {
                Debug.Log("VelocityCore: AIR BOOST ENDED - " + reason + " | Grounded=" + this.player.GetGrounded());
            }
        }
    }

    #endregion

    #region Dive Bomb

    /// <summary>
    /// Check if Dive Bomb can be activated
    /// </summary>
    private bool CanActivateDiveBomb()
    {
        if (this.isDiveBombing) return false;
        if (this.diveBombUsed) return false;
        if (this.isAirBoostCharging) return false;
        if (this.isAirBoostActive) return false;
        if (this.currentCharge < this.diveBombCost) return false;
        if (this.player.GetGrounded()) return false;

        return true;
    }

    /// <summary>
    /// Activate Dive Bomb
    /// </summary>
    private void ActivateDiveBomb()
    {
        this.isDiveBombing = true;
        this.diveBombUsed = true;
        this.diveBombDirection = this.player.currentPlayerDirection;

        // Consume 50% charge
        this.currentCharge = Mathf.Max(0f, this.currentCharge - this.diveBombCost);

        // Set dive velocity - fast downward with slight forward
        this.player.velocity.y = -this.diveBombDownwardVelocity;
        this.player.velocity.x = this.diveBombForwardVelocity * this.diveBombDirection;
        this.player.SetBothHorizontalVelocities(this.diveBombForwardVelocity * this.diveBombDirection);

        // Play Super Peel Out animation (legs spinning - looks cleaner than roll)
        this.player.GetAnimatorManager().PlayAnimation(ANIM_SUPER_PEEL_OUT);

        if (this.showDebugLog)
        {
            Debug.Log("VelocityCore: DIVE BOMB ACTIVATED! Direction=" + this.diveBombDirection + " | Cost=50%");
        }
    }

    /// <summary>
    /// Update Dive Bomb state
    /// </summary>
    private void UpdateDiveBomb()
    {
        // Maintain fast downward velocity
        if (this.player.velocity.y > -this.diveBombDownwardVelocity)
        {
            this.player.velocity.y = -this.diveBombDownwardVelocity;
        }

        // Keep slight forward momentum
        this.player.velocity.x = this.diveBombForwardVelocity * this.diveBombDirection;

        // Keep playing Super Peel Out animation continuously
        if (!this.player.GetAnimatorManager().AnimationIsPlaying(ANIM_SUPER_PEEL_OUT))
        {
            this.player.GetAnimatorManager().PlayAnimation(ANIM_SUPER_PEEL_OUT);
        }

        if (this.showDebugLog)
        {
            Debug.Log("VelocityCore: DIVE BOMBING | Velocity=(" + this.player.velocity.x.ToString("F1") + ", " + this.player.velocity.y.ToString("F1") + ")");
        }
    }

    /// <summary>
    /// Called when Dive Bomb lands - triggers Flow State
    /// </summary>
    private void OnDiveBombLanded()
    {
        this.isDiveBombing = false;

        if (this.showDebugLog)
        {
            Debug.Log("VelocityCore: DIVE BOMB LANDED! Triggering Flow State...");
        }

        // Trigger Flow State!
        this.ActivateFlowStateFromDiveBomb();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Called when player takes damage
    /// </summary>
    public void OnPlayerDamaged()
    {
        if (this.isFlowStateActive)
        {
            this.DeactivateFlowState("Took damage");
        }

        // Cancel dive bomb if active
        if (this.isDiveBombing)
        {
            this.isDiveBombing = false;
        }

        // Cancel air boost if active
        if (this.isAirBoostCharging || this.isAirBoostActive)
        {
            this.EndAirBoost("Took damage");
        }
    }

    /// <summary>
    /// Called when player hits a wall
    /// </summary>
    public void OnWallCollision()
    {
        if (this.isFlowStateActive)
        {
            this.DeactivateFlowState("Hit wall");
        }

        // Cancel dive bomb if active
        if (this.isDiveBombing)
        {
            this.isDiveBombing = false;
        }

        // Cancel air boost if active
        if (this.isAirBoostCharging || this.isAirBoostActive)
        {
            this.EndAirBoost("Hit wall");
        }
    }

    /// <summary>
    /// Add charge from impacts (enemies, breakables)
    /// </summary>
    public void AddImpactCharge(float amount)
    {
        this.currentCharge = Mathf.Min(this.currentCharge + amount, this.maxCharge);
    }

    /// <summary>
    /// Get current charge as percentage (0-1)
    /// </summary>
    public float GetChargePercent()
    {
        return this.currentCharge / this.maxCharge;
    }

    /// <summary>
    /// Get current charge value
    /// </summary>
    public float GetCurrentCharge()
    {
        return this.currentCharge;
    }

    /// <summary>
    /// Set current charge directly (for level start/checkpoints)
    /// </summary>
    public void SetCharge(float amount)
    {
        this.currentCharge = Mathf.Clamp(amount, 0f, this.maxCharge);
    }

    /// <summary>
    /// Fill charge to maximum (for level start/checkpoints)
    /// </summary>
    public void FillCharge()
    {
        this.currentCharge = this.maxCharge;
    }

    /// <summary>
    /// Check if fully charged (100%)
    /// </summary>
    public bool IsFullyCharged()
    {
        return this.currentCharge >= this.maxCharge;
    }

    /// <summary>
    /// Check if has enough charge for Air Boost/Dive Bomb (50%)
    /// </summary>
    public bool HasHalfCharge()
    {
        return this.currentCharge >= this.airBoostCost;
    }

    /// <summary>
    /// Check if Flow State is currently active
    /// </summary>
    public bool IsFlowStateActive()
    {
        return this.isFlowStateActive;
    }

    /// <summary>
    /// Check if any Air Boost state is active
    /// </summary>
    public bool IsAirBoostActive()
    {
        return this.isAirBoostCharging || this.isAirBoostActive;
    }

    /// <summary>
    /// Check if Dive Bomb is active
    /// </summary>
    public bool IsDiveBombing()
    {
        return this.isDiveBombing;
    }

    /// <summary>
    /// Check if player has kill speed (for Speed = Power)
    /// </summary>
    public bool HasKillSpeed()
    {
        if (this.player == null) return false;
        float currentSpeed = Mathf.Abs(this.player.GetHorizontalVelocity());
        return currentSpeed >= this.enemyKillThreshold;
    }

    /// <summary>
    /// Get the enemy kill speed threshold
    /// </summary>
    public float GetEnemyKillThreshold()
    {
        return this.enemyKillThreshold;
    }

    /// <summary>
    /// Consume a specific amount of charge
    /// </summary>
    public void ConsumeCharge(float amount)
    {
        if (amount < 0)
        {
            this.currentCharge = 0f;
        }
        else
        {
            this.currentCharge = Mathf.Max(0f, this.currentCharge - amount);
        }
    }

    #endregion
}