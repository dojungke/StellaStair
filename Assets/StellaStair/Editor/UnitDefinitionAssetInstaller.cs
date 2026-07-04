using StellaStair.Units;
using UnityEditor;
using UnityEngine;

namespace StellaStair.Editor
{
    public static class UnitDefinitionAssetInstaller
    {
        private const string Root = "Assets/StellaStair/Resources/UnitDefinitions";

        [InitializeOnLoadMethod]
        private static void ScheduleEnsureAssets()
        {
            EditorApplication.delayCall -= EnsureAssets;
            EditorApplication.delayCall += EnsureAssets;
        }

        [MenuItem("Stella Stair/Ensure Unit Definition Assets")]
        public static void EnsureAssets()
        {
            EnsureFolder("Assets/StellaStair", "Resources");
            EnsureFolder("Assets/StellaStair/Resources", "UnitDefinitions");

            CreateIfMissing("Knight", "Knight", 4, 5f, 14, 4, 1, 1, 1, false);
            CreateIfMissing("Archer", "Archer", 5, 5f, 9, 3, 4, 2, 1, true,
                minimumAttackRange: 2,
                attackDistanceRule: AttackDistanceRule.DistantOnly);
            CreateIfMissing("Wizard", "Wizard", 4, 5f, 8, 3, 4, 2, 0, true, 1, 1);
            CreateIfMissing("EnemyGuard", "Enemy Guard", 4, 5f, 12, 3, 1, 1, 0, false);
            CreateIfMissing("EnemySoldier", "Enemy Soldier", 5, 5f, 8, 2, 1, 1, 0, false);
            AssetDatabase.SaveAssets();
        }

        private static void CreateIfMissing(string assetName, string displayName,
            int movement, float moveSpeed, int health, int damage, int range,
            int verticalRange, int knockback, bool canPierceUnits,
            int areaKnockbackRadius = 0, int areaKnockbackDistance = 0,
            int minimumAttackRange = 0,
            AttackDistanceRule attackDistanceRule = AttackDistanceRule.Any)
        {
            var path = $"{Root}/{assetName}.asset";
            if (AssetDatabase.LoadAssetAtPath<UnitDefinition>(path) != null)
                return;

            var definition = ScriptableObject.CreateInstance<UnitDefinition>();
            definition.Configure(displayName, movement, moveSpeed, health, damage,
                range, verticalRange, knockback, canPierceUnits,
                areaKnockbackRadius, areaKnockbackDistance, minimumAttackRange,
                attackDistanceRule);
            AssetDatabase.CreateAsset(definition, path);
        }

        private static void EnsureFolder(string parent, string name)
        {
            var path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
