using System;
using System.Collections.Generic;
using Game.Sim.Stroke;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Client.Stroke
{
    // Detects a mouse/touch drag via the new Input System's Pointer device, draws it live
    // on a UI canvas, and on release feeds the resampled points into Game.sim's StrokeAnalyzer.
    public class StrokeInputController : MonoBehaviour
    {
        [SerializeField] private RectTransform canvasRect;
        [SerializeField] private UILineRenderer lineRenderer;
        [SerializeField] private int resampleCount = 24;

        // Raised after a completed drag has been analyzed, with the same fixed-point points
        // that were fed into the analyzer (for consumers that need the raw trajectory too).
        public event Action<StrokeResult, IReadOnlyList<StrokePoint>> StrokeCompleted;

        private readonly List<Vector2> localPoints = new List<Vector2>();
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private PointerEventData pointerEventData;
        private bool isDragging;

        private void Update()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null)
                return;

            if (pointer.press.wasPressedThisFrame)
            {
                // A press that starts on a button/toggle belongs to the UI, not to a stroke;
                // otherwise a slight finger drift while tapping gets recorded as a turn.
                Vector2 pressPos = pointer.position.ReadValue();
                if (!IsOverInteractableUi(pressPos))
                    BeginStroke(pressPos);
            }
            else if (isDragging && pointer.press.wasReleasedThisFrame)
            {
                AppendPoint(pointer.position.ReadValue());
                EndStroke();
            }
            else if (isDragging && pointer.press.isPressed)
            {
                AppendPoint(pointer.position.ReadValue());
            }
        }

        // Only Selectables count: a plain full-screen Image (or the stroke line itself) may be a
        // raycast target, so EventSystem.IsPointerOverGameObject() alone would block every stroke.
        private bool IsOverInteractableUi(Vector2 screenPos)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
                return false;

            pointerEventData ??= new PointerEventData(eventSystem);
            pointerEventData.Reset();
            pointerEventData.position = screenPos;

            uiHits.Clear();
            eventSystem.RaycastAll(pointerEventData, uiHits);
            foreach (RaycastResult hit in uiHits)
            {
                if (hit.gameObject.GetComponentInParent<Selectable>() != null)
                    return true;
            }
            return false;
        }

        private void BeginStroke(Vector2 screenPos)
        {
            isDragging = true;
            localPoints.Clear();
            AppendPoint(screenPos);
        }

        private void AppendPoint(Vector2 screenPos)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, null, out Vector2 localPoint))
                return;

            if (localPoints.Count > 0 && Vector2.Distance(localPoints[localPoints.Count - 1], localPoint) < 1f)
                return;

            localPoints.Add(localPoint);
            lineRenderer.SetPoints(localPoints);
        }

        private void EndStroke()
        {
            isDragging = false;

            if (localPoints.Count >= 2)
            {
                List<Vector2> resampled = StrokeResampler.Resample(localPoints, resampleCount);
                List<StrokePoint> simPoints = ToSimPoints(resampled);
                StrokeResult result = StrokeAnalyzer.Analyze(simPoints);
                LogResult(result);
                LogRawRange(simPoints); // TEMP: coordinate range check, remove after normalization is decided
                StrokeCompleted?.Invoke(result, simPoints);
            }

            localPoints.Clear();
            lineRenderer.SetPoints(localPoints);
        }

        private static List<StrokePoint> ToSimPoints(List<Vector2> screenSpacePoints)
        {
            // Boundary conversion: Game.Client's float coordinates become Game.sim's fixed-point.
            var simPoints = new List<StrokePoint>(screenSpacePoints.Count);
            foreach (Vector2 p in screenSpacePoints)
            {
                long rawX = (long)(p.x * Fixed.Scale);
                long rawY = (long)(p.y * Fixed.Scale);
                simPoints.Add(new StrokePoint(rawX, rawY));
            }
            return simPoints;
        }

        // TEMP: prints the raw fixed-point bounding box of a stroke (canvasRect local units * Fixed.Scale).
        private static void LogRawRange(List<StrokePoint> points)
        {
            long minX = long.MaxValue, maxX = long.MinValue, minY = long.MaxValue, maxY = long.MinValue;
            foreach (StrokePoint p in points)
            {
                minX = Math.Min(minX, p.X.Raw);
                maxX = Math.Max(maxX, p.X.Raw);
                minY = Math.Min(minY, p.Y.Raw);
                maxY = Math.Max(maxY, p.Y.Raw);
            }
            Debug.Log($"[StrokeRange] raw X: {minX} ~ {maxX}, raw Y: {minY} ~ {maxY} (local X: {minX / (float)Fixed.Scale:F1} ~ {maxX / (float)Fixed.Scale:F1}, local Y: {minY / (float)Fixed.Scale:F1} ~ {maxY / (float)Fixed.Scale:F1})");
        }

        private static void LogResult(StrokeResult result)
        {
            if (result.Type == StrokeType.Slash)
            {
                float startAngle = DirectionToAngleDegrees(result.StartDirectionX, result.StartDirectionY);
                float endAngle = DirectionToAngleDegrees(result.EndDirectionX, result.EndDirectionY);
                Debug.Log($"[Stroke] Slash - Length: {result.Length}, StartAngle: {startAngle:F1} deg, EndAngle: {endAngle:F1} deg");
            }
            else
            {
                Debug.Log($"[Stroke] Thrust - Area: {result.Area}");
            }
        }

        private static float DirectionToAngleDegrees(Fixed dirX, Fixed dirY)
        {
            // Boundary conversion back to float: only Game.Client is allowed to call Mathf/trig.
            float x = dirX.Raw / (float)Fixed.Scale;
            float y = dirY.Raw / (float)Fixed.Scale;
            return Mathf.Atan2(y, x) * Mathf.Rad2Deg;
        }
    }
}
