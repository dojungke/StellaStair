using StellaStair.Battle;
using StellaStair.Town;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace StellaStair.Editor
{
    public static class TownHubUiBinder
    {
        [MenuItem("Stella Stair/Create Town Hub UI In Current Scene")]
        public static void CreateInCurrentScene()
        {
            var progression = Object.FindAnyObjectByType<StageProgression>(FindObjectsInactive.Include);
            if (progression == null)
            {
                Debug.LogError("StageProgression이 있는 전투 씬에서 실행하세요.");
                return;
            }

            var presenter = progression.GetComponent<TownHubPresenter>();
            if (presenter == null)
                presenter = Undo.AddComponent<TownHubPresenter>(progression.gameObject);
            presenter.EnsureUiExistsInScene();
            EditorUtility.SetDirty(presenter);
            EditorSceneManager.MarkSceneDirty(progression.gameObject.scene);
            Selection.activeGameObject = presenter.TownRoot;
        }
    }
}
