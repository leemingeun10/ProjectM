using System.Collections.Generic;
using System.Text;
using Game.Client.Stroke;
using Game.Sim.Stroke;
using Game.Sim.Technique;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Technique
{
    // Bridges the counter UI to StrokeInputController's completed strokes and Game.sim's
    // TechniqueBuilder. A finished stroke is only a pending preview; it becomes a turn when the
    // player presses the save-turn button, using the counter toggle state at that moment.
    // Saved turns are listed with a delete button each.
    public class TechniqueSequenceController : MonoBehaviour
    {
        [SerializeField] private StrokeInputController strokeInput;
        [SerializeField] private Toggle counterToggle;
        [SerializeField] private Text statusText;
        // Discards the pending stroke only; saved turns are kept.
        [SerializeField] private Button resetButton;
        [SerializeField] private Button saveTurnButton;
        [SerializeField] private Text pendingText;
        [SerializeField] private RectTransform turnListContainer;
        // Inactive template cloned once per possible turn; needs a "Label" Text child and a Button child.
        [SerializeField] private RectTransform turnRowTemplate;

        private readonly TechniqueBuilder builder = new TechniqueBuilder();
        private readonly List<Text> turnRowLabels = new List<Text>();
        private readonly List<GameObject> turnRows = new List<GameObject>();

        private bool hasPending;
        private StrokeResult pendingStroke;
        private IReadOnlyList<StrokePoint> pendingPoints;

        public TechniqueBuilder Builder => builder;

        // Live counter toggle state, read by the drawing preview when a stroke completes.
        public bool IsCounterOn => counterToggle.isOn;

        private void Awake()
        {
            turnRowTemplate.gameObject.SetActive(false);
            for (int i = 0; i < TechniqueBuilder.MaxTurns; i++)
            {
                RectTransform row = Instantiate(turnRowTemplate, turnListContainer);
                row.name = $"TurnRow{i + 1}";
                int turnIndex = i;
                row.GetComponentInChildren<Button>(true).onClick.AddListener(() => RemoveTurn(turnIndex));
                turnRowLabels.Add(row.Find("Label").GetComponent<Text>());
                turnRows.Add(row.gameObject);
            }
        }

        private void OnEnable()
        {
            strokeInput.StrokeCompleted += OnStrokeCompleted;
            resetButton.onClick.AddListener(ClearPending);
            saveTurnButton.onClick.AddListener(SavePendingTurn);
            UpdateUi();
        }

        private void OnDisable()
        {
            strokeInput.StrokeCompleted -= OnStrokeCompleted;
            resetButton.onClick.RemoveListener(ClearPending);
            saveTurnButton.onClick.RemoveListener(SavePendingTurn);
        }

        // Clears the whole sequence (saved turns and pending stroke), e.g. when switching slots.
        // Named ResetSequence rather than Reset: MonoBehaviour.Reset is an editor message.
        public void ResetSequence()
        {
            builder.Reset();
            DiscardPending();
            counterToggle.isOn = false;
            UpdateUi();
            Debug.Log("[Technique] 시퀀스 초기화됨");
        }

        // Replaces the current sequence with saved steps, replayed through TryAddStep so the
        // counter budget is rebuilt the same way as when they were drawn.
        public void LoadSteps(IReadOnlyList<TechniqueStep> steps)
        {
            builder.Reset();
            DiscardPending();
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

        // A new stroke replaces any pending one; nothing is added to the builder yet.
        private void OnStrokeCompleted(StrokeResult stroke, IReadOnlyList<StrokePoint> rawPoints)
        {
            hasPending = true;
            pendingStroke = stroke;
            pendingPoints = rawPoints;
            UpdateUi();
        }

        private void ClearPending()
        {
            if (!hasPending)
                return;
            DiscardPending();
            UpdateUi();
            Debug.Log("[Technique] 저장 안 한 스트로크를 지웠습니다.");
        }

        private void DiscardPending()
        {
            hasPending = false;
            pendingPoints = null;
            strokeInput.ClearDisplayedStroke();
        }

        private void SavePendingTurn()
        {
            if (!hasPending)
                return;

            bool isCounter = counterToggle.isOn;
            TechniqueAddResult addResult = builder.TryAddStep(isCounter, pendingStroke, pendingPoints);
            switch (addResult)
            {
                case TechniqueAddResult.Success:
                    LogStepSummary(builder.Steps[builder.TurnCount - 1]);
                    if (builder.IsFull)
                        LogSequenceComplete();
                    DiscardPending();
                    // Counter mode applies to a single turn; the player re-enables it per turn.
                    counterToggle.isOn = false;
                    break;
                case TechniqueAddResult.SequenceFull:
                    Debug.LogWarning("[Technique] 시퀀스가 이미 10턴을 채웠습니다. 턴을 삭제한 뒤 저장하세요.");
                    break;
                case TechniqueAddResult.CounterLimitReached:
                    Debug.LogWarning("[Technique] 카운터 횟수를 모두 사용했습니다. 카운터를 끄거나 카운터 턴을 삭제하세요.");
                    break;
            }
            UpdateUi();
        }

        private void RemoveTurn(int turnIndex)
        {
            if (!builder.RemoveAt(turnIndex))
                return;
            Debug.Log($"[Technique] 턴 {turnIndex + 1} 삭제");
            UpdateUi();
        }

        private void UpdateUi()
        {
            statusText.text = $"턴 {builder.TurnCount} / {TechniqueBuilder.MaxTurns}\n카운터 남음: {builder.RemainingCounters}";
            // Leave an already-on toggle switchable off even when no counters remain.
            counterToggle.interactable = counterToggle.isOn || (builder.RemainingCounters > 0 && !builder.IsFull);
            resetButton.interactable = hasPending;
            saveTurnButton.interactable = hasPending && !builder.IsFull;

            pendingText.text = !hasPending ? "틀 안에 선을 그리세요"
                : builder.IsFull ? $"미리보기: {FormatStroke(pendingStroke)} (10턴 가득 참)"
                : $"미리보기: {FormatStroke(pendingStroke)} (저장 안 됨)";

            for (int i = 0; i < turnRows.Count; i++)
            {
                bool used = i < builder.TurnCount;
                turnRows[i].SetActive(used);
                if (used)
                    turnRowLabels[i].text = FormatTurn(builder.Steps[i]);
            }
        }

        private static string FormatTurn(TechniqueStep step)
            => $"턴 {step.TurnIndex + 1}  {(step.IsCounter ? "Counter · " : "")}{FormatStroke(step.Stroke)}";

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
                ? $"Slash {stroke.Length}cm"
                : $"Thrust {stroke.Area}cm²";
    }
}
