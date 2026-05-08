using UnityEngine;

[ExecuteAlways]
public sealed class GassWindField : MonoBehaviour
{
    static readonly int WindDirectionId = Shader.PropertyToID("_WindDirection");
    static readonly int WindStrengthId = Shader.PropertyToID("_WindStrength");
    static readonly int WindSpeedId = Shader.PropertyToID("_WindSpeed");
    static readonly int WindScaleId = Shader.PropertyToID("_WindScale");
    static readonly int GustStrengthId = Shader.PropertyToID("_GustStrength");
    static readonly int GustScaleId = Shader.PropertyToID("_GustScale");
    static readonly int WindTimeId = Shader.PropertyToID("_GassWindTime");

    [Header("Wind Shape")]
    public Vector3 windDirection = new Vector3(1f, 0f, 0.28f);
    [Range(0f, 2f)] public float strength = 0.58f;
    [Range(0f, 6f)] public float speed = 1.65f;
    [Range(0.001f, 0.15f)] public float scale = 0.035f;

    [Header("Gusts")]
    [Range(0f, 2f)] public float gustStrength = 0.42f;
    [Range(0f, 8f)] public float gustScale = 2.35f;

    [Header("Optional Unity Wind Zone")]
    public WindZone windZone;

    public Vector3 NormalizedDirection
    {
        get
        {
            Vector3 direction = windDirection;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector3.forward;
            }

            return direction.normalized;
        }
    }

    public float CurrentTime
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

    void Reset()
    {
        windZone = GetComponent<WindZone>();
    }

    void OnEnable()
    {
        if (windZone == null)
        {
            windZone = GetComponent<WindZone>();
        }

        ApplyGlobals();
    }

    void Update()
    {
        ApplyGlobals();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.SceneView.RepaintAll();
        }
#endif
    }

    public void ApplyTo(MaterialPropertyBlock block)
    {
        if (block == null)
        {
            return;
        }

        Vector3 direction = NormalizedDirection;
        block.SetVector(WindDirectionId, new Vector4(direction.x, 0f, direction.z, 0f));
        block.SetFloat(WindStrengthId, strength);
        block.SetFloat(WindSpeedId, speed);
        block.SetFloat(WindScaleId, scale);
        block.SetFloat(GustStrengthId, gustStrength);
        block.SetFloat(GustScaleId, gustScale);
        block.SetFloat(WindTimeId, CurrentTime);
    }

    public void ApplyGlobals()
    {
        Vector3 direction = NormalizedDirection;
        Shader.SetGlobalVector(WindDirectionId, new Vector4(direction.x, 0f, direction.z, 0f));
        Shader.SetGlobalFloat(WindStrengthId, strength);
        Shader.SetGlobalFloat(WindSpeedId, speed);
        Shader.SetGlobalFloat(WindScaleId, scale);
        Shader.SetGlobalFloat(GustStrengthId, gustStrength);
        Shader.SetGlobalFloat(GustScaleId, gustScale);
        Shader.SetGlobalFloat(WindTimeId, CurrentTime);

        if (windZone != null)
        {
            ApplyWindZoneSettings(direction);
        }
    }

    void ApplyWindZoneSettings(Vector3 direction)
    {
        float windMain = Mathf.Max(0.01f, strength);
        float pulseMagnitude = gustStrength * 0.35f;

        if (windZone.mode != WindZoneMode.Directional)
        {
            windZone.mode = WindZoneMode.Directional;
        }

        if (!Mathf.Approximately(windZone.windMain, windMain))
        {
            windZone.windMain = windMain;
        }

        if (!Mathf.Approximately(windZone.windTurbulence, gustStrength))
        {
            windZone.windTurbulence = gustStrength;
        }

        if (!Mathf.Approximately(windZone.windPulseFrequency, speed))
        {
            windZone.windPulseFrequency = speed;
        }

        if (!Mathf.Approximately(windZone.windPulseMagnitude, pulseMagnitude))
        {
            windZone.windPulseMagnitude = pulseMagnitude;
        }

        Quaternion desiredRotation = Quaternion.LookRotation(direction, Vector3.up);
        if (Quaternion.Angle(transform.rotation, desiredRotation) > 0.01f)
        {
            transform.rotation = desiredRotation;
        }
    }

    void OnDrawGizmos()
    {
        Vector3 origin = transform.position;
        Vector3 direction = NormalizedDirection;
        Gizmos.color = new Color(1f, 0.82f, 0.22f, 0.9f);
        Gizmos.DrawLine(origin, origin + direction * 10f);
        Gizmos.DrawSphere(origin + direction * 10f, 0.35f);
    }
}
