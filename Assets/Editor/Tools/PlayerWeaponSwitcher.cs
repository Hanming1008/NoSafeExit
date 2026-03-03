#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PlayerWeaponSwitcher
{
    private const string NewWeaponName = "SM_Wep_Preset_A_Rifle_02";
    private const string OldWeaponName = "assault1";

    [MenuItem("Tools/NoSafeExit/Switch Player Weapon To New Rifle")]
    public static void SwitchPlayerWeapon()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            Debug.LogError("[WeaponSwitch] Player with tag 'Player' not found.");
            return;
        }

        PlayerShoot playerShoot = player.GetComponent<PlayerShoot>();
        if (playerShoot == null)
        {
            Debug.LogError("[WeaponSwitch] PlayerShoot component not found on Player.");
            return;
        }

        Transform handR = FindDeepChild(player.transform, "Hand_R");
        if (handR == null)
        {
            Debug.LogError("[WeaponSwitch] Hand_R not found under Player.");
            return;
        }

        GameObject newWeapon = GameObject.Find(NewWeaponName);
        if (newWeapon == null)
        {
            Debug.LogError($"[WeaponSwitch] New weapon '{NewWeaponName}' not found in scene.");
            return;
        }

        Transform oldWeapon = FindDeepChild(player.transform, OldWeaponName);

        Vector3 targetLocalPos = new Vector3(0.02f, -0.03f, 0.06f);
        Vector3 targetLocalRot = new Vector3(0f, 180f, 180f);
        Vector3 targetLocalScale = new Vector3(0.7f, 0.7f, 0.7f);

        if (oldWeapon != null)
        {
            targetLocalPos = oldWeapon.localPosition;
            targetLocalRot = oldWeapon.localEulerAngles;
            targetLocalScale = oldWeapon.localScale;
        }

        Transform newWeaponTransform = newWeapon.transform;
        Undo.RecordObject(newWeaponTransform, "Switch Player Weapon");
        newWeaponTransform.SetParent(handR, false);
        newWeaponTransform.localPosition = targetLocalPos;
        newWeaponTransform.localEulerAngles = targetLocalRot;
        newWeaponTransform.localScale = targetLocalScale;

        Transform muzzle = FindDeepChild(newWeaponTransform, "Muzzle");
        if (muzzle == null)
        {
            GameObject muzzleGo = new GameObject("Muzzle");
            muzzle = muzzleGo.transform;
            muzzle.SetParent(newWeaponTransform, false);
            muzzle.localPosition = new Vector3(0f, 0.025f, 0.62f);
            muzzle.localRotation = Quaternion.identity;
        }

        Transform shellEject = FindDeepChild(newWeaponTransform, "ShellEjectPoint_Player");
        if (shellEject == null)
        {
            GameObject shellGo = new GameObject("ShellEjectPoint_Player");
            shellEject = shellGo.transform;
            shellEject.SetParent(muzzle, false);
            shellEject.localPosition = new Vector3(0.03f, 0.03f, -0.15f);
            shellEject.localRotation = Quaternion.identity;
        }

        Undo.RecordObject(playerShoot, "Assign New Weapon Fire Points");
        playerShoot.muzzle = muzzle;
        playerShoot.shellEjectPoint = shellEject;

        if (oldWeapon != null && oldWeapon.gameObject != newWeapon)
        {
            Undo.RecordObject(oldWeapon.gameObject, "Disable Old Weapon");
            oldWeapon.gameObject.SetActive(false);
        }

        EditorUtility.SetDirty(playerShoot);
        EditorUtility.SetDirty(newWeapon);
        if (oldWeapon != null)
            EditorUtility.SetDirty(oldWeapon.gameObject);

        EditorSceneManager.MarkSceneDirty(player.scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"[WeaponSwitch] Done. New weapon: {newWeapon.name}, Muzzle: {muzzle.GetHierarchyPath()}, Shell: {shellEject.GetHierarchyPath()}");
    }

    private static Transform FindDeepChild(Transform root, string name)
    {
        if (root == null)
            return null;

        if (root.name == name)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeepChild(root.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }

    private static string GetHierarchyPath(this Transform t)
    {
        if (t == null)
            return string.Empty;

        string path = t.name;
        Transform current = t.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
}
#endif
