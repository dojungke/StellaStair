using UnityEngine;

namespace StellaStair.Grid
{
    [CreateAssetMenu(menuName = "Stella Stair/Tactical Objective Data", fileName = "ObjectiveData")]
    public sealed class TacticalObjectiveData : ScriptableObject
    {
        [field: SerializeField] public string DisplayName { get; private set; } = "Objective";
        [field: SerializeField, TextArea(2, 4)] public string Description { get; private set; } = string.Empty;
        [field: SerializeField, Min(1)] public int MaxHealth { get; private set; } = 8;
        [field: SerializeField, Min(1)] public int Level { get; private set; } = 1;
        [field: SerializeField] public Sprite Sprite { get; private set; }
    }
}
