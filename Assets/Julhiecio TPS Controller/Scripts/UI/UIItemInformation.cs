using System.Collections;
using System.Collections.Generic;
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

        [Header("Essentials")]
        public Sprite EmptySprite;
        public Sprite OverrideWeaponIcon;
        public Image Icon;
        public Text ItemName;
        public Text ItemQuantity;
        public GameObject BulletLabel;
        public Text BulletQuantity;
        public Image ItemHealth;
        void Start()
        {
            //Player = JUGameManager.PlayerController;
            if (OverrideWeaponIcon == null)
            {
                OverrideWeaponIcon = Resources.Load<Sprite>("UI/OIP");
            }
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

            // Keep header fixed.
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
                Icon.sprite = EmptySprite;
                BulletLabel.SetActive(false);
                ItemHealth.fillAmount = 1;
                return;
            }

            if (CurrentItem is Weapon weapon)
            {
                Icon.sprite = OverrideWeaponIcon != null ? OverrideWeaponIcon : CurrentItem.ItemIcon;
                BulletLabel.SetActive(true);
                BulletQuantity.text = weapon.BulletsAmounts + "/" + weapon.TotalBullets;
                ItemHealth.fillAmount = weapon.BulletsPerMagazine > 0
                    ? (float)weapon.BulletsAmounts / weapon.BulletsPerMagazine
                    : 1f;
                return;
            }

            Icon.sprite = CurrentItem.ItemIcon;
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
    }

}
