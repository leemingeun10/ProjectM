using System.Collections.Generic;
using System.Text;
using Game.Client.Cultivation;
using Game.Client.Stroke;
using Game.Sim.Cultivation;
using Game.Sim.Shape;
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
    //
    // Cultivation: 외공 sets the stroke length cap. When a turn is saved, its drawn shape is matched
    // against the hidden-technique templates; a match is activated if the sequence still has the
    // 내공 for it. 내공 here is a per-sequence budget (the trained InternalPower, minus what this
    // sequence's activated turns cost; deleting such a turn refunds it), never written back to the
    // save. Activation is only logged and tagged on the turn row for now: it has no battle effect yet.
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
        private IReadOnlyList<ShapePoint> pendingShape;

        private CultivationStats cultivation;
        // Hidden technique template id activated by each saved turn (null = ordinary turn),
        // kept index-aligned with builder.Steps.
        private readonly List<string> hiddenByTurn = new List<string>();
        // Shape scores of the pending stroke, shown in its preview so the player sees how close it was.
        private string pendingShapeSummary;
        // Outcome of the last save (hidden technique activated or not), shown until the next stroke.
        private string notice;

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
            // This object stays enabled across screens, so later 단련 training arrives via Saved.
            CultivationStore.Saved += ApplyCultivation;
            ApplyCultivation(CultivationStore.Load());
        }

        private void OnDisable()
        {
            strokeInput.StrokeCompleted -= OnStrokeCompleted;
            resetButton.onClick.RemoveListener(ClearPending);
            saveTurnButton.onClick.RemoveListener(SavePendingTurn);
            CultivationStore.Saved -= ApplyCultivation;
        }

        private void ApplyCultivation(CultivationStats stats)
        {
            cultivation = stats;
            strokeInput.MaxStrokeLengthCm = CultivationBattleRules.MaxStrokeLength(stats.ExternalPower);
            UpdateUi();
        }

        // Clears the whole sequence (saved turns and pending stroke), e.g. when switching slots.
        // Named ResetSequence rather than Reset: MonoBehaviour.Reset is an editor message.
        public void ResetSequence()
        {
            builder.Reset();
            hiddenByTurn.Clear();
            DiscardPending();
            counterToggle.isOn = false;
            UpdateUi();
            Debug.Log("[Technique] 시퀀스 초기화됨");
        }

        // Replaces the current sequence with saved steps, replayed through TryAddStep so the
        // counter budget is rebuilt the same way as when they were drawn. Hidden techniques are not
        // saved, so each loaded turn's stored points are recognised again, spending 내공 in order.
        public void LoadSteps(IReadOnlyList<TechniqueStep> steps)
        {
            builder.Reset();
            hiddenByTurn.Clear();
            DiscardPending();
            counterToggle.isOn = false;
            foreach (TechniqueStep step in steps)
            {
                if (builder.TryAddStep(step.IsCounter, step.Stroke, step.RawPoints) != TechniqueAddResult.Success)
                {
                    Debug.LogWarning($"[Technique] 저장된 스텝 {step.TurnIndex + 1}을 복원하지 못해 이후 스텝은 무시됩니다.");
                    break;
                }
                pendingShape = ToShapePoints(step.RawPoints);
                hiddenByTurn.Add(TryActivateHidden(step.TurnIndex + 1));
            }
            pendingShape = null;
            notice = null;
            UpdateUi();
        }

        // A new stroke replaces any pending one; nothing is added to the builder yet.
        private void OnStrokeCompleted(StrokeResult stroke, IReadOnlyList<StrokePoint> rawPoints)
        {
            hasPending = true;
            pendingStroke = stroke;
            pendingPoints = rawPoints;
            pendingShape = strokeInput.LastShapePoints;
            pendingShapeSummary = DescribeShape();
            notice = null;
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
            pendingShape = null;
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
                    hiddenByTurn.Add(TryActivateHidden(builder.TurnCount));
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
            hiddenByTurn.RemoveAt(turnIndex);
            Debug.Log($"[Technique] 턴 {turnIndex + 1} 삭제");
            UpdateUi();
        }

        // Template id of the hidden technique the pending shape activates as turn `turnNumber`, or
        // null when it matches nothing or the sequence lacks the 내공 (then it is an ordinary turn).
        private string TryActivateHidden(int turnNumber)
        {
            ShapeMatchResult? match = ShapeRecognizer.Recognize(pendingShape, ShapeTemplates.All, ShapeRecognizer.DefaultThreshold);
            if (match == null)
            {
                notice = $"턴 {turnNumber}: 일반 공격으로 저장 (무공 모양 아님)";
                return null;
            }

            string name = HiddenTechniqueNames.Of(match.Value.TemplateId);
            int before = RemainingInternalPower();
            (bool activated, int remaining) = CultivationBattleRules.TryActivateHiddenTechnique(
                before, match.Value, CultivationBattleRules.HiddenTechniqueCosts);
            if (!activated)
            {
                Debug.Log($"[Hidden] 턴 {turnNumber}: [{name}] 모양이지만 내공이 부족해 일반 공격으로 처리합니다 (점수 {match.Value.Score}, 내공 {before}).");
                notice = $"턴 {turnNumber}: [{name}] 내공 부족 ({before}/{CultivationBattleRules.HiddenTechniqueCosts[match.Value.TemplateId]}) → 일반 공격";
                return null;
            }

            Debug.Log($"[Hidden] 턴 {turnNumber}: [{name}] 인식됨! (점수 {match.Value.Score}, 내공 {before} → {remaining})");
            notice = $"턴 {turnNumber}: [{name}] 인식됨!  내공 {before} → {remaining}";
            return match.Value.TemplateId;
        }

        // Each hidden technique's score for the pending shape (the best of its template variants;
        // ShapeRecognizer.DefaultThreshold activates), e.g. "삼재검법 9420" when one passes or
        // "무공 모양 아님 (가장 가까움: 매화 8700)". The console log lists every score.
        private string DescribeShape()
        {
            var scores = new StringBuilder();
            string passed = null, closest = null;
            int passedScore = -1, closestScore = -1;
            var seen = new HashSet<string>();
            foreach (ShapeTemplate template in ShapeTemplates.All)
            {
                if (!seen.Add(template.Id))
                    continue;
                var variants = new List<ShapeTemplate>();
                foreach (ShapeTemplate t in ShapeTemplates.All)
                {
                    if (t.Id == template.Id)
                        variants.Add(t);
                }

                int score = ShapeRecognizer.Recognize(pendingShape, variants, 0)?.Score ?? 0;
                string name = HiddenTechniqueNames.Of(template.Id);
                if (scores.Length > 0)
                    scores.Append(" · ");
                scores.Append($"{name.Substring(0, 2)} {score}");
                if (score > closestScore)
                {
                    closest = name;
                    closestScore = score;
                }
                if (score >= ShapeRecognizer.DefaultThreshold && score > passedScore)
                {
                    passed = name;
                    passedScore = score;
                }
            }

            string cut = strokeInput.LastStrokeHitLengthLimit ? " · 한도에서 끊김" : "";
            Debug.Log($"[Hidden] 모양 점수: {scores} (기준 {ShapeRecognizer.DefaultThreshold}), 길이 {strokeInput.LastStrokeLengthCm:F1}/{strokeInput.MaxStrokeLengthCm}cm{cut}");
            return (passed != null ? $"{passed} {passedScore}" : $"무공 모양 아님 (가장 가까움: {closest.Substring(0, 2)} {closestScore})") + cut;
        }

        // Saved points are the 24-point resampled stroke (frame cm as Fixed raw = cm * 1000), the
        // same scale as StrokeInputController.LastShapePoints.
        private static ShapePoint[] ToShapePoints(IReadOnlyList<StrokePoint> points)
        {
            var shape = new ShapePoint[points.Count];
            for (int i = 0; i < points.Count; i++)
                shape[i] = new ShapePoint((int)points[i].X.Raw, (int)points[i].Y.Raw);
            return shape;
        }

        // The trained 내공 minus what this sequence's activated turns already cost.
        private int RemainingInternalPower()
        {
            int remaining = cultivation.InternalPower;
            foreach (string templateId in hiddenByTurn)
            {
                if (templateId != null)
                    remaining -= CultivationBattleRules.HiddenTechniqueCosts[templateId];
            }
            return remaining;
        }

        private void UpdateUi()
        {
            statusText.text = $"턴 {builder.TurnCount} / {TechniqueBuilder.MaxTurns}\n카운터 남음: {builder.RemainingCounters}";
            // Leave an already-on toggle switchable off even when no counters remain.
            counterToggle.interactable = counterToggle.isOn || (builder.RemainingCounters > 0 && !builder.IsFull);
            resetButton.interactable = hasPending;
            saveTurnButton.interactable = hasPending && !builder.IsFull;

            // The status box only fits two lines, so the cultivation numbers ride on this bar.
            pendingText.text = !hasPending
                ? (notice ?? "틀 안에 선을 그리세요") + $"  ·  한도 {strokeInput.MaxStrokeLengthCm}cm · 내공 {RemainingInternalPower()}"
                : builder.IsFull ? $"{FormatStroke(pendingStroke)} · {pendingShapeSummary} (10턴 가득 참)"
                : $"{FormatStroke(pendingStroke)} · {pendingShapeSummary}";

            for (int i = 0; i < turnRows.Count; i++)
            {
                bool used = i < builder.TurnCount;
                turnRows[i].SetActive(used);
                if (used)
                    turnRowLabels[i].text = FormatTurn(builder.Steps[i], hiddenByTurn[i]);
            }
        }

        private static string FormatTurn(TechniqueStep step, string hiddenTemplateId)
            // The hidden technique goes first: long labels run under the row's delete button.
            => $"턴 {step.TurnIndex + 1}  "
               + (hiddenTemplateId != null ? $"[{HiddenTechniqueNames.Of(hiddenTemplateId)}] " : "")
               + $"{(step.IsCounter ? "Counter · " : "")}{FormatStroke(step.Stroke)}";

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
