using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace JUTPS.FX
{
    public class HitMarkerEffect : MonoBehaviour
    {
        public static HitMarkerEffect instance;

        private Image HitImage;
        private AudioSource HitSound;

        [Header("Hit Effect")]
        public bool EnableHitEffect = true;
        public AudioClip HitAudioClip;
        public string[] HitTags;

        public Color HitColor = Color.white;
        public float Speed = 5;
        private Color ClearWhite = new Color(1, 1, 1, 0);

        [Header("Damage Count")]
        public bool ShowDamage;
        public AudioClip CriticalDamageAudioClip;
        public Text DamageText;
        public float CriticalHitMax = 50;
        public float TextFadeSpeed = 3;
        public Color NormalHitColor = Color.white, CriticalHitColor = Color.red;
        private Vector3 HitDamagePosition;
        

        [Header("Damage Float Animation")]
        public Vector3 DamageStartOffset = new Vector3(0f, 0.25f, 0f);
        public Vector3 DamageFloatVelocity = new Vector3(0f, 2.3f, 0f);
        [Range(0f, 5f)] public float DamageFloatDamping = 2.2f;
        [Range(0f, 1f)] public float RandomHorizontalSpread = 0.18f;
        [Range(0f, 1f)] public float RandomDepthSpread = 0.08f;

        [Header("Crosshair Hit Feedback")]
        public bool TriggerCrosshairHitFeedback = true;
        public string[] CrosshairHitTags = new string[] { "Enemy", "Skin", "Zombie", "Monster", "Destructible", "Shootable" };

        private Vector3 _currentDamageOffset;
        private Vector3 _currentDamageVelocity;
        private float CurrentDamage;
        private string CurrentHitTag;
        void Awake()
        {
            HitSound = GetComponent<AudioSource>();
            HitImage = GetComponent<Image>();
            if (DamageText != null) DamageText.color = Color.clear;
        }
        private void OnEnable()
        {
            instance = this;
        }
        // Update is called once per frame
        void Update()
        {
            if (HitImage != null && EnableHitEffect)
            {
                HitImage.color = Color.Lerp(HitImage.color, ClearWhite, Speed * Time.deltaTime);
            }

            if (ShowDamage && DamageText != null)
            {
                if (DamageText.color.a > 0.001f)
                {
                    _currentDamageOffset += _currentDamageVelocity * Time.deltaTime;
                    _currentDamageVelocity = Vector3.Lerp(_currentDamageVelocity, Vector3.zero, DamageFloatDamping * Time.deltaTime);

                    JUTPS.UI.UIElementToWorldPosition.SetUIWorldPosition(
                        DamageText.gameObject,
                        HitDamagePosition + _currentDamageOffset,
                        Vector3.zero
                    );

                    DamageText.color = Color.Lerp(DamageText.color, ClearWhite, TextFadeSpeed * Time.deltaTime);
                }
            }
        }

        private void Hit()
        {
            bool IsCriticalHit = CurrentDamage > CriticalHitMax;

            if (HitImage != null && EnableHitEffect)
            {
                HitImage.color = HitColor;
            }

            if (HitSound != null && HitAudioClip != null)
            {
                HitSound.PlayOneShot(HitAudioClip);
            }

            if (DamageText != null && ShowDamage)
            {
                DamageText.text = ((int)CurrentDamage).ToString();
                DamageText.color = IsCriticalHit ? CriticalHitColor : NormalHitColor;

                _currentDamageOffset = DamageStartOffset + new Vector3(
                    Random.Range(-RandomHorizontalSpread, RandomHorizontalSpread),
                    0f,
                    Random.Range(-RandomDepthSpread, RandomDepthSpread)
                );
                _currentDamageVelocity = DamageFloatVelocity;

                if (CriticalDamageAudioClip != null && IsCriticalHit && HitSound != null)
                {
                    HitSound.Stop();
                    HitSound.PlayOneShot(CriticalDamageAudioClip);
                }
            }

            if (TriggerCrosshairHitFeedback && IsTagInList(CurrentHitTag, CrosshairHitTags))
            {
                JUTPS.UI.Crosshair.ShowHitFeedback(IsCriticalHit);
                CrosshairCursor.ShowHitFeedback(IsCriticalHit);
            }
        }
        public static void HitCheck(string CollidedObjectTag, Vector3 hitPosition = default(Vector3), float Damage = 0)
        {
            if (!instance)
                return;

            foreach (string tag in instance.HitTags)
            {
                if (CollidedObjectTag == tag)
                {
                    instance.HitDamagePosition = hitPosition;
                    instance.CurrentDamage = Damage;
                    instance.CurrentHitTag = CollidedObjectTag;
                    instance.Hit();
                }
            }
        }

        private bool IsTagInList(string targetTag, string[] tags)
        {
            if (string.IsNullOrEmpty(targetTag) || tags == null || tags.Length == 0)
                return false;

            for (int i = 0; i < tags.Length; i++)
            {
                if (targetTag == tags[i])
                    return true;
            }

            return false;
        }
    }
}
