using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using JUTPS.JUInputSystem;
using JUTPS.CameraSystems;
using JUTPS.WeaponSystem;
using JUTPS;
using UnityEngine.InputSystem;

namespace JUTPS.UI
{
    [AddComponentMenu("JU TPS/UI/Crosshair")]
    public class Crosshair : MonoBehaviour
    {
        public static Crosshair Instance;
        public static bool AimingOnTarget;
        public static bool AimingOnFriend;
        public static GameObject ObjectOnCrosshairPoint;

        private JUCameraController cameraController;
        private JUCharacterController player;

        [Header("Settings")]
        public float CrosshairSensibility = 6;
        public float CrosshairChangeSpeed = 4;
        private float SmoothedWeaponPrecision;

        [Header("Hide Settings")]
        public bool FollowMousePosition;
        public bool HideOnNoWeaponUsing;
        public bool HideOnAiming;
        public bool OnlyShowOnFireMode;

        [Header("Visual Settings")]
        public Image[] Crosshairs;

        private Image CrosshairCenterPoint;
        private Canvas ParentCanvas;
        [HideInInspector] public List<Vector3> CrosshairsStartPositions = new List<Vector3>();
        [HideInInspector] public Vector3 CrosshairStartScale;

        public bool ChangeColor = true;
        public bool FilterPlayer = true;
        public string[] TargetTags = new string[] { "Enemy", "Skin", "Vehicle", "Zombie", "Monster", "Destructible", "Shootable", "Player" };
        public string[] NoShootableTags = new string[] { "Friend", "Unshootable" };
        public Color NormalColor = Color.white, ShootableColor = Color.red, NonShootableColor = new Color(1, 1, 1, 0.3f);

        [Header("Hit Feedback")]
        public bool EnableHitFeedback = true;
        [Range(0.02f, 0.5f)] public float HitFeedbackDuration = 0.12f;
        public Color HitFeedbackColor = new Color(1f, 0.92f, 0.2f, 1f);
        public Color CriticalHitFeedbackColor = new Color(1f, 0.2f, 0.2f, 1f);
        public bool RotateCrosshairsOnHit = true;
        [Range(-90f, 90f)] public float HitRotationZ = 35f;
        private Quaternion[] crosshairsStartRotations;
        private float hitFeedbackTimer;
        private Color currentHitFeedbackColor = Color.white;

        protected virtual void Start()
        {
            //Assign this instance
            Instance = this;

            //Assign camera
            cameraController = FindObjectOfType<JUCameraController>();

            //if theres no player, theres nothing to 
            var playerobject = GameObject.FindGameObjectWithTag("Player");
            player = playerobject.GetComponent<JUCharacterController>();
            if (player == null) return;

            //Save Crosshairs start positions
            CrosshairsStartPositions = GetCrosshairPositions(Crosshairs);
            CacheCrosshairStartRotations();

            //Save Start Scale
            CrosshairStartScale = Crosshairs[0].transform.localScale;

            //Get crosshair center point
            CrosshairCenterPoint = GetComponent<Image>();
            ParentCanvas = GetComponentInParent<Canvas>();
        }
        protected virtual void Update()
        {
            //if theres no player, no crosshair update
            if (player == null) return;
            UpdateHitFeedback();
            UpdateObjectOnCrosshairPoint();
            UpdateCrosshairColor();
            UpdateCrosshair();
        }
        protected virtual void UpdateCrosshair()
        {
            //If no crosshair, no update
            if (Crosshairs.Length == 0) return;

            //Get precision
            Weapon WeaponInUse = (player.LeftHandWeapon == null) ? player.RightHandWeapon : player.LeftHandWeapon;
            SmoothedWeaponPrecision = GetWeaponPrecisionValue(SmoothedWeaponPrecision, WeaponInUse, CrosshairChangeSpeed);

            if (Crosshairs.Length > 1)
            {
                //Dynamic update crosshair points position towards crosshair center
                MoveTowardsCenter(Crosshairs, CrosshairsStartPositions, SmoothedWeaponPrecision);
            }
            else
            {
                //Resize crosshair to precision
                ResizeCrosshair(Crosshairs[0], SmoothedWeaponPrecision);
            }

            //Update visibility
            if (OnlyShowOnFireMode)
            {
                SetActiveCrosshair(player.FiringMode && !player.IsAiming);
            }
            else
            {
                HideCrosshairOnNoWeaponUsing();
                HideCrosshairOnAiming();
            }

            //Update CrosshairPosition
            if (FollowMousePosition && Mouse.current != null)
            {
                Vector2 movePos;
                Vector2 mousePos = Mouse.current.position.value;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(ParentCanvas.transform as RectTransform, mousePos, ParentCanvas.worldCamera, out movePos);

                Vector3 mousePosOnUi = ParentCanvas.transform.TransformPoint(movePos);

                //Set fake mouse Cursor
                CrosshairCenterPoint.transform.position = mousePos;

                //Move the Object/Panel
                transform.position = mousePos;
            }
        }
        protected virtual void UpdateCrosshairColor()
        {
            if (!ChangeColor && hitFeedbackTimer <= 0f) return;

            Color color = (EnableHitFeedback && hitFeedbackTimer > 0f)
                ? currentHitFeedbackColor
                : GetCurrentCrosshairColor(ObjectOnCrosshairPoint);

            ApplyCrosshairColor(color);
        }

        protected virtual void UpdateObjectOnCrosshairPoint()
        {
            if (cameraController == null)
            {
                ObjectOnCrosshairPoint = null;
                return;
            }
            //Debug.Log(ObjectOnCrosshairPoint);
            GetObjectOnCrosshairPoint(cameraController.mCamera, cameraController.CrosshairRaycastLayerMask, out ObjectOnCrosshairPoint);
            if (ObjectOnCrosshairPoint != null && FilterPlayer)
            {
                if (ObjectOnCrosshairPoint.layer == 15)
                {
                    JUTPS.CharacterBrain.JUCharacterBrain controllerBrain = ObjectOnCrosshairPoint.GetComponentInParent<JUTPS.CharacterBrain.JUCharacterBrain>();
                    if (controllerBrain != null)
                    {
                        if (cameraController == controllerBrain.MyPivotCamera)
                        {
                            ObjectOnCrosshairPoint = null;
                        }
                    }
                }
            }
        }
        private void OnDisable()
        {
            ObjectOnCrosshairPoint = null;
        }

        public Color GetCurrentCrosshairColor(GameObject ObjectOnCrosshairPoint)
        {
            Color color = NormalColor;
            if (ObjectOnCrosshairPoint == null) return color;

            if (IsAimingOnNonShootableObject(ObjectOnCrosshairPoint, NoShootableTags)) color = NonShootableColor;
            if (IsAimingOnShootableObject(ObjectOnCrosshairPoint, TargetTags)) color = ShootableColor;

            return color;
        }
        public static void GetObjectOnCrosshairPoint(Camera camera, LayerMask CrosshairRaycastLayerMask, out GameObject ObjectOnMousePosition)
        {
            if (Mouse.current == null)
            {
                ObjectOnMousePosition = null;
                return;
            }

            ObjectOnMousePosition = null;

            //Create a ray on mouse position
            Ray MouseRay = camera.ScreenPointToRay(Mouse.current.position.value);
            RaycastHit hit;
            if (Physics.Raycast(MouseRay, out hit, 1000, CrosshairRaycastLayerMask))
            {
                ObjectOnMousePosition = hit.collider.gameObject;
            }
        }
        public static bool IsAimingOnShootableObject(GameObject ObjectOnMousePosition, string[] TargetList)
        {
            bool isAimingOnTarget = false;

            foreach (string tag in TargetList)
            {
                if (ObjectOnMousePosition.tag == tag) isAimingOnTarget = true;
            }

            return isAimingOnTarget;
        }
        public static bool IsAimingOnNonShootableObject(GameObject ObjectOnMousePosition, string[] FriendList)
        {
            bool isAimingOnFriend = false;

            foreach (string tag in FriendList)
            {
                if (ObjectOnMousePosition.tag == tag) isAimingOnFriend = true;
            }

            return isAimingOnFriend;
        }



        public void MoveTowardsCenter(Image[] crosshairs, List<Vector3> crosshairStartPositions, float precision)
        {
            for (int i = 0; i < crosshairs.Length; i++)
            {
                Vector3 normal = crosshairs[i].transform.position - crosshairs[i].transform.parent.position;
                crosshairs[i].transform.localPosition = crosshairStartPositions[i] + normal.normalized * precision;
            }
        }
        public void ResizeCrosshair(Image crosshair, float precision)
        {
            if (crosshair == null) return;

            float CurrentSize = CrosshairStartScale.x + precision * CrosshairSensibility;
            crosshair.transform.localScale = new Vector3(CurrentSize, CurrentSize, CurrentSize);
        }
        public void SetActiveCrosshair(bool enabled)
        {
            if (Crosshairs.Length < 2)
            {
                Crosshairs[0].enabled = enabled;
            }
            else
            {
                foreach (Image img in Crosshairs)
                {
                    img.enabled = enabled;
                    CrosshairCenterPoint.enabled = enabled;
                }
            }
        }
        protected void HideCrosshairOnNoWeaponUsing()
        {
            if (!HideOnNoWeaponUsing) return;
            SetActiveCrosshair((player.HoldableItemInUseRightHand || player.HoldableItemInUseLeftHand) ? true : false);
        }
        public void HideCrosshairOnAiming()
        {
            if (!HideOnAiming || (HideOnNoWeaponUsing && player.HoldableItemInUseRightHand == null)) return;
            SetActiveCrosshair(!player.IsAiming);
        }

        public List<Vector3> GetCrosshairPositions(Image[] crosshairs)
        {
            List<Vector3> crosshairPositions = new List<Vector3>();
            foreach (Image img in crosshairs)
            {
                crosshairPositions.Add(img.transform.localPosition);
            }
            return crosshairPositions;
        }
        public static float GetWeaponPrecisionValue(float Current, Weapon WeaponInUse, float Speed = 8)
        {
            if (Instance == null)
            {
                Instance = FindObjectOfType<Crosshair>();
                return 0;
            }
            var precision = Mathf.Lerp(Current, Instance.CrosshairSensibility * 100 * (WeaponInUse ? WeaponInUse.ShotErrorProbability : 0), Speed * Time.deltaTime);
            return precision;
        }

        private void UpdateHitFeedback()
        {
            if (hitFeedbackTimer <= 0f) return;

            hitFeedbackTimer -= Time.deltaTime;
            if (hitFeedbackTimer <= 0f)
            {
                hitFeedbackTimer = 0f;
                RestoreCrosshairRotationAfterHit();
            }
        }

        private void ApplyCrosshairColor(Color color)
        {
            if (Crosshairs.Length > 1)
            {
                foreach (Image img in Crosshairs)
                {
                    if (img != null) img.color = color;
                }
            }
            else if (Crosshairs.Length == 1 && Crosshairs[0] != null)
            {
                Crosshairs[0].color = color;
            }

            if (CrosshairCenterPoint != null)
            {
                CrosshairCenterPoint.color = color;
            }
        }

        private void CacheCrosshairStartRotations()
        {
            if (Crosshairs == null || Crosshairs.Length == 0) return;

            crosshairsStartRotations = new Quaternion[Crosshairs.Length];
            for (int i = 0; i < Crosshairs.Length; i++)
            {
                if (Crosshairs[i] != null)
                {
                    crosshairsStartRotations[i] = Crosshairs[i].transform.localRotation;
                }
            }
        }

        private void ApplyHitRotation()
        {
            if (!RotateCrosshairsOnHit || Crosshairs == null || Crosshairs.Length <= 1) return;
            if (crosshairsStartRotations == null || crosshairsStartRotations.Length != Crosshairs.Length)
            {
                CacheCrosshairStartRotations();
            }

            for (int i = 0; i < Crosshairs.Length; i++)
            {
                if (Crosshairs[i] == null) continue;
                Vector3 euler = crosshairsStartRotations[i].eulerAngles;
                Crosshairs[i].transform.localRotation = Quaternion.Euler(euler.x, euler.y, euler.z + HitRotationZ);
            }
        }

        private void RestoreCrosshairRotationAfterHit()
        {
            if (crosshairsStartRotations == null || Crosshairs == null) return;

            for (int i = 0; i < Crosshairs.Length; i++)
            {
                if (Crosshairs[i] == null) continue;
                if (i < crosshairsStartRotations.Length)
                {
                    Crosshairs[i].transform.localRotation = crosshairsStartRotations[i];
                }
            }
        }

        public void TriggerHitFeedback(bool criticalHit)
        {
            if (!EnableHitFeedback) return;

            currentHitFeedbackColor = criticalHit ? CriticalHitFeedbackColor : HitFeedbackColor;
            hitFeedbackTimer = HitFeedbackDuration;
            ApplyHitRotation();
            ApplyCrosshairColor(currentHitFeedbackColor);
        }

        public static void ShowHitFeedback(bool criticalHit)
        {
            if (Instance == null)
            {
                Instance = FindObjectOfType<Crosshair>();
            }

            if (Instance != null)
            {
                Instance.TriggerHitFeedback(criticalHit);
            }
        }

    }

}
