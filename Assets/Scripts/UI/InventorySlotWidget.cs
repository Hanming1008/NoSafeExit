using UnityEngine;
using UnityEngine.EventSystems;

public enum InventorySlotWidgetMode
{
    Backpack,
    Quickbar,
    Equipment
}

public class InventorySlotWidget : MonoBehaviour, IPointerClickHandler
{
    private GameplayUIRoot owner;
    private int slotIndex;
    private InventorySlotWidgetMode mode;

    public void Configure(GameplayUIRoot uiOwner, int index, InventorySlotWidgetMode widgetMode)
    {
        owner = uiOwner;
        slotIndex = index;
        mode = widgetMode;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (owner == null)
            return;

        if (mode == InventorySlotWidgetMode.Backpack)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
                owner.OpenContextMenuForBackpackSlot(slotIndex, eventData.position);

            return;
        }

        if (mode == InventorySlotWidgetMode.Quickbar)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                owner.TryUseQuickbarSlot(slotIndex);
            else if (eventData.button == PointerEventData.InputButton.Right)
                owner.ClearQuickbarSlot(slotIndex);

            return;
        }

        if (mode == InventorySlotWidgetMode.Equipment)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
                owner.OpenContextMenuForEquipmentSlot((EquipmentSlotType)slotIndex, eventData.position);
        }
    }
}
