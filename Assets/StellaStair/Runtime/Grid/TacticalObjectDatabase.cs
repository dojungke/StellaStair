using UnityEngine;

namespace StellaStair.Grid
{
    [CreateAssetMenu(
        fileName = "TacticalObjectDatabase",
        menuName = "Stella Stair/Tactical Object Database")]
    public sealed class TacticalObjectDatabase : ScriptableObject
    {
        [Header("Tiles")]
        [SerializeField, Min(1)] private int woodMaxHealth = 2;

        [Header("Crates")]
        [SerializeField, Min(1)] private int crateMaxHealth = 2;
        [SerializeField, Min(1)] private int bombCrateMaxHealth = 1;
        [SerializeField, Min(1)] private int bombCrateExplosionDamage = 3;

        [Header("Objectives")]
        [SerializeField, Min(1)] private int attackObjectiveMaxHealth = 8;
        [SerializeField, Min(1)] private int defenseObjectiveMaxHealth = 12;

        public int WoodMaxHealth => woodMaxHealth;
        public int CrateMaxHealth => crateMaxHealth;
        public int BombCrateMaxHealth => bombCrateMaxHealth;
        public int BombCrateExplosionDamage => bombCrateExplosionDamage;
        public int AttackObjectiveMaxHealth => attackObjectiveMaxHealth;
        public int DefenseObjectiveMaxHealth => defenseObjectiveMaxHealth;
    }
}
