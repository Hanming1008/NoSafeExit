using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class GridInventorySpriteGenerator
{
    private const string OutputFolder = "Assets/Data/UI/Icons/GridInventory";
    private const string ItemIconOutputFolder = "Assets/Data/UI/Icons/Items";

    private readonly struct WeaponGridJob
    {
        public readonly string WeaponAssetPath;
        public readonly string FallbackPrefabPath;
        public readonly string OutputPath;
        public readonly Vector3 RotationEuler;
        public readonly Vector3 PositionOffset;
        public readonly float OrthoSizeScale;

        public WeaponGridJob(
            string weaponAssetPath,
            string fallbackPrefabPath,
            string outputPath,
            Vector3 rotationEuler,
            Vector3 positionOffset,
            float orthoSizeScale)
        {
            WeaponAssetPath = weaponAssetPath;
            FallbackPrefabPath = fallbackPrefabPath;
            OutputPath = outputPath;
            RotationEuler = rotationEuler;
            PositionOffset = positionOffset;
            OrthoSizeScale = orthoSizeScale;
        }
    }

    private readonly struct ItemSpriteJob
    {
        public readonly string ItemAssetPath;
        public readonly string OutputName;
        public readonly Vector3 RotationEuler;
        public readonly Vector3 PositionOffset;
        public readonly float IconOrthoScale;
        public readonly float GridOrthoScale;

        public ItemSpriteJob(
            string itemAssetPath,
            string outputName,
            Vector3 rotationEuler,
            Vector3 positionOffset,
            float iconOrthoScale,
            float gridOrthoScale)
        {
            ItemAssetPath = itemAssetPath;
            OutputName = outputName;
            RotationEuler = rotationEuler;
            PositionOffset = positionOffset;
            IconOrthoScale = iconOrthoScale;
            GridOrthoScale = gridOrthoScale;
        }
    }

    [MenuItem("Tools/NoSafeExit/Generate Grid Inventory Sprites")]
    public static void GenerateGridInventorySprites()
    {
        EnsureFolder("Assets/Data/UI");
        EnsureFolder("Assets/Data/UI/Icons");
        EnsureFolder(OutputFolder);
        EnsureFolder(ItemIconOutputFolder);

        var jobs = new[]
        {
            new WeaponGridJob(
                "Assets/Data/Items/Weapons/Weapon_HK416.asset",
                "Assets/Synty/PolygonMilitary/Prefabs/Weapons/Modular_Presets/SM_Wep_Preset_A_Rifle_02.prefab",
                OutputFolder + "/Grid_HK416.png",
                new Vector3(0f, 270f, 0f),
                Vector3.zero,
                1.08f),
            new WeaponGridJob(
                "Assets/Data/Items/Weapons/Weapon_AK47.asset",
                "Assets/Synty/PolygonMilitary/Prefabs/Weapons/Modular_Presets/SM_Wep_Preset_B_Rifle_03.prefab",
                OutputFolder + "/Grid_AK47.png",
                new Vector3(0f, 270f, 0f),
                Vector3.zero,
                1.08f),
            new WeaponGridJob(
                "Assets/Data/Items/Weapons/Weapon_Glock.asset",
                "Assets/Julhiecio TPS Controller/Demos/Demo Prefabs/Items/Weapons/Guns/P226.prefab",
                OutputFolder + "/Grid_Glock.png",
                new Vector3(0f, 270f, 0f),
                new Vector3(0f, -0.015f, 0f),
                1.18f),
        };

        foreach (var job in jobs)
        {
            GenerateWeaponGridSprite(job);
        }

        var itemJobs = new[]
        {
            new ItemSpriteJob(
                "Assets/Data/Items/Debug/Debug_Ammo_556.asset",
                "Debug_Ammo_556",
                new Vector3(18f, 225f, 0f),
                new Vector3(0f, -0.01f, 0f),
                1.08f,
                1.08f),
            new ItemSpriteJob(
                "Assets/Data/Items/Ammo/Ammo_556x45mm.asset",
                "Ammo_556x45mm",
                new Vector3(18f, 225f, 0f),
                new Vector3(0f, -0.01f, 0f),
                1.08f,
                1.08f),
            new ItemSpriteJob(
                "Assets/Data/Items/Ammo/Ammo_9x19mm.asset",
                "Ammo_9x19mm",
                new Vector3(18f, 225f, 0f),
                new Vector3(0f, -0.01f, 0f),
                1.08f,
                1.08f),
            new ItemSpriteJob(
                "Assets/Data/Items/Debug/Debug_Medkit.asset",
                "Debug_Medkit",
                new Vector3(84f, 180f, 0f),
                new Vector3(0f, -0.005f, 0f),
                1.02f,
                1.06f),
            new ItemSpriteJob(
                "Assets/Data/Items/Consumables/Consumable_Water.asset",
                "Consumable_Water",
                new Vector3(10f, 215f, 0f),
                new Vector3(0.015f, -0.02f, 0f),
                1.05f,
                1.12f),
            new ItemSpriteJob(
                "Assets/Data/Items/Consumables/Consumable_Food.asset",
                "Consumable_Food",
                new Vector3(14f, 220f, 0f),
                new Vector3(0.01f, -0.025f, 0f),
                1.08f,
                1.08f),
        };

        foreach (var job in itemJobs)
        {
            GenerateItemSprites(job);
        }

        AssignIconFallbacks();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Generated grid inventory sprites for the first-pass weapon set.");
    }

    private static void GenerateItemSprites(ItemSpriteJob job)
    {
        var itemAsset = AssetDatabase.LoadAssetAtPath<ItemDefinition>(job.ItemAssetPath);
        if (itemAsset == null)
            throw new InvalidOperationException("Missing item asset at " + job.ItemAssetPath);

        var prefab = itemAsset.worldPrefab;
        if (prefab == null)
            throw new InvalidOperationException("Missing world prefab for item render at " + job.ItemAssetPath);

        var iconOutputPath = ItemIconOutputFolder + "/Icon_" + job.OutputName + ".png";
        var gridOutputPath = OutputFolder + "/Grid_" + job.OutputName + ".png";

        var iconSprite = RenderPrefabToSprite(
            prefab,
            512,
            512,
            job.RotationEuler,
            job.PositionOffset,
            job.IconOrthoScale,
            iconOutputPath,
            "ITEM_ICON_" + itemAsset.name);

        var gridWidth = Mathf.Max(256, itemAsset.inventoryColumns * 256);
        var gridHeight = Mathf.Max(256, itemAsset.inventoryRows * 256);
        var gridSprite = RenderPrefabToSprite(
            prefab,
            gridWidth,
            gridHeight,
            job.RotationEuler,
            job.PositionOffset,
            job.GridOrthoScale,
            gridOutputPath,
            "ITEM_GRID_" + itemAsset.name);

        itemAsset.icon = iconSprite;
        itemAsset.gridInventorySprite = gridSprite;
        EditorUtility.SetDirty(itemAsset);
    }

    private static void GenerateWeaponGridSprite(WeaponGridJob job)
    {
        var weaponAsset = AssetDatabase.LoadAssetAtPath<WeaponItemDefinition>(job.WeaponAssetPath);
        if (weaponAsset == null)
            throw new InvalidOperationException("Missing weapon asset at " + job.WeaponAssetPath);

        var prefab = string.IsNullOrEmpty(job.FallbackPrefabPath)
            ? null
            : AssetDatabase.LoadAssetAtPath<GameObject>(job.FallbackPrefabPath);

        if (prefab == null)
            prefab = weaponAsset.equippedPrefab;

        if (prefab == null)
            throw new InvalidOperationException("Missing prefab for grid inventory render at " + job.WeaponAssetPath);

        var textureWidth = Mathf.Max(256, weaponAsset.inventoryColumns * 256);
        var textureHeight = Mathf.Max(256, weaponAsset.inventoryRows * 256);

        weaponAsset.gridInventorySprite = RenderPrefabToSprite(
            prefab,
            textureWidth,
            textureHeight,
            job.RotationEuler,
            job.PositionOffset,
            job.OrthoSizeScale,
            job.OutputPath,
            "WEAPON_GRID_" + weaponAsset.name);
        EditorUtility.SetDirty(weaponAsset);
    }

    private static Sprite RenderPrefabToSprite(
        GameObject prefab,
        int textureWidth,
        int textureHeight,
        Vector3 rotationEuler,
        Vector3 positionOffset,
        float orthoSizeScale,
        string outputPath,
        string tempName)
    {
        var aspect = textureWidth / (float)textureHeight;

        GameObject instance = null;
        GameObject cameraGo = null;
        GameObject keyLightGo = null;
        GameObject fillLightGo = null;
        RenderTexture renderTexture = null;
        Texture2D texture = null;

        try
        {
            instance = Object.Instantiate(prefab);
            if (instance == null)
                throw new InvalidOperationException("Could not instantiate prefab for render " + prefab.name);

            instance.name = "TEMP_" + tempName;
            instance.hideFlags = HideFlags.HideAndDontSave;
            SetLayerRecursively(instance, 31);

            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            var bounds = CalculateBounds(instance);
            var center = bounds.center + positionOffset;
            var extents = bounds.extents;

            cameraGo = new GameObject("TEMP_CAMERA_" + tempName)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.cullingMask = 1 << 31;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            camera.orthographic = true;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 50f;
            camera.aspect = aspect;
            camera.orthographicSize = Mathf.Max(extents.y, extents.x / aspect) * orthoSizeScale;
            cameraGo.transform.rotation = Quaternion.Euler(rotationEuler);
            cameraGo.transform.position = center - (cameraGo.transform.forward * 4f);

            keyLightGo = CreateLight("TEMP_KEY_" + tempName, new Vector3(40f, 135f, 0f), 0.82f, new Color(0.86f, 0.88f, 0.92f));
            fillLightGo = CreateLight("TEMP_FILL_" + tempName, new Vector3(330f, 315f, 0f), 0.18f, new Color(0.62f, 0.66f, 0.74f));

            renderTexture = new RenderTexture(textureWidth, textureHeight, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4
            };
            renderTexture.Create();
            camera.targetTexture = renderTexture;
            camera.Render();

            var previousActive = RenderTexture.active;
            RenderTexture.active = renderTexture;
            texture = new Texture2D(textureWidth, textureHeight, TextureFormat.ARGB32, false);
            texture.ReadPixels(new Rect(0, 0, textureWidth, textureHeight), 0, 0);
            texture.Apply();
            RenderTexture.active = previousActive;

            File.WriteAllBytes(GetAbsoluteProjectPath(outputPath), texture.EncodeToPNG());
            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(outputPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(outputPath);
        }
        finally
        {
            if (instance != null)
                Object.DestroyImmediate(instance);
            if (cameraGo != null)
                Object.DestroyImmediate(cameraGo);
            if (keyLightGo != null)
                Object.DestroyImmediate(keyLightGo);
            if (fillLightGo != null)
                Object.DestroyImmediate(fillLightGo);
            if (renderTexture != null)
            {
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
            }
            if (texture != null)
                Object.DestroyImmediate(texture);
        }
    }

    private static void AssignIconFallbacks()
    {
        var guids = AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/Data/Items" });
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (item == null)
                continue;

            if (item.gridInventorySprite != null)
                continue;

            if (item.icon == null)
                continue;

            item.gridInventorySprite = item.icon;
            EditorUtility.SetDirty(item);
        }
    }

    private static GameObject CreateLight(string name, Vector3 eulerAngles, float intensity, Color color)
    {
        var lightGo = new GameObject(name)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = intensity;
        light.color = color;
        light.shadows = LightShadows.None;
        light.cullingMask = 1 << 31;
        lightGo.transform.rotation = Quaternion.Euler(eulerAngles);
        return lightGo;
    }

    private static Bounds CalculateBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return new Bounds(root.transform.position, Vector3.one * 0.5f);

        var bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        foreach (Transform child in root.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private static void EnsureFolder(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath))
            return;

        var parent = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
        var folderName = Path.GetFileName(assetPath);

        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(folderName))
            throw new InvalidOperationException("Invalid folder path: " + assetPath);

        AssetDatabase.CreateFolder(parent, folderName);
    }

    private static string GetAbsoluteProjectPath(string assetPath)
    {
        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrEmpty(projectRoot))
            throw new InvalidOperationException("Could not resolve project root.");

        return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
    }
}
