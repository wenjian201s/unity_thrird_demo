using UnityEngine;

[ExecuteAlways]
public sealed class GassWindTextureField : MonoBehaviour
{
    static readonly int WindFieldTexId = Shader.PropertyToID("_WindFieldTex");
    static readonly int WindFieldOriginScaleId = Shader.PropertyToID("_WindFieldOriginScale");
    static readonly int WindFieldStrengthId = Shader.PropertyToID("_WindFieldStrength");

    [Header("Field")]
    [Range(32, 192)] public int resolution = 96;
    [Min(8f)] public float tileWidth = 96f;
    [Range(0f, 3f)] public float fieldStrength = 1.15f;
    [Range(1f, 30f)] public float updatesPerSecond = 20f;

    [Header("Wind Particles")]
    [Range(2, 32)] public int particleCount = 14;
    [Range(2f, 42f)] public float particleRadius = 18f;
    [Range(0f, 1f)] public float turbulence = 0.18f;
    [Range(0f, 1f)] public float globalPush = 0.38f;
    public int seed = 20260512;
    public GassWindField sourceWind;

    Texture2D windTexture;
    Color[] pixels;
    Vector2[] currentWind;
    Vector2[] previousWind;
    WindParticle[] particles;
    float nextUpdateTime;
    int lastResolution;
    int lastParticleCount;

    struct WindParticle
    {
        public Vector2 position;
        public Vector2 direction;
        public float strength;
        public float radius;
        public float speed;
        public float phase;
    }

    public Texture Texture => windTexture;

    public Vector4 OriginScale
    {
        get
        {
            Vector3 origin = transform.position;
            float invTile = tileWidth > 0.0001f ? 1f / tileWidth : 1f;
            return new Vector4(origin.x, origin.z, invTile, tileWidth);
        }
    }

    void OnEnable()
    {
        EnsureResources();
        RebuildParticles();
        UpdateField(true);
        ApplyGlobals();
    }

    void OnDisable()
    {
        if (windTexture != null)
        {
            DestroyTexture(windTexture);
            windTexture = null;
        }
    }

    void OnValidate()
    {
        resolution = Mathf.Clamp(resolution, 32, 192);
        particleCount = Mathf.Clamp(particleCount, 2, 32);
        tileWidth = Mathf.Max(8f, tileWidth);
        updatesPerSecond = Mathf.Max(1f, updatesPerSecond);
        EnsureResources();
        RebuildParticles();
        UpdateField(true);
        ApplyGlobals();
    }

    void Update()
    {
        EnsureResources();

        float now = CurrentTime;
        if (now >= nextUpdateTime)
        {
            nextUpdateTime = now + 1f / updatesPerSecond;
            UpdateParticles(now);
            UpdateField(false);
            ApplyGlobals();
        }
    }

    public void ApplyTo(MaterialPropertyBlock block)
    {
        if (block == null)
        {
            return;
        }

        EnsureResources();
        block.SetTexture(WindFieldTexId, windTexture);
        block.SetVector(WindFieldOriginScaleId, OriginScale);
        block.SetFloat(WindFieldStrengthId, fieldStrength);
    }

    public void ApplyGlobals()
    {
        EnsureResources();
        Shader.SetGlobalTexture(WindFieldTexId, windTexture);
        Shader.SetGlobalVector(WindFieldOriginScaleId, OriginScale);
        Shader.SetGlobalFloat(WindFieldStrengthId, fieldStrength);
    }

    void EnsureResources()
    {
        bool needsTexture = windTexture == null || lastResolution != resolution;
        if (needsTexture)
        {
            if (windTexture != null)
            {
                DestroyTexture(windTexture);
            }

            windTexture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true)
            {
                name = "Gass Runtime Wind Field",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            pixels = new Color[resolution * resolution];
            currentWind = new Vector2[pixels.Length];
            previousWind = new Vector2[pixels.Length];
            lastResolution = resolution;
        }

        if (particles == null || lastParticleCount != particleCount)
        {
            RebuildParticles();
        }
    }

    void RebuildParticles()
    {
        lastParticleCount = particleCount;
        particles = new WindParticle[particleCount];
        System.Random random = new System.Random(seed);
        for (int i = 0; i < particles.Length; i++)
        {
            float angle = NextFloat(random) * Mathf.PI * 2f;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            particles[i] = new WindParticle
            {
                position = new Vector2(NextFloat(random), NextFloat(random)),
                direction = direction,
                strength = Mathf.Lerp(0.45f, 1.25f, NextFloat(random)),
                radius = Mathf.Lerp(particleRadius * 0.65f, particleRadius * 1.35f, NextFloat(random)),
                speed = Mathf.Lerp(0.08f, 0.32f, NextFloat(random)),
                phase = NextFloat(random) * Mathf.PI * 2f
            };
        }
    }

    void UpdateParticles(float now)
    {
        if (particles == null)
        {
            return;
        }

        Vector2 baseDirection = BaseWindDirection2D();
        for (int i = 0; i < particles.Length; i++)
        {
            WindParticle particle = particles[i];
            Vector2 cross = new Vector2(-baseDirection.y, baseDirection.x);
            float wobble = Mathf.Sin(now * 0.37f + particle.phase) * turbulence;
            Vector2 velocity = (baseDirection + cross * wobble).normalized * particle.speed;
            particle.position += velocity / Mathf.Max(tileWidth, 1f);
            particle.position.x = Repeat01(particle.position.x);
            particle.position.y = Repeat01(particle.position.y);
            particle.direction = Vector2.Lerp(particle.direction, (baseDirection + cross * wobble).normalized, 0.08f).normalized;
            particles[i] = particle;
        }
    }

    void UpdateField(bool resetPrevious)
    {
        if (pixels == null || currentWind == null || previousWind == null)
        {
            return;
        }

        for (int i = 0; i < currentWind.Length; i++)
        {
            previousWind[i] = resetPrevious ? Vector2.zero : currentWind[i];
        }

        Vector2 baseDirection = BaseWindDirection2D();
        Vector2 global = baseDirection * globalPush;
        float invResolution = 1f / resolution;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int index = y * resolution + x;
                Vector2 uv = new Vector2((x + 0.5f) * invResolution, (y + 0.5f) * invResolution);
                Vector2 wind = global;

                for (int p = 0; p < particles.Length; p++)
                {
                    WindParticle particle = particles[p];
                    Vector2 delta = ShortestTileDelta(uv, particle.position) * tileWidth;
                    Vector2 forward = particle.direction;
                    Vector2 right = new Vector2(-forward.y, forward.x);
                    float invRadius = 1f / Mathf.Max(0.001f, particle.radius);
                    float forwardMask = 1f - Mathf.Clamp01(Mathf.Abs(Vector2.Dot(forward, delta)) * invRadius);
                    float rightMask = 1f - Mathf.Clamp01(Mathf.Abs(Vector2.Dot(right, delta)) * invRadius);
                    float mask = forwardMask * rightMask;
                    wind += forward * (mask * particle.strength);
                }

                float noise = Mathf.PerlinNoise((uv.x + CurrentTime * 0.015f) * 11f, (uv.y - CurrentTime * 0.011f) * 11f);
                Vector2 noiseVector = new Vector2(noise - 0.5f, Mathf.PerlinNoise(uv.x * 17f + 9f, uv.y * 17f + 2f) - 0.5f);
                wind += noiseVector * turbulence;
                wind = Vector2.ClampMagnitude(wind, 1f);
                currentWind[index] = wind;

                Vector2 prev = resetPrevious ? wind : previousWind[index];
                pixels[index] = new Color(
                    wind.x * 0.5f + 0.5f,
                    wind.y * 0.5f + 0.5f,
                    prev.x * 0.5f + 0.5f,
                    prev.y * 0.5f + 0.5f);
            }
        }

        windTexture.SetPixels(pixels);
        windTexture.Apply(false, false);
    }

    Vector2 BaseWindDirection2D()
    {
        Vector3 direction = sourceWind != null ? sourceWind.NormalizedDirection : transform.forward;
        Vector2 result = new Vector2(direction.x, direction.z);
        if (result.sqrMagnitude < 0.0001f)
        {
            result = Vector2.right;
        }

        return result.normalized;
    }

    float CurrentTime
    {
        get
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                return (float)UnityEditor.EditorApplication.timeSinceStartup;
            }
#endif
            return Time.time;
        }
    }

    static Vector2 ShortestTileDelta(Vector2 a, Vector2 b)
    {
        Vector2 delta = a - b;
        delta.x -= Mathf.Round(delta.x);
        delta.y -= Mathf.Round(delta.y);
        return delta;
    }

    static float Repeat01(float value)
    {
        return value - Mathf.Floor(value);
    }

    static float NextFloat(System.Random random)
    {
        return (float)random.NextDouble();
    }

    static void DestroyTexture(Texture2D texture)
    {
        if (texture == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(texture);
        }
        else
        {
            DestroyImmediate(texture);
        }
    }
}
