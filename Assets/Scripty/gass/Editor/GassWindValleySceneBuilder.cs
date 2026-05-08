#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GassWindValleySceneBuilder
{
    const string Root = "Assets/Scripty/gass";
    const string TerrainName = "Gass Wind Valley Terrain";
    const string GrassRendererName = "Gass NPR Wind Grass Renderer";
    const string WindFieldName = "Gass Directional Wind Field";
    const string TerrainDataPath = Root + "/Terrain/GassWindValleyTerrainData.asset";
    const string GrassMeshPath = Root + "/Meshes/GassGrassBladeCluster.asset";
    const string WindTexturePath = Root + "/Textures/GassWindFlow.png";
    const string GroundTexturePath = Root + "/Textures/GassWheatGroundNoise.png";
    const string GrassMaterialPath = Root + "/Materials/M_Gass_NPR_WindGrass.mat";
    const string TerrainMaterialPath = Root + "/Materials/M_Gass_NPR_WindValleyTerrain.mat";
    const string SkyboxMaterialPath = Root + "/Materials/M_Gass_Warm_Skybox.mat";

    [MenuItem("Tools/Gass/Build NPR Wind Valley Grass Scene")]
    public static void BuildActiveGassScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || activeScene.name != "gass")
        {
            Debug.LogError("Gass builder refused to run because the active scene is not named 'gass'.");
            return;
        }

        EnsureFolders();

        Texture2D windTexture = CreateWindTexture(WindTexturePath, 256);
        Texture2D groundTexture = CreateGroundTexture(GroundTexturePath, 256);
        Mesh grassMesh = CreateGrassBladeClusterMesh(GrassMeshPath);
        Material grassMaterial = CreateGrassMaterial(windTexture);
        Material terrainMaterial = CreateTerrainMaterial(groundTexture);
        Terrain terrain = CreateTerrain(terrainMaterial);
        GassWindField windField = CreateWindField();
        CreateGrassRenderer(terrain, grassMesh, grassMaterial, windTexture, windField);
        ConfigureEnvironment();
        ConfigureLightingAndCamera(terrain);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        Debug.Log("Gass NPR wind valley grass scene generated in the active gass scene.");
    }

    static void EnsureFolders()
    {
        string[] folders =
        {
            Root + "/Scripts",
            Root + "/Shaders",
            Root + "/Editor",
            Root + "/Materials",
            Root + "/Textures",
            Root + "/Meshes",
            Root + "/Terrain"
        };

        foreach (string folder in folders)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
                string name = Path.GetFileName(folder);
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }

    static Texture2D CreateWindTexture(string assetPath, int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size;
                float v = (float)y / size;
                float a = Mathf.PerlinNoise(u * 4.3f + 11.2f, v * 4.3f + 3.9f);
                float b = Mathf.PerlinNoise(u * 9.1f + 1.7f, v * 9.1f + 18.4f);
                float c = Mathf.PerlinNoise(u * 18.5f + 7.5f, v * 18.5f + 22.1f);
                float swirl = Mathf.Sin((u + v) * Mathf.PI * 6f) * 0.5f + 0.5f;
                pixels[y * size + x] = new Color(
                    Mathf.Lerp(a, c, 0.28f),
                    Mathf.Lerp(b, swirl, 0.22f),
                    Mathf.Lerp(a, b, 0.5f),
                    1f);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        WriteTexture(assetPath, texture, false);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
    }

    static Texture2D CreateGroundTexture(string assetPath, int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size;
                float v = (float)y / size;
                float broad = Mathf.PerlinNoise(u * 7.5f, v * 7.5f);
                float fine = Mathf.PerlinNoise(u * 31.0f + 5.3f, v * 31.0f + 9.2f);
                float value = Mathf.Clamp01(broad * 0.72f + fine * 0.28f);
                pixels[y * size + x] = new Color(value, value, value, 1f);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        WriteTexture(assetPath, texture, true);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
    }

    static void WriteTexture(string assetPath, Texture2D texture, bool srgb)
    {
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        File.WriteAllBytes(fullPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.sRGBTexture = srgb;
            importer.SaveAndReimport();
        }
    }

    static Material CreateGrassMaterial(Texture2D windTexture)
    {
        Shader shader = Shader.Find("Gass/NPR Wind Grass");
        if (shader == null)
        {
            Debug.LogError("Missing shader: Gass/NPR Wind Grass");
            return null;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(GrassMaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, GrassMaterialPath);
        }

        material.shader = shader;
        material.enableInstancing = true;
        material.SetColor("_BaseColor", new Color(0.82f, 0.64f, 0.31f, 1f));
        material.SetColor("_TipColor", new Color(1.0f, 0.93f, 0.68f, 1f));
        material.SetColor("_ShadowColor", new Color(0.46f, 0.35f, 0.16f, 1f));
        material.SetColor("_RimColor", new Color(1.0f, 0.98f, 0.86f, 1f));
        material.SetTexture("_WindTex", windTexture);
        material.SetFloat("_WindStrength", 0.58f);
        material.SetFloat("_WindSpeed", 1.65f);
        material.SetFloat("_WindScale", 0.035f);
        material.SetFloat("_GustStrength", 0.42f);
        material.SetFloat("_GustScale", 2.35f);
        material.SetFloat("_BladeLean", 0.34f);
        material.SetFloat("_NprSteps", 3f);
        EditorUtility.SetDirty(material);
        return material;
    }

    static Material CreateTerrainMaterial(Texture2D groundTexture)
    {
        Shader shader = Shader.Find("Gass/NPR Wind Valley Terrain");
        if (shader == null)
        {
            Debug.LogError("Missing shader: Gass/NPR Wind Valley Terrain");
            return null;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(TerrainMaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, TerrainMaterialPath);
        }

        material.shader = shader;
        material.SetTexture("_GroundTex", groundTexture);
        material.SetColor("_BaseColor", new Color(0.66f, 0.50f, 0.19f, 1f));
        material.SetColor("_RidgeColor", new Color(0.95f, 0.73f, 0.27f, 1f));
        material.SetColor("_ValleyColor", new Color(0.48f, 0.35f, 0.13f, 1f));
        material.SetColor("_ShadowColor", new Color(0.32f, 0.27f, 0.13f, 1f));
        material.SetFloat("_NoiseScale", 0.038f);
        material.SetFloat("_ValleyWidth", 36f);
        material.SetFloat("_NprSteps", 3f);
        EditorUtility.SetDirty(material);
        return material;
    }

    static Mesh CreateGrassBladeClusterMesh(string assetPath)
    {
        Mesh mesh = new Mesh();
        mesh.name = "GassGrassBladeCluster";

        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<Color> colors = new List<Color>();
        List<int> indices = new List<int>();

        int bladeCount = 5;
        int segments = 3;
        for (int blade = 0; blade < bladeCount; blade++)
        {
            float angle = blade * (360f / bladeCount) + Mathf.Sin(blade * 12.9898f) * 13f;
            Quaternion rotation = Quaternion.Euler(0f, angle, 0f);
            Vector3 forward = rotation * Vector3.forward;
            Vector3 right = rotation * Vector3.right;
            float height = Mathf.Lerp(0.86f, 1.24f, Frac(Mathf.Sin(blade * 19.17f) * 43.37f));
            float width = Mathf.Lerp(0.16f, 0.28f, Frac(Mathf.Sin(blade * 27.03f) * 91.11f));
            float sideLean = Mathf.Lerp(-0.18f, 0.18f, Frac(Mathf.Sin(blade * 8.31f) * 17.19f));

            int startIndex = vertices.Count;
            for (int segment = 0; segment <= segments; segment++)
            {
                float t = (float)segment / segments;
                float taper = Mathf.Lerp(1f, 0.08f, t);
                Vector3 center = forward * (t * t * 0.42f) + right * (sideLean * t * t);
                center.y = t * height;
                Vector3 left = center - right * width * taper;
                Vector3 rightPos = center + right * width * taper;
                vertices.Add(left);
                vertices.Add(rightPos);
                normals.Add((forward + Vector3.up * 0.25f).normalized);
                normals.Add((forward + Vector3.up * 0.25f).normalized);
                uvs.Add(new Vector2(0f, t));
                uvs.Add(new Vector2(1f, t));
                colors.Add(new Color(1f, 1f, 1f, Mathf.Lerp(0.72f, 1f, t)));
                colors.Add(new Color(1f, 1f, 1f, Mathf.Lerp(0.72f, 1f, t)));
            }

            for (int segment = 0; segment < segments; segment++)
            {
                int i = startIndex + segment * 2;
                indices.Add(i);
                indices.Add(i + 2);
                indices.Add(i + 1);
                indices.Add(i + 1);
                indices.Add(i + 2);
                indices.Add(i + 3);
            }
        }

        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colors);
        mesh.SetTriangles(indices, 0);
        mesh.RecalculateBounds();

        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, assetPath);
            return mesh;
        }

        EditorUtility.CopySerialized(mesh, existing);
        existing.name = "GassGrassBladeCluster";
        EditorUtility.SetDirty(existing);
        Object.DestroyImmediate(mesh);
        return existing;
    }

    static Terrain CreateTerrain(Material terrainMaterial)
    {
        const int resolution = 257;
        TerrainData terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
        if (terrainData == null)
        {
            terrainData = new TerrainData();
            AssetDatabase.CreateAsset(terrainData, TerrainDataPath);
        }

        terrainData.heightmapResolution = resolution;
        terrainData.size = new Vector3(220f, 28f, 220f);
        terrainData.SetHeights(0, 0, BuildTerrainHeights(resolution, terrainData.size));
        terrainData.wavingGrassAmount = 0.12f;
        terrainData.wavingGrassSpeed = 0.45f;
        terrainData.wavingGrassStrength = 0.5f;
        EditorUtility.SetDirty(terrainData);

        GameObject terrainObject = GameObject.Find(TerrainName);
        if (terrainObject == null)
        {
            terrainObject = Terrain.CreateTerrainGameObject(terrainData);
            terrainObject.name = TerrainName;
        }

        terrainObject.transform.position = new Vector3(-terrainData.size.x * 0.5f, 0f, -terrainData.size.z * 0.5f);
        Terrain terrain = terrainObject.GetComponent<Terrain>();
        if (terrain == null)
        {
            terrain = terrainObject.AddComponent<Terrain>();
        }

        TerrainCollider collider = terrainObject.GetComponent<TerrainCollider>();
        if (collider == null)
        {
            collider = terrainObject.AddComponent<TerrainCollider>();
        }

        terrain.terrainData = terrainData;
        terrain.materialTemplate = terrainMaterial;
        terrain.drawInstanced = true;
        terrain.heightmapPixelError = 3.2f;
        terrain.basemapDistance = 1200f;
        collider.terrainData = terrainData;
        return terrain;
    }

    static float[,] BuildTerrainHeights(int resolution, Vector3 terrainSize)
    {
        float[,] heights = new float[resolution, resolution];
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float u = (float)x / (resolution - 1);
                float v = (float)y / (resolution - 1);
                float localX = u * terrainSize.x;
                float localZ = v * terrainSize.z;
                float center = terrainSize.x * 0.5f + Mathf.Sin(localZ * 0.035f) * terrainSize.x * 0.075f;
                float valley = Mathf.Exp(-Mathf.Pow(Mathf.Abs(localX - center) / (terrainSize.x * 0.22f), 2f));
                float edgeLift = Mathf.Pow(Mathf.Abs(u - 0.5f) * 2f, 1.85f) * 0.28f;
                float longRidge = Mathf.Sin(v * Mathf.PI * 4.3f + Mathf.Sin(u * 5.1f)) * 0.018f;
                float broadNoise = (Mathf.PerlinNoise(u * 4.6f + 12.1f, v * 4.6f + 6.2f) - 0.5f) * 0.06f;
                float fineNoise = (Mathf.PerlinNoise(u * 16.0f + 1.3f, v * 16.0f + 8.7f) - 0.5f) * 0.018f;
                float height = 0.18f + edgeLift + longRidge + broadNoise + fineNoise - valley * 0.18f;
                heights[y, x] = Mathf.Clamp01(height);
            }
        }

        return heights;
    }

    static GassWindField CreateWindField()
    {
        GameObject windObject = GameObject.Find(WindFieldName);
        if (windObject == null)
        {
            windObject = new GameObject(WindFieldName);
        }

        windObject.transform.position = new Vector3(-36f, 14f, -44f);
        windObject.transform.rotation = Quaternion.LookRotation(new Vector3(1f, 0f, 0.28f).normalized, Vector3.up);

        WindZone windZone = windObject.GetComponent<WindZone>();
        if (windZone == null)
        {
            windZone = windObject.AddComponent<WindZone>();
        }

        GassWindField windField = windObject.GetComponent<GassWindField>();
        if (windField == null)
        {
            windField = windObject.AddComponent<GassWindField>();
        }

        windField.windZone = windZone;
        windField.windDirection = new Vector3(1f, 0f, 0.28f);
        windField.strength = 0.58f;
        windField.speed = 1.65f;
        windField.scale = 0.035f;
        windField.gustStrength = 0.42f;
        windField.gustScale = 2.35f;
        windField.ApplyGlobals();
        EditorUtility.SetDirty(windObject);
        return windField;
    }

    static void CreateGrassRenderer(Terrain terrain, Mesh mesh, Material grassMaterial, Texture2D windTexture, GassWindField windField)
    {
        GameObject grassObject = GameObject.Find(GrassRendererName);
        if (grassObject == null)
        {
            grassObject = new GameObject(GrassRendererName);
        }

        grassObject.transform.position = Vector3.zero;
        GassNprGrassRenderer renderer = grassObject.GetComponent<GassNprGrassRenderer>();
        if (renderer == null)
        {
            renderer = grassObject.AddComponent<GassNprGrassRenderer>();
        }

        renderer.targetTerrain = terrain;
        renderer.bladeClusterMesh = mesh;
        renderer.grassMaterial = grassMaterial;
        renderer.windTexture = windTexture;
        renderer.windField = windField;
        renderer.targetBladeClusters = 32000;
        renderer.chunksPerAxis = 10;
        renderer.terrainPadding = 2f;
        renderer.heightRange = new Vector2(1.45f, 2.85f);
        renderer.widthRange = new Vector2(0.88f, 1.45f);
        renderer.valleyHeightBoost = -0.12f;
        renderer.valleyDensityDrop = 0.18f;
        renderer.drawInSceneView = true;
        renderer.cullByFrustum = true;
        renderer.maxRenderDistance = 132f;
        renderer.farLodStartDistance = 72f;
        renderer.farLodStride = 3;
        renderer.boundsHeightPadding = 6f;
        renderer.shadowCasting = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = true;
        renderer.seed = 20260507;
        renderer.Rebuild();
        EditorUtility.SetDirty(grassObject);
    }

    static void ConfigureEnvironment()
    {
        Shader skyboxShader = Shader.Find("Skybox/Procedural");
        if (skyboxShader != null)
        {
            Material skybox = AssetDatabase.LoadAssetAtPath<Material>(SkyboxMaterialPath);
            if (skybox == null)
            {
                skybox = new Material(skyboxShader);
                AssetDatabase.CreateAsset(skybox, SkyboxMaterialPath);
            }

            skybox.shader = skyboxShader;
            skybox.SetColor("_SkyTint", new Color(0.62f, 0.79f, 1f, 1f));
            skybox.SetColor("_GroundColor", new Color(0.62f, 0.55f, 0.42f, 1f));
            skybox.SetFloat("_AtmosphereThickness", 0.78f);
            skybox.SetFloat("_Exposure", 1.02f);
            RenderSettings.skybox = skybox;
            EditorUtility.SetDirty(skybox);
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.70f, 0.76f, 0.82f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.62f, 0.52f, 0.34f, 1f);
        RenderSettings.ambientGroundColor = new Color(0.26f, 0.22f, 0.16f, 1f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.70f, 0.79f, 0.92f, 1f);
        RenderSettings.fogDensity = 0.0032f;
    }

    static void ConfigureLightingAndCamera(Terrain terrain)
    {
        Light sun = Object.FindObjectOfType<Light>();
        if (sun != null)
        {
            sun.name = "Directional Light";
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(43f, -38f, 0f);
            sun.intensity = 1.28f;
            sun.color = new Color(1f, 0.9f, 0.66f, 1f);
            EditorUtility.SetDirty(sun);
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = GameObject.Find("Main Camera");
            if (cameraObject == null)
            {
                cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
            }

            camera = cameraObject.GetComponent<Camera>();
            if (camera == null)
            {
                camera = cameraObject.AddComponent<Camera>();
            }
        }

        Vector3 center = terrain.transform.position + new Vector3(terrain.terrainData.size.x * 0.5f, 0f, terrain.terrainData.size.z * 0.5f);
        Vector3 cameraPosition = center + new Vector3(-54f, 0f, -74f);
        cameraPosition.y = terrain.transform.position.y + terrain.SampleHeight(cameraPosition) + 4.4f;
        Vector3 lookTarget = center + new Vector3(0f, 0f, 26f);
        lookTarget.y = terrain.transform.position.y + terrain.SampleHeight(lookTarget) + 7.2f;
        camera.transform.position = cameraPosition;
        camera.transform.LookAt(lookTarget);
        camera.fieldOfView = 45f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 420f;
        EditorUtility.SetDirty(camera);
    }

    static float Frac(float value)
    {
        return value - Mathf.Floor(value);
    }
}
#endif
