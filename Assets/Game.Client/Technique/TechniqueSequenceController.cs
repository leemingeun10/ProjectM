using System.Collections.Generic;
using System.Text;
using Game.Client.Stroke;
using Game.Sim.Stroke;
using Game.Sim.Technique;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Technique
{
    // Bridges the stance/counter UI to StrokeInputController's completed strokes,
    // recording each one into Game.sim's TechniqueBuilder turn by turn.
    public class TechniqueSequenceController : MonoBehaviour
    {
        [SerializeField] private StrokeInputController strokeInput;
        [SerializeField] private Toggle standToggle;
        [SerializeField] private Toggle sitToggle;
        [SerializeField] private Toggle advanceToggle;
        [SerializeField] private Toggle retreatToggle;
        [SerializeField] private Toggle counterToggle;
        [SerializeField] private Text statusText;

        private readonly TechniqueBuilder builder = new TechniqueBuilder();

        public TechniqueBuilder Builder => builder;

        private void OnEnable()
        {
            strokeInput.StrokeCompleted += OnStrokeCompleted;
            UpdateUi();
        }

        private void OnDisable()
        {
            strokeInput.StrokeCompleted -= OnStrokeCompleted;
        }

        private void OnStrokeCompleted(StrokeResult stroke, IReadOnlyList<StrokePoint> rawPoints)
        {
            Stance stance = GetSelectedStance();
            bool isCounter = counterToggle.isOn;

            TechniqueAddResult addResult = builder.TryAddStep(stance, isCounter, stroke, rawPoints);
            switch (addResult)
            {
                case TechniqueAddResult.Success:
                    LogStepSummary(builder.Steps[builder.TurnCount - 1]);
                    if (builder.IsFull)
                        LogSequenceComplete();
                    break;
                case TechniqueAddResult.SequenceFull:
                    Debug.LogWarning("[Technique] 시퀀스가 이미 10턴을 채웠습니다. 이번 스트로크는 무시됩니다.");
                    break;
                case TechniqueAddResult.CounterLimitReached:
                    Debug.LogWarning("[Technique] 카운터 횟수를 모두 사용했습니다. 이번 스트로크는 무시됩니다.");
                    break;
            }

            // Counter mode applies to a single stroke; the player re-enables it per turn.
            counterToggle.isOn = false;
            UpdateUi();
        }

        private Stance GetSelectedStance()
        {
            if (sitToggle.isOn) return Stance.Sit;
            if (advanceToggle.isOn) return Stance.Advance;
            if (retreatToggle.isOn) return Stance.Retreat;
            return Stance.Stand;
        }

        private void UpdateUi()
        {
            statusText.text = $"턴 {builder.TurnCount} / {TechniqueBuilder.MaxTurns}\n카운터 남음: {builder.RemainingCounters}";
            counterToggle.interactable = builder.RemainingCounters > 0 && !builder.IsFull;
        }

        private static void LogStepSummary(TechniqueStep step)
        {
            Debug.Log($"[Technique] 턴 {step.TurnIndex + 1} - Stance:{step.Stance} Counter:{step.IsCounter} {FormatStroke(step.Stroke)}");
        }

        private void LogSequenceComplete()
        {
            var sb = new StringBuilder();
            sb.AppendLine("시퀀스 완성");
            foreach (TechniqueStep step in builder.Steps)
                sb.AppendLine($"턴 {step.TurnIndex + 1}: Stance={step.Stance} Counter={step.IsCounter} {FormatStroke(step.Stroke)}");

            Debug.Log(sb.ToString());
        }

        private static string FormatStroke(StrokeResult stroke)
            => stroke.Type == StrokeType.Slash
                ? $"Slash Length={stroke.Length}"
                : $"Thrust Area={stroke.Area}";
    }
}
