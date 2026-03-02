/// <summary>
/// The attack phases for the Snake Boss
/// </summary>
public enum SnakeAttackPhase
{
    /// <summary>When the snake erupts from the ground and fires fireballs</summary>
    FireballEruption,
    /// <summary>When the snake weaves across the screen descending with each pass</summary>
    DescendingWeave,
    /// <summary>When the snake slams its body down on alternating sides</summary>
    BodySlam,
    /// <summary>Brief pause between attacks</summary>
    Transitioning,
}