using UnityEngine;

public class VelocityCoreVisuals : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private VelocityCore velocityCore;
    [SerializeField]
    private Player player;

    [Header("Glow Settings")]
    [SerializeField]
    private SpriteRenderer glowRenderer;
    public Sprite glowSprite;
    public Color glowColor = new Color(0f, 1f, 1f, 1f);
    public float minGlowScale = 0f;
    public float maxGlowScale = 75f;
    [Range(0f, 1f)]
    public float minGlowAlpha = 0f;
    [Range(0f, 1f)]
    public float maxGlowAlpha = 0.8f;

    [Header("Position Settings")]
    public Vector3 positionOffset = new Vector3(-40f, 30f, 0f);
    public float flipSpeed = 10f;

    [Header("Pulse Settings")]
    public bool enablePulse = true;
    public float pulseSpeed = 2f;
    public float pulseIntensity = 5f;

    private float pulseTimer = 0f;
    private Vector3 currentOffset;

    private void Reset()
    {
        this.velocityCore = this.GetComponent<VelocityCore>();
        if (this.velocityCore == null)
        {
            this.velocityCore = this.GetComponentInParent<VelocityCore>();
        }
        this.player = this.GetComponent<Player>();
        if (this.player == null)
        {
            this.player = this.GetComponentInParent<Player>();
        }
    }

    private void Start()
    {
        if (this.velocityCore == null || this.player == null)
        {
            this.Reset();
        }
        if (this.velocityCore == null)
        {
            Debug.LogError("VelocityCoreVisuals: No VelocityCore reference found!");
            return;
        }
        this.currentOffset = this.positionOffset;
        if (this.glowRenderer == null)
        {
            this.CreateGlowSprite();
        }
    }

    private void Update()
    {
        if (this.velocityCore == null || this.glowRenderer == null) return;
        this.UpdateGlowPosition();
        this.UpdateGlowEffect();
    }

    private void CreateGlowSprite()
    {
        GameObject glowObject = new GameObject("VelocityCore_Glow");
        glowObject.transform.SetParent(this.transform);
        glowObject.transform.localPosition = this.positionOffset;
        glowObject.transform.localScale = Vector3.zero;
        this.glowRenderer = glowObject.AddComponent<SpriteRenderer>();
        
        if (this.glowSprite != null)
        {
            this.glowRenderer.sprite = this.glowSprite;
        }
        else
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>("Regular Stage/Invincibility Sparkles/Sprites/Invincibility_Sparkles");
            if (sprites != null && sprites.Length > 4)
            {
                this.glowSprite = sprites[4];
                this.glowRenderer.sprite = this.glowSprite;
            }
            else
            {
                this.glowSprite = Resources.Load<Sprite>("Regular Stage/Invincibility Sparkles/Sprites/Invincibility_Sparkles_4");
                if (this.glowSprite != null)
                {
                    this.glowRenderer.sprite = this.glowSprite;
                }
                else
                {
                    this.glowRenderer.sprite = this.CreateCircleSprite();
                    Debug.LogWarning("VelocityCoreVisuals: Could not load sparkle sprite, using fallback circle");
                }
            }
        }
        
        this.glowRenderer.color = this.glowColor;
        this.glowRenderer.sortingLayerName = "Player Layer";
        this.glowRenderer.sortingOrder = 100;
        Debug.Log("VelocityCoreVisuals: Created glow sprite");
    }

    private Sprite CreateCircleSprite()
    {
        int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float normalizedDistance = distance / radius;
                float alpha = Mathf.Clamp01(1f - normalizedDistance);
                alpha = alpha * alpha;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private void UpdateGlowPosition()
    {
        int playerDirection = this.player.currentPlayerDirection;
        
        Vector3 targetOffset = new Vector3(
            Mathf.Abs(this.positionOffset.x) * -playerDirection,
            this.positionOffset.y,
            this.positionOffset.z
        );
        
        this.currentOffset = Vector3.Lerp(this.currentOffset, targetOffset, this.flipSpeed * Time.deltaTime);
        this.glowRenderer.transform.localPosition = this.currentOffset;
    }

    private void UpdateGlowEffect()
    {
        float chargePercent = this.velocityCore.GetChargePercent();
        float baseScale = Mathf.Lerp(this.minGlowScale, this.maxGlowScale, chargePercent);
        float pulseOffset = 0f;
        if (this.enablePulse && this.velocityCore.IsFullyCharged())
        {
            this.pulseTimer += Time.deltaTime * this.pulseSpeed;
            pulseOffset = Mathf.Sin(this.pulseTimer * Mathf.PI * 2f) * this.pulseIntensity;
        }
        else
        {
            this.pulseTimer = 0f;
        }
        float finalScale = baseScale + pulseOffset;
        this.glowRenderer.transform.localScale = new Vector3(finalScale, finalScale, 1f);
        float alpha = Mathf.Lerp(this.minGlowAlpha, this.maxGlowAlpha, chargePercent);
        Color currentColor = this.glowColor;
        currentColor.a = alpha;
        this.glowRenderer.color = currentColor;
    }

    public void SetGlowRenderer(SpriteRenderer renderer)
    {
        this.glowRenderer = renderer;
    }
}