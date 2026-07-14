using StellaStair.Battle;
using StellaStair.Grid;
using UnityEngine;

namespace StellaStair.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class TacticalStageBackground : MonoBehaviour
    {
        private const string DefaultBackgroundResource = "BattleArt/ForestVillageBackground";

        [SerializeField] private Sprite defaultBackground;
        [SerializeField] private Color defaultTint = Color.white;
        [SerializeField] private int sortingOrder = -1000;
        [SerializeField, Min(1f)] private float fillOverscan = 1.02f;

        private Camera targetCamera;
        private SpriteRenderer backgroundRenderer;
        private int lastPixelWidth;
        private int lastPixelHeight;
        private float lastOrthographicSize;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallForTacticalScene()
        {
            var board = FindAnyObjectByType<TacticalBoard>();
            if (board == null)
                return;

            RefreshTerrainArt(board);
            RefreshForCurrentStage();
        }

        public static void RefreshForCurrentStage()
        {
            var target = Camera.main ?? FindAnyObjectByType<Camera>();
            if (target == null)
                return;

            var presenter = target.GetComponent<TacticalStageBackground>();
            if (presenter == null)
                presenter = target.gameObject.AddComponent<TacticalStageBackground>();
            presenter.Refresh();
        }

        private static void RefreshTerrainArt(TacticalBoard board)
        {
            RefreshTilemap(board.WalkableTilemap);
            RefreshTilemap(board.LadderTilemap);
            RefreshTilemap(board.WoodTilemap);
            RefreshTilemap(board.CrateTilemap);
            RefreshTilemap(board.BombCrateTilemap);
        }

        private static void RefreshTilemap(UnityEngine.Tilemaps.Tilemap tilemap)
        {
            if (tilemap == null)
                return;

            tilemap.RefreshAllTiles();
        }

        private void Awake()
        {
            targetCamera = GetComponent<Camera>();
            EnsureRenderer();
            Refresh();
        }

        private void OnEnable()
        {
            targetCamera ??= GetComponent<Camera>();
            EnsureRenderer();
            Refresh();
        }

        private void LateUpdate()
        {
            if (targetCamera == null || backgroundRenderer == null ||
                backgroundRenderer.sprite == null)
                return;

            if (lastPixelWidth != targetCamera.pixelWidth ||
                lastPixelHeight != targetCamera.pixelHeight ||
                !Mathf.Approximately(lastOrthographicSize, targetCamera.orthographicSize))
                FitToCamera();
        }

        public void Refresh()
        {
            targetCamera ??= GetComponent<Camera>();
            EnsureRenderer();
            defaultBackground ??= Resources.Load<Sprite>(DefaultBackgroundResource);

            var progression = FindAnyObjectByType<StageProgression>();
            var stage = progression != null ? progression.CurrentStage : null;
            backgroundRenderer.sprite = stage != null && stage.backgroundSprite != null
                ? stage.backgroundSprite
                : defaultBackground;
            backgroundRenderer.color = stage != null ? stage.BackgroundTint : defaultTint;
            backgroundRenderer.sortingOrder = sortingOrder;
            backgroundRenderer.enabled = backgroundRenderer.sprite != null;
            FitToCamera();
        }

        private void EnsureRenderer()
        {
            if (backgroundRenderer != null)
                return;

            var child = transform.Find("Stage Background");
            if (child != null)
                backgroundRenderer = child.GetComponent<SpriteRenderer>();
            if (backgroundRenderer != null)
                return;

            var backgroundObject = new GameObject("Stage Background", typeof(SpriteRenderer));
            backgroundObject.transform.SetParent(transform, false);
            backgroundRenderer = backgroundObject.GetComponent<SpriteRenderer>();
        }

        private void FitToCamera()
        {
            if (targetCamera == null || backgroundRenderer == null ||
                backgroundRenderer.sprite == null || !targetCamera.orthographic)
                return;

            var spriteSize = backgroundRenderer.sprite.bounds.size;
            if (spriteSize.x <= 0f || spriteSize.y <= 0f)
                return;

            var cameraHeight = targetCamera.orthographicSize * 2f;
            var cameraWidth = cameraHeight * targetCamera.aspect;
            var scale = Mathf.Max(cameraWidth / spriteSize.x, cameraHeight / spriteSize.y) *
                        Mathf.Max(1f, fillOverscan);
            backgroundRenderer.transform.localPosition = new Vector3(
                0f, 0f, Mathf.Max(1f, targetCamera.nearClipPlane + 0.1f));
            backgroundRenderer.transform.localRotation = Quaternion.identity;
            backgroundRenderer.transform.localScale = new Vector3(scale, scale, 1f);

            lastPixelWidth = targetCamera.pixelWidth;
            lastPixelHeight = targetCamera.pixelHeight;
            lastOrthographicSize = targetCamera.orthographicSize;
        }
    }
}
