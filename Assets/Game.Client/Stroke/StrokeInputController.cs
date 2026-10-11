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
    //
    // Strokes may only start inside drawFrame, a FrameWidthCm x FrameHeightCm rectangle of physical screen
    // distance (via Screen.dpi). Game.sim receives centimetres from the frame centre, so a stroke
    // measures the same on every device regardless of resolution or canvas scaling.
    // The finished stroke stays on screen until the next stroke starts or ClearDisplayedStroke().
    public class StrokeInputController : MonoBehaviour
    {
        public const float FrameWidthCm = 4.5f;
        public const float FrameHeightCm = 4.5f;
        private const float CmPerInch = 2.54f;
        // Below this Screen.dpi is treated as unknown (0 in some editors/emulators, or nonsense).
        private const float MinPlausibleDpi = 72f;
        // Typical Android mdpi baseline.
        private const float FallbackDpi = 160f;
        private const float ReferenceCircleRadiusCm = 1f;

        [SerializeField] private RectTransform canvasRect;
        [SerializeField] private UILineRenderer lineRenderer;
        // Centred rectangle the stroke must start in; resized at runtime to FrameWidthCm x FrameHeightCm.
        [SerializeField] private RectTransform drawFrame;
        [SerializeField] private int resampleCount = 24;

        // Raised after a completed drag has been analyzed, with the same fixed-point points
        // that were fed into the analyzer (for consumers that need the raw trajectory too).
        public event Action<StrokeResult, IReadOnlyList<StrokePoint>> StrokeCompleted;

        private readonly List<Vector2> localPoints = new List<Vector2>();
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private PointerEventData pointerEventData;
        private bool isDragging;
        private Canvas canvas;
        private float dpi;
        private float frameScaleFactor = -1f;

        private void Awake()
        {
            canvas = canvasRect.GetComponentInParent<Canvas>();
            dpi = ResolveDpi();
            LogReferenceCircleArea();
        }

        private void Update()
        {
            // The canvas scale changes with resolution/orientation; keep the frame at its physical size.
            if (!Mathf.Approximately(canvas.scaleFactor, frameScaleFactor))
                ResizeFrame();

            Pointer pointer = Pointer.current;
            if (pointer == null)
                return;

            if (pointer.press.wasPressedThisFrame)
            {
                // A press that starts on a button/toggle belongs to the UI, not to a stroke;
                // otherwise a slight finger drift while tapping gets recorded as a turn.
                // Presses outside the frame are ignored entirely; once started, a drag may leave it.
                Vector2 pressPos = pointer.position.ReadValue();
                if (IsInsideFrame(pressPos) && !IsOverInteractableUi(pressPos))
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

        // Removes the finished stroke left on screen. A drag in progress is left alone.
        public void ClearDisplayedStroke()
        {
            if (isDragging)
                return;
            localPoints.Clear();
            lineRenderer.SetPoints(localPoints);
        }

        private static float ResolveDpi()
        {
            float reported = Screen.dpi;
            if (reported >= MinPlausibleDpi)
                return reported;

            Debug.LogWarning($"[Stroke] Screen.dpi({reported})가 비정상적으로 작아 기본값 {FallbackDpi}dpi로 그리기 틀 크기를 계산합니다.");
            return FallbackDpi;
        }

        private void ResizeFrame()
        {
            frameScaleFactor = canvas.scaleFactor;
            float pixelsPerCm = dpi / CmPerInch;
            // Canvas units = screen pixels / scaleFactor (Screen Space - Overlay).
            float unitsPerCm = pixelsPerCm / frameScaleFactor;
            drawFrame.anchorMin = drawFrame.anchorMax = new Vector2(0.5f, 0.5f);
            drawFrame.pivot = new Vector2(0.5f, 0.5f);
            drawFrame.anchoredPosition = Vector2.zero;
            drawFrame.sizeDelta = new Vector2(FrameWidthCm * unitsPerCm, FrameHeightCm * unitsPerCm);
            Debug.Log($"[Stroke] 그리기 틀: {FrameWidthCm}x{FrameHeightCm}cm = {FrameWidthCm * pixelsPerCm:F0}x{FrameHeightCm * pixelsPerCm:F0}px ({dpi}dpi)");
        }

        private bool IsInsideFrame(Vector2 screenPos)
            => RectTransformUtility.RectangleContainsScreenPoint(drawFrame, screenPos, null);

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

        // The drawn line is left on screen as the pending stroke's preview.
        private void EndStroke()
        {
            isDragging = false;

            if (localPoints.Count < 2)
            {
                ClearDisplayedStroke();
                return;
            }

            List<Vector2> resampled = StrokeResampler.Resample(ToFrameCm(localPoints), resampleCount);
            List<StrokePoint> simPoints = ToSimPoints(resampled);
            StrokeResult result = StrokeAnalyzer.Analyze(simPoints);
            LogResult(result);
            LogRawRange(simPoints); // TEMP: coordinate range check, remove after normalization is decided
            StrokeCompleted?.Invoke(result, simPoints);
        }

        // canvasRect local units -> centimetres, origin at the frame centre (y up).
        private List<Vector2> ToFrameCm(List<Vector2> canvasPoints)
        {
            Vector2 center = canvasRect.InverseTransformPoint(drawFrame.TransformPoint(drawFrame.rect.center));
            float unitsPerCm = drawFrame.rect.width / FrameWidthCm;

            var cmPoints = new List<Vector2>(canvasPoints.Count);
            foreach (Vector2 p in canvasPoints)
                cmPoints.Add((p - center) / unitsPerCm);
            return cmPoints;
        }

        private static List<StrokePoint> ToSimPoints(List<Vector2> cmPoints)
        {
            // Boundary conversion: Game.Client's float centimetres become Game.sim's fixed-point.
            var simPoints = new List<StrokePoint>(cmPoints.Count);
            foreach (Vector2 p in cmPoints)
            {
                long rawX = (long)Math.Round(p.x * Fixed.Scale);
                long rawY = (long)Math.Round(p.y * Fixed.Scale);
                simPoints.Add(new StrokePoint(rawX, rawY));
            }
            return simPoints;
        }

        // Calibration aid for rebalancing: the Area a perfect radius-1cm circle gets after going
        // through the same resample -> fixed-point -> analyzer path as a drawn stroke.
        private void LogReferenceCircleArea()
        {
            const int segments = 256;
            var circle = new List<Vector2>(segments + 1);
            for (int i = 0; i <= segments; i++)
            {
                float angle = 2f * Mathf.PI * i / segments;
                circle.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ReferenceCircleRadiusCm);
            }

            StrokeResult result = StrokeAnalyzer.Analyze(ToSimPoints(StrokeResampler.Resample(circle, resampleCount)));
            string value = result.Type == StrokeType.Thrust ? $"Area: {result.Area}" : $"Thrust가 아닌 {result.Type}으로 판정됨";
            Debug.Log($"[StrokeScale] 반지름 {ReferenceCircleRadiusCm}cm 원(이론값 {Mathf.PI * ReferenceCircleRadiusCm * ReferenceCircleRadiusCm:F3}cm²) - {value}");
        }

        // TEMP: prints the raw fixed-point bounding box of a stroke (cm from frame centre * Fixed.Scale).
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
            Debug.Log($"[StrokeRange] raw X: {minX} ~ {maxX}, raw Y: {minY} ~ {maxY} (cm X: {minX / (float)Fixed.Scale:F2} ~ {maxX / (float)Fixed.Scale:F2}, cm Y: {minY / (float)Fixed.Scale:F2} ~ {maxY / (float)Fixed.Scale:F2})");
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
