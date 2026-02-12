using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CameraParallaxHandler : MonoBehaviour
{
    [Tooltip("A list of all the backgrounds responsible for following the camera")]
    private List<ParallaxController> parallaxBackgrounds = new List<ParallaxController>();

    [Tooltip("A list of parallax reset triggers set within the stage"), SerializeField]
    private List<ParallaxResetTrigger> parallaxResetTriggers = new List<ParallaxResetTrigger>();
    
    [Tooltip("Gets the active parallax reset trigger"), SerializeField]
    private ParallaxResetTrigger activeParallaxResetTrigger;

    /// <summary>
    /// Registers a background with parallax properties to the list of active backgrounds
    /// <param name="parallaxController"/> The background to be registered </param>
    /// </summary>
    public void RegisterBackGround(ParallaxController parallaxController) => this.parallaxBackgrounds.Add(parallaxController);

    /// <summary>
    /// Updates the positions of all the backgrounds registered to this camera
    /// </summary>
    public void UpdateParallaxBackgroundPositions()
    {
        foreach (ParallaxController parallaxController in this.parallaxBackgrounds)
        {
            parallaxController.UpdateParallaxPosition();
        }
    }

    /// <summary>
    /// Registers a parallax reset trigger
    /// <param name="trigger"/> The trigger to be registered </param>
    /// </summary>
    public void RegisterParallaxResetTrigger(ParallaxResetTrigger trigger)
    {
        this.parallaxResetTriggers.Add(trigger);
        this.parallaxResetTriggers = this.parallaxResetTriggers.OrderBy(x => x.transform.position.x).ToList();
        
        if (this.activeParallaxResetTrigger == null)
        {
            this.activeParallaxResetTrigger = trigger;
        }
    }

    /// <summary>
    /// Checks if player has crossed a parallax reset trigger
    /// <param name="playerPosition"/> The current player position </param>
    /// </summary>
    public void CheckParallaxResetTriggers(Vector2 playerPosition)
    {
        if (this.parallaxResetTriggers.Count == 0)
        {
            return;
        }

        foreach (ParallaxResetTrigger trigger in this.parallaxResetTriggers)
        {
            if (trigger.CheckAndTrigger(playerPosition))
            {
                this.ApplyParallaxReset(trigger.GetTargetParallaxYOffset());
                break;
            }
        }
    }

    /// <summary>
    /// Applies the parallax Y offset to all backgrounds
    /// <param name="targetYOffset"/> The Y offset to apply </param>
    /// </summary>
    public void ApplyParallaxReset(float targetYOffset)
    {
        foreach (ParallaxController parallaxController in this.parallaxBackgrounds)
        {
            parallaxController.ResetVerticalPosition(targetYOffset);
        }
    }

    /// <summary>
    /// Gets the list of registered parallax backgrounds
    /// </summary>
    public List<ParallaxController> GetParallaxBackgrounds() => this.parallaxBackgrounds;
}