using UnityEngine;
using UnityEngine.UI;
using JUTPS.ItemSystem;
using JUTPS.WeaponSystem;

namespace JUTPS.InventorySystem.UI
{
    public class UIItemInformation : MonoBehaviour
    {
        private JUHoldableItem CurrentItem;
        private JUCharacterController Player;
        private Weapon lastPreviewWeapon;

        private GameObject previewRoot;
        private GameObject previewInstance;
        private Camera previewCamera;
        private Light previewLight;
        private RenderTexture previewRenderTexture;
        private Texture2D previewTexture;
        private Sprite previewSprite;

        [Header("Essentials")]
        public Sprite EmptySprite;
        public Sprite OverrideWeaponIcon;
        public Image Icon;
        public Text ItemName;
        public Text ItemQuantity;
        public GameObject BulletLabel;
        public Text BulletQuantity;
        public Image ItemHealth;

        [Header("3D Weapon Preview")]
        public bool use3DWeaponPreview = true;
        [Range(128, 1024)] public int previewResolution = 512;
        [Range(1f, 4f)] public float previewPadding = 1.35f;
        [Range(1f, 3f)] public float previewZoom = 1f;
        [Range(0f, 0.2f)] public float previewSafeMargin = 0.02f;
        public Vector3 previewViewDirection = new Vector3(-0.95f, 0.2f, -1f);
        [Range(-1f, 1f)] public float previewHorizontalFrameOffset = 0f;
        [Range(-1f, 1f)] public float previewVerticalFrameOffset = 0f;
        public Color previewBackgroundColor = new Color(0f, 0f, 0f, 0f);
        [Range(24, 31)] public int previewLayer = 30;

        void Start()
        {
            if (OverrideWeaponIcon == null)
            {
                OverrideWeaponIcon = Resources.Load<Sprite>("UI/OIP");
            }
        }

        private void OnDestroy()
        {
            CleanupPreviewResources();
        }

        // Update is called once per frame
        void Update()
        {
            if (Player == null)
            {
                Player = JUGameManager.PlayerController;
                return;
            }

            if (Player.Inventory == null) return;

            ItemName.text = "Player";
            CurrentItem = Player.HoldableItemInUseRightHand;
            bool showingWeapon = CurrentItem is Weapon;

            if (ItemQuantity != null)
            {
                ItemQuantity.text = showingWeapon ? "HK416" : string.Empty;
                if (ItemQuantity.gameObject.activeSelf != showingWeapon)
                {
                    ItemQuantity.gameObject.SetActive(showingWeapon);
                }
            }

            if (CurrentItem == null)
            {
                SetIconSprite(EmptySprite);
                BulletLabel.SetActive(false);
                ItemHealth.fillAmount = 1;
                return;
            }

            if (CurrentItem is Weapon weapon)
            {
                Sprite preview = null;
                bool previewReady = use3DWeaponPreview && TryGetWeaponPreviewSprite(weapon, out preview);
                if (previewReady && preview != null)
                {
                    SetIconSprite(preview);
                }
                else
                {
                    SetIconSprite(OverrideWeaponIcon != null ? OverrideWeaponIcon : CurrentItem.ItemIcon);
                }

                BulletLabel.SetActive(true);
                BulletQuantity.text = weapon.BulletsAmounts + "/" + weapon.TotalBullets;
                ItemHealth.fillAmount = weapon.BulletsPerMagazine > 0
                    ? (float)weapon.BulletsAmounts / weapon.BulletsPerMagazine
                    : 1f;
                return;
            }

            SetIconSprite(CurrentItem.ItemIcon);
            BulletLabel.SetActive(false);

            if (CurrentItem is MeleeWeapon meleeWeapon)
            {
                ItemHealth.fillAmount = meleeWeapon.MeleeWeaponHealth / 100f;
                return;
            }

            ItemHealth.fillAmount = CurrentItem.MaxItemQuantity > 0
                ? (float)CurrentItem.ItemQuantity / CurrentItem.MaxItemQuantity
                : 1f;
        }

        private void SetIconSprite(Sprite sprite)
        {
            if (Icon == null)
                return;

            Icon.sprite = sprite;
            Icon.preserveAspect = true;

            if (!Icon.gameObject.activeSelf)
                Icon.gameObject.SetActive(true);
        }

        private bool TryGetWeaponPreviewSprite(Weapon weapon, out Sprite sprite)
        {
            sprite = null;
            if (weapon == null || Icon == null)
                return false;

            EnsurePreviewObjects();
            if (previewCamera == null || previewRenderTexture == null)
                return false;

            if (lastPreviewWeapon != weapon || previewSprite == null)
            {
                RebuildPreviewInstance(weapon);
                if (previewInstance == null)
                    return false;

                FramePreviewCamera(previewInstance);
                RenderPreviewToSprite();
                lastPreviewWeapon = weapon;
            }

            sprite = previewSprite;
            return sprite != null;
        }

        private void EnsurePreviewObjects()
        {
            if (previewRoot == null)
            {
                previewRoot = new GameObject($"_UIWeaponPreview_{GetInstanceID()}");
                previewRoot.hideFlags = HideFlags.HideAndDontSave;
                previewRoot.transform.position = new Vector3(15000f, 15000f, 15000f);
            }

            if (previewCamera == null)
            {
                GameObject cameraObject = new GameObject("PreviewCamera");
                cameraObject.hideFlags = HideFlags.HideAndDontSave;
                cameraObject.transform.SetParent(previewRoot.transform, false);

                previewCamera = cameraObject.AddComponent<Camera>();
                previewCamera.clearFlags = CameraClearFlags.SolidColor;
                previewCamera.backgroundColor = previewBackgroundColor;
                previewCamera.fieldOfView = 24f;
                previewCamera.cullingMask = 1 << previewLayer;
                previewCamera.nearClipPlane = 0.01f;
                previewCamera.farClipPlane = 100f;
                previewCamera.enabled = false;
            }

            if (previewLight == null)
            {
                GameObject lightObject = new GameObject("PreviewLight");
                lightObject.hideFlags = HideFlags.HideAndDontSave;
                lightObject.transform.SetParent(previewRoot.transform, false);

                previewLight = lightObject.AddComponent<Light>();
                previewLight.type = LightType.Directional;
                previewLight.intensity = 1.2f;
                previewLight.color = Color.white;
                previewLight.shadows = LightShadows.None;
                previewLight.cullingMask = 1 << previewLayer;
            }
            else
            {
                previewLight.cullingMask = 1 << previewLayer;
            }


            if (previewRenderTexture == null || previewRenderTexture.width != previewResolution || previewRenderTexture.height != previewResolution)
            {
                if (previewRenderTexture != null)
                    previewRenderTexture.Release();

                previewRenderTexture = new RenderTexture(previewResolution, previewResolution, 24, RenderTextureFormat.ARGB32);
                previewRenderTexture.name = $"WeaponPreviewRT_{GetInstanceID()}";
                previewRenderTexture.Create();

                previewCamera.targetTexture = previewRenderTexture;
            }

            if (previewTexture == null || previewTexture.width != previewResolution || previewTexture.height != previewResolution)
            {
                previewTexture = new Texture2D(previewResolution, previewResolution, TextureFormat.RGBA32, false);
                previewTexture.name = $"WeaponPreviewTex_{GetInstanceID()}";
            }
        }

        private void RebuildPreviewInstance(Weapon weapon)
        {
            if (previewInstance != null)
            {
                Destroy(previewInstance);
                previewInstance = null;
            }

            previewInstance = Instantiate(weapon.gameObject, previewRoot.transform);
            previewInstance.name = "PreviewWeaponModel";
            previewInstance.transform.localPosition = Vector3.zero;
            previewInstance.transform.localRotation = Quaternion.identity;
            previewInstance.transform.localScale = Vector3.one;

            StripToVisualComponents(previewInstance);
            SetLayerRecursively(previewInstance, previewLayer);
        }

        private static void StripToVisualComponents(GameObject root)
        {
            Component[] components = root.GetComponentsInChildren<Component>(true);
            foreach (Component component in components)
            {
                if (component == null)
                    continue;

                if (component is Transform ||
                    component is MeshRenderer ||
                    component is SkinnedMeshRenderer ||
                    component is MeshFilter)
                {
                    continue;
                }

                // Disable non-visual components instead of removing them to avoid dependency errors.
                if (component is Behaviour behaviour)
                {
                    behaviour.enabled = false;
                    continue;
                }

                if (component is Collider collider)
                {
                    collider.enabled = false;
                    continue;
                }

                if (component is Rigidbody rigidbody)
                {
                    rigidbody.isKinematic = true;
                    rigidbody.detectCollisions = false;
                    rigidbody.linearVelocity = Vector3.zero;
                    rigidbody.angularVelocity = Vector3.zero;
                }
            }
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private void FramePreviewCamera(GameObject target)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            Vector3 center = bounds.center;
            float radius = Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z));
            radius = Mathf.Max(radius, 0.2f) * previewPadding;

            Vector3 viewDirection = previewViewDirection.sqrMagnitude > 0.0001f
                ? previewViewDirection.normalized
                : new Vector3(-0.9f, 0.2f, -1f).normalized;

            float fov = previewCamera.fieldOfView * Mathf.Deg2Rad;
            Quaternion baseRotation = Quaternion.LookRotation(viewDirection, Vector3.up);
            Vector3 framingOffset = (baseRotation * Vector3.right) * (previewHorizontalFrameOffset * radius)
                                  + (baseRotation * Vector3.up) * (previewVerticalFrameOffset * radius);

            float fitDistance = radius / Mathf.Tan(fov * 0.5f) + radius;
            float distance = fitDistance / Mathf.Max(1f, previewZoom);
            Vector3 lookTarget = center + framingOffset;

            const int maxFitIterations = 24;
            for (int i = 0; i < maxFitIterations; i++)
            {
                previewCamera.transform.position = center - viewDirection * distance;
                previewCamera.transform.rotation = Quaternion.LookRotation(lookTarget - previewCamera.transform.position, Vector3.up);
                previewCamera.nearClipPlane = 0.01f;
                previewCamera.farClipPlane = distance + radius * 8f;

                if (IsBoundsInsideViewport(bounds, previewSafeMargin))
                    break;

                distance *= 1.08f;
            }

            previewLight.transform.rotation = Quaternion.LookRotation(-viewDirection, Vector3.up);
        }

        private bool IsBoundsInsideViewport(Bounds bounds, float margin)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;

            Vector3[] corners = new Vector3[]
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, max.y, max.z)
            };

            float low = Mathf.Clamp01(margin);
            float high = 1f - low;

            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 v = previewCamera.WorldToViewportPoint(corners[i]);
                if (v.z <= 0f)
                    return false;

                if (v.x < low || v.x > high || v.y < low || v.y > high)
                    return false;
            }

            return true;
        }

        private void RenderPreviewToSprite()
        {
            if (previewCamera == null || previewRenderTexture == null || previewTexture == null)
                return;

            previewCamera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = previewRenderTexture;
            previewTexture.ReadPixels(new Rect(0, 0, previewResolution, previewResolution), 0, 0, false);
            previewTexture.Apply(false, false);
            RenderTexture.active = previous;

            if (previewSprite != null)
                Destroy(previewSprite);

            previewSprite = Sprite.Create(
                previewTexture,
                new Rect(0, 0, previewTexture.width, previewTexture.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        private void CleanupPreviewResources()
        {
            if (previewInstance != null)
                Destroy(previewInstance);
            if (previewSprite != null)
                Destroy(previewSprite);
            if (previewTexture != null)
                Destroy(previewTexture);
            if (previewRenderTexture != null)
                previewRenderTexture.Release();
            if (previewRoot != null)
                Destroy(previewRoot);
        }
    }

}
