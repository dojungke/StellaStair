using UnityEngine;

namespace StellaStair.Town
{
    public enum EquipmentSlot
    {
        Armor,
        Weapon
    }

    public enum WeaponKind
    {
        None,
        Sword,
        Bow,
        Staff
    }
    [CreateAssetMenu(fileName = "TownItem", menuName = "Stella Stair/Town Item")]
    public sealed class TownItemData : ScriptableObject
    {
        [SerializeField] private string displayName = "New Item";
        [SerializeField, TextArea] private string description = string.Empty;
        [SerializeField, Min(0)] private int price = 100;
        [SerializeField, Min(0)] private int maxHealthBonus;
        [SerializeField, Min(0)] private int attackDamageBonus;
        [SerializeField, Min(0)] private int movementBonus;
        [SerializeField] private Sprite icon;
        [SerializeField] private EquipmentSlot equipmentSlot = EquipmentSlot.Armor;
        [SerializeField] private WeaponKind weaponKind = WeaponKind.None;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public int Price => Mathf.Max(0, price);
        public int MaxHealthBonus => Mathf.Max(0, maxHealthBonus);
        public int AttackDamageBonus => Mathf.Max(0, attackDamageBonus);
        public int MovementBonus => Mathf.Max(0, movementBonus);
        public Sprite Icon => icon;
        public EquipmentSlot EquipmentSlot => equipmentSlot;
        public WeaponKind WeaponKind => weaponKind;

        public static TownItemData CreateRuntime(
            string itemName, string itemDescription, int itemPrice,
            EquipmentSlot slot, WeaponKind kind = WeaponKind.None,
            int healthBonus = 0, int damageBonus = 0, int moveBonus = 0)
        {
            var item = CreateInstance<TownItemData>();
            item.name = itemName;
            item.displayName = itemName;
            item.description = itemDescription;
            item.price = itemPrice;
            item.maxHealthBonus = healthBonus;
            item.attackDamageBonus = damageBonus;
            item.movementBonus = moveBonus;
            item.equipmentSlot = slot;
            item.weaponKind = slot == EquipmentSlot.Weapon ? kind : WeaponKind.None;
            item.hideFlags = HideFlags.HideAndDontSave;
            return item;
        }
    }
}
