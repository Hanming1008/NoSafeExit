#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using JUTPS;
using JUTPS.CameraSystems;

public static class BindTopDownCameraToJUPlayer
{
    [MenuItem("Tools/NoSafeExit/Bind Camera To New JU Player")]
    public static void BindCamera()
    {
        TDCameraController cameraController = Object.FindObjectOfType<TDCameraController>(true);
        if (cameraController == null)
        {
            Debug.LogError("[BindTopDownCameraToJUPlayer] TDCameraController not found in scene.");
            return;
        }

        JUCharacterController[] players = Object.FindObjectsOfType<JUCharacterController>(true);
        JUCharacterController target = null;

        for (int i = 0; i < players.Length; i++)
        {
            JUCharacterController p = players[i];
            if (p == null || !p.gameObject.activeInHierarchy)
                continue;

            if (p.GetComponent<Animator>() == null)
                continue;

            target = p;
            break;
        }

        if (target == null && players.Length > 0)
            target = players[0];

        if (target == null)
        {
            Debug.LogError("[BindTopDownCameraToJUPlayer] JUCharacterController not found.");
            return;
        }

        Undo.RecordObject(cameraController, "Bind Camera Target");
        cameraController.PlayerTarget = target;
        cameraController.TargetToFollow = target.transform;
        EditorUtility.SetDirty(cameraController);

        Scene scene = cameraController.gameObject.scene;
        if (scene.IsValid())
            EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log("[BindTopDownCameraToJUPlayer] Camera target set to: " + target.gameObject.name);
    }
}
#endif
