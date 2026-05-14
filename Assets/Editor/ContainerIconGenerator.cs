using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ContainerIconGenerator
{
    private const string CharacterPrefabPath = "Assets/Synty/PolygonMilitary/Prefabs/Characters/Alt_Soldiers/SM_Chr_Soldier_Female_01_Alt_02.prefab";
    private const string OutputFolder = "Assets/Data/UI/Icons/Containers";

    [MenuItem("Tools/NoSafeExit/Generate Container Icons")]
    public static void GenerateContainerIcons()
    {
        EnsureFolder("Assets/Data/UI");
        EnsureFolder("Assets/Data/UI/Icons");
        EnsureFolder(OutputFolder);

        GenerateBackpackIcon(
            "Assets/Synty/PolygonMilitary/Prefabs/Characters/Attachments/SM_Chr_Attach_Backpack_01.prefab",
            "Assets/Data/Items/Containers/Container_Backpack_4x4.asset",
            OutputFolder + "/Icon_Backpack_Large.png");

        GenerateBackpackIcon(
            "Assets/Synty/PolygonMilitary/Prefabs/Characters/Attachments/SM_Chr_Attach_Backpack_02.prefab",
            "Assets/Data/Items/Containers/Container_Backpack_Basic.asset",
            OutputFolder + "/Icon_Backpack_Basic.png");

        GenerateArmorVisualIcon(
            CharacterPrefabPath,
            "Assets/Data/Items/Armor/Armor_Body_LevelI.asset",
            OutputFolder + "/Icon_ChestRig_Operator.png",
            new[]
            {
                "SM_Chr_Attach_Padding_01",
                "SM_Chr_Attach_Nameplate_08",
                "SM_Chr_Attach_Pouch_Mag_Single_Handle_01",
                "SM_Chr_Attach_Pouch_Mag_Single_Handle_01 (1)",
                "SM_Chr_Attach_Pouch_Mag_Pistol_01",
                "SM_Chr_Attach_Pouch_Mag_Pistol_01 (1)",
                "SM_Chr_Attach_Pouch_Mag_Double_01",
                "SM_Chr_Attach_Pouch_Mag_Double_01 (1)",
                "SM_Chr_Attach_Grenade_Flash_01"
            });

        GenerateArmorPrefabIcon(
            "Assets/Synty/PolygonMilitary/Prefabs/Characters/Attachments/SM_Chr_Attach_Helmet_07.prefab",
            "Assets/Data/Items/Armor/Armor_Helmet_Operator.asset",
            OutputFolder + "/Icon_Helmet_Operator.png");

        GenerateArmorPrefabIcon(
            "Assets/Synty/PolygonMilitary/Prefabs/Characters/Attachments/SM_Chr_Attach_Helmet_09.prefab",
            "Assets/Data/Items/Armor/Armor_Helmet_LevelI.asset",
            OutputFolder + "/Icon_Helmet_LevelI.png");

        GenerateArmorVisualIcon(
            CharacterPrefabPath,
            "Assets/Data/Items/Armor/Armor_Helmet_LevelIII.asset",
            OutputFolder + "/Icon_Helmet_LevelIII.png",
            new[]
            {
                "SM_Chr_Attach_Helmet_01_Goggles_01",
                "SM_Chr_Attach_Helmet_01_Goggles_01_Glass",
                "SM_Chr_Attach_Helmet_02",
                "SM_Chr_Attach_NVG_03"
            });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Generated container icons.");
    }

    private static void GenerateBackpackIcon(string prefabPath, string assetPath, string outputPath)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var containerAsset = AssetDatabase.LoadAssetAtPath<ContainerItemDefinition>(assetPath);

        if (prefab == null)
            throw new InvalidOperationException("Missing prefab at " + prefabPath);

        if (containerAsset == null)
            throw new InvalidOperationException("Missing container asset at " + assetPath);

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

            instance.name = "TEMP_CONTAINER_ICON_" + containerAsset.name;
            instance.hideFlags = HideFlags.HideAndDontSave;
            SetLayerRecursively(instance, 31);

            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            var bounds = CalculateBounds(instance);
            var center = bounds.center + new Vector3(0.02f, -0.02f, 0f);
            var extents = bounds.extents;

            cameraGo = new GameObject("TEMP_CONTAINER_CAMERA_" + containerAsset.name)
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
            camera.aspect = 1f;
            camera.orthographicSize = Mathf.Max(extents.y, extents.x) * 1.12f;
            cameraGo.transform.rotation = Quaternion.Euler(8f, 225f, 0f);
            cameraGo.transform.position = center - (cameraGo.transform.forward * 4f);

            keyLightGo = CreateLight("TEMP_CONTAINER_KEY_" + containerAsset.name, new Vector3(35f, 150f, 0f), 0.84f, new Color(0.86f, 0.88f, 0.92f));
            fillLightGo = CreateLight("TEMP_CONTAINER_FILL_" + containerAsset.name, new Vector3(330f, 300f, 0f), 0.18f, new Color(0.62f, 0.66f, 0.74f));

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

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(outputPath);
            containerAsset.icon = sprite;
            containerAsset.gridInventorySprite = sprite;
            EditorUtility.SetDirty(containerAsset);
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

    private static void GenerateArmorVisualIcon(
        string characterPrefabPath,
        string assetPath,
        string outputPath,
        string[] visibleObjectNames)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(characterPrefabPath);
        var armorAsset = AssetDatabase.LoadAssetAtPath<ArmorItemDefinition>(assetPath);

        if (prefab == null)
            throw new InvalidOperationException("Missing character prefab at " + characterPrefabPath);

        if (armorAsset == null)
            throw new InvalidOperationException("Missing armor asset at " + assetPath);

        if (visibleObjectNames == null || visibleObjectNames.Length == 0)
            throw new InvalidOperationException("No visual object names provided for " + assetPath);

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
                throw new InvalidOperationException("Could not instantiate prefab " + characterPrefabPath);

            instance.name = "TEMP_ARMOR_ICON_" + armorAsset.name;
            instance.hideFlags = HideFlags.HideAndDontSave;
            SetLayerRecursively(instance, 31);

            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = false;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            EnableNamedRenderers(instance.transform, visibleObjectNames);

            var bounds = CalculateBounds(instance, includeDisabled: false);
            var center = bounds.center + new Vector3(0.02f, -0.02f, 0f);
            var extents = bounds.extents;

            cameraGo = new GameObject("TEMP_ARMOR_CAMERA_" + armorAsset.name)
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
            camera.aspect = 1f;
            camera.orthographicSize = Mathf.Max(extents.y, extents.x) * 1.08f;
            cameraGo.transform.rotation = Quaternion.Euler(6f, 215f, 0f);
            cameraGo.transform.position = center - (cameraGo.transform.forward * 4f);

            keyLightGo = CreateLight("TEMP_ARMOR_KEY_" + armorAsset.name, new Vector3(32f, 140f, 0f), 0.82f, new Color(0.86f, 0.88f, 0.92f));
            fillLightGo = CreateLight("TEMP_ARMOR_FILL_" + armorAsset.name, new Vector3(330f, 300f, 0f), 0.22f, new Color(0.62f, 0.66f, 0.74f));

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

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(outputPath);
            armorAsset.icon = sprite;
            armorAsset.gridInventorySprite = sprite;
            EditorUtility.SetDirty(armorAsset);
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

    private static void GenerateArmorPrefabIcon(string prefabPath, string assetPath, string outputPath)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var armorAsset = AssetDatabase.LoadAssetAtPath<ArmorItemDefinition>(assetPath);

        if (prefab == null)
            throw new InvalidOperationException("Missing prefab at " + prefabPath);

        if (armorAsset == null)
            throw new InvalidOperationException("Missing armor asset at " + assetPath);

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

            instance.name = "TEMP_ARMOR_PREFAB_ICON_" + armorAsset.name;
            instance.hideFlags = HideFlags.HideAndDontSave;
            SetLayerRecursively(instance, 31);

            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            var bounds = CalculateBounds(instance);
            var center = bounds.center + new Vector3(0f, -0.01f, 0f);
            var extents = bounds.extents;

            cameraGo = new GameObject("TEMP_ARMOR_PREFAB_CAMERA_" + armorAsset.name)
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
            camera.aspect = 1f;
            camera.orthographicSize = Mathf.Max(extents.y, extents.x) * 1.14f;
            cameraGo.transform.rotation = Quaternion.Euler(8f, 220f, 0f);
            cameraGo.transform.position = center - (cameraGo.transform.forward * 4f);

            keyLightGo = CreateLight("TEMP_ARMOR_PREFAB_KEY_" + armorAsset.name, new Vector3(35f, 150f, 0f), 0.84f, new Color(0.86f, 0.88f, 0.92f));
            fillLightGo = CreateLight("TEMP_ARMOR_PREFAB_FILL_" + armorAsset.name, new Vector3(330f, 300f, 0f), 0.20f, new Color(0.62f, 0.66f, 0.74f));

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

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(outputPath);
            armorAsset.icon = sprite;
            armorAsset.gridInventorySprite = sprite;
            EditorUtility.SetDirty(armorAsset);
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

    private static Bounds CalculateBounds(GameObject root, bool includeDisabled = true)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        Renderer firstRenderer = null;
        foreach (var renderer in renderers)
        {
            if (includeDisabled || renderer.enabled)
            {
                firstRenderer = renderer;
                break;
            }
        }

        if (firstRenderer == null)
            return new Bounds(root.transform.position, Vector3.one * 0.5f);

        var bounds = firstRenderer.bounds;
        foreach (var renderer in renderers)
        {
            if (!includeDisabled && !renderer.enabled)
                continue;

            bounds.Encapsulate(renderer.bounds);
        }

        return bounds;
    }

    private static void EnableNamedRenderers(Transform root, string[] names)
    {
        foreach (var objectName in names)
        {
            var match = FindChildRecursive(root, objectName);
            if (match == null)
            {
                Debug.LogWarning("ContainerIconGenerator: could not find visual object '" + objectName + "'.");
                continue;
            }

            foreach (var renderer in match.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = true;
        }
    }

    private static Transform FindChildRecursive(Transform root, string targetName)
    {
        if (root.name == targetName)
            return root;

        foreach (Transform child in root)
        {
            var found = FindChildRecursive(child, targetName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        foreach (Transform child in root.transform)
            SetLayerRecursively(child.gameObject, layer);
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
