using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Snake-game style body. Records the head's path at fixed distance
/// intervals, then places body segments at evenly spaced distances
/// along that recorded path.
/// 
/// Path points are guaranteed to be recordInterval apart — no clustering.
/// Segments interpolate smoothly between path points for fluid movement.
/// 
/// All sprites should face RIGHT by default.
/// Author - pj - Velocity Runner
/// </summary>
public class SnakeBody : MonoBehaviour
{
    // ===== Segment References =====
    [Header("Segment Setup")]
    [SerializeField] private Transform headTransform;
    [SerializeField] private List<Transform> bodySegments = new List<Transform>();
    [SerializeField] private Transform tailTransform;

    // ===== Sprites =====
    [Header("Sprites (face RIGHT by default)")]
    [SerializeField] private Sprite headSprite;
    [SerializeField] private Sprite bodySprite;
    [SerializeField] private Sprite tailSprite;

    // ===== Settings =====
    [Header("Chain Settings")]
    [Tooltip("Distance between each segment in world units"), SerializeField]
    private float segmentDistance = 16f;

    [Tooltip("Path recording interval (smaller = smoother, 2-4 recommended)"), SerializeField]
    private float recordInterval = 2f;

    [SerializeField] private bool isActive = false;
    [SerializeField] private int bodySortingOrder = 5;
    [SerializeField] private int headSortingOrder = 10;

    // ===== Internal =====
    private SpriteRenderer headRenderer;
    private List<SpriteRenderer> bodyRenderers = new List<SpriteRenderer>();
    private SpriteRenderer tailRenderer;
    private bool initialized = false;

    /// <summary>
    /// Recorded path. pathPoints[0] = most recent recorded position (near head).
    /// Each point is exactly recordInterval from its neighbors.
    /// The head is distanceSinceLastRecord AHEAD of pathPoints[0].
    /// </summary>
    private List<Vector2> pathPoints = new List<Vector2>();

    /// <summary>
    /// How far the head has moved since pathPoints[0] was recorded.
    /// </summary>
    private float distanceSinceLastRecord = 0f;

    private Vector2 lastHeadDirection = Vector2.up;
    private Vector2 prevHeadPos;

    // ===== Rigid Mode (Attack 3) =====
    /// <summary>When true, segments lock into a straight line at rigidAngle instead of path-following.</summary>
    private bool rigidMode = false;
    /// <summary>Angle in degrees from head toward body. 0=right, 90=up, 180=left, 270=down.</summary>
    private float rigidAngle = 90f;

    // =========================================================================
    // LIFECYCLE
    // =========================================================================

    private void Start() => this.Initialize();

    private void Initialize()
    {
        if (this.initialized) return;
        this.initialized = true;

        // Cache renderers
        if (this.headTransform != null)
        {
            this.headRenderer = this.headTransform.GetComponent<SpriteRenderer>();
        }

        this.bodyRenderers.Clear();
        foreach (Transform seg in this.bodySegments)
        {
            this.bodyRenderers.Add(seg != null ? seg.GetComponent<SpriteRenderer>() : null);
        }

        if (this.tailTransform != null)
        {
            this.tailRenderer = this.tailTransform.GetComponent<SpriteRenderer>();
        }

        // Assign sprites
        if (this.headRenderer != null && this.headSprite != null)
        {
            this.headRenderer.sprite = this.headSprite;
            this.headRenderer.sortingOrder = this.headSortingOrder;
        }

        for (int i = 0; i < this.bodyRenderers.Count; i++)
        {
            if (this.bodyRenderers[i] != null && this.bodySprite != null)
            {
                this.bodyRenderers[i].sprite = this.bodySprite;
                this.bodyRenderers[i].sortingOrder = this.bodySortingOrder;
            }
        }

        if (this.tailRenderer != null && this.tailSprite != null)
        {
            this.tailRenderer.sprite = this.tailSprite;
            this.tailRenderer.sortingOrder = this.bodySortingOrder;
        }

        // Un-parent body and tail
        foreach (Transform seg in this.bodySegments)
        {
            if (seg != null) seg.SetParent(null);
        }

        if (this.tailTransform != null) this.tailTransform.SetParent(null);

        if (this.headTransform != null)
        {
            this.prevHeadPos = V2(this.headTransform.position);
        }

        this.SetBodyVisible(this.isActive);
    }

    private void LateUpdate()
    {
        if (!this.isActive || this.headTransform == null) return;

        if (this.rigidMode)
        {
            this.PlaceSegmentsRigid();
            this.RotateHeadRigid();
        }
        else
        {
            this.UpdatePath();
            this.PlaceAllSegments();
            this.RotateHead();
            this.prevHeadPos = V2(this.headTransform.position);
        }
    }

    private void OnDestroy()
    {
        foreach (Transform seg in this.bodySegments)
        {
            if (seg != null) Destroy(seg.gameObject);
        }
        if (this.tailTransform != null) Destroy(this.tailTransform.gameObject);
    }

    // =========================================================================
    // PATH RECORDING
    // =========================================================================

    /// <summary>
    /// Records a new path point every time the head moves recordInterval units.
    /// </summary>
    private void UpdatePath()
    {
        Vector2 headPos = V2(this.headTransform.position);
        Vector2 delta = headPos - this.prevHeadPos;
        float frameDist = delta.magnitude;

        if (frameDist < 0.001f) return;

        this.lastHeadDirection = delta.normalized;
        this.distanceSinceLastRecord += frameDist;

        // Drop new path points for each full interval traveled
        while (this.distanceSinceLastRecord >= this.recordInterval)
        {
            this.distanceSinceLastRecord -= this.recordInterval;

            // Calculate exact position where this interval was crossed
            Vector2 recordPos = headPos - this.lastHeadDirection * this.distanceSinceLastRecord;
            this.pathPoints.Insert(0, recordPos);
        }

        // Trim — keep enough for all segments + buffer
        int followers = this.bodySegments.Count + (this.tailTransform != null ? 1 : 0);
        int maxNeeded = Mathf.CeilToInt((followers + 1) * this.segmentDistance / this.recordInterval) + 4;
        if (this.pathPoints.Count > maxNeeded)
        {
            this.pathPoints.RemoveRange(maxNeeded, this.pathPoints.Count - maxNeeded);
        }
    }

    // =========================================================================
    // SEGMENT PLACEMENT
    // =========================================================================

    /// <summary>
    /// Places every body segment and tail at the correct distance behind the head.
    /// 
    /// Distance model:
    /// - Head is at distance 0
    /// - pathPoints[0] is at distance: distanceSinceLastRecord
    /// - pathPoints[n] is at distance: distanceSinceLastRecord + n * recordInterval
    /// 
    /// For a segment at target distance D:
    ///   We solve: distanceSinceLastRecord + n * recordInterval = D
    ///   n = (D - distanceSinceLastRecord) / recordInterval
    ///   Interpolate between pathPoints[floor(n)] and pathPoints[ceil(n)]
    /// </summary>
    private void PlaceAllSegments()
    {
        Vector2 headPos = V2(this.headTransform.position);

        for (int i = 0; i < this.bodySegments.Count; i++)
        {
            if (this.bodySegments[i] == null) continue;

            float targetDist = this.segmentDistance * (i + 1);
            Vector2 pos = this.SamplePath(targetDist, headPos, out Vector2 dir);

            this.bodySegments[i].position = new Vector3(pos.x, pos.y, 0f);
            this.bodySegments[i].rotation = Quaternion.Euler(0f, 0f, SnapToCardinal(dir));
        }

        if (this.tailTransform != null)
        {
            float tailDist = this.segmentDistance * (this.bodySegments.Count + 1);
            Vector2 tailPos = this.SamplePath(tailDist, headPos, out Vector2 tailDir);

            this.tailTransform.position = new Vector3(tailPos.x, tailPos.y, 0f);
            this.tailTransform.rotation = Quaternion.Euler(0f, 0f, SnapToCardinal(tailDir));
        }
    }

    /// <summary>
    /// Samples a position along the recorded path at the given distance
    /// behind the head. Also outputs the direction of the path at that point.
    /// </summary>
    private Vector2 SamplePath(float distance, Vector2 headPos, out Vector2 direction)
    {
        // Convert distance to a fractional path index
        // pathPoints[0] is at distance distanceSinceLastRecord from head
        // pathPoints[n] is at distance distanceSinceLastRecord + n * recordInterval
        float n = (distance - this.distanceSinceLastRecord) / this.recordInterval;

        if (n < 0f)
        {
            // Target is between head and pathPoints[0]
            // Interpolate from head to pathPoints[0]
            direction = this.lastHeadDirection;

            if (this.pathPoints.Count > 0)
            {
                float t = (distance / this.distanceSinceLastRecord);
                return Vector2.Lerp(headPos, this.pathPoints[0], Mathf.Clamp01(t));
            }

            return headPos - this.lastHeadDirection * distance;
        }

        int indexA = Mathf.FloorToInt(n);
        int indexB = indexA + 1;
        float frac = n - indexA;

        Vector2 posA = this.SafeGetPoint(indexA, headPos);
        Vector2 posB = this.SafeGetPoint(indexB, headPos);

        // Direction: from posB toward posA (toward the head)
        Vector2 dir = posA - posB;
        direction = (dir.sqrMagnitude > 0.001f) ? dir.normalized : this.lastHeadDirection;

        return Vector2.Lerp(posA, posB, frac);
    }

    /// <summary>
    /// Gets pathPoints[index] safely. If index is out of range, extrapolates.
    /// </summary>
    private Vector2 SafeGetPoint(int index, Vector2 headPos)
    {
        if (index >= 0 && index < this.pathPoints.Count)
        {
            return this.pathPoints[index];
        }

        // Extrapolate beyond the recorded path
        Vector2 lastDir;
        Vector2 lastPoint;

        if (this.pathPoints.Count >= 2)
        {
            lastPoint = this.pathPoints[this.pathPoints.Count - 1];
            lastDir = (this.pathPoints[this.pathPoints.Count - 2] - lastPoint).normalized;
        }
        else if (this.pathPoints.Count == 1)
        {
            lastPoint = this.pathPoints[0];
            lastDir = this.lastHeadDirection;
        }
        else
        {
            lastPoint = headPos;
            lastDir = this.lastHeadDirection;
        }

        int overshoot = index - (this.pathPoints.Count - 1);
        return lastPoint - lastDir * (overshoot * this.recordInterval);
    }

    // =========================================================================
    // HEAD ROTATION
    // =========================================================================

    private void RotateHead()
    {
        if (this.headTransform == null) return;

        if (this.lastHeadDirection.sqrMagnitude > 0.001f)
        {
            this.headTransform.rotation = Quaternion.Euler(0f, 0f, SnapToCardinal(this.lastHeadDirection));
        }
    }

    // =========================================================================
    // RIGID MODE (Attack 3 — Trap Door Slam)
    // =========================================================================

    /// <summary>
    /// Places all segments in a straight line from the head at the current rigidAngle.
    /// Segments face TOWARD the head (opposite of rigidAngle).
    /// </summary>
    private void PlaceSegmentsRigid()
    {
        Vector2 headPos = V2(this.headTransform.position);
        float angleRad = this.rigidAngle * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));

        // Segments face toward the head (opposite of body extension direction)
        float segmentRotation = this.rigidAngle + 180f;
        if (segmentRotation >= 360f) segmentRotation -= 360f;

        for (int i = 0; i < this.bodySegments.Count; i++)
        {
            if (this.bodySegments[i] == null) continue;

            Vector2 pos = headPos + direction * this.segmentDistance * (i + 1);
            this.bodySegments[i].position = new Vector3(pos.x, pos.y, 0f);
            this.bodySegments[i].rotation = Quaternion.Euler(0f, 0f, segmentRotation);
        }

        if (this.tailTransform != null)
        {
            float tailDist = this.segmentDistance * (this.bodySegments.Count + 1);
            Vector2 tailPos = headPos + direction * tailDist;
            this.tailTransform.position = new Vector3(tailPos.x, tailPos.y, 0f);
            this.tailTransform.rotation = Quaternion.Euler(0f, 0f, segmentRotation);
        }
    }

    /// <summary>
    /// Points the head along the rigid angle direction (head faces toward the body/arena).
    /// </summary>
    private void RotateHeadRigid()
    {
        if (this.headTransform == null) return;

        // Head faces the same direction as the body extends
        float headAngle = this.rigidAngle;
        this.headTransform.rotation = Quaternion.Euler(0f, 0f, SnapToCardinal(new Vector2(
            Mathf.Cos(headAngle * Mathf.Deg2Rad),
            Mathf.Sin(headAngle * Mathf.Deg2Rad)
        )));
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    /// <summary>Shorthand: Vector3 to Vector2 (drop Z).</summary>
    private static Vector2 V2(Vector3 v) => new Vector2(v.x, v.y);

    /// <summary>Snaps direction to nearest 90°: 0=Right, 90=Up, 180=Left, 270=Down.</summary>
    public static float SnapToCardinal(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.001f) return 0f;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        if (angle < 0f) angle += 360f;

        float snapped = Mathf.Round(angle / 90f) * 90f;
        if (snapped >= 360f) snapped = 0f;

        return snapped;
    }

    // =========================================================================
    // PUBLIC API
    // =========================================================================

    /// <summary>
    /// Sets the head direction hint. Only used when the path is empty
    /// (e.g. right after SnapToHead) so segments know which way to trail.
    /// During normal movement, direction is detected automatically from
    /// the head's position changes — you don't need to call this every frame.
    /// </summary>
    public void SetDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.001f) return;
        this.lastHeadDirection = direction.normalized;
    }

    public Vector2 GetDirection() => this.lastHeadDirection;

    // ===== Rigid Mode API =====

    /// <summary>
    /// Enables or disables rigid mode. In rigid mode, segments are locked in a
    /// straight line at the set angle instead of following the recorded path.
    /// When disabling, rebuilds the path so normal following resumes cleanly.
    /// </summary>
    public void SetRigidMode(bool enabled)
    {
        this.rigidMode = enabled;

        if (!enabled)
        {
            // Exiting rigid mode — rebuild path from current state
            this.SnapToHead();
        }
    }

    /// <summary>
    /// Sets the angle (in degrees) from the head toward the body/tail.
    /// 0=right, 90=up, 180=left, 270=down.
    /// Only applies when rigid mode is enabled.
    /// </summary>
    public void SetRigidAngle(float angle) => this.rigidAngle = angle;

    /// <summary>Returns the current rigid angle in degrees.</summary>
    public float GetRigidAngle() => this.rigidAngle;

    /// <summary>Returns whether rigid mode is currently enabled.</summary>
    public bool IsRigidMode() => this.rigidMode;

    /// <summary>
    /// Assigns a material to all segment renderers (head, body, tail).
    /// Used for the boss flash effect — all segments share the same material
    /// so toggling _Threshold flashes the entire snake.
    /// </summary>
    public void SetAllMaterials(Material material)
    {
        if (material == null) return;

        if (this.headRenderer != null) this.headRenderer.material = material;

        foreach (SpriteRenderer renderer in this.bodyRenderers)
        {
            if (renderer != null) renderer.material = material;
        }

        if (this.tailRenderer != null) this.tailRenderer.material = material;
    }

    public void SetBodyVisible(bool visible)
    {
        if (!this.initialized) this.Initialize();

        this.isActive = visible;

        if (this.headRenderer != null) this.headRenderer.enabled = visible;

        for (int i = 0; i < this.bodySegments.Count; i++)
        {
            if (this.bodySegments[i] != null) this.bodySegments[i].gameObject.SetActive(visible);
            if (i < this.bodyRenderers.Count && this.bodyRenderers[i] != null) this.bodyRenderers[i].enabled = visible;
        }

        if (this.tailTransform != null) this.tailTransform.gameObject.SetActive(visible);
        if (this.tailRenderer != null) this.tailRenderer.enabled = visible;
    }

    public bool IsActive() => this.isActive;

    /// <summary>
    /// Clears path and builds a clean trail behind the head.
    /// </summary>
    public void SnapToHead()
    {
        if (!this.initialized) this.Initialize();

        Vector2 headPos = this.headTransform != null
            ? V2(this.headTransform.position)
            : V2(this.transform.position);

        if (this.headTransform != null)
        {
            this.headTransform.position = new Vector3(headPos.x, headPos.y, 0f);
        }

        Vector2 trailDir = -this.lastHeadDirection;

        // Build clean evenly-spaced path behind head
        int followers = this.bodySegments.Count + (this.tailTransform != null ? 1 : 0);
        int requiredPoints = Mathf.CeilToInt((followers + 1) * this.segmentDistance / this.recordInterval) + 4;

        this.pathPoints.Clear();
        for (int i = 0; i < requiredPoints; i++)
        {
            // pathPoints[0] is recordInterval behind head,
            // pathPoints[n] is (n+1) * recordInterval behind head
            this.pathPoints.Add(headPos + trailDir * this.recordInterval * (i + 1));
        }

        this.distanceSinceLastRecord = this.recordInterval;
        this.prevHeadPos = headPos;

        this.PlaceAllSegments();
        this.RotateHead();
    }

    public float GetSegmentDistance() => this.segmentDistance;

    public void SetSegmentDistance(float distance)
    {
        this.segmentDistance = Mathf.Max(0.1f, distance);
    }

    public List<Transform> GetAllSegments()
    {
        List<Transform> all = new List<Transform>();
        if (this.headTransform != null) all.Add(this.headTransform);
        all.AddRange(this.bodySegments);
        if (this.tailTransform != null) all.Add(this.tailTransform);
        return all;
    }

    /// <summary>
    /// Returns all segments in tail-first order: tail, body (reversed), head.
    /// Used for sequential explosion from tail to head.
    /// </summary>
    public List<Transform> GetSegmentsTailFirst()
    {
        List<Transform> reversed = new List<Transform>();
        if (this.tailTransform != null) reversed.Add(this.tailTransform);

        for (int i = this.bodySegments.Count - 1; i >= 0; i--)
        {
            if (this.bodySegments[i] != null) reversed.Add(this.bodySegments[i]);
        }

        if (this.headTransform != null) reversed.Add(this.headTransform);
        return reversed;
    }
}