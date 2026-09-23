using System.Collections;
using Game.Sim.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Battle
{
    // Plays back an already-computed BattleResult turn by turn. Presentation only: the result
    // is never modified, and all float/timing work stays on this side of the Game.sim boundary.
    public class BattleReplayController : MonoBehaviour
    {
        private const float TurnInterval = 1.2f;
        private const float HpLerpDuration = 0.3f;
        private const float FastSpeed = 2f;

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

        private BattleResult currentResult;
        private Coroutine replayRoutine;
        private bool isPaused;
        private bool isFinished;
        private float speed = 1f;

        private void Awake()
        {
            replayPanel.SetActive(false);
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
        public void StartReplay(BattleResult result)
        {
            currentResult = result;
            replayPanel.SetActive(true);
            Restart();
        }

        private void Restart()
        {
            if (currentResult == null)
                return;

            StopReplay();
            isPaused = false;
            isFinished = false;
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
            replayPanel.SetActive(false);
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

                float fromA = shownA;
                float fromB = shownB;
                float toA = Mathf.Max(0, e.HpAAfter);
                float toB = Mathf.Max(0, e.HpBAfter);

                // Elapsed time only advances while unpaused, so pausing freezes both the HP
                // tween and the turn timer and resuming continues from the exact same point.
                float elapsed = 0f;
                while (elapsed < TurnInterval / speed)
                {
                    float t = Mathf.Clamp01(elapsed / HpLerpDuration);
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
            }

            resultText.text = DescribeOutcome(result.Outcome);
            resultText.gameObject.SetActive(true);
            isFinished = true;
            replayRoutine = null;
            UpdateControlLabels();
        }

        private void SetHp(float hpA, float hpB)
        {
            hpFillA.fillAmount = hpA / BattleConstants.StartingHP;
            hpFillB.fillAmount = hpB / BattleConstants.StartingHP;
            hpTextA.text = $"A: {Mathf.RoundToInt(hpA)}";
            hpTextB.text = $"B: {Mathf.RoundToInt(hpB)}";
        }

        private void UpdateControlLabels()
        {
            playPauseLabel.text = isPaused || isFinished ? "재생" : "일시정지";
            speedLabel.text = speed > 1f ? "2x" : "1x";
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
