using StellaStair.Grid;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace StellaStair.Editor
{
    internal static class EnvironmentArtBinder
    {
        private const string BackgroundPath =
            "Assets/StellaStair/Resources/BattleArt/ForestVillageBackground.png";

        private static readonly ArtBinding[] TileBindings =
        {
            new("Assets/StellaStair/Art/Environment/GroundGrass.png",
                "Assets/StellaStair/Sample/Ground.asset"),
            new("Assets/StellaStair/Art/Environment/GroundStone.png",
                "Assets/StellaStair/Sample/GroundStone.asset"),
            new("Assets/StellaStair/Art/Environment/WoodPlatform.png",
                "Assets/StellaStair/Sample/Wood.asset"),
            new("Assets/StellaStair/Art/Environment/Ladder.png",
                "Assets/StellaStair/Sample/Ladder.asset"),
            new("Assets/StellaStair/Art/Environment/Crate.png",
                "Assets/StellaStair/Sample/Crate.asset"),
            new("Assets/StellaStair/Art/Environment/BombCrate.png",
                "Assets/StellaStair/Sample/BombCrate.asset")
        };

        private static bool isBinding;

        [InitializeOnLoadMethod]
        private static void ScheduleBinding()
        {
            EditorApplication.delayCall -= BindEnvironmentArt;
            EditorApplication.delayCall += BindEnvironmentArt;
        }

        private static void BindEnvironmentArt()
        {
            if (isBinding || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            isBinding = true;
            try
            {
                var changed = false;
                for (var i = 0; i < TileBindings.Length; i++)
                {
                    var binding = TileBindings[i];
                    var sprite = ImportSprite(binding.SpritePath, 512f);
                    var tile = AssetDatabase.LoadAssetAtPath<Tile>(binding.TilePath);
                    if (sprite == null || tile == null)
                        continue;

                    if (tile.sprite == sprite && tile.color == Color.white)
                        continue;

                    tile.sprite = sprite;
                    tile.color = Color.white;
                    EditorUtility.SetDirty(tile);
                    changed = true;
                }

                var background = ImportSprite(BackgroundPath, 100f);
                if (background != null)
                    changed |= BindMapBackgrounds(background);

                if (changed)
                    AssetDatabase.SaveAssets();
                RefreshOpenTilemaps();
                SceneView.RepaintAll();
                Debug.Log(changed
                    ? "Environment art sprites rebound successfully."
                    : "Environment art sprites refreshed successfully.");
            }
            finally
            {
                isBinding = false;
            }
        }

        private static Sprite ImportSprite(string path, float pixelsPerUnit)
        {
            AssetDatabase.ImportAsset(
                path,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                Debug.LogError($"Environment art importer is unavailable: {path}");
                return null;
            }

            var needsReimport =
                importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                !Mathf.Approximately(importer.spritePixelsPerUnit, pixelsPerUnit) ||
                importer.mipmapEnabled ||
                !importer.alphaIsTransparency ||
                importer.textureCompression != TextureImporterCompression.Uncompressed ||
                importer.wrapMode != TextureWrapMode.Clamp;

            if (needsReimport)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = pixelsPerUnit;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                Debug.LogError($"Environment art sprite could not be loaded: {path}");
            return sprite;
        }

        private static bool BindMapBackgrounds(Sprite background)
        {
            var changed = false;
            var mapGuids = AssetDatabase.FindAssets(
                "t:TacticalMapData", new[] { "Assets/StellaStair/Maps" });
            for (var i = 0; i < mapGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(mapGuids[i]);
                var map = AssetDatabase.LoadAssetAtPath<TacticalMapData>(path);
                if (map == null ||
                    map.backgroundSprite == background && map.backgroundTint == Color.white)
                    continue;

                map.backgroundSprite = background;
                map.backgroundTint = Color.white;
                EditorUtility.SetDirty(map);
                changed = true;
            }

            return changed;
        }

        private static void RefreshOpenTilemaps()
        {
            var tilemaps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include);
            for (var i = 0; i < tilemaps.Length; i++)
                tilemaps[i].RefreshAllTiles();
        }

        private sealed class ArtBinding
        {
            public ArtBinding(string spritePath, string tilePath)
            {
                SpritePath = spritePath;
                TilePath = tilePath;
            }

            public string SpritePath { get; }
            public string TilePath { get; }
        }
    }
}
