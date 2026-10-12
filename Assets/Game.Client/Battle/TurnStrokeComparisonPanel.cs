using System.Collections.Generic;
using Game.Client.Stroke;
using Game.Client.Ui;
using Game.Sim.Battle;
using Game.Sim.Stroke;
using Game.Sim.Technique;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Battle
{
    // Replay HUD showing the two strokes that met this turn side by side: the player's ("나의 검로")
    // and the opponent's ("상대 검로"), each fitted to its own small preview. Driven by
    // BattleReplayController; it only displays recorded TechniqueStep points.
    public class TurnStrokeComparisonPanel : MonoBehaviour
    {
        // Enough for smooth curves and a smooth reveal; strokes are recorded with 24-25 points.
        private const int DisplayPointCount = 48;

        [SerializeField] private UIInkPolyline strokeA;
        [SerializeField] private UIInkPolyline strokeB;
        [SerializeField] private Text typeTextA;
        [SerializeField] private Text typeTextB;

        public void Show(TechniqueStep? stepA, TechniqueStep? stepB, BattleStepType typeA, BattleStepType typeB)
        {
            gameObject.SetActive(true);
            Fill(strokeA, typeTextA, stepA, typeA);
            Fill(strokeB, typeTextB, stepB, typeB);
        }

        // 0..1: how much of both strokes is drawn so far.
        public void SetReveal(float reveal)
        {
            strokeA.Reveal = reveal;
            strokeB.Reveal = reveal;
        }

        public void Clear()
        {
            strokeA.SetPoints(null);
            strokeB.SetPoints(null);
            typeTextA.text = string.Empty;
            typeTextB.text = string.Empty;
            gameObject.SetActive(false);
        }

        private static void Fill(UIInkPolyline line, Text typeText, TechniqueStep? step, BattleStepType type)
        {
            IReadOnlyList<StrokePoint> raw = step?.RawPoints;
            if (raw == null || raw.Count == 0)
            {
                line.SetPoints(null);
                typeText.text = $"{TurnNarrator.DescribeStepType(type)}\n<size=22>궤적 기록 없음</size>";
                return;
            }

            line.SetPoints(StrokeResampler.Resample(StrokeFitter.FitToUnitSquare(raw), DisplayPointCount));
            // A counter keeps its drawn shape; say what that shape was so the preview makes sense.
            typeText.text = type == BattleStepType.Counter
                ? $"{TurnNarrator.DescribeStepType(type)}\n<size=22>{ShapeName(step.Value)} 궤적</size>"
                : TurnNarrator.DescribeStepType(type);
        }

        private static string ShapeName(TechniqueStep step)
            => step.Stroke.Type == StrokeType.Thrust ? "찌르기" : "베기";
    }
}
