using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class WeaponHudIconGenerator
{
    private const string OutputFolder = "Assets/Data/UI/Icons/Weapons";
    private const string EquipmentOutputFolder = "Assets/Data/UI/Icons/EquipmentSlots";

    private readonly struct WeaponIconJob
    {
        public readonly string Name;
        public readonly string PrefabPath;
        public readonly string WeaponAssetPath;
        public readonly string OutputPath;
        public readonly Vector3 Rotation;
        public readonly Vector3 PositionOffset;
        public readonly float DistanceScale;

        public WeaponIconJob(
            string name,
            string prefabPath,
            string weaponAssetPath,
            string outputPath,
            Vector3 rotation,
            Vector3 positionOffset,
            float distanceScale)
        {
            Name = name;
            PrefabPath = prefabPath;
            WeaponAssetPath = weaponAssetPath;
            OutputPath = outputPath;
            Rotation = rotation;
            PositionOffset = positionOffset;
            DistanceScale = distanceScale;
        }
    }

    [MenuItem("Tools/NoSafeExit/Generate Weapon HUD Icons")]
    public static void GenerateWeaponHudIcons()
    {
        EnsureFolder("Assets/Data/UI");
        EnsureFolder("Assets/Data/UI/Icons");
        EnsureFolder(OutputFolder);
        EnsureFolder(EquipmentOutputFolder);

        var jobs = new[]
        {
            new WeaponIconJob(
                "HK416",
                "Assets/Synty/PolygonMilitary/Prefabs/Weapons/Modular_Presets/SM_Wep_Preset_A_Rifle_02.prefab",
                "Assets/Data/Items/Weapons/Weapon_HK416.asset",
                OutputFolder + "/Icon_HK416.png",
                new Vector3(16f, 48f, 28f),
                new Vector3(0.06f, -0.01f, 0f),
                0.68f),
            new WeaponIconJob(
                "AK47",
                "Assets/Synty/PolygonMilitary/Prefabs/Weapons/Modular_Presets/SM_Wep_Preset_B_Rifle_03.prefab",
                "Assets/Data/Items/Weapons/Weapon_AK47.asset",
                OutputFolder + "/Icon_AK47.png",
                new Vector3(16f, 48f, 28f),
                new Vector3(0.07f, -0.02f, 0f),
                0.7f),
            new WeaponIconJob(
                "SVD",
                "Assets/Synty/PolygonMilitary/Prefabs/Weapons/Modular_Presets/SM_Wep_Preset_B_Sniper_01.prefab",
                "Assets/Data/Items/Weapons/Weapon_SVD.asset",
                OutputFolder + "/Icon_SVD.png",
                new Vector3(16f, 48f, 28f),
                new Vector3(0.07f, -0.02f, 0f),
                0.7f),
            new WeaponIconJob(
                "Groza",
                "Assets/Synty/PolygonMilitary/Prefabs/Weapons/Modular_Presets/SM_Wep_Preset_B_Rifle_02.prefab",
                "Assets/Data/Items/Weapons/Weapon_Groza.asset",
                OutputFolder + "/Icon_Groza.png",
                new Vector3(16f, 48f, 28f),
                new Vector3(0.07f, -0.02f, 0f),
                0.7f),
            new WeaponIconJob(
                "MK12",
                "Assets/Synty/PolygonMilitary/Prefabs/Weapons/Modular_Presets/SM_Wep_Preset_A_Sniper_01.prefab",
                "Assets/Data/Items/Weapons/Weapon_MK12.asset",
                OutputFolder + "/Icon_MK12.png",
                new Vector3(16f, 48f, 28f),
                new Vector3(0.06f, -0.01f, 0f),
                0.68f),
            new WeaponIconJob(
                "MCX",
                "Assets/Synty/PolygonMilitary/Prefabs/Weapons/Modular_Presets/SM_Wep_Preset_A_SMG_01.prefab",
                "Assets/Data/Items/Weapons/Weapon_MCX.asset",
                OutputFolder + "/Icon_MCX.png",
                new Vector3(16f, 48f, 28f),
                new Vector3(0.06f, -0.01f, 0f),
                0.68f),
        };

        foreach (var job in jobs)
        {
            GenerateSingle(job);
        }

        GenerateEquipmentSlotIcons();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Generated dedicated weapon HUD icons for HK416 and AK47, plus equipment slot rifle renders.");
    }

    private static void GenerateEquipmentSlotIcons()
    {
        GenerateEquipmentSlotSingle(
            "HK416_Equip",
            "Assets/Synty/PolygonMilitary/Prefabs/Weapons/Modular_Presets/SM_Wep_Preset_A_Rifle_02.prefab",
            "Assets/Data/Items/Weapons/Weapon_HK416.asset",
            EquipmentOutputFolder + "/Equip_HK416.png",
            new Vector3(0f, 270f, 0f),
            new Vector3(0f, 0f, 0f),
            1.05f);

        GenerateEquipmentSlotSingle(
            "AK47_Equip",
            "Assets/Synty/PolygonMilitary/Prefabs/Weapons/Modular_Presets/SM_Wep_Preset_B_Rifle_03.prefab",
            "Assets/Data/Items/Weapons/Weapon_AK47.asset",
            EquipmentOutputFolder + "/Equip_AK47.png",
            new Vector3(0f, 270f, 0f),
            new Vector3(0f, 0f, 0f),
            1.05f);

        GenerateEquipmentSlotSingle(
            "SVD_Equip",
            "Assets/Synty/PolygonMilitary/Prefabs/Weapons/Modular_Presets/SM_Wep_Preset_B_Sniper_01.prefab",
            "Assets/Data/Items/Weapons/Weapon_SVD.asset",
            EquipmentOutputFolder + "/Equip_SVD.png",
            new Vector3(0f, 270f, 0f),
            new Vector3(0f, 0f, 0f),
            1.50f);

        GenerateEquipmentSlotSingle(
            "Groza_Equip",
            "Assets/Synty/PolygonMilitary/Prefabs/Weapons/Modular_Presets/SM_Wep_Preset_B_Rifle_02.prefab",
            "Assets/Data/Items/Weapons/Weapon_Groza.asset",
            EquipmentOutputFolder + "/Equip_Groza.png",
            new Vector3(0f, 270f, 0f),
            new Vector3(0f, 0f, 0f),
            1.05f);

        GenerateEquipmentSlotSingle(
            "MK12_Equip",
            "Assets/Synty/PolygonMilitary/Prefabs/Weapons/Modular_Presets/SM_Wep_Preset_A_Sniper_01.prefab",
            "Assets/Data/Items/Weapons/Weapon_MK12.asset",
            EquipmentOutputFolder + "/Equip_MK12.png",
            new Vector3(0f, 270f, 0f),
            new Vector3(0f, 0f, 0f),
            1.05f);

        GenerateEquipmentSlotSingle(
            "MCX_Equip",
            "Assets/Synty/PolygonMilitary/Prefabs/Weapons/Modular_Presets/SM_Wep_Preset_A_SMG_01.prefab",
            "Assets/Data/Items/Weapons/Weapon_MCX.asset",
            EquipmentOutputFolder + "/Equip_MCX.png",
            new Vector3(0f, 270f, 0f),
            new Vector3(0f, 0f, 0f),
            1.05f);
    }

    private static void GenerateSingle(WeaponIconJob job)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(job.PrefabPath);
        var weaponAsset = AssetDatabase.LoadAssetAtPath<WeaponItemDefinition>(job.WeaponAssetPath);

        if (prefab == null)
        {
            throw new InvalidOperationException("Missing prefab at " + job.PrefabPath);
        }

        if (weaponAsset == null)
        {
            throw new InvalidOperationException("Missing weapon asset at " + job.WeaponAssetPath);
        }

        GameObject instance = null;
        GameObject cameraGo = null;
        GameObject keyLightGo = null;
        GameObject fillLightGo = null;
        RenderTexture renderTexture = null;
        Texture2D texture = null;

        try
        {
            instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException("Could not instantiate prefab " + job.PrefabPath);
            }

            instance.name = "TEMP_ICON_" + job.Name;
            instance.hideFlags = HideFlags.HideAndDontSave;
            SetLayerRecursively(instance, 31);

            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            var bounds = CalculateBounds(instance);
            var center = bounds.center + job.PositionOffset;
            var rotation = Quaternion.Euler(job.Rotation);
            const float fieldOfView = 24f;
            var radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
            var distance = (radius * job.DistanceScale) / Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);

            cameraGo = new GameObject("TEMP_ICON_CAMERA_" + job.Name)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.cullingMask = 1 << 31;
            camera.fieldOfView = fieldOfView;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 50f;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            camera.orthographic = false;
            cameraGo.transform.rotation = rotation;
            cameraGo.transform.position = center - (cameraGo.transform.forward * distance);

            keyLightGo = CreateLight("TEMP_ICON_KEY_" + job.Name, new Vector3(38f, 145f, 0f), 0.72f, new Color(0.82f, 0.85f, 0.9f));
            fillLightGo = CreateLight("TEMP_ICON_FILL_" + job.Name, new Vector3(340f, 300f, 0f), 0.1f, new Color(0.62f, 0.66f, 0.74f));

            renderTexture = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4
            };
            renderTexture.Create();
            camera.targetTexture = renderTexture;
            camera.Render();

            var previousActive = RenderTexture.active;
            RenderTexture.active = renderTexture;
            texture = new Texture2D(512, 512, TextureFormat.ARGB32, false);
            texture.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
            texture.Apply();
            RenderTexture.active = previousActive;

            File.WriteAllBytes(GetAbsoluteProjectPath(job.OutputPath), texture.EncodeToPNG());
            AssetDatabase.ImportAsset(job.OutputPath, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(job.OutputPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            weaponAsset.icon = AssetDatabase.LoadAssetAtPath<Sprite>(job.OutputPath);
            EditorUtility.SetDirty(weaponAsset);
        }
        finally
        {
            if (instance != null)
            {
                Object.DestroyImmediate(instance);
            }

            if (cameraGo != null)
            {
                Object.DestroyImmediate(cameraGo);
            }

            if (keyLightGo != null)
            {
                Object.DestroyImmediate(keyLightGo);
            }

            if (fillLightGo != null)
            {
                Object.DestroyImmediate(fillLightGo);
            }

            if (renderTexture != null)
            {
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
            }

            if (texture != null)
            {
                Object.DestroyImmediate(texture);
            }
        }
    }

    private static void GenerateEquipmentSlotSingle(
        string name,
        string prefabPath,
        string weaponAssetPath,
        string outputPath,
        Vector3 rotationEuler,
        Vector3 positionOffset,
        float orthoSizeScale)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var weaponAsset = AssetDatabase.LoadAssetAtPath<WeaponItemDefinition>(weaponAssetPath);

        if (prefab == null)
            throw new InvalidOperationException("Missing prefab at " + prefabPath);

        if (weaponAsset == null)
            throw new InvalidOperationException("Missing weapon asset at " + weaponAssetPath);

        GameObject instance = null;
        GameObject cameraGo = null;
        GameObject keyLightGo = null;
        GameObject fillLightGo = null;
        RenderTexture renderTexture = null;
        Texture2D texture = null;

        try
        {
            instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
                throw new InvalidOperationException("Could not instantiate prefab " + prefabPath);

            instance.name = "TEMP_EQUIP_ICON_" + name;
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

            cameraGo = new GameObject("TEMP_EQUIP_CAMERA_" + name)
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
            camera.aspect = 4f;
            camera.orthographicSize = Mathf.Max(extents.y, extents.x * 0.25f) * orthoSizeScale;
            cameraGo.transform.rotation = Quaternion.Euler(rotationEuler);
            cameraGo.transform.position = center - (cameraGo.transform.forward * 4f);

            keyLightGo = CreateLight("TEMP_EQUIP_KEY_" + name, new Vector3(40f, 135f, 0f), 0.82f, new Color(0.86f, 0.88f, 0.92f));
            fillLightGo = CreateLight("TEMP_EQUIP_FILL_" + name, new Vector3(330f, 315f, 0f), 0.18f, new Color(0.62f, 0.66f, 0.74f));

            renderTexture = new RenderTexture(1024, 256, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4
            };
            renderTexture.Create();
            camera.targetTexture = renderTexture;
            camera.Render();

            var previousActive = RenderTexture.active;
            RenderTexture.active = renderTexture;
            texture = new Texture2D(1024, 256, TextureFormat.ARGB32, false);
            texture.ReadPixels(new Rect(0, 0, 1024, 256), 0, 0);
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

            weaponAsset.equipmentSlotIcon = AssetDatabase.LoadAssetAtPath<Sprite>(outputPath);
            EditorUtility.SetDirty(weaponAsset);
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
        {
            return new Bounds(root.transform.position, Vector3.one * 0.5f);
        }

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
        {
            return;
        }

        var parent = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
        var folderName = Path.GetFileName(assetPath);

        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(folderName))
        {
            throw new InvalidOperationException("Invalid folder path: " + assetPath);
        }

        AssetDatabase.CreateFolder(parent, folderName);
    }

    private static string GetAbsoluteProjectPath(string assetPath)
    {
        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrEmpty(projectRoot))
        {
            throw new InvalidOperationException("Could not resolve project root.");
        }

        return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
    }
}
