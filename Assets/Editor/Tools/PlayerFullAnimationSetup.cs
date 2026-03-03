#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PlayerFullAnimationSetup
{
    private const string IdleFbxPath = "Assets/Animations/Player/Mixamo/Rifle_Idle.fbx";
    private const string WalkFbxPath = "Assets/Animations/Player/Mixamo/Rifle_Walk.fbx";
    private const string RunFbxPath = "Assets/Animations/Player/Mixamo/Rifle_Run.fbx";
    private const string ShootFbxPath = "Assets/Animations/Player/Mixamo/Rifle_Shoot.fbx";
    private const string DeathFbxPath = "Assets/Animations/Player/Mixamo/Standing_Death.fbx";

    private const string ControllerDir = "Assets/Animations/Player/Controllers";
    private const string ControllerPath = ControllerDir + "/Player_Combat.controller";

    [MenuItem("Tools/NoSafeExit/Setup Player Full Animations")]
    public static void SetupPlayerFullAnimations()
    {
        if (!ValidateAnimationFiles())
            return;

        PrepareImportSettings(IdleFbxPath, true);
        PrepareImportSettings(WalkFbxPath, true);
        PrepareImportSettings(RunFbxPath, true);
        PrepareImportSettings(ShootFbxPath, false);
        PrepareImportSettings(DeathFbxPath, false);

        AnimationClip idleClip = LoadMainClip(IdleFbxPath);
        AnimationClip walkClip = LoadMainClip(WalkFbxPath);
        AnimationClip runClip = LoadMainClip(RunFbxPath);
        AnimationClip shootClip = LoadMainClip(ShootFbxPath);
        AnimationClip deathClip = LoadMainClip(DeathFbxPath);

        if (idleClip == null || walkClip == null || runClip == null || shootClip == null || deathClip == null)
        {
            Debug.LogError("[FullAnimSetup] One or more animation clips could not be loaded.");
            return;
        }

        AnimatorController controller = BuildController(idleClip, walkClip, runClip, shootClip, deathClip);
        if (controller == null)
            return;

        BindToPlayer(controller);

        AssetDatabase.SaveAssets();
        Debug.Log($"[FullAnimSetup] Done. Controller: {ControllerPath}");
    }

    private static bool ValidateAnimationFiles()
    {
        bool ok = true;
        ok &= ValidateFile(IdleFbxPath);
        ok &= ValidateFile(WalkFbxPath);
        ok &= ValidateFile(RunFbxPath);
        ok &= ValidateFile(ShootFbxPath);
        ok &= ValidateFile(DeathFbxPath);
        return ok;
    }

    private static bool ValidateFile(string path)
    {
        if (File.Exists(path))
            return true;

        Debug.LogError($"[FullAnimSetup] Missing file: {path}");
        return false;
    }

    private static void PrepareImportSettings(string modelPath, bool loop)
    {
        ModelImporter importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"[FullAnimSetup] ModelImporter not found: {modelPath}");
            return;
        }

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
                if (clips[i].loopTime != loop)
                {
                    clips[i].loopTime = loop;
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

    private static AnimationClip LoadMainClip(string modelPath)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(modelPath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                return clip;
        }

        return null;
    }

    private static AnimatorController BuildController(
        AnimationClip idleClip,
        AnimationClip walkClip,
        AnimationClip runClip,
        AnimationClip shootClip,
        AnimationClip deathClip)
    {
        if (!AssetDatabase.IsValidFolder(ControllerDir))
            AssetDatabase.CreateFolder("Assets/Animations/Player", "Controllers");

        AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (existing != null)
            AssetDatabase.DeleteAsset(ControllerPath);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        if (controller == null)
        {
            Debug.LogError("[FullAnimSetup] Failed to create AnimatorController.");
            return null;
        }

        AddParameter(controller, "Speed", AnimatorControllerParameterType.Float);
        AddParameter(controller, "IsMoving", AnimatorControllerParameterType.Bool);
        AddParameter(controller, "IsSprinting", AnimatorControllerParameterType.Bool);
        AddParameter(controller, "IsDead", AnimatorControllerParameterType.Bool);
        AddParameter(controller, "Shoot", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        AnimatorState idle = sm.AddState("Idle_Rifle", new Vector3(260f, 120f, 0f));
        AnimatorState walk = sm.AddState("Walk_Rifle", new Vector3(500f, 120f, 0f));
        AnimatorState run = sm.AddState("Run_Rifle", new Vector3(740f, 120f, 0f));
        AnimatorState shoot = sm.AddState("Shoot_Rifle", new Vector3(500f, 340f, 0f));
        AnimatorState death = sm.AddState("Death", new Vector3(260f, 520f, 0f));

        idle.motion = idleClip;
        walk.motion = walkClip;
        run.motion = runClip;
        shoot.motion = shootClip;
        death.motion = deathClip;

        sm.defaultState = idle;

        // Locomotion transitions.
        AnimatorStateTransition idleToWalk = AddTransition(idle, walk, false, 0f, 0.08f);
        idleToWalk.AddCondition(AnimatorConditionMode.Greater, 0.08f, "Speed");
        idleToWalk.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");

        AnimatorStateTransition walkToIdle = AddTransition(walk, idle, false, 0f, 0.08f);
        walkToIdle.AddCondition(AnimatorConditionMode.Less, 0.05f, "Speed");

        AnimatorStateTransition idleToRun = AddTransition(idle, run, false, 0f, 0.06f);
        idleToRun.AddCondition(AnimatorConditionMode.Greater, 0.72f, "Speed");
        idleToRun.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");

        AnimatorStateTransition walkToRun = AddTransition(walk, run, false, 0f, 0.06f);
        walkToRun.AddCondition(AnimatorConditionMode.Greater, 0.72f, "Speed");
        walkToRun.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");

        AnimatorStateTransition runToWalk = AddTransition(run, walk, false, 0f, 0.08f);
        runToWalk.AddCondition(AnimatorConditionMode.Less, 0.60f, "Speed");

        AnimatorStateTransition runToIdle = AddTransition(run, idle, false, 0f, 0.08f);
        runToIdle.AddCondition(AnimatorConditionMode.Less, 0.05f, "Speed");

        // Shoot transitions.
        AnimatorStateTransition anyToShoot = sm.AddAnyStateTransition(shoot);
        anyToShoot.hasExitTime = false;
        anyToShoot.hasFixedDuration = true;
        anyToShoot.duration = 0.03f;
        anyToShoot.canTransitionToSelf = false;
        anyToShoot.AddCondition(AnimatorConditionMode.If, 0f, "Shoot");
        anyToShoot.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");

        AnimatorStateTransition shootToRun = AddTransition(shoot, run, true, 0.85f, 0.05f);
        shootToRun.AddCondition(AnimatorConditionMode.Greater, 0.72f, "Speed");

        AnimatorStateTransition shootToWalk = AddTransition(shoot, walk, true, 0.85f, 0.05f);
        shootToWalk.AddCondition(AnimatorConditionMode.Greater, 0.08f, "Speed");
        shootToWalk.AddCondition(AnimatorConditionMode.Less, 0.72f, "Speed");

        AnimatorStateTransition shootToIdle = AddTransition(shoot, idle, true, 0.85f, 0.05f);
        shootToIdle.AddCondition(AnimatorConditionMode.Less, 0.08f, "Speed");

        // Death transition.
        AnimatorStateTransition anyToDeath = sm.AddAnyStateTransition(death);
        anyToDeath.hasExitTime = false;
        anyToDeath.hasFixedDuration = true;
        anyToDeath.duration = 0.05f;
        anyToDeath.canTransitionToSelf = false;
        anyToDeath.AddCondition(AnimatorConditionMode.If, 0f, "IsDead");

        return controller;
    }

    private static void AddParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        for (int i = 0; i < controller.parameters.Length; i++)
        {
            if (controller.parameters[i].name == name)
                return;
        }

        controller.AddParameter(name, type);
    }

    private static AnimatorStateTransition AddTransition(
        AnimatorState from,
        AnimatorState to,
        bool hasExitTime,
        float exitTime,
        float duration)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = hasExitTime;
        transition.exitTime = exitTime;
        transition.hasFixedDuration = true;
        transition.duration = duration;
        transition.canTransitionToSelf = false;
        return transition;
    }

    private static void BindToPlayer(AnimatorController controller)
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            Debug.LogError("[FullAnimSetup] Player with tag 'Player' not found.");
            return;
        }

        Animator modelAnimator = player.GetComponentInChildren<Animator>(true);
        if (modelAnimator == null)
        {
            Debug.LogError("[FullAnimSetup] No Animator found under Player hierarchy.");
            return;
        }

        modelAnimator.runtimeAnimatorController = controller;
        modelAnimator.applyRootMotion = false;
        EditorUtility.SetDirty(modelAnimator);

        PlayerAnimatorBridge bridge = player.GetComponent<PlayerAnimatorBridge>();
        if (bridge == null)
            bridge = Undo.AddComponent<PlayerAnimatorBridge>(player);

        bridge.animator = modelAnimator;
        EditorUtility.SetDirty(bridge);
        EditorUtility.SetDirty(player);

        EditorSceneManager.MarkSceneDirty(player.scene);
    }
}
#endif
