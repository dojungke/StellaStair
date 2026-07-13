using StellaStair.Units;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace StellaStair.Editor
{
    public static class KnightAnimationControllerInstaller
    {
        private const string AnimationFolder = "Assets/StellaStair/Animations";
        private const string KnightControllerPath = AnimationFolder + "/Knight.controller";
        private const string KnightIdleClipPath = AnimationFolder + "/Knight_Idle.anim";
        private const string KnightWalkClipPath = AnimationFolder + "/Knight_Walk.anim";
        private const string KnightAttackClipPath = AnimationFolder + "/Knight_Attack.anim";
        private const string KnightDefinitionPath = "Assets/StellaStair/Resources/UnitDefinitions/Knight.asset";

        [InitializeOnLoadMethod]
        private static void ScheduleEnsureAssets()
        {
            EditorApplication.delayCall -= EnsureAssets;
            EditorApplication.delayCall += EnsureAssets;
        }
        public static void EnsureAssets()
        {
            EnsureFolder("Assets/StellaStair", "Animations");
            var idle = LoadOrCreateClip(KnightIdleClipPath, CreateIdleClip);
            var walk = LoadOrCreateClip(KnightWalkClipPath, CreateWalkClip);
            var attack = LoadOrCreateClip(KnightAttackClipPath, CreateAttackClip);
            RemoveRootPositionCurves(idle);
            RemoveRootPositionCurves(walk);
            RemoveRootPositionCurves(attack);
            var controller = LoadOrCreateController(idle, walk, attack);
            AssignControllerToKnight(controller);
            AssetDatabase.SaveAssets();
        }

        private static AnimationClip LoadOrCreateClip(string path, System.Func<AnimationClip> factory)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null)
                return clip;
            clip = factory();
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        private static AnimationClip CreateIdleClip()
        {
            var clip = new AnimationClip { name = "Knight_Idle", frameRate = 30f };
            clip.wrapMode = WrapMode.Loop;
            SetRotationCurve(clip, 0f, 0f, 0.6f, 0f);
            return clip;
        }

        private static AnimationClip CreateWalkClip()
        {
            var clip = new AnimationClip { name = "Knight_Walk", frameRate = 30f };
            clip.wrapMode = WrapMode.Loop;
            SetRotationCurve(clip, 0f, -3.5f, 0.12f, 3.5f, 0.24f, -3.5f, 0.36f, 3.5f, 0.48f, -3.5f);
            return clip;
        }

        private static AnimationClip CreateAttackClip()
        {
            var clip = new AnimationClip { name = "Knight_Attack", frameRate = 30f };
            clip.wrapMode = WrapMode.Once;
            SetRotationCurve(clip, 0f, 0f, 0.06f, -8f, 0.12f, 5f, 0.18f, 0f);
            return clip;
        }

        private static void RemoveRootPositionCurves(AnimationClip clip)
        {
            if (clip == null)
                return;
            clip.SetCurve(string.Empty, typeof(Transform), "m_LocalPosition", null);
            EditorUtility.SetDirty(clip);
        }
        private static AnimatorController LoadOrCreateController(AnimationClip idle, AnimationClip walk, AnimationClip attack)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(KnightControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(KnightControllerPath);
            }

            EnsureParameter(controller, "Moving", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "Attack", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "State", AnimatorControllerParameterType.Int);
            EnsureParameter(controller, "Facing", AnimatorControllerParameterType.Float);

            var layer = controller.layers[0];
            var stateMachine = layer.stateMachine;
            var idleState = GetOrCreateState(stateMachine, "Idle", idle, new Vector3(240f, 120f, 0f));
            var walkState = GetOrCreateState(stateMachine, "Walk", walk, new Vector3(480f, 120f, 0f));
            var attackState = GetOrCreateState(stateMachine, "Attack", attack, new Vector3(360f, 280f, 0f));
            stateMachine.defaultState = idleState;
            EnsureBoolTransition(idleState, walkState, "Moving", true);
            EnsureBoolTransition(walkState, idleState, "Moving", false);
            EnsureTriggerTransition(idleState, attackState, "Attack");
            EnsureTriggerTransition(walkState, attackState, "Attack");
            EnsureExitTransition(attackState, idleState);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            foreach (var parameter in controller.parameters)
                if (parameter.name == name && parameter.type == type)
                    return;
            controller.AddParameter(name, type);
        }

        private static AnimatorState GetOrCreateState(AnimatorStateMachine stateMachine, string name,
            Motion motion, Vector3 position)
        {
            foreach (var child in stateMachine.states)
            {
                if (child.state.name != name)
                    continue;
                child.state.motion = motion;
                return child.state;
            }
            var state = stateMachine.AddState(name, position);
            state.motion = motion;
            return state;
        }

        private static void EnsureBoolTransition(AnimatorState from, AnimatorState to, string parameter, bool value)
        {
            foreach (var transition in from.transitions)
                if (transition.destinationState == to)
                    return;
            var created = from.AddTransition(to);
            created.hasExitTime = false;
            created.duration = 0.04f;
            created.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameter);
        }

        private static void EnsureTriggerTransition(AnimatorState from, AnimatorState to, string parameter)
        {
            foreach (var transition in from.transitions)
                if (transition.destinationState == to)
                    return;
            var created = from.AddTransition(to);
            created.hasExitTime = false;
            created.duration = 0.02f;
            created.AddCondition(AnimatorConditionMode.If, 0f, parameter);
        }

        private static void EnsureExitTransition(AnimatorState from, AnimatorState to)
        {
            foreach (var transition in from.transitions)
                if (transition.destinationState == to && transition.hasExitTime)
                    return;
            var created = from.AddTransition(to);
            created.hasExitTime = true;
            created.exitTime = 0.95f;
            created.duration = 0.04f;
        }

        private static void AssignControllerToKnight(RuntimeAnimatorController controller)
        {
            var definition = AssetDatabase.LoadAssetAtPath<UnitDefinition>(KnightDefinitionPath);
            if (definition == null)
                return;
            var serialized = new SerializedObject(definition);
            var property = serialized.FindProperty("<AnimationController>k__BackingField");
            if (property == null)
                return;
            property.objectReferenceValue = controller;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        private static void SetVectorCurve(AnimationClip clip, string propertyName, params object[] keys)
        {
            var x = new AnimationCurve();
            var y = new AnimationCurve();
            var z = new AnimationCurve();
            for (var i = 0; i < keys.Length; i += 2)
            {
                var time = (float)keys[i];
                var value = (Vector3)keys[i + 1];
                x.AddKey(time, value.x);
                y.AddKey(time, value.y);
                z.AddKey(time, value.z);
            }
            clip.SetCurve(string.Empty, typeof(Transform), propertyName + ".x", x);
            clip.SetCurve(string.Empty, typeof(Transform), propertyName + ".y", y);
            clip.SetCurve(string.Empty, typeof(Transform), propertyName + ".z", z);
        }

        private static void SetRotationCurve(AnimationClip clip, params float[] keys)
        {
            var curve = new AnimationCurve();
            for (var i = 0; i < keys.Length; i += 2)
                curve.AddKey(keys[i], keys[i + 1]);
            clip.SetCurve(string.Empty, typeof(Transform), "localEulerAnglesRaw.z", curve);
        }

        private static void EnsureFolder(string parent, string name)
        {
            var path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
