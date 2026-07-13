using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace StellaStair.Presentation
{
    [CreateAssetMenu(menuName = "Stella Stair/Battle UI Data", fileName = "BattleUI")]
    public sealed class BattleUiData : ScriptableObject
    {
        [Serializable]
        public sealed class Element
        {
            public string hierarchyPath;
            public bool active;
            public Vector2 anchorMin;
            public Vector2 anchorMax;
            public Vector2 anchoredPosition;
            public Vector2 sizeDelta;
            public Vector2 pivot;
            public Vector3 localScale = Vector3.one;
            public float localRotationZ;
            public bool hasTmpText;
            public string tmpText;
        }

        [SerializeField] private List<Element> elements = new();
        [SerializeField] private GameObject runtimeUiPrefab;
        public IReadOnlyList<Element> Elements => elements;
        public GameObject RuntimeUiPrefab => runtimeUiPrefab;

        public void SetRuntimeUiPrefab(GameObject prefab) => runtimeUiPrefab = prefab;

        public void CaptureCurrentScene()
        {
            elements.Clear();
            foreach (var rect in FindUiElements())
            {
                var saved = new Element
                {
                    hierarchyPath = GetHierarchyPath(rect),
                    active = rect.gameObject.activeSelf,
                    anchorMin = rect.anchorMin,
                    anchorMax = rect.anchorMax,
                    anchoredPosition = rect.anchoredPosition,
                    sizeDelta = rect.sizeDelta,
                    pivot = rect.pivot,
                    localScale = rect.localScale,
                    localRotationZ = rect.localEulerAngles.z
                };
                if (rect.TryGetComponent<TMP_Text>(out var label))
                {
                    saved.hasTmpText = true;
                    saved.tmpText = label.text;
                }
                elements.Add(saved);
            }
        }

        public void ApplyToCurrentScene()
        {
            var sceneLevelUpOverlays = GetSceneLevelUpUpgradeOverlays();
            var keepSceneLevelUpUi = sceneLevelUpOverlays.Count > 0;
            var byPath = BuildElementLookup();
            var hasSavedUi = false;
            foreach (var saved in elements)
            {
                if (saved != null &&
                    (!keepSceneLevelUpUi || !IsLevelUpUpgradeUiPath(saved.hierarchyPath)) &&
                    TryFindElement(byPath, saved.hierarchyPath, out _))
                {
                    hasSavedUi = true;
                    break;
                }
            }

            if (!hasSavedUi && runtimeUiPrefab != null)
            {
                UnityEngine.Object.Instantiate(runtimeUiPrefab).name = runtimeUiPrefab.name;
                if (keepSceneLevelUpUi)
                    RemoveInstantiatedLevelUpUpgradeUi(sceneLevelUpOverlays);
                byPath = BuildElementLookup();
            }

            foreach (var saved in elements)
            {
                if (saved == null ||
                    keepSceneLevelUpUi && IsLevelUpUpgradeUiPath(saved.hierarchyPath) ||
                    !TryFindElement(byPath, saved.hierarchyPath, out var rect))
                    continue;
                rect.anchorMin = saved.anchorMin;
                rect.anchorMax = saved.anchorMax;
                rect.anchoredPosition = saved.anchoredPosition;
                rect.sizeDelta = saved.sizeDelta;
                rect.pivot = saved.pivot;
                rect.localScale = saved.localScale;
                rect.localRotation = Quaternion.Euler(0f, 0f, saved.localRotationZ);
                if (saved.hasTmpText && rect.TryGetComponent<TMP_Text>(out var label))
                    label.text = saved.tmpText;
                rect.gameObject.SetActive(saved.active);
            }
        }
        private static HashSet<GameObject> GetSceneLevelUpUpgradeOverlays()
        {
            var overlays = new HashSet<GameObject>();
            foreach (var rect in FindUiElements())
            {
                if (rect != null && rect.name == "Level Up Upgrade Overlay")
                    overlays.Add(rect.gameObject);
            }
            return overlays;
        }

        private static void RemoveInstantiatedLevelUpUpgradeUi(HashSet<GameObject> sceneOverlays)
        {
            foreach (var rect in FindUiElements())
            {
                if (rect == null || rect.name != "Level Up Upgrade Overlay" ||
                    sceneOverlays.Contains(rect.gameObject))
                    continue;
                DestroyUiObject(rect.gameObject);
            }
        }

        private static void DestroyUiObject(GameObject target)
        {
            if (target == null)
                return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(target);
            else
                UnityEngine.Object.DestroyImmediate(target);
        }

        private static bool IsLevelUpUpgradeUiPath(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                (path.EndsWith("Level Up Upgrade Overlay", StringComparison.Ordinal) ||
                 path.Contains("Level Up Upgrade Overlay/", StringComparison.Ordinal) ||
                 path.EndsWith("Level Up Upgrade Panel", StringComparison.Ordinal) ||
                 path.Contains("Level Up Upgrade Panel/", StringComparison.Ordinal));
        }

        private static Dictionary<string, RectTransform> BuildElementLookup()
        {
            var lookup = new Dictionary<string, RectTransform>();
            foreach (var rect in FindUiElements())
                lookup[GetHierarchyPath(rect)] = rect;
            return lookup;
        }

        private static bool TryFindElement(
            Dictionary<string, RectTransform> lookup,
            string savedPath,
            out RectTransform element)
        {
            if (lookup.TryGetValue(savedPath, out element))
                return true;
            foreach (var pair in lookup)
            {
                if (pair.Key.EndsWith($"/{savedPath}"))
                {
                    element = pair.Value;
                    return true;
                }
            }
            element = null;
            return false;
        }

        private static IEnumerable<RectTransform> FindUiElements()
        {
            foreach (var rect in UnityEngine.Object.FindObjectsByType<RectTransform>(
                         FindObjectsInactive.Include))
            {
                if (rect != null && rect.GetComponentInParent<Canvas>(true) != null)
                    yield return rect;
            }
        }

        public static string GetHierarchyPath(Transform transform)
        {
            var path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = $"{transform.name}/{path}";
            }
            return path;
        }
    }
}
