using System.Collections.Generic;
using System.Text;
using Game.Client.Stroke;
using Game.Sim.Stroke;
using Game.Sim.Technique;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Technique
{
    // Bridges the counter UI to StrokeInputController's completed strokes,
    // recording each one into Game.sim's TechniqueBuilder turn by turn.
    public class TechniqueSequenceController : MonoBehaviour
    {
        [SerializeField] private StrokeInputController strokeInput;
        [SerializeField] private Toggle counterToggle;
        [SerializeField] private Text statusText;
        [SerializeField] private Button resetButton;

        private readonly TechniqueBuilder builder = new TechniqueBuilder();

        private bool counterResetPending;

        public TechniqueBuilder Builder => builder;

        // Live counter toggle state. Stays true for the rest of the frame in which a stroke
        // completes, so other StrokeCompleted listeners see it regardless of subscription order.
        public bool IsCounterOn => counterToggle.isOn;

        private void OnEnable()
        {
            strokeInput.StrokeCompleted += OnStrokeCompleted;
            resetButton.onClick.AddListener(ResetSequence);
            UpdateUi();
        }

        private void OnDisable()
        {
            strokeInput.StrokeCompleted -= OnStrokeCompleted;
            resetButton.onClick.RemoveListener(ResetSequence);
        }

        // Named ResetSequence rather than Reset: MonoBehaviour.Reset is an editor message.
        public void ResetSequence()
        {
            builder.Reset();
            counterResetPending = false;
            counterToggle.isOn = false;
            UpdateUi();
            Debug.Log("[Technique] 시퀀스 초기화됨");
        }

        // Replaces the current sequence with saved steps, replayed through TryAddStep so the
        // counter budget is rebuilt the same way as when they were drawn.
        public void LoadSteps(IReadOnlyList<TechniqueStep> steps)
        {
            builder.Reset();
            counterResetPending = false;
            counterToggle.isOn = false;
            foreach (TechniqueStep step in steps)
            {
                if (builder.TryAddStep(step.IsCounter, step.Stroke, step.RawPoints) != TechniqueAddResult.Success)
                {
                    Debug.LogWarning($"[Technique] 저장된 스텝 {step.TurnIndex + 1}을 복원하지 못해 이후 스텝은 무시됩니다.");
                    break;
                }
            }
            UpdateUi();
        }

        private void OnStrokeCompleted(StrokeResult stroke, IReadOnlyList<StrokePoint> rawPoints)
        {
            bool isCounter = counterToggle.isOn;

            TechniqueAddResult addResult = builder.TryAddStep(isCounter, stroke, rawPoints);
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
            // Cleared in LateUpdate so every StrokeCompleted listener this frame reads IsCounterOn intact.
            counterResetPending = true;
            UpdateUi();
        }

        private void LateUpdate()
        {
            if (!counterResetPending)
                return;

            counterResetPending = false;
            counterToggle.isOn = false;
            UpdateUi();
        }

        private void UpdateUi()
        {
            statusText.text = $"턴 {builder.TurnCount} / {TechniqueBuilder.MaxTurns}\n카운터 남음: {builder.RemainingCounters}";
            counterToggle.interactable = builder.RemainingCounters > 0 && !builder.IsFull;
        }

        private static void LogStepSummary(TechniqueStep step)
        {
            Debug.Log($"[Technique] 턴 {step.TurnIndex + 1} - Counter:{step.IsCounter} {FormatStroke(step.Stroke)}");
        }

        private void LogSequenceComplete()
        {
            var sb = new StringBuilder();
            sb.AppendLine("시퀀스 완성");
            foreach (TechniqueStep step in builder.Steps)
                sb.AppendLine($"턴 {step.TurnIndex + 1}: Counter={step.IsCounter} {FormatStroke(step.Stroke)}");

            Debug.Log(sb.ToString());
        }

        private static string FormatStroke(StrokeResult stroke)
            => stroke.Type == StrokeType.Slash
                ? $"Slash Length={stroke.Length}"
                : $"Thrust Area={stroke.Area}";
    }
}
