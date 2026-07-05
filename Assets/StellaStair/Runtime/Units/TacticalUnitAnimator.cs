using UnityEngine;

namespace StellaStair.Units
{
    public enum TacticalUnitAnimationState
    {
        Idle,
        Move,
        Attack,
        Hit,
        Death,
        LevelUp
    }

    public sealed class TacticalUnitAnimator : MonoBehaviour
    {
        [Header("Animator")]
        [SerializeField] private Animator animator;
        [SerializeField] private string stateParameter = "State";
        [SerializeField] private string movingParameter = "Moving";
        [SerializeField] private string attackTrigger = "Attack";
        [SerializeField] private string hitTrigger = "Hit";
        [SerializeField] private string deathTrigger = "Death";
        [SerializeField] private string levelUpTrigger = "LevelUp";
        [SerializeField] private string facingParameter = "Facing";
        [SerializeField] private bool flipRootWithFacing = true;
        [SerializeField] private bool lockRootLocalPosition = true;

        [Header("Procedural Walk")]
        [SerializeField] private bool proceduralWalkMotion = false;
        [SerializeField, Min(0f)] private float walkBobHeight = 0.045f;
        [SerializeField, Min(0f)] private float walkSwayAngle = 3.5f;
        [SerializeField, Min(0.1f)] private float walkFrequency = 7.5f;

        private int stateHash;
        private int movingHash;
        private int attackHash;
        private int hitHash;
        private int deathHash;
        private int levelUpHash;
        private int facingHash;
        private Vector3 initialScale;
        private Vector3 baseLocalPosition;
        private Quaternion baseLocalRotation;
        private bool moving;

        private void Awake()
        {
            initialScale = transform.localScale;
            baseLocalPosition = transform.localPosition;
            baseLocalRotation = transform.localRotation;
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            CacheHashes();
        }

        public void ApplyController(RuntimeAnimatorController controller)
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            if (animator == null && controller != null)
                animator = gameObject.AddComponent<Animator>();
            if (animator != null && controller != null)
                animator.runtimeAnimatorController = controller;
        }

        public void Bind(TacticalUnit unit)
        {
            if (unit == null)
                return;
            initialScale = transform.localScale;
            baseLocalPosition = transform.localPosition;
            baseLocalRotation = transform.localRotation;
            SetFacing(unit.FacingDirection);
            PlayIdle();
        }

        public void CaptureCurrentLocalPose()
        {
            baseLocalPosition = transform.localPosition;
            baseLocalRotation = transform.localRotation;
        }

        public void PlayIdle()
        {
            moving = false;
            ResetProceduralPose();
            SetState(TacticalUnitAnimationState.Idle);
            SetBool(movingHash, false);
        }

        public void PlayMove()
        {
            moving = true;
            SetState(TacticalUnitAnimationState.Move);
            SetBool(movingHash, true);
        }

        public void PlayAttack()
        {
            moving = false;
            ResetProceduralPose();
            SetState(TacticalUnitAnimationState.Attack);
            SetTrigger(attackHash);
        }

        public void PlayHit()
        {
            moving = false;
            ResetProceduralPose();
            SetState(TacticalUnitAnimationState.Hit);
            SetTrigger(hitHash);
        }

        public void PlayDeath()
        {
            moving = false;
            ResetProceduralPose();
            SetState(TacticalUnitAnimationState.Death);
            SetTrigger(deathHash);
        }

        public void PlayLevelUp()
        {
            moving = false;
            ResetProceduralPose();
            SetState(TacticalUnitAnimationState.LevelUp);
            SetTrigger(levelUpHash);
        }

        private void Update()
        {
            if (!proceduralWalkMotion || !moving)
                return;

            var phase = Time.time * walkFrequency;
            var bob = Mathf.Abs(Mathf.Sin(phase)) * walkBobHeight;
            var sway = Mathf.Sin(phase) * walkSwayAngle;
            transform.localPosition = baseLocalPosition + Vector3.up * bob;
            transform.localRotation = baseLocalRotation * Quaternion.Euler(0f, 0f, sway);
        }

        private void LateUpdate()
        {
            if (!lockRootLocalPosition || proceduralWalkMotion)
                return;
            transform.localPosition = baseLocalPosition;
        }

        private void ResetProceduralPose()
        {
            transform.localPosition = baseLocalPosition;
            transform.localRotation = baseLocalRotation;
        }

        public void SetFacing(int direction)
        {
            var normalized = direction < 0 ? -1 : 1;
            SetFloat(facingHash, normalized);
            if (!flipRootWithFacing)
                return;
            transform.localScale = new Vector3(
                Mathf.Abs(initialScale.x) * normalized,
                initialScale.y,
                initialScale.z);
        }

        private void CacheHashes()
        {
            stateHash = Animator.StringToHash(stateParameter);
            movingHash = Animator.StringToHash(movingParameter);
            attackHash = Animator.StringToHash(attackTrigger);
            hitHash = Animator.StringToHash(hitTrigger);
            deathHash = Animator.StringToHash(deathTrigger);
            levelUpHash = Animator.StringToHash(levelUpTrigger);
            facingHash = Animator.StringToHash(facingParameter);
        }

        private void SetState(TacticalUnitAnimationState state)
        {
            if (HasParameter(stateHash, AnimatorControllerParameterType.Int))
                animator.SetInteger(stateHash, (int)state);
        }

        private void SetBool(int hash, bool value)
        {
            if (HasParameter(hash, AnimatorControllerParameterType.Bool))
                animator.SetBool(hash, value);
        }

        private void SetFloat(int hash, float value)
        {
            if (HasParameter(hash, AnimatorControllerParameterType.Float))
                animator.SetFloat(hash, value);
        }

        private void SetTrigger(int hash)
        {
            if (HasParameter(hash, AnimatorControllerParameterType.Trigger))
                animator.SetTrigger(hash);
        }

        private bool HasParameter(int hash, AnimatorControllerParameterType type)
        {
            if (animator == null || hash == 0)
                return false;
            foreach (var parameter in animator.parameters)
                if (parameter.nameHash == hash && parameter.type == type)
                    return true;
            return false;
        }
    }
}
