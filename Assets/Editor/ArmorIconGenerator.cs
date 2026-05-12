using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ArmorIconGenerator
{
    private const string OutputFolder = "Assets/Data/UI/Icons/Armor";

    [MenuItem("Tools/NoSafeExit/Generate Armor Icons")]
    public static void GenerateArmorIcons()
    {
        EnsureFolder("Assets/Data/UI");
        EnsureFolder("Assets/Data/UI/Icons");
        EnsureFolder(OutputFolder);

        GenerateChestRigIcon(
            "Assets/Synty/PolygonMilitary/Prefabs/Characters/Alt_Soldiers/SM_Chr_Soldier_Female_01_Alt_02.prefab",
            "Assets/Data/Items/Debug/Debug_Chest_Rig.asset",
            OutputFolder + "/Icon_ChestRig_Operator.png");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Generated armor icons.");
    }

    private static void GenerateChestRigIcon(string sourceCharacterPrefabPath, string armorAssetPath, string outputPath)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(sourceCharacterPrefabPath);
        var armorAsset = AssetDatabase.LoadAssetAtPath<ArmorItemDefinition>(armorAssetPath);

        if (prefab == null)
            throw new InvalidOperationException("Missing prefab at " + sourceCharacterPrefabPath);

        if (armorAsset == null)
            throw new InvalidOperationException("Missing armor asset at " + armorAssetPath);

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
                throw new InvalidOperationException("Could not instantiate prefab " + sourceCharacterPrefabPath);

            instance.name = "TEMP_ARMOR_ICON_" + armorAsset.name;
            instance.hideFlags = HideFlags.HideAndDontSave;
            SetLayerRecursively(instance, 31);

            var allRenderers = instance.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in allRenderers)
            {
                renderer.enabled = false;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            var enabledRoots = new List<Transform>();
            foreach (var objectName in armorAsset.equippedVisualObjectNames)
            {
                if (string.IsNullOrWhiteSpace(objectName))
                    continue;

                var found = FindDeepChild(instance.transform, objectName);
                if (found == null)
                    continue;

                enabledRoots.Add(found);
                foreach (var renderer in found.GetComponentsInChildren<Renderer>(true))
                    renderer.enabled = true;
            }

            if (enabledRoots.Count == 0)
                throw new InvalidOperationException("No visual roots found for " + armorAsset.displayName);

            var bounds = CalculateEnabledBounds(allRenderers, instance.transform.position);
            var center = bounds.center + new Vector3(0f, 0.02f, 0f);
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
            camera.orthographicSize = Mathf.Max(extents.y, extents.x) * 1.18f;
            cameraGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            cameraGo.transform.position = center - (cameraGo.transform.forward * 4f);

            keyLightGo = CreateLight("TEMP_ARMOR_KEY_" + armorAsset.name, new Vector3(35f, 150f, 0f), 0.84f, new Color(0.86f, 0.88f, 0.92f));
            fillLightGo = CreateLight("TEMP_ARMOR_FILL_" + armorAsset.name, new Vector3(330f, 300f, 0f), 0.18f, new Color(0.62f, 0.66f, 0.74f));

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

    private static Transform FindDeepChild(Transform root, string exactName)
    {
        if (root == null || string.IsNullOrWhiteSpace(exactName))
            return null;

        if (root.name == exactName)
            return root;

        foreach (Transform child in root)
        {
            var found = FindDeepChild(child, exactName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static Bounds CalculateEnabledBounds(Renderer[] renderers, Vector3 fallbackPosition)
    {
        Bounds bounds = new Bounds(fallbackPosition, Vector3.one * 0.5f);
        bool initialized = false;

        foreach (var renderer in renderers)
        {
            if (renderer == null || !renderer.enabled)
                continue;

            if (!initialized)
            {
                bounds = renderer.bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return bounds;
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
