using System.Collections;
using UnityEngine;

/// <summary>
/// Simple intro cutscene - player falls from height and poses on landing
/// </summary>
public class IntroCutscene : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField, Tooltip("How high above start point to spawn")]
    private float spawnHeight = 200f;

    [SerializeField, Tooltip("How long to hold victory pose")]
    private float poseDuration = 2f;

    [SerializeField, Tooltip("Direction player faces (1 = right, -1 = left)")]
    private int facingDirection = 1;

    [Header("Animation Names")]
    [SerializeField]
    private string fallAnimation = "Air Walk";

    [SerializeField]
    private string landAnimation = "Victory Entry";

    [Header("Debug")]
    [SerializeField]
    private bool showDebugLog = false;

    private Player player;
    private bool cutsceneComplete = false;

    private void Start()
    {
        this.StartCoroutine(this.PlayIntroCutscene());
    }

    private IEnumerator PlayIntroCutscene()
    {
        // Wait a frame for everything to initialize
        yield return null;

        // Get player reference
        this.player = GMStageManager.Instance().GetPlayer();

        if (this.player == null)
        {
            Debug.LogError("IntroCutscene: No player found!");
            yield break;
        }

        if (this.showDebugLog)
        {
            Debug.Log("IntroCutscene: Starting cutscene");
        }

        // Set player to cutscene state
        this.player.SetPlayerState(PlayerState.Cutscene);
        this.player.GetInputManager().SetInputRestriction(InputRestriction.All);

        // Store start position and move player up
        Vector3 startPos = this.player.transform.position;
        this.player.transform.position = new Vector3(startPos.x, startPos.y + this.spawnHeight, startPos.z);

        // Set facing direction
        this.player.currentPlayerDirection = this.facingDirection;
        this.player.GetSpriteController().transform.localScale = new Vector3(this.facingDirection, 1, 1);

        // Make sure player is not grounded
        this.player.SetGrounded(false);

        // Play fall animation
        this.player.GetAnimatorManager().SetUpdateMode(AnimatorUpdateMode.UpdateWithoutFiringSwitchTrigger);
        this.player.GetAnimatorManager().PlayAnimation(this.fallAnimation);

        if (this.showDebugLog)
        {
            Debug.Log("IntroCutscene: Player falling from height " + this.spawnHeight);
        }

        // Wait until player lands
        while (!this.player.GetGrounded())
        {
            // Keep playing fall animation
            if (!this.player.GetAnimatorManager().AnimationIsPlaying(this.fallAnimation))
            {
                this.player.GetAnimatorManager().PlayAnimation(this.fallAnimation);
            }

            yield return null;
        }

        if (this.showDebugLog)
        {
            Debug.Log("IntroCutscene: Player landed, playing pose");
        }

        // Player landed - stop movement
        this.player.velocity = Vector2.zero;
        this.player.groundVelocity = 0f;

        // Play landing pose
        this.player.GetAnimatorManager().PlayAnimation(this.landAnimation);

        // Wait for pose duration
        yield return new WaitForSeconds(this.poseDuration);

        if (this.showDebugLog)
        {
            Debug.Log("IntroCutscene: Cutscene complete, returning control");
        }

        // Return control to player
        this.player.GetAnimatorManager().SetUpdateMode(AnimatorUpdateMode.Regular);
        this.player.GetInputManager().SetInputRestriction(InputRestriction.None);
        this.player.SetPlayerState(PlayerState.Awake);
        this.player.GetAnimatorManager().SwitchSubstate(SubState.Idle);

        this.cutsceneComplete = true;
    }

    /// <summary>
    /// Check if cutscene has finished
    /// </summary>
    public bool IsCutsceneComplete()
    {
        return this.cutsceneComplete;
    }
}