using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Place this at any point to reset the parallax vertical position when the player crosses it
/// Works similarly to CameraTriggerController
/// </summary>
public class ParallaxResetTrigger : MonoBehaviour
{
    [SerializeField, Button(nameof(FindNextAndPreviousParallaxTriggers))]
    private bool findNextAndPreviousParallaxTriggers;

    [Help("The Y offset to apply to the parallax backgrounds when this trigger is crossed"), SerializeField]
    private float targetParallaxYOffset = 0f;

    [SerializeField, Tooltip("The parallax trigger ahead of this")]
    private ParallaxResetTrigger previousParallaxTrigger;
    [SerializeField, Tooltip("The parallax trigger after this")]
    private ParallaxResetTrigger nextParallaxTrigger;

    [SerializeField]
    private Color debugColor = Color.cyan;

    [SerializeField, Tooltip("Track which direction player last crossed from")]
    private float lastPlayerX = float.MinValue;
    
    [SerializeField, Tooltip("Has this trigger been activated")]
    private bool hasTriggered = false;

    private void Start()
    {
        this.FindNextAndPreviousParallaxTriggers();
        HedgehogCamera.Instance().GetCameraParallaxHandler().RegisterParallaxResetTrigger(this);
        this.lastPlayerX = GMStageManager.Instance().GetPlayer().transform.position.x;
    }

    /// <summary>
    /// Find the next and previous triggers based on position
    /// </summary>
    private void FindNextAndPreviousParallaxTriggers()
    {
        List<ParallaxResetTrigger> allTriggers = FindObjectsOfType<ParallaxResetTrigger>().ToList();
        ParallaxResetTrigger minTrigger = null;
        ParallaxResetTrigger maxTrigger = null;

        for (int x = 0; x < allTriggers.Count; x++)
        {
            if (allTriggers[x] == this)
            {
                continue;
            }

            if (allTriggers[x].transform.position.x < this.transform.position.x)
            {
                if (minTrigger == null || allTriggers[x].transform.position.x > minTrigger.transform.position.x)
                {
                    minTrigger = allTriggers[x];
                }
            }

            if (allTriggers[x].transform.position.x > this.transform.position.x)
            {
                if (maxTrigger == null || allTriggers[x].transform.position.x < maxTrigger.transform.position.x)
                {
                    maxTrigger = allTriggers[x];
                }
            }
        }

        this.previousParallaxTrigger = minTrigger;
        this.nextParallaxTrigger = maxTrigger;

        if (Application.isPlaying == false)
        {
            General.SetDirty(this);
        }
    }

    /// <summary>
    /// Check if player crossed this trigger and activate if so
    /// <param name="playerPosition"/> The current player position </param>
    /// </summary>
    public bool CheckAndTrigger(Vector2 playerPosition)
    {
        bool crossedRight = this.lastPlayerX < this.transform.position.x && playerPosition.x >= this.transform.position.x;
        bool crossedLeft = this.lastPlayerX > this.transform.position.x && playerPosition.x <= this.transform.position.x;
        
        this.lastPlayerX = playerPosition.x;

        if (crossedRight || crossedLeft)
        {
            this.hasTriggered = true;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Get the target parallax Y offset
    /// </summary>
    public float GetTargetParallaxYOffset() => this.targetParallaxYOffset;

    /// <summary>
    /// Get the next parallax trigger
    /// </summary>
    public ParallaxResetTrigger GetNextParallaxTrigger() => this.nextParallaxTrigger;

    /// <summary>
    /// Get the previous parallax trigger
    /// </summary>
    public ParallaxResetTrigger GetPreviousParallaxTrigger() => this.previousParallaxTrigger;

    private void OnDrawGizmos()
    {
#if UNITY_EDITOR
        SceneView sceneView = SceneView.currentDrawingSceneView;

        if (sceneView != null)
        {
            Vector3 position = Camera.current.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 10f));
            position.y = sceneView.pivot.y;
            this.transform.position = new Vector3(this.transform.position.x, position.y);
        }

        Gizmos.color = this.debugColor;
        HedgehogCamera hedgehogCamera = HedgehogCamera.Instance();

        if (hedgehogCamera == null)
        {
            return;
        }

        // Draw vertical line from top to bottom of act bounds
        Vector3 triggerPosition = new Vector2(this.transform.position.x, this.targetParallaxYOffset);

        GizmosExtra.DrawPolyLine(4, 
            new Vector2(this.transform.position.x, hedgehogCamera.GetCameraBoundsHandler().GetActBounds().GetTopBorderPosition()), 
            new Vector2(this.transform.position.x, hedgehogCamera.GetCameraBoundsHandler().GetActBounds().GetBottomBorderPosition()), 
            this.debugColor);
        
        // Draw arrows and line at the target Y offset position
        GizmosExtra.Draw2DArrow(triggerPosition, 90);
        GizmosExtra.Draw2DArrow(triggerPosition, 270);
        Gizmos.DrawLine(triggerPosition - new Vector3(64, 0), triggerPosition + new Vector3(64, 0));
        
        // Draw label to distinguish from camera triggers
        Handles.Label(triggerPosition + new Vector3(70, 0), "Parallax", new GUIStyle() { normal = new GUIStyleState() { textColor = this.debugColor } });
#endif
    }
}