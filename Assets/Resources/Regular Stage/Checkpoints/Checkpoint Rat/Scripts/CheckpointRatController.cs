using UnityEngine;

/// <summary>
/// The checkpoint rat checkpoint type which swaps between two idle animations on activation
/// </summary>
public class CheckpointRatController : CheckpointController
{
    [SerializeField]
    private Animator animator;

    [Tooltip("The audio played when the checkpoint is touched")]
    public AudioClip checkpointTouchedSound;

    [SerializeField]
    private bool activated;

    public override void Reset()
    {
        base.Reset();
        this.animator = this.GetComponentInChildren<Animator>();
    }

    protected override void Start()
    {
        base.Start();

        if (this.animator == null)
        {
            this.Reset();
        }

        if (this.CheckPointIsActive())
        {
            this.activated = true;
            this.animator.SetBool("Activated", true);
        }
    }

    /// <summary>
    /// Only allow collision if the checkpoint has not been activated yet
    /// <param name="player">The player object to check against  </param>
    /// <param name="solidBoxColliderBounds">The players solid box colliders bounds  </param>
    /// </summary>
    public override bool HedgeIsCollisionValid(Player player, Bounds solidBoxColliderBounds)
    {
        return this.activated == false;
    }

    /// <summary>
    /// Activate the checkpoint on contact: switch animation, play sound, register position
    /// <param name="player">The player object  </param>
    /// </summary>
    public override void HedgeOnCollisionEnter(Player player)
    {
        base.HedgeOnCollisionEnter(player);
        this.activated = true;
        this.animator.SetBool("Activated", true);
        GMAudioManager.Instance().PlayOneShot(this.checkpointTouchedSound);
        this.RegistorCheckPointPosition();
    }
}
