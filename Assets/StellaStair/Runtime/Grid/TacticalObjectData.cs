using UnityEngine;

namespace StellaStair.Grid
{
    [CreateAssetMenu(menuName = "Stella Stair/Tactical Object Data", fileName = "ObjectData")]
    public sealed class TacticalObjectData : ScriptableObject
    {
        [field: SerializeField] public string DisplayName { get; private set; } = "Object";
        [field: SerializeField, TextArea(2, 4)] public string Description { get; private set; } = string.Empty;
        [field: SerializeField, Min(1)] public int MaxHealth { get; private set; } = 2;
        [field: SerializeField] public Sprite Sprite { get; private set; }
        [field: SerializeField, TextArea(1, 2)] public string FunctionDescription { get; private set; } = string.Empty;
        [field: SerializeField] public bool Explosive { get; private set; }
        [field: SerializeField, Min(0)] public int ExplosionDamage { get; private set; } = 3;
    }
}
