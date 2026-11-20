using System;
using System.Collections;
using System.Collections.Generic;
using Game.Client.Flow;
using Game.Sim.Battle;
using Game.Sim.Technique;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Battle
{
    // Plays back an already-computed BattleResult turn by turn. Presentation only: the result
    // is never modified, and all float/timing work stays on this side of the Game.sim boundary.
    // Each turn also starts characters A and B on the motion CombatClipSelector picks; the clip
    // speed follows the replay speed and freezes while paused.
    // Lives on the battle screen: when the replay ends a result panel offers "다시 보기" and
    // "홈으로", and leaving the battle screen by any route closes the replay.
    // Each turn also shows both sides' recorded strokes for that turn (drawn in step with the
    // swing) and a one-line explanation of how the turn resolved (TurnNarrator).
    // A turn takes ReplayTurnTiming.Normal (1.6 s, impact at 0.48 s; 2x halves it like everything
    // else) unless a side has a TechniqueCast for it: then that side plays the technique clip and the
    // turn stretches to fit it, with the impact on the clip's main strike. Only the presentation changes.
    public class BattleReplayController : MonoBehaviour
    {
        private const float HpLerpDuration = 0.3f;
        private const float FastSpeed = 2f;
        private const int OutcomeFontSize = 130;
        // The "/max" after each side's HP is written smaller so the line still fits beside the bar.
        private const int MaxHpFontSize = 24;

        // Optional overrides for a replay that is part of something else (the story map). Every
        // field left null keeps the normal 수련/비무 behaviour: the outcome word, and "홈으로" on the
        // result panel and the close button.
        public class ReplayExit
        {
            // Replaces the outcome word on the result panel (e.g. "기절").
            public string OutcomeLabel;
            // Label and action of the result panel's exit button.
            public string ContinueLabel;
            public Action OnContinue;
            // Label and action of the close button in the control row.
            public string CloseLabel;
            public Action OnClose;
        }

        [SerializeField] private GameObject replayPanel;
        [SerializeField] private Image hpFillA;
        [SerializeField] private Image hpFillB;
        [SerializeField] private Text hpTextA;
        [SerializeField] private Text hpTextB;
        [SerializeField] private Text turnText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Text resultText;
        [SerializeField] private Button playPauseButton;
        [SerializeField] private Text playPauseLabel;
        [SerializeField] private Button speedButton;
        [SerializeField] private Text speedLabel;
        [SerializeField] private Button restartButton;
        // Leaves mid-replay; goes home like the result panel's home button.
        [SerializeField] private Button closeButton;
        [SerializeField] private GameFlowController flow;
        // Shown once the replay ends; holds resultText and the two follow-up choices.
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private Button replayAgainButton;
        [SerializeField] private Button resultHomeButton;
        [SerializeField] private CharacterMotionPlayer characterA;
        [SerializeField] private CharacterMotionPlayer characterB;
        [SerializeField] private BattleCameraRig cameraRig;
        [SerializeField] private WorldHpBar worldHpA;
        [SerializeField] private WorldHpBar worldHpB;
        [SerializeField] private ImpactFlash impactFlash;
        [SerializeField] private TurnStrokeComparisonPanel strokeComparison;

        private BattleResult currentResult;
        private IReadOnlyList<TechniqueStep> currentStepsA;
        private IReadOnlyList<TechniqueStep> currentStepsB;
        private IReadOnlyList<TechniqueCast> currentCastsA;
        private IReadOnlyList<TechniqueCast> currentCastsB;
        private string currentResultNote;
        private Action pendingOnFinished;
        private ReplayExit currentExit;
        private Text closeLabel;
        private Text resultExitLabel;
        private string defaultCloseLabel;
        private string defaultResultExitLabel;
        private Coroutine replayRoutine;
        private bool isPaused;
        private bool isFinished;
        private float speed = 1f;

        private void Awake()
        {
            replayPanel.SetActive(false);
            resultPanel.SetActive(false);
            SetWorldHpVisible(false);
            closeLabel = closeButton.GetComponentInChildren<Text>(true);
            resultExitLabel = resultHomeButton.GetComponentInChildren<Text>(true);
            defaultCloseLabel = closeLabel != null ? closeLabel.text : null;
            defaultResultExitLabel = resultExitLabel != null ? resultExitLabel.text : null;
            // "나 210/210" is wider than the old "나  100" and has no good place to wrap.
            hpTextA.horizontalOverflow = HorizontalWrapMode.Overflow;
            hpTextB.horizontalOverflow = HorizontalWrapMode.Overflow;

            // Same fail-once policy as StrokeAnimationPlayer: report unwired characters up front.
            var missing = new List<string>();
            if (characterA == null) missing.Add(nameof(characterA));
            if (characterB == null) missing.Add(nameof(characterB));
            if (cameraRig == null) missing.Add(nameof(cameraRig));
            if (worldHpA == null) missing.Add(nameof(worldHpA));
            if (worldHpB == null) missing.Add(nameof(worldHpB));
            if (impactFlash == null) missing.Add(nameof(impactFlash));
            if (strokeComparison == null) missing.Add(nameof(strokeComparison));
            if (missing.Count == 0)
                return;

            Debug.LogError($"[BattleReplayController] '{name}' has unassigned field(s): {string.Join(", ", missing)}. Component disabled.", this);
            enabled = false;
        }

        private void OnEnable()
        {
            playPauseButton.onClick.AddListener(TogglePause);
            speedButton.onClick.AddListener(ToggleSpeed);
            restartButton.onClick.AddListener(Restart);
            closeButton.onClick.AddListener(OnCloseClicked);
            replayAgainButton.onClick.AddListener(Restart);
            resultHomeButton.onClick.AddListener(OnResultExitClicked);
            flow.ScreenExited += OnScreenExited;
        }

        private void OnDisable()
        {
            playPauseButton.onClick.RemoveListener(TogglePause);
            speedButton.onClick.RemoveListener(ToggleSpeed);
            restartButton.onClick.RemoveListener(Restart);
            closeButton.onClick.RemoveListener(OnCloseClicked);
            replayAgainButton.onClick.RemoveListener(Restart);
            resultHomeButton.onClick.RemoveListener(OnResultExitClicked);
            flow.ScreenExited -= OnScreenExited;
        }

        private void GoHome() => flow.GoTo(GameScreen.Home);

        private void OnCloseClicked()
        {
            if (currentExit?.OnClose != null)
                currentExit.OnClose();
            else
                GoHome();
        }

        private void OnResultExitClicked()
        {
            if (currentExit?.OnContinue != null)
                currentExit.OnContinue();
            else
                GoHome();
        }

        private void OnScreenExited(GameScreen screen)
        {
            if (screen == GameScreen.Battle)
                Close();
        }

        // Replaces any replay in progress with the new result and plays it from the start.
        // stepsA/stepsB are the two sequences the result was simulated from (A = player), used
        // only to show each turn's strokes.
        // resultNote, if given, is shown under the outcome when the replay ends.
        // onFinished runs exactly once: when the replay first reaches its end, or earlier if the
        // replay is closed or replaced first, so skipping a replay cannot skip its consequences.
        // castsA/castsB, if given, mark the turns each side performs a technique on (see TechniqueCast).
        // exit, if given, overrides the outcome word and where the exit buttons go (see ReplayExit).
        public void StartReplay(BattleResult result, IReadOnlyList<TechniqueStep> stepsA, IReadOnlyList<TechniqueStep> stepsB,
            string resultNote = null, Action onFinished = null,
            IReadOnlyList<TechniqueCast> castsA = null, IReadOnlyList<TechniqueCast> castsB = null,
            ReplayExit exit = null)
        {
            FireFinished();
            currentExit = exit;
            if (closeLabel != null)
                closeLabel.text = exit?.CloseLabel ?? defaultCloseLabel;
            if (resultExitLabel != null)
                resultExitLabel.text = exit?.ContinueLabel ?? defaultResultExitLabel;
            currentResult = result;
            currentStepsA = stepsA;
            currentStepsB = stepsB;
            currentCastsA = castsA;
            currentCastsB = castsB;
            currentResultNote = resultNote;
            pendingOnFinished = onFinished;
            replayPanel.SetActive(true);
            SetWorldHpVisible(true);
            Restart();
        }

        private void Restart()
        {
            if (currentResult == null)
                return;

            StopReplay();
            isPaused = false;
            isFinished = false;
            characterA.StopToIdle();
            characterB.StopToIdle();
            cameraRig.SetFocused(false);
            SetHp(currentResult.MaxHpA, currentResult.MaxHpB);
            turnText.text = $"턴 0 / {BattleSimulator.TurnsPerBattle}";
            descriptionText.text = string.Empty;
            strokeComparison.Clear();
            resultPanel.SetActive(false);
            UpdateControlLabels();
            replayRoutine = StartCoroutine(PlayTurns(currentResult));
        }

        private void TogglePause()
        {
            if (currentResult == null)
                return;

            // Once the replay has ended, "play" starts it over.
            if (isFinished)
            {
                Restart();
                return;
            }

            isPaused = !isPaused;
            UpdateControlLabels();
        }

        private void ToggleSpeed()
        {
            speed = speed > 1f ? 1f : FastSpeed;
            UpdateControlLabels();
        }

        private void Close()
        {
            FireFinished();
            StopReplay();
            characterA.StopToIdle();
            characterB.StopToIdle();
            SetMotionSpeed(1f);
            cameraRig.SetFocused(false);
            strokeComparison.Clear();
            replayPanel.SetActive(false);
            resultPanel.SetActive(false);
            SetWorldHpVisible(false);
        }

        private void StopReplay()
        {
            if (replayRoutine != null)
            {
                StopCoroutine(replayRoutine);
                replayRoutine = null;
            }
        }

        private IEnumerator PlayTurns(BattleResult result)
        {
            float shownA = result.MaxHpA;
            float shownB = result.MaxHpB;

            foreach (TurnEvent e in result.Events)
            {
                turnText.text = $"턴 {e.TurnIndex + 1} / {BattleSimulator.TurnsPerBattle}";
                descriptionText.text = TurnNarrator.Describe(e);
                strokeComparison.Show(StepAt(currentStepsA, e.TurnIndex), StepAt(currentStepsB, e.TurnIndex), e.TypeA, e.TypeB);
                strokeComparison.SetReveal(0f);

                TechniqueClip? clipA = TechniqueClipCatalog.For(TechniqueCast.Find(currentCastsA, e.TurnIndex));
                TechniqueClip? clipB = TechniqueClipCatalog.For(TechniqueCast.Find(currentCastsB, e.TurnIndex));
                ReplayTurnTiming timing = ReplayTurnTiming.For(clipA, clipB);
                TechniqueStep? stepA = StepAt(currentStepsA, e.TurnIndex);
                TechniqueStep? stepB = StepAt(currentStepsB, e.TurnIndex);
                // A hidden-technique step (삼재검법) plays its chosen move's clip with its qi, on the
                // normal turn timing.
                string stateA = clipA?.State ?? CombatClipSelector.StateForTurn(e, true, stepA);
                string stateB = clipB?.State ?? CombatClipSelector.StateForTurn(e, false, stepB);
                bool startedA = false;
                bool startedB = false;
                LogTurnTiming(e.TurnIndex, timing, stateA, stateB);

                cameraRig.SetFocused(true);
                bool impactShown = false;

                float fromA = shownA;
                float fromB = shownB;
                float toA = Mathf.Max(0, e.HpAAfter);
                float toB = Mathf.Max(0, e.HpBAfter);

                // Elapsed time only advances while unpaused, so pausing freezes both the HP
                // tween and the turn timer and resuming continues from the exact same point.
                // turnTime is the turn's own clock in 1x seconds, compared against timing directly.
                float elapsed = 0f;
                while (elapsed < timing.Interval / speed)
                {
                    float turnTime = elapsed * speed;
                    // Each clip starts once its side's delay has passed (on the first frame when it
                    // is 0), so both strikes land on timing.Impact.
                    if (!startedA && turnTime >= timing.StartDelayA)
                    {
                        startedA = true;
                        characterA.PlayTurn(stateA, stepA?.HiddenTechniqueId);
                    }

                    if (!startedB && turnTime >= timing.StartDelayB)
                    {
                        startedB = true;
                        characterB.PlayTurn(stateB, stepB?.HiddenTechniqueId);
                    }

                    // The previews finish drawing as the blades meet.
                    strokeComparison.SetReveal(turnTime / timing.Impact);
                    if (!impactShown && turnTime >= timing.Impact)
                    {
                        impactShown = true;
                        LogImpact(e.TurnIndex, timing, turnTime);
                        PlayImpact(e);
                    }

                    if (turnTime >= timing.FocusEnd)
                        cameraRig.SetFocused(false);

                    float t = Mathf.Clamp01((elapsed - timing.Impact / speed) / HpLerpDuration);
                    shownA = Mathf.Lerp(fromA, toA, t);
                    shownB = Mathf.Lerp(fromB, toB, t);
                    SetHp(shownA, shownB);

                    yield return null;
                    if (!isPaused)
                        elapsed += Time.deltaTime;
                }

                shownA = toA;
                shownB = toB;
                SetHp(shownA, shownB);
                strokeComparison.SetReveal(1f);
                cameraRig.SetFocused(false);
            }

            // The base font size is the note's; the outcome is enlarged inline so the note lines
            // keep their own (smaller) line height.
            string outcome = $"<size={OutcomeFontSize}>{currentExit?.OutcomeLabel ?? DescribeOutcome(result.Outcome)}</size>";
            resultText.text = string.IsNullOrEmpty(currentResultNote)
                ? outcome
                : $"{outcome}\n{currentResultNote}";
            resultPanel.SetActive(true);
            isFinished = true;
            replayRoutine = null;
            UpdateControlLabels();
            SetMotionSpeed(1f);
            FireFinished();
        }

        // Cleared before invoking so a callback that starts another replay cannot re-enter it.
        private void FireFinished()
        {
            Action callback = pendingOnFinished;
            pendingOnFinished = null;
            callback?.Invoke();
        }

        // Editor/dev-build log for checking technique turns; normal turns log nothing.
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogTurnTiming(int turnIndex, ReplayTurnTiming timing, string stateA, string stateB)
        {
            if (timing.IsTechniqueTurn)
                Debug.Log($"[BattleReplay] turn {turnIndex + 1} start (t={Time.time:F3}): interval {timing.Interval:F3}s, impact at {timing.Impact:F3}s, " +
                          $"A {stateA} from {timing.StartDelayA:F3}s, B {stateB} from {timing.StartDelayB:F3}s");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogImpact(int turnIndex, ReplayTurnTiming timing, float turnTime)
        {
            if (timing.IsTechniqueTurn)
                Debug.Log($"[BattleReplay] turn {turnIndex + 1} impact (t={Time.time:F3}) at turn time {turnTime:F3}s");
        }

        private static TechniqueStep? StepAt(IReadOnlyList<TechniqueStep> steps, int turnIndex)
            => steps != null && turnIndex >= 0 && turnIndex < steps.Count ? steps[turnIndex] : (TechniqueStep?)null;

        private void SetMotionSpeed(float motionSpeed)
        {
            characterA.PlaybackSpeed = motionSpeed;
            characterB.PlaybackSpeed = motionSpeed;
        }

        // Bars are filled against each side's own maximum HP (its 내공), which the text also names
        // since the two sides rarely start equal.
        private void SetHp(float hpA, float hpB)
        {
            int maxA = currentResult.MaxHpA;
            int maxB = currentResult.MaxHpB;
            hpFillA.fillAmount = hpA / maxA;
            hpFillB.fillAmount = hpB / maxB;
            hpTextA.text = $"나 {Mathf.RoundToInt(hpA)}<size={MaxHpFontSize}>/{maxA}</size>";
            hpTextB.text = $"상대 {Mathf.RoundToInt(hpB)}<size={MaxHpFontSize}>/{maxB}</size>";
            worldHpA.SetHp(hpA, maxA);
            worldHpB.SetHp(hpB, maxB);
        }

        private void SetWorldHpVisible(bool visible)
        {
            if (worldHpA != null) worldHpA.gameObject.SetActive(visible);
            if (worldHpB != null) worldHpB.gameObject.SetActive(visible);
        }

        // Damage 0 (nullified/missed) means no reaction. The flash only marks actual blade contact.
        private void PlayImpact(TurnEvent e)
        {
            if (e.DamageToA > 0)
                characterA.PlayHitReaction();
            if (e.DamageToB > 0)
                characterB.PlayHitReaction();
            if (e.Reason == TurnReason.CrossContact)
                impactFlash.Play((characterA.SwordTipPosition + characterB.SwordTipPosition) * 0.5f);
        }

        private void UpdateControlLabels()
        {
            playPauseLabel.text = isPaused || isFinished ? "재생" : "일시정지";
            speedLabel.text = speed > 1f ? "2x" : "1x";
            if (!isFinished)
                SetMotionSpeed(isPaused ? 0f : speed);
        }

        private static string DescribeOutcome(BattleOutcome outcome)
        {
            switch (outcome)
            {
                // Player is always A.
                case BattleOutcome.WinA: return "승리";
                case BattleOutcome.WinB: return "패배";
                default: return "무승부";
            }
        }
    }
}
