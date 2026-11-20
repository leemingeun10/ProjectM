using System;
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
    // Cultivation: the trained 내공 is the builder's per-sequence budget for hidden techniques
    // (TechniqueBuilder.InternalPower), never written back to the save. The stroke length cap is
    // fixed (StrokeInputController.MaxStrokeLengthCm) and does not depend on 외공.
    //
    // Hidden techniques: when a pending stroke (counter off) is recognised as an active technique's
    // shape and the remaining 내공 can pay for it, the choice panel offers its moves. While it is
    // open, saving waits for a move; the chosen move is saved with the step (TechniqueBuilder's
    // hidden TryAddStep) and previewed with its qi. Cancelling, drawing again or turning counter on
    // closes the panel and the stroke is saved as an ordinary turn. Loaded steps keep their saved
    // hidden fields as they are; nothing is recognised again.
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
        [SerializeField] private HiddenTechniqueChoicePanel hiddenChoicePanel;

        private readonly TechniqueBuilder builder = new TechniqueBuilder();
        private readonly List<Text> turnRowLabels = new List<Text>();
        private readonly List<GameObject> turnRows = new List<GameObject>();

        private bool hasPending;
        private StrokeResult pendingStroke;
        private IReadOnlyList<StrokePoint> pendingPoints;
        private IReadOnlyList<ShapePoint> pendingShape;
        // Hidden technique the choice panel is offering for the pending stroke, or null.
        private string offeredHiddenId;

        // Shape score of the pending stroke, shown in its preview so the player sees how close it was.
        private string pendingShapeSummary;
        // Outcome of the last save, shown until the next stroke.
        private string notice;

        public TechniqueBuilder Builder => builder;

        // Live counter toggle state, read by the drawing preview when a stroke completes.
        public bool IsCounterOn => counterToggle.isOn;

        // A move was picked for the pending stroke's hidden technique (id, move, points), for the preview.
        public event Action<string, HiddenTechniqueMove, IReadOnlyList<StrokePoint>> HiddenMoveChosen;

        private bool IsChoosingHidden => offeredHiddenId != null && hiddenChoicePanel.IsOpen;

        private void Awake()
        {
            turnRowTemplate.gameObject.SetActive(false);
            hiddenChoicePanel.Close();
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
            counterToggle.onValueChanged.AddListener(OnCounterToggled);
            hiddenChoicePanel.MoveChosen += OnHiddenMoveChosen;
            hiddenChoicePanel.Cancelled += OnHiddenCancelled;
            // This object stays enabled across screens, so later 단련 training arrives via Saved.
            CultivationStore.Saved += ApplyCultivation;
            ApplyCultivation(CultivationStore.Load());
        }

        private void OnDisable()
        {
            strokeInput.StrokeCompleted -= OnStrokeCompleted;
            resetButton.onClick.RemoveListener(ClearPending);
            saveTurnButton.onClick.RemoveListener(SavePendingTurn);
            counterToggle.onValueChanged.RemoveListener(OnCounterToggled);
            hiddenChoicePanel.MoveChosen -= OnHiddenMoveChosen;
            hiddenChoicePanel.Cancelled -= OnHiddenCancelled;
            CultivationStore.Saved -= ApplyCultivation;
        }

        private void ApplyCultivation(CultivationStats stats)
        {
            builder.InternalPower = stats.InternalPower;
            UpdateUi();
        }

        // Clears the whole sequence (saved turns and pending stroke), e.g. when switching slots.
        // Named ResetSequence rather than Reset: MonoBehaviour.Reset is an editor message.
        public void ResetSequence()
        {
            builder.Reset();
            DiscardPending();
            counterToggle.isOn = false;
            notice = null;
            UpdateUi();
            Debug.Log("[Technique] 시퀀스 초기화됨");
        }

        // Replaces the current sequence with saved steps, replayed through TryAddStep so the
        // counter and 내공 budgets are rebuilt the same way as when they were drawn. Hidden steps
        // keep their saved technique and move.
        public void LoadSteps(IReadOnlyList<TechniqueStep> steps)
        {
            builder.Reset();
            DiscardPending();
            counterToggle.isOn = false;
            foreach (TechniqueStep step in steps)
            {
                TechniqueAddResult result = builder.TryAddStep(step.IsCounter, step.Stroke, step.RawPoints, step.HiddenTechniqueId, step.HiddenMove);
                if (result != TechniqueAddResult.Success)
                {
                    Debug.LogWarning($"[Technique] 저장된 스텝 {step.TurnIndex + 1}을 복원하지 못해 ({result}) 이후 스텝은 무시됩니다.");
                    break;
                }
            }
            notice = null;
            UpdateUi();
        }

        // A new stroke replaces any pending one (closing a choice it had); nothing is added to the builder yet.
        private void OnStrokeCompleted(StrokeResult stroke, IReadOnlyList<StrokePoint> rawPoints)
        {
            CloseHiddenChoice();
            hasPending = true;
            pendingStroke = stroke;
            pendingPoints = rawPoints;
            pendingShape = strokeInput.LastShapePoints;
            pendingShapeSummary = DescribeShape();
            notice = null;
            OfferHiddenChoice();
            UpdateUi();
        }

        // Opens the choice panel when the pending stroke can become a hidden turn: counter off,
        // room left, an active technique's shape and enough remaining 내공.
        private void OfferHiddenChoice()
        {
            if (counterToggle.isOn || builder.IsFull)
                return;
            ShapeMatchResult? match = ShapeRecognizer.Recognize(pendingShape, ShapeTemplates.Active, ShapeRecognizer.DefaultThreshold);
            if (match == null || !HiddenTechniques.TryGetActive(match.Value.TemplateId, out HiddenTechniqueDefinition hidden))
                return;
            if (builder.RemainingInternalPower < hidden.InternalPowerCost)
            {
                notice = $"[{HiddenTechniqueNames.Of(hidden.Id)}] 내공 부족 ({builder.RemainingInternalPower}/{hidden.InternalPowerCost}) → 일반 선";
                return;
            }

            offeredHiddenId = hidden.Id;
            hiddenChoicePanel.Open(hidden.Id, hidden.InternalPowerCost, builder.RemainingInternalPower);
            Debug.Log($"[Hidden] [{HiddenTechniqueNames.Of(hidden.Id)}] 인식 (점수 {match.Value.Score}): 동작 선택 대기");
        }

        private void CloseHiddenChoice()
        {
            offeredHiddenId = null;
            hiddenChoicePanel.Close();
        }

        private void OnHiddenMoveChosen(HiddenTechniqueMove move)
        {
            if (offeredHiddenId == null || !hasPending)
                return;
            HiddenMoveChosen?.Invoke(offeredHiddenId, move, pendingPoints);
            UpdateUi();
        }

        private void OnHiddenCancelled()
        {
            offeredHiddenId = null;
            UpdateUi();
        }

        private void OnCounterToggled(bool on)
        {
            if (on && offeredHiddenId != null)
            {
                CloseHiddenChoice();
                UpdateUi();
            }
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
            CloseHiddenChoice();
            hasPending = false;
            pendingPoints = null;
            pendingShape = null;
            strokeInput.ClearDisplayedStroke();
        }

        private void SavePendingTurn()
        {
            if (!hasPending)
                return;
            // Saving waits for a move while the choice is open (the button is disabled too).
            if (IsChoosingHidden && hiddenChoicePanel.Selected == HiddenTechniqueMove.None)
                return;

            bool isCounter = counterToggle.isOn;
            TechniqueAddResult addResult;
            if (IsChoosingHidden)
            {
                addResult = builder.TryAddStep(isCounter, pendingStroke, pendingPoints, offeredHiddenId, hiddenChoicePanel.Selected);
                if (IsHiddenRefusal(addResult))
                {
                    // Save the shape as an ordinary turn instead and say why.
                    Debug.Log($"[Hidden] [{HiddenTechniqueNames.Of(offeredHiddenId)}] 저장 거부 ({addResult}) → 일반 턴으로 저장");
                    notice = $"[{HiddenTechniqueNames.Of(offeredHiddenId)}] {DescribeRefusal(addResult)} → 일반 선으로 저장";
                    addResult = builder.TryAddStep(isCounter, pendingStroke, pendingPoints);
                }
            }
            else
            {
                addResult = builder.TryAddStep(isCounter, pendingStroke, pendingPoints);
            }

            switch (addResult)
            {
                case TechniqueAddResult.Success:
                    TechniqueStep saved = builder.Steps[builder.TurnCount - 1];
                    if (saved.IsHidden)
                        notice = $"턴 {saved.TurnIndex + 1}: [{HiddenTechniqueNames.OfStep(saved)}] 저장 · 남은 내공 {builder.RemainingInternalPower}";
                    LogStepSummary(saved);
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

        private static bool IsHiddenRefusal(TechniqueAddResult result)
            => result == TechniqueAddResult.NotEnoughInternalPower
               || result == TechniqueAddResult.UnknownHiddenTechnique
               || result == TechniqueAddResult.HiddenTechniqueMoveMismatch
               || result == TechniqueAddResult.HiddenTechniqueOnCounter;

        private static string DescribeRefusal(TechniqueAddResult result)
            => result == TechniqueAddResult.NotEnoughInternalPower ? "내공 부족" : "사용할 수 없음";

        private void RemoveTurn(int turnIndex)
        {
            if (!builder.RemoveAt(turnIndex))
                return;
            Debug.Log($"[Technique] 턴 {turnIndex + 1} 삭제");
            UpdateUi();
        }

        // The active techniques' best score for the pending shape (ShapeRecognizer.DefaultThreshold
        // passes), e.g. "삼재검법 9420", or "무공 모양 아님 (가장 가까움: 삼재 8700)".
        private string DescribeShape()
        {
            var scores = new StringBuilder();
            string passed = null, closest = null;
            int passedScore = -1, closestScore = -1;
            var seen = new HashSet<string>();
            foreach (ShapeTemplate template in ShapeTemplates.Active)
            {
                if (!seen.Add(template.Id))
                    continue;
                var variants = new List<ShapeTemplate>();
                foreach (ShapeTemplate t in ShapeTemplates.Active)
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
            Debug.Log($"[Hidden] 모양 점수: {scores} (기준 {ShapeRecognizer.DefaultThreshold}), 길이 {strokeInput.LastStrokeLengthCm:F1}/{StrokeInputController.MaxStrokeLengthCm:0.##}cm{cut}");
            return (passed != null ? $"{passed} {passedScore}" : $"무공 모양 아님 (가장 가까움: {closest.Substring(0, 2)} {closestScore})") + cut;
        }

        private void UpdateUi()
        {
            statusText.text = $"턴 {builder.TurnCount} / {TechniqueBuilder.MaxTurns}\n카운터 남음: {builder.RemainingCounters}";
            // Leave an already-on toggle switchable off even when no counters remain.
            counterToggle.interactable = counterToggle.isOn || (builder.RemainingCounters > 0 && !builder.IsFull);
            resetButton.interactable = hasPending;
            bool waitingForMove = IsChoosingHidden && hiddenChoicePanel.Selected == HiddenTechniqueMove.None;
            saveTurnButton.interactable = hasPending && !builder.IsFull && !waitingForMove;

            string chosen = IsChoosingHidden && hiddenChoicePanel.Selected != HiddenTechniqueMove.None
                ? $" · {HiddenTechniqueNames.Of(offeredHiddenId)}·{HiddenTechniqueNames.MoveName(hiddenChoicePanel.Selected)}"
                : waitingForMove ? " · 동작을 고르세요" : "";
            // The status box only fits two lines, so the cultivation numbers ride on this bar.
            pendingText.text = !hasPending
                ? (notice ?? "틀 안에 선을 그리세요") + $"  ·  한도 {StrokeInputController.MaxStrokeLengthCm:0.##}cm · 내공 {builder.RemainingInternalPower}"
                : builder.IsFull ? $"{FormatStroke(pendingStroke)} · {pendingShapeSummary} (10턴 가득 참)"
                : $"{(notice != null ? notice + " · " : "")}{FormatStroke(pendingStroke)} · {pendingShapeSummary}{chosen}";

            for (int i = 0; i < turnRows.Count; i++)
            {
                bool used = i < builder.TurnCount;
                turnRows[i].SetActive(used);
                if (used)
                    turnRowLabels[i].text = FormatTurn(builder.Steps[i]);
            }
        }

        // A hidden turn shows only its technique and move: the stroke behind it no longer decides
        // how it fights, and long labels would run under the row's delete button.
        private static string FormatTurn(TechniqueStep step)
            => step.IsHidden
                ? $"턴 {step.TurnIndex + 1}  {HiddenTechniqueNames.OfStep(step)}"
                : $"턴 {step.TurnIndex + 1}  {(step.IsCounter ? "Counter · " : "")}{FormatStroke(step.Stroke)}";

        private static void LogStepSummary(TechniqueStep step)
        {
            Debug.Log($"[Technique] 턴 {step.TurnIndex + 1} - Counter:{step.IsCounter} {FormatStroke(step.Stroke)}{(step.IsHidden ? " " + HiddenTechniqueNames.OfStep(step) : "")}");
        }

        private void LogSequenceComplete()
        {
            var sb = new StringBuilder();
            sb.AppendLine("시퀀스 완성");
            foreach (TechniqueStep step in builder.Steps)
                sb.AppendLine($"턴 {step.TurnIndex + 1}: Counter={step.IsCounter} {FormatStroke(step.Stroke)}{(step.IsHidden ? " " + HiddenTechniqueNames.OfStep(step) : "")}");

            Debug.Log(sb.ToString());
        }

        private static string FormatStroke(StrokeResult stroke)
            => stroke.Type == StrokeType.Slash
                ? $"Slash {stroke.Length}cm"
                : $"Thrust {stroke.Area}cm²";
    }
}
