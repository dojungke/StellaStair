using System.Collections;
using StellaStair.Units;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace StellaStair.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(-100)]
    public sealed class TacticalCameraPan : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float keyboardSpeed = 8f;
        [SerializeField] private bool clampPosition = true;
        [SerializeField] private Vector2 minimumPosition = new(-12f, -3f);
        [SerializeField] private Vector2 maximumPosition = new(12f, 8f);
        [SerializeField, Min(1f)] private float leftDragThreshold = 8f;
        [SerializeField] private float focusVerticalOffset = 0.5f;
        [SerializeField, Min(0f)] private float minimumFocusDistance = 3f;

        private Camera controlledCamera;
        private Vector2 previousPointerPosition;
        private Vector2 dragStartPosition;
        private bool dragging;
        private bool leftDragCandidate;

        public bool SuppressLeftClickThisFrame { get; private set; }

        private void Awake() => controlledCamera = GetComponent<Camera>();

        public IEnumerator FocusOn(TacticalUnit unit, float duration = 0.35f)
        {
            if (unit == null || controlledCamera == null)
                yield break;

            var start = transform.position;
            var target = unit.GetPreviewStandingWorldPosition(unit.Position);
            target.y += focusVerticalOffset;
            target.z = start.z;
            if (Vector2.Distance(start, target) <= minimumFocusDistance)
                yield break;
            var elapsed = 0f;
            duration = Mathf.Max(0.01f, duration);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                var position = Vector3.Lerp(start, target, t);
                position.z = start.z;
                transform.position = position;
                ClampPosition();
                yield return null;
            }
            transform.position = target;
            ClampPosition();
        }

        public IEnumerator FocusOnPosition(Vector3 worldPosition, float duration = 0.45f, bool ignoreMinimumFocusDistance = false)
        {
            if (controlledCamera == null)
                yield break;

            var start = transform.position;
            var target = worldPosition;
            target.y += focusVerticalOffset;
            target.z = start.z;
            if (!ignoreMinimumFocusDistance && Vector2.Distance(start, target) <= minimumFocusDistance)
                yield break;
            var elapsed = 0f;
            duration = Mathf.Max(0.01f, duration);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                var position = Vector3.Lerp(start, target, t);
                position.z = start.z;
                transform.position = position;
                ClampPosition();
                yield return null;
            }
            transform.position = target;
            ClampPosition();
        }

        public IEnumerator RestorePosition(Vector3 worldPosition, float duration = 0.35f)
        {
            if (controlledCamera == null)
                yield break;

            var start = transform.position;
            var target = worldPosition;
            target.z = start.z;
            var elapsed = 0f;
            duration = Mathf.Max(0.01f, duration);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                var position = Vector3.Lerp(start, target, t);
                position.z = start.z;
                transform.position = position;
                ClampPosition();
                yield return null;
            }
            transform.position = target;
            ClampPosition();
        }

        private void Update()
        {
            SuppressLeftClickThisFrame = false;
            MoveWithKeyboard();
            MoveWithPointer();
            ClampPosition();
        }

        private void MoveWithKeyboard()
        {
            if (Keyboard.current == null)
                return;

            var direction = Vector2.zero;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                direction.x -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                direction.x += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
                direction.y -= 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
                direction.y += 1f;

            if (direction.sqrMagnitude > 1f)
                direction.Normalize();
            transform.position += (Vector3)(direction * (keyboardSpeed * Time.unscaledDeltaTime));
        }

        private void MoveWithPointer()
        {
            if (Mouse.current == null || controlledCamera == null)
                return;

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                leftDragCandidate = EventSystem.current == null ||
                                    !EventSystem.current.IsPointerOverGameObject();
                dragging = false;
                dragStartPosition = Mouse.current.position.ReadValue();
                previousPointerPosition = dragStartPosition;
            }
            else if (Mouse.current.middleButton.wasPressedThisFrame ||
                     Mouse.current.rightButton.wasPressedThisFrame)
            {
                dragging = EventSystem.current == null ||
                           !EventSystem.current.IsPointerOverGameObject();
                leftDragCandidate = false;
                previousPointerPosition = Mouse.current.position.ReadValue();
            }

            if (leftDragCandidate && Mouse.current.leftButton.isPressed && !dragging)
            {
                var distance = Vector2.Distance(
                    dragStartPosition, Mouse.current.position.ReadValue());
                dragging = distance >= leftDragThreshold;
            }

            if (Mouse.current.leftButton.wasReleasedThisFrame && leftDragCandidate)
            {
                SuppressLeftClickThisFrame = dragging;
                leftDragCandidate = false;
                dragging = false;
                return;
            }

            if (Mouse.current.middleButton.wasReleasedThisFrame &&
                !Mouse.current.rightButton.isPressed ||
                Mouse.current.rightButton.wasReleasedThisFrame &&
                !Mouse.current.middleButton.isPressed)
                dragging = false;
            if (!dragging ||
                !Mouse.current.leftButton.isPressed &&
                !Mouse.current.middleButton.isPressed && !Mouse.current.rightButton.isPressed)
                return;

            var pointerPosition = Mouse.current.position.ReadValue();
            var screenDelta = pointerPosition - previousPointerPosition;
            previousPointerPosition = pointerPosition;
            var worldPerPixel = controlledCamera.orthographic
                ? controlledCamera.orthographicSize * 2f / Mathf.Max(1, Screen.height)
                : 0.01f;
            transform.position -= new Vector3(
                screenDelta.x * worldPerPixel,
                screenDelta.y * worldPerPixel,
                0f);
        }

        private void ClampPosition()
        {
            if (!clampPosition)
                return;
            var position = transform.position;
            position.x = Mathf.Clamp(position.x, minimumPosition.x, maximumPosition.x);
            position.y = Mathf.Clamp(position.y, minimumPosition.y, maximumPosition.y);
            transform.position = position;
        }
    }
}
