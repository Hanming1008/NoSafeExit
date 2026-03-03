#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PlayerWeaponSocketRigSetup
{
    private const string WeaponName = "SM_Wep_Preset_A_Rifle_02";
    private const string HandBoneName = "Hand_R";

    [MenuItem("Tools/NoSafeExit/Setup Player Weapon Socket Rig")]
    public static void SetupRig()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            Debug.LogError("[WeaponRig] Player with tag 'Player' not found.");
            return;
        }

        Transform hand = FindDeepChild(player.transform, HandBoneName);
        if (hand == null)
        {
            Debug.LogError("[WeaponRig] Hand_R not found under Player.");
            return;
        }

        Transform weapon = FindDeepChild(player.transform, WeaponName);
        if (weapon == null)
        {
            Debug.LogError($"[WeaponRig] Weapon '{WeaponName}' not found under Player.");
            return;
        }

        // Capture current placement relative to current parent before reparenting.
        Vector3 capturedLocalPos = weapon.localPosition;
        Quaternion capturedLocalRot = weapon.localRotation;
        Vector3 capturedLocalScale = weapon.localScale;
        Transform capturedParent = weapon.parent;

        Transform socket = GetOrCreateChild(hand, "WeaponSocket_R");
        Transform weaponRoot = GetOrCreateChild(socket, "WeaponRoot");
        Transform modelOffset = GetOrCreateChild(weaponRoot, "ModelOffset");

        // Keep socket and root neutral so all art tuning lives under ModelOffset.
        socket.localPosition = Vector3.zero;
        socket.localRotation = Quaternion.identity;
        socket.localScale = Vector3.one;

        weaponRoot.localPosition = Vector3.zero;
        weaponRoot.localRotation = Quaternion.identity;
        weaponRoot.localScale = Vector3.one;

        // If weapon was not already under ModelOffset, move it there and preserve current visible pose via ModelOffset.
        if (weapon.parent != modelOffset)
        {
            if (capturedParent == hand)
            {
                modelOffset.localPosition = capturedLocalPos;
                modelOffset.localRotation = capturedLocalRot;
                modelOffset.localScale = capturedLocalScale;
            }

            weapon.SetParent(modelOffset, false);
            weapon.localPosition = Vector3.zero;
            weapon.localRotation = Quaternion.identity;
            weapon.localScale = Vector3.one;
        }

        // Runtime check showed this Idle pose rotates hand so weapon points upward.
        // Apply a stable baseline correction on X while preserving Y/Z tuning.
        Vector3 euler = modelOffset.localEulerAngles;
        euler.x = 90f;
        modelOffset.localEulerAngles = euler;

        // Rebind shooting references to the active weapon points.
        PlayerShoot playerShoot = player.GetComponent<PlayerShoot>();
        if (playerShoot != null)
        {
            Transform muzzle = FindDeepChild(weapon, "Muzzle");
            if (muzzle == null)
            {
                muzzle = new GameObject("Muzzle").transform;
                muzzle.SetParent(weapon, false);
                muzzle.localPosition = new Vector3(0f, 0.045f, 0.84f);
                muzzle.localRotation = Quaternion.identity;
            }

            Transform shell = FindDeepChild(weapon, "ShellEjectPoint_Player");
            if (shell == null)
            {
                shell = new GameObject("ShellEjectPoint_Player").transform;
                shell.SetParent(muzzle, false);
                shell.localPosition = new Vector3(0.065f, 0.055f, -0.42f);
                shell.localRotation = Quaternion.identity;
            }

            playerShoot.muzzle = muzzle;
            playerShoot.shellEjectPoint = shell;
            EditorUtility.SetDirty(playerShoot);
        }

        EditorUtility.SetDirty(player);
        
        EditorSceneManager.MarkSceneDirty(player.scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"[WeaponRig] Done. Tune only '{modelOffset.GetHierarchyPath()}' for weapon alignment.");
    }

    private static Transform GetOrCreateChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
            return child;

        GameObject go = new GameObject(childName);
        Undo.RegisterCreatedObjectUndo(go, "Create Weapon Rig Node");
        Transform t = go.transform;
        t.SetParent(parent, false);
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;
        return t;
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
