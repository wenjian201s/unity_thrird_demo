using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
public sealed class GassNprGrassRenderer : MonoBehaviour
{
    const int MaxInstancesPerDraw = 1023;

    sealed class ChunkData
    {
        public Bounds bounds;
        public readonly List<Matrix4x4[]> nearBatches = new List<Matrix4x4[]>();
        public readonly List<int> nearCounts = new List<int>();
        public readonly List<Matrix4x4[]> farBatches = new List<Matrix4x4[]>();
        public readonly List<int> farCounts = new List<int>();
    }

    [Header("References")]
    public Terrain targetTerrain;
    public Mesh bladeClusterMesh;
    public Material grassMaterial;
    public Texture2D windTexture;
    public GassWindField windField;
    public GassWindTextureField windTextureField;

    [Header("Distribution")]
    [Min(128)] public int targetBladeClusters = 32000;
    [Range(4, 16)] public int chunksPerAxis = 10;
    [Min(0f)] public float terrainPadding = 2f;
    public int seed = 20260507;

    [Header("Blade Scale")]
    public Vector2 heightRange = new Vector2(1.45f, 2.85f);
    public Vector2 widthRange = new Vector2(0.88f, 1.45f);
    [Range(-0.6f, 1f)] public float valleyHeightBoost = -0.12f;
    [Range(0f, 0.8f)] public float valleyDensityDrop = 0.18f;

    [Header("Performance")]
    public bool drawInSceneView;
    public bool cullByFrustum = true;
    [Min(0f)] public float maxRenderDistance = 132f;
    [Min(0f)] public float farLodStartDistance = 72f;
    [Range(2, 6)] public int farLodStride = 3;
    [Min(0f)] public float boundsHeightPadding = 6f;

    [Header("Rendering")]
    public ShadowCastingMode shadowCasting = ShadowCastingMode.Off;
    public bool receiveShadows = true;

    readonly List<ChunkData> chunks = new List<ChunkData>();
    readonly Plane[] cameraPlanes = new Plane[6];
    MaterialPropertyBlock propertyBlock;
    bool needsRebuild = true;

    void OnEnable()
    {
        if (grassMaterial != null)
        {
            grassMaterial.enableInstancing = true;
        }

        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        Rebuild();
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }

    void OnValidate()
    {
        targetBladeClusters = Mathf.Max(128, targetBladeClusters);
        chunksPerAxis = Mathf.Clamp(chunksPerAxis, 4, 16);
        heightRange = SanitizeRange(heightRange, 0.1f, 5f);
        widthRange = SanitizeRange(widthRange, 0.05f, 3f);
        farLodStride = Mathf.Clamp(farLodStride, 2, 6);
        needsRebuild = true;
    }

    void Update()
    {
        if (needsRebuild)
        {
            Rebuild();
        }
    }

    void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        DrawGrass(camera);
    }

    public void Rebuild()
    {
        needsRebuild = false;
        chunks.Clear();

        if (targetTerrain == null || targetTerrain.terrainData == null)
        {
            return;
        }

        TerrainData data = targetTerrain.terrainData;
        Vector3 terrainPosition = targetTerrain.transform.position;
        Vector3 terrainSize = data.size;
        float usableSizeX = Mathf.Max(1f, terrainSize.x - terrainPadding * 2f);
        float usableSizeZ = Mathf.Max(1f, terrainSize.z - terrainPadding * 2f);
        float chunkSizeX = usableSizeX / chunksPerAxis;
        float chunkSizeZ = usableSizeZ / chunksPerAxis;
        int chunkCount = chunksPerAxis * chunksPerAxis;
        int clustersPerChunk = Mathf.Max(1, Mathf.CeilToInt((float)targetBladeClusters / chunkCount));
        int farStride = Mathf.Max(2, farLodStride);
        System.Random random = new System.Random(seed);

        for (int z = 0; z < chunksPerAxis; z++)
        {
            for (int x = 0; x < chunksPerAxis; x++)
            {
                List<Matrix4x4> matrices = new List<Matrix4x4>(clustersPerChunk);
                for (int i = 0; i < clustersPerChunk; i++)
                {
                    float localX = terrainPadding + (x + NextFloat(random)) * chunkSizeX;
                    float localZ = terrainPadding + (z + NextFloat(random)) * chunkSizeZ;
                    float valley01 = EvaluateValley(localX, localZ, terrainSize);
                    float keepChance = 1f - valley01 * valleyDensityDrop;
                    if (NextFloat(random) > keepChance)
                    {
                        continue;
                    }

                    float normalizedX = Mathf.Clamp01(localX / terrainSize.x);
                    float normalizedZ = Mathf.Clamp01(localZ / terrainSize.z);
                    Vector3 worldPosition = new Vector3(
                        terrainPosition.x + localX,
                        terrainPosition.y + targetTerrain.SampleHeight(new Vector3(terrainPosition.x + localX, 0f, terrainPosition.z + localZ)),
                        terrainPosition.z + localZ);

                    Vector3 normal = data.GetInterpolatedNormal(normalizedX, normalizedZ);
                    Quaternion surfaceRotation = Quaternion.FromToRotation(Vector3.up, normal);
                    Quaternion yawRotation = Quaternion.Euler(0f, NextFloat(random) * 360f, 0f);
                    float height = Mathf.Lerp(heightRange.x, heightRange.y, NextFloat(random));
                    height *= 1f + valley01 * valleyHeightBoost;
                    float width = Mathf.Lerp(widthRange.x, widthRange.y, NextFloat(random));
                    Vector3 scale = new Vector3(width, height, width);
                    matrices.Add(Matrix4x4.TRS(worldPosition, surfaceRotation * yawRotation, scale));
                }

                if (matrices.Count == 0)
                {
                    continue;
                }

                ChunkData chunk = new ChunkData();
                float centerX = terrainPadding + (x + 0.5f) * chunkSizeX;
                float centerZ = terrainPadding + (z + 0.5f) * chunkSizeZ;
                Vector3 boundsCenter = new Vector3(
                    terrainPosition.x + centerX,
                    terrainPosition.y + terrainSize.y * 0.5f,
                    terrainPosition.z + centerZ);
                Vector3 boundsSize = new Vector3(
                    chunkSizeX + terrainPadding * 2f,
                    terrainSize.y + Mathf.Max(heightRange.y, 1f) * 2f + boundsHeightPadding,
                    chunkSizeZ + terrainPadding * 2f);
                chunk.bounds = new Bounds(boundsCenter, boundsSize);

                AddBatches(matrices, chunk.nearBatches, chunk.nearCounts);

                List<Matrix4x4> farMatrices = new List<Matrix4x4>(Mathf.Max(1, matrices.Count / farStride));
                for (int i = 0; i < matrices.Count; i += farStride)
                {
                    farMatrices.Add(matrices[i]);
                }

                AddBatches(farMatrices, chunk.farBatches, chunk.farCounts);
                chunks.Add(chunk);
            }
        }
    }

    void DrawGrass(Camera camera)
    {
        if (camera == null || bladeClusterMesh == null || grassMaterial == null || chunks.Count == 0)
        {
            return;
        }

        if (!ShouldRenderForCamera(camera))
        {
            return;
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        propertyBlock.Clear();
        if (windTexture != null)
        {
            propertyBlock.SetTexture("_WindTex", windTexture);
        }

        if (windField != null)
        {
            windField.ApplyTo(propertyBlock);
        }

        if (windTextureField != null)
        {
            windTextureField.ApplyTo(propertyBlock);
        }

        grassMaterial.enableInstancing = true;
        int renderLayer = gameObject.layer;
        float maxDistanceSqr = maxRenderDistance > 0f ? maxRenderDistance * maxRenderDistance : float.PositiveInfinity;
        float farLodDistanceSqr = farLodStartDistance > 0f ? farLodStartDistance * farLodStartDistance : float.PositiveInfinity;
        Vector3 cameraPosition = camera.transform.position;

        if (cullByFrustum)
        {
            GeometryUtility.CalculateFrustumPlanes(camera, cameraPlanes);
        }

        for (int i = 0; i < chunks.Count; i++)
        {
            ChunkData chunk = chunks[i];
            if (chunk.bounds.SqrDistance(cameraPosition) > maxDistanceSqr)
            {
                continue;
            }

            if (cullByFrustum && !GeometryUtility.TestPlanesAABB(cameraPlanes, chunk.bounds))
            {
                continue;
            }

            bool useFarLod = chunk.bounds.SqrDistance(cameraPosition) > farLodDistanceSqr;
            List<Matrix4x4[]> batches = useFarLod ? chunk.farBatches : chunk.nearBatches;
            List<int> counts = useFarLod ? chunk.farCounts : chunk.nearCounts;
            DrawBatches(batches, counts, renderLayer, camera);
        }
    }

    bool ShouldRenderForCamera(Camera camera)
    {
        if (!enabled || !gameObject.activeInHierarchy || !camera.enabled)
        {
            return false;
        }

        if (camera.cameraType == CameraType.Preview || camera.cameraType == CameraType.Reflection)
        {
            return false;
        }

        if (!drawInSceneView && camera.cameraType == CameraType.SceneView)
        {
            return false;
        }

        return true;
    }

    void DrawBatches(List<Matrix4x4[]> batches, List<int> counts, int renderLayer, Camera camera)
    {
        for (int i = 0; i < batches.Count; i++)
        {
            Graphics.DrawMeshInstanced(
                bladeClusterMesh,
                0,
                grassMaterial,
                batches[i],
                counts[i],
                propertyBlock,
                shadowCasting,
                receiveShadows,
                renderLayer,
                camera,
                LightProbeUsage.Off,
                null);
        }
    }

    static void AddBatches(List<Matrix4x4> matrices, List<Matrix4x4[]> batches, List<int> counts)
    {
        int index = 0;
        while (index < matrices.Count)
        {
            int count = Mathf.Min(MaxInstancesPerDraw, matrices.Count - index);
            Matrix4x4[] batch = new Matrix4x4[MaxInstancesPerDraw];
            for (int i = 0; i < count; i++)
            {
                batch[i] = matrices[index + i];
            }

            batches.Add(batch);
            counts.Add(count);
            index += count;
        }
    }

    static float EvaluateValley(float localX, float localZ, Vector3 terrainSize)
    {
        float centerX = terrainSize.x * 0.5f + Mathf.Sin(localZ * 0.035f) * terrainSize.x * 0.075f;
        float distance = Mathf.Abs(localX - centerX);
        float width = terrainSize.x * 0.18f;
        return Mathf.Exp(-(distance * distance) / (width * width));
    }

    static Vector2 SanitizeRange(Vector2 range, float min, float max)
    {
        range.x = Mathf.Clamp(range.x, min, max);
        range.y = Mathf.Clamp(range.y, min, max);
        if (range.y < range.x)
        {
            float temp = range.x;
            range.x = range.y;
            range.y = temp;
        }

        return range;
    }

    static float NextFloat(System.Random random)
    {
        return (float)random.NextDouble();
    }
}
