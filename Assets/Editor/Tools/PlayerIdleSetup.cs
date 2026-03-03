#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
using UnityEngine;

public static class PlayerIdleSetup
{
    private const string IdleFbxPath = "Assets/Animations/Player/Mixamo/Rifle_Idle.fbx";
    private const string ControllerDir = "Assets/Animations/Player/Controllers";
    private const string ControllerPath = ControllerDir + "/Player_Idle.controller";

    [MenuItem("Tools/NoSafeExit/Setup Player Idle Animation")]
    public static void SetupPlayerIdleAnimation()
    {
        if (!File.Exists(IdleFbxPath))
        {
            Debug.LogError($"Idle FBX not found: {IdleFbxPath}");
            return;
        }

        EnsureHumanoidImport(IdleFbxPath);
        AssetDatabase.ImportAsset(IdleFbxPath, ImportAssetOptions.ForceUpdate);

        var idleClip = LoadMainClip(IdleFbxPath);
        if (idleClip == null)
        {
            Debug.LogError("Could not find a valid AnimationClip in Rifle_Idle.fbx");
            return;
        }

        EnsureLooping(IdleFbxPath);
        AssetDatabase.ImportAsset(IdleFbxPath, ImportAssetOptions.ForceUpdate);

        if (!AssetDatabase.IsValidFolder(ControllerDir))
        {
            AssetDatabase.CreateFolder("Assets/Animations/Player", "Controllers");
        }

        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        var stateMachine = controller.layers[0].stateMachine;
        stateMachine.states = new ChildAnimatorState[0];

        var idleState = stateMachine.AddState("Idle_Rifle", new Vector3(250f, 120f, 0f));
        idleState.motion = idleClip;
        stateMachine.defaultState = idleState;

        var player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            Debug.LogError("Player object not found by tag 'Player'.");
            return;
        }

        var modelAnimator = player.GetComponentInChildren<Animator>();
        if (modelAnimator == null)
        {
            Debug.LogError("No Animator found under Player hierarchy.");
            return;
        }

        modelAnimator.runtimeAnimatorController = controller;
        modelAnimator.applyRootMotion = false;

        EditorUtility.SetDirty(modelAnimator);
        EditorSceneManager.MarkSceneDirty(player.scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"Player idle animation setup done. Controller: {ControllerPath}, Clip: {idleClip.name}");
    }

    private static AnimationClip LoadMainClip(string modelPath)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath(modelPath);
        foreach (var obj in assets)
        {
            if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                return clip;
        }

        return null;
    }

    private static void EnsureHumanoidImport(string modelPath)
    {
        var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if (importer == null)
            return;

        if (importer.animationType != ModelImporterAnimationType.Human)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            importer.SaveAndReimport();
        }
    }

    private static void EnsureLooping(string modelPath)
    {
        var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if (importer == null)
            return;

        var clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
        {
            clips = importer.defaultClipAnimations;
        }

        bool changed = false;
        for (int i = 0; i < clips.Length; i++)
        {
            if (!clips[i].loopTime)
            {
                clips[i].loopTime = true;
                changed = true;
            }
        }

        if (changed)
        {
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
    }
}
#endif
