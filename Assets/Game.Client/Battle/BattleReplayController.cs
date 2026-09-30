using System.Collections;
using System.Collections.Generic;
using Game.Sim.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Battle
{
    // Plays back an already-computed BattleResult turn by turn. Presentation only: the result
    // is never modified, and all float/timing work stays on this side of the Game.sim boundary.
    // Each turn also starts characters A and B on the motion for their BattleStepType; the clip
    // speed follows the replay speed and freezes while paused.
    public class BattleReplayController : MonoBehaviour
    {
        private const float TurnInterval = 1.2f;
        private const float HpLerpDuration = 0.3f;
        private const float FastSpeed = 2f;
        // Fraction of a turn at which the blades are treated as meeting: hit reactions and the
        // impact flash fire here rather than at turn start, when the swings have barely begun.
        private const float ImpactFraction = 0.3f;
        // Fraction of a turn after which the camera eases back out of its turn focus.
        private const float FocusEndFraction = 0.65f;

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
        [SerializeField] private Button closeButton;
        [SerializeField] private CharacterMotionPlayer characterA;
        [SerializeField] private CharacterMotionPlayer characterB;
        [SerializeField] private BattleCameraRig cameraRig;
        [SerializeField] private WorldHpBar worldHpA;
        [SerializeField] private WorldHpBar worldHpB;
        [SerializeField] private ImpactFlash impactFlash;

        private BattleResult currentResult;
        private string currentResultNote;
        private Coroutine replayRoutine;
        private bool isPaused;
        private bool isFinished;
        private float speed = 1f;

        private void Awake()
        {
            replayPanel.SetActive(false);
            SetWorldHpVisible(false);

            // Same fail-once policy as StrokeAnimationPlayer: report unwired characters up front.
            var missing = new List<string>();
            if (characterA == null) missing.Add(nameof(characterA));
            if (characterB == null) missing.Add(nameof(characterB));
            if (cameraRig == null) missing.Add(nameof(cameraRig));
            if (worldHpA == null) missing.Add(nameof(worldHpA));
            if (worldHpB == null) missing.Add(nameof(worldHpB));
            if (impactFlash == null) missing.Add(nameof(impactFlash));
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
            closeButton.onClick.AddListener(Close);
        }

        private void OnDisable()
        {
            playPauseButton.onClick.RemoveListener(TogglePause);
            speedButton.onClick.RemoveListener(ToggleSpeed);
            restartButton.onClick.RemoveListener(Restart);
            closeButton.onClick.RemoveListener(Close);
        }

        // Replaces any replay in progress with the new result and plays it from the start.
        // resultNote, if given, is shown under the outcome when the replay ends.
        public void StartReplay(BattleResult result, string resultNote = null)
        {
            currentResult = result;
            currentResultNote = resultNote;
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
            SetHp(BattleConstants.StartingHP, BattleConstants.StartingHP);
            turnText.text = $"턴 0 / {BattleSimulator.TurnsPerBattle}";
            descriptionText.text = string.Empty;
            resultText.gameObject.SetActive(false);
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
            StopReplay();
            characterA.StopToIdle();
            characterB.StopToIdle();
            SetMotionSpeed(1f);
            cameraRig.SetFocused(false);
            replayPanel.SetActive(false);
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
            float shownA = BattleConstants.StartingHP;
            float shownB = BattleConstants.StartingHP;

            foreach (TurnEvent e in result.Events)
            {
                turnText.text = $"턴 {e.TurnIndex + 1} / {BattleSimulator.TurnsPerBattle}";
                descriptionText.text = DescribeTurn(e);
                characterA.Play(MotionStateFor(e.TypeA));
                characterB.Play(MotionStateFor(e.TypeB));
                cameraRig.SetFocused(true);
                bool impactShown = false;

                float fromA = shownA;
                float fromB = shownB;
                float toA = Mathf.Max(0, e.HpAAfter);
                float toB = Mathf.Max(0, e.HpBAfter);

                // Elapsed time only advances while unpaused, so pausing freezes both the HP
                // tween and the turn timer and resuming continues from the exact same point.
                float elapsed = 0f;
                while (elapsed < TurnInterval / speed)
                {
                    float turnProgress = elapsed * speed / TurnInterval;
                    if (!impactShown && turnProgress >= ImpactFraction)
                    {
                        impactShown = true;
                        PlayImpact(e);
                    }

                    if (turnProgress >= FocusEndFraction)
                        cameraRig.SetFocused(false);

                    float t = Mathf.Clamp01((elapsed - ImpactFraction * TurnInterval / speed) / HpLerpDuration);
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
                cameraRig.SetFocused(false);
            }

            resultText.text = string.IsNullOrEmpty(currentResultNote)
                ? DescribeOutcome(result.Outcome)
                : $"{DescribeOutcome(result.Outcome)}\n<size=40>{currentResultNote}</size>";
            resultText.gameObject.SetActive(true);
            isFinished = true;
            replayRoutine = null;
            UpdateControlLabels();
            SetMotionSpeed(1f);
        }

        private void SetMotionSpeed(float motionSpeed)
        {
            characterA.PlaybackSpeed = motionSpeed;
            characterB.PlaybackSpeed = motionSpeed;
        }

        // Counter maps to Guard regardless of whether the counter landed; hit reactions come later.
        private static string MotionStateFor(BattleStepType type)
        {
            switch (type)
            {
                case BattleStepType.Thrust: return CharacterMotionPlayer.ThrustState;
                case BattleStepType.Counter: return CharacterMotionPlayer.GuardState;
                default: return CharacterMotionPlayer.SlashState;
            }
        }

        private void SetHp(float hpA, float hpB)
        {
            hpFillA.fillAmount = hpA / BattleConstants.StartingHP;
            hpFillB.fillAmount = hpB / BattleConstants.StartingHP;
            hpTextA.text = $"A: {Mathf.RoundToInt(hpA)}";
            hpTextB.text = $"B: {Mathf.RoundToInt(hpB)}";
            worldHpA.SetHp(hpA, BattleConstants.StartingHP);
            worldHpB.SetHp(hpB, BattleConstants.StartingHP);
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

        private static string DescribeTurn(TurnEvent e)
            => $"A: {DescribeStepType(e.TypeA)}  vs  B: {DescribeStepType(e.TypeB)}   →  {DescribeReason(e.Reason)}   →  A -{e.DamageToA}  B -{e.DamageToB}";

        private static string DescribeStepType(BattleStepType type)
        {
            switch (type)
            {
                case BattleStepType.Slash: return "검로";
                case BattleStepType.Thrust: return "찌르기";
                case BattleStepType.Counter: return "반격";
                default: return type.ToString();
            }
        }

        private static string DescribeReason(TurnReason reason)
        {
            switch (reason)
            {
                case TurnReason.CrossContact: return "접촉 성공";
                case TurnReason.Miss: return "접촉 실패";
                case TurnReason.Unconditional: return "무조건 판정";
                default: return reason.ToString();
            }
        }

        private static string DescribeOutcome(BattleOutcome outcome)
        {
            switch (outcome)
            {
                case BattleOutcome.WinA: return "A 승리";
                case BattleOutcome.WinB: return "B 승리";
                default: return "무승부";
            }
        }
    }
}
