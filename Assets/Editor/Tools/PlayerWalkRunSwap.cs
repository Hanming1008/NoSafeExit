#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class PlayerWalkRunSwap
{
    private const string ControllerPath = "Assets/Animations/Player/Controllers/Player_Combat.controller";
    private const string WalkClipPath = "Assets/Synty/AnimationBaseLocomotion/Animations/Polygon/Feminine/Locomotion/Walk/A_Walk_F_Femn.fbx";
    private const string RunClipPath = "Assets/Synty/AnimationBaseLocomotion/Animations/Polygon/Feminine/Locomotion/Run/A_Run_F_Femn.fbx";

    [MenuItem("Tools/NoSafeExit/Swap Player Walk+Run To Synty")]
    public static void SwapPlayerWalkRun()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError("[WalkRunSwap] Controller not found: " + ControllerPath);
            return;
        }

        PrepareHumanoidLoopImport(WalkClipPath);
        PrepareHumanoidLoopImport(RunClipPath);

        AnimationClip walkClip = LoadPrimaryClip(WalkClipPath);
        AnimationClip runClip = LoadPrimaryClip(RunClipPath);
        if (walkClip == null || runClip == null)
        {
            Debug.LogError("[WalkRunSwap] Failed to load walk or run clip.");
            return;
        }

        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        bool walkUpdated = TrySetStateMotionRecursive(sm, "Walk_Rifle", walkClip);
        if (!walkUpdated)
            walkUpdated = TrySetStateMotionRecursive(sm, "Walk", walkClip);

        bool runUpdated = TrySetStateMotionRecursive(sm, "Run_Rifle", runClip);
        if (!runUpdated)
            runUpdated = TrySetStateMotionRecursive(sm, "Run", runClip);

        if (!walkUpdated || !runUpdated)
        {
            Debug.LogError($"[WalkRunSwap] State not found. walkUpdated={walkUpdated}, runUpdated={runUpdated}");
            return;
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[WalkRunSwap] Updated Walk -> {walkClip.name}, Run -> {runClip.name}");
    }

    private static bool TrySetStateMotionRecursive(AnimatorStateMachine sm, string stateName, Motion motion)
    {
        ChildAnimatorState[] states = sm.states;
        for (int i = 0; i < states.Length; i++)
        {
            if (states[i].state != null && states[i].state.name == stateName)
            {
                states[i].state.motion = motion;
                EditorUtility.SetDirty(states[i].state);
                return true;
            }
        }

        ChildAnimatorStateMachine[] children = sm.stateMachines;
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].stateMachine != null && TrySetStateMotionRecursive(children[i].stateMachine, stateName, motion))
                return true;
        }

        return false;
    }

    private static AnimationClip LoadPrimaryClip(string fbxPath)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        for (int i = 0; i < assets.Length; i++)
        {
            AnimationClip clip = assets[i] as AnimationClip;
            if (clip == null)
                continue;

            if (clip.name.StartsWith("__preview__"))
                continue;

            return clip;
        }

        return null;
    }

    private static void PrepareHumanoidLoopImport(string modelPath)
    {
        ModelImporter importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if (importer == null)
            return;

        bool changed = false;

        if (importer.animationType != ModelImporterAnimationType.Human)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            changed = true;
        }

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
            clips = importer.defaultClipAnimations;

        if (clips != null && clips.Length > 0)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                if (!clips[i].loopTime)
                {
                    clips[i].loopTime = true;
                    changed = true;
                }

                if (!clips[i].loopPose)
                {
                    clips[i].loopPose = true;
                    changed = true;
                }
            }

            importer.clipAnimations = clips;
        }

        if (changed)
            importer.SaveAndReimport();
        else
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
    }
}
#endif
