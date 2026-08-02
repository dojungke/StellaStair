using System;
using System.Collections.Generic;
using StellaStair.Battle;
using StellaStair.Grid;

namespace StellaStair.Town
{
    public static class TownProgressState
    {
        private static readonly Dictionary<TownItemData, int> inventory = new();
        private static readonly Dictionary<string, TownItemData> armorEquipment = new();
        private const int BossMissionInterval = 5;
        private static readonly Dictionary<string, TownItemData> weaponEquipment = new();
        private static int gold = 300;
        private static string selectedStageName;
        private static bool firstBattleCompleted;
        private static bool guildIntroductionPlaying;
        private static bool guildIntroductionCompleted;
        private static TacticalPartyLevel partyLevel = TacticalPartyLevel.Beginner;
        private static int completedMissionsAtCurrentPartyLevel;

        public static int Gold => gold;
        public static bool FirstBattleCompleted => firstBattleCompleted;
        public static bool GuildIntroductionCompleted => guildIntroductionCompleted;
        public static TacticalPartyLevel PartyLevel => partyLevel;
        public static int CompletedMissionsAtCurrentPartyLevel => completedMissionsAtCurrentPartyLevel;
        public static int MissionsUntilBoss => BossMissionInterval;
        public static bool NextMissionIsBoss => completedMissionsAtCurrentPartyLevel >= BossMissionInterval - 1;
        public static IReadOnlyDictionary<TownItemData, int> Inventory => inventory;

        public static void AddGold(int amount) => gold += UnityEngine.Mathf.Max(0, amount);
        public static void SetPartyLevel(TacticalPartyLevel level)
        {
            partyLevel = level;
            completedMissionsAtCurrentPartyLevel = 0;
        }

        public static bool TryPurchase(TownItemData item)
        {
            if (item == null || gold < item.Price) return false;
            gold -= item.Price;
            inventory[item] = GetOwnedCount(item) + 1;
            return true;
        }

        public static int GetOwnedCount(TownItemData item) =>
            item != null && inventory.TryGetValue(item, out var count) ? count : 0;

        public static int GetAvailableCount(TownItemData item)
        {
            if (item == null) return 0;
            var equipped = 0;
            foreach (var pair in armorEquipment)
                if (pair.Value == item) equipped++;
            foreach (var pair in weaponEquipment)
                if (pair.Value == item) equipped++;
            return UnityEngine.Mathf.Max(0, GetOwnedCount(item) - equipped);
        }

        public static TownItemData GetEquippedItem(string unitKey, EquipmentSlot slot)
        {
            if (string.IsNullOrWhiteSpace(unitKey)) return null;
            var equipment = slot == EquipmentSlot.Armor ? armorEquipment : weaponEquipment;
            return equipment.TryGetValue(unitKey, out var item) ? item : null;
        }

        public static bool CanEquip(string unitKey, TownItemData item)
        {
            if (string.IsNullOrWhiteSpace(unitKey) || item == null) return false;
            if (item.EquipmentSlot == EquipmentSlot.Armor) return true;
            return item.WeaponKind == GetRequiredWeaponKind(unitKey);
        }

        public static bool TryEquip(string unitKey, TownItemData item)
        {
            if (!CanEquip(unitKey, item)) return false;
            var equipment = item.EquipmentSlot == EquipmentSlot.Armor ? armorEquipment : weaponEquipment;
            if (equipment.TryGetValue(unitKey, out var current) && current == item) return true;
            if (GetAvailableCount(item) <= 0) return false;
            equipment[unitKey] = item;
            return true;
        }

        public static void Unequip(string unitKey, EquipmentSlot slot)
        {
            if (string.IsNullOrWhiteSpace(unitKey)) return;
            var equipment = slot == EquipmentSlot.Armor ? armorEquipment : weaponEquipment;
            equipment.Remove(unitKey);
        }

        public static WeaponKind GetRequiredWeaponKind(string unitKey)
        {
            if (string.IsNullOrWhiteSpace(unitKey)) return WeaponKind.None;
            if (unitKey.IndexOf("Knight", StringComparison.OrdinalIgnoreCase) >= 0 || unitKey.Contains("\uAE30\uC0AC"))
                return WeaponKind.Sword;
            if (unitKey.IndexOf("Archer", StringComparison.OrdinalIgnoreCase) >= 0 || unitKey.Contains("\uAD81\uC218"))
                return WeaponKind.Bow;
            if (unitKey.IndexOf("Wizard", StringComparison.OrdinalIgnoreCase) >= 0 ||
                unitKey.IndexOf("Mage", StringComparison.OrdinalIgnoreCase) >= 0 || unitKey.Contains("\uB9C8\uBC95\uC0AC"))
                return WeaponKind.Staff;
            return WeaponKind.None;
        }

        public static IReadOnlyList<string> GetUnlockedPartyKeys()
        {
            var result = new List<string> { "Knight" };
            if (firstBattleCompleted) result.Add("Archer");
            if (guildIntroductionCompleted) result.Add("Wizard");
            return result;
        }

        public static bool IsPartyMemberUnlocked(string unitKey)
        {
            if (string.IsNullOrWhiteSpace(unitKey)) return false;
            if (unitKey.IndexOf("Knight", StringComparison.OrdinalIgnoreCase) >= 0 || unitKey.Contains("\uAE30\uC0AC"))
                return true;
            if (unitKey.IndexOf("Archer", StringComparison.OrdinalIgnoreCase) >= 0 || unitKey.Contains("\uAD81\uC218"))
                return firstBattleCompleted;
            if (unitKey.IndexOf("Wizard", StringComparison.OrdinalIgnoreCase) >= 0 ||
                unitKey.IndexOf("Mage", StringComparison.OrdinalIgnoreCase) >= 0 || unitKey.Contains("\uB9C8\uBC95\uC0AC"))
                return guildIntroductionCompleted;
            return true;
        }

        public static void CompleteFirstBattle() => firstBattleCompleted = true;

        public static bool TryBeginGuildIntroduction()
        {
            if (!firstBattleCompleted || guildIntroductionPlaying || guildIntroductionCompleted)
                return false;
            guildIntroductionPlaying = true;
            return true;
        }

        public static void CompleteGuildIntroduction()
        {
            guildIntroductionPlaying = false;
            guildIntroductionCompleted = true;
        }


        public static void RecordStageClear(TacticalMapData stage)
        {
            if (stage == null)
                return;

            if (stage.IsBossMission)
            {
                partyLevel = TacticalMissionGradeUtility.GetNextPartyLevel(partyLevel);
                completedMissionsAtCurrentPartyLevel = 0;
                return;
            }

            completedMissionsAtCurrentPartyLevel = UnityEngine.Mathf.Clamp(
                completedMissionsAtCurrentPartyLevel + 1,
                0,
                BossMissionInterval - 1);
        }

        public static bool IsStageAvailableForCurrentParty(TacticalMapData stage)
        {
            if (stage == null)
                return false;
            if (!TacticalMissionGradeUtility.IsSameAsPartyLevel(stage.missionGrade, partyLevel))
                return false;
            return NextMissionIsBoss ? stage.IsBossMission : !stage.IsBossMission;
        }

        public static void SelectStage(string stageName) => selectedStageName = stageName;

        public static string ConsumeSelectedStageName()
        {
            var value = selectedStageName;
            selectedStageName = null;
            return value;
        }
    }
}