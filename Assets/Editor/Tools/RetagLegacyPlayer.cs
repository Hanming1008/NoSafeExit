using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RetagLegacyPlayer
{
    [MenuItem("Tools/NoSafeExit/Retag Legacy Player To Untagged")]
    public static void Run()
    {
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        int changed = 0;

        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject go = allObjects[i];
            if (go == null)
                continue;
            if (!go.scene.IsValid())
                continue;
            if ((go.hideFlags & (HideFlags.NotEditable | HideFlags.HideAndDontSave)) != 0)
                continue;
            if (go.name != "Player")
                continue;
            if (!go.CompareTag("Player"))
                continue;

            Undo.RecordObject(go, "Retag Legacy Player");
            go.tag = "Untagged";
            EditorUtility.SetDirty(go);
            changed++;
        }

        if (changed > 0)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
        }

        Debug.Log($"[NoSafeExit] Retagged legacy Player objects: {changed}");
    }
}
