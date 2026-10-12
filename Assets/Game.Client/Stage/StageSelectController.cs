using System;
using System.Collections.Generic;
using System.Text;
using Game.Client.Battle;
using Game.Client.Flow;
using Game.Client.Progress;
using Game.Client.Pvp;
using Game.Client.Technique;
using Game.Client.Ui;
using Game.Sim.Ai;
using Game.Sim.Battle;
using Game.Sim.Progress;
using Game.Sim.Pvp;
using Game.Sim.Technique;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Stage
{
    // Battle entry for both the 수련 (PvE stage list) and 비무 (PvP lobby) screens.
    // Stage list: one button per StageCatalog entry, locked ones disabled. Picking a stage pits
    // one of the player's complete technique slots (asking which when there are several) against
    // that stage's seeded AI, moves to the battle screen to replay it, and records the clear on a win.
    // PvP: the lobby's start button runs the same slot pick against an opponent from IPvpBackend
    // and reports the outcome once the replay ends.
    // Techniques are never drawn here: with no complete slot the popup points to 무공 제작.
    // Game.sim decides everything; this only wires UI and files.
    public class StageSelectController : MonoBehaviour
    {
        // Darker than InkTheme.Gold so it stays readable on hanji cards.
        private const string ClearedColorTag = "#7A5A1E";

        [SerializeField] private GameFlowController flow;
        [SerializeField] private RectTransform stageButtonContainer;
        // Inactive template cloned once per stage; must have a Text child for the label.
        [SerializeField] private Button stageButtonTemplate;
        // On the 비무 screen: record card and the start button.
        [SerializeField] private Text pvpStatusText;
        [SerializeField] private Button pvpButton;
        // Global HUD shown on every screen but the title, so the record is always in view.
        [SerializeField] private Text pvpRecordHudText;
        [SerializeField] private Text messageText;
        [SerializeField] private BattleReplayController replayController;
        // Popup shared by both screens: picks among complete slots, or explains why a battle
        // cannot start (offering the way to 무공 제작).
        [SerializeField] private GameObject slotPickPanel;
        [SerializeField] private Text slotPickTitle;
        [SerializeField] private Text slotPickBody;
        // One entry per slot (index 0 = slot 1); each needs a Text child.
        [SerializeField] private Button[] slotPickButtons = new Button[TechniqueSlotStore.SlotCount];
        [SerializeField] private Button slotPickCancelButton;
        [SerializeField] private Button slotPickWorkshopButton;

        private readonly List<Button> stageButtons = new List<Button>();
        private ProgressData progress;
        private Action<int> pendingSlotAction;
        // Swap point for a real server: nothing else here knows which backend is in use.
        private IPvpBackend pvpBackend;

        private void Awake()
        {
            progress = ProgressStore.Load();
            pvpBackend = new LocalFakePvpBackend();
            stageButtonTemplate.gameObject.SetActive(false);

            foreach (StageDefinition stage in StageCatalog.Stages)
            {
                Button button = Instantiate(stageButtonTemplate, stageButtonContainer);
                button.name = $"Stage{stage.StageIndex}";
                button.gameObject.SetActive(true);
                int stageIndex = stage.StageIndex;
                button.onClick.AddListener(() => PlayStage(stageIndex));
                stageButtons.Add(button);
            }

            for (int i = 0; i < slotPickButtons.Length; i++)
            {
                int slot = i + 1;
                slotPickButtons[i].onClick.AddListener(() => OnSlotPicked(slot));
            }

            messageText.text = string.Empty;
            slotPickPanel.SetActive(false);
            RefreshStageButtons();
            RefreshPvpRecord();
        }

        private void OnEnable()
        {
            slotPickCancelButton.onClick.AddListener(CloseSlotPick);
            slotPickWorkshopButton.onClick.AddListener(GoToWorkshop);
            pvpButton.onClick.AddListener(PlayPvp);
            flow.ScreenEntered += OnScreenEntered;
            flow.ScreenExited += OnScreenExited;
        }

        private void OnDisable()
        {
            slotPickCancelButton.onClick.RemoveListener(CloseSlotPick);
            slotPickWorkshopButton.onClick.RemoveListener(GoToWorkshop);
            pvpButton.onClick.RemoveListener(PlayPvp);
            flow.ScreenEntered -= OnScreenEntered;
            flow.ScreenExited -= OnScreenExited;
        }

        private void OnScreenEntered(GameScreen screen)
        {
            if (screen == GameScreen.Training)
            {
                messageText.text = string.Empty;
                RefreshStageButtons();
            }
            else if (screen == GameScreen.Sparring)
            {
                RefreshPvpRecord();
            }
        }

        // The popup belongs to whichever screen opened it.
        private void OnScreenExited(GameScreen screen) => CloseSlotPick();

        private void GoToWorkshop()
        {
            CloseSlotPick();
            flow.GoTo(GameScreen.Workshop);
        }

        private void RefreshStageButtons()
        {
            for (int i = 0; i < stageButtons.Count; i++)
            {
                StageDefinition stage = StageCatalog.Stages[i];
                bool unlocked = ProgressRules.IsStageUnlocked(progress, stage.StageIndex);
                bool cleared = stage.StageIndex <= progress.HighestClearedStage;

                // Locked colours come from InkButtonLabel, which follows interactable.
                Button button = stageButtons[i];
                button.interactable = unlocked;
                Text label = button.GetComponentInChildren<Text>(true);
                label.text = cleared ? $"{stage.StageIndex}\n<size=24><color={ClearedColorTag}>클리어</color></size>"
                    : unlocked ? $"{stage.StageIndex}\n<size=24>{DescribeDifficulty(stage.Difficulty)}</size>"
                    : $"{stage.StageIndex}\n<size=24>잠김</size>";
            }

            bool pvpUnlocked = ProgressRules.IsPvpUnlocked(progress);
            pvpButton.interactable = pvpUnlocked;
            pvpButton.GetComponentInChildren<Text>(true).text = pvpUnlocked
                ? "상대 찾기"
                : $"비무 잠김\n<size=24>스테이지 {StageCatalog.PvpUnlockStageIndex} 클리어 시 해제</size>";
        }

        private void RefreshPvpRecord()
        {
            PvpRecord record = pvpBackend.GetRecord();
            int score = RankingRules.CalculateScore(record);
            RankingTier tier = RankingRules.GetTier(score);
            string tierTag = ColorUtility.ToHtmlStringRGB(TierColor(tier));

            pvpStatusText.text = $"{record.Wins}승  {record.Losses}패  {record.Draws}무\n"
                + $"<size=40><color=#{tierTag}>{DescribeTier(tier)}</color>  {score}점</size>";
            pvpRecordHudText.text = $"비무 {record.Wins}승 {record.Losses}패 {record.Draws}무\n"
                + $"<color=#{tierTag}>{DescribeTier(tier)} · {score}점</color>";
        }

        private static string DescribeTier(RankingTier tier)
        {
            switch (tier)
            {
                case RankingTier.Silver: return "은패";
                case RankingTier.Gold: return "금패";
                case RankingTier.Platinum: return "백금패";
                default: return "동패";
            }
        }

        private static Color TierColor(RankingTier tier)
        {
            switch (tier)
            {
                case RankingTier.Silver: return new Color(0.75f, 0.75f, 0.78f);   // #BFBFC7
                case RankingTier.Gold: return InkTheme.Gold;
                case RankingTier.Platinum: return new Color(0.45f, 0.8f, 1f);     // #73CCFF
                default: return new Color(0.72f, 0.45f, 0.2f);                    // #B87333 bronze
            }
        }

        private void PlayStage(int stageIndex) => PickSlotThen(slot => PlayStageWithSlot(stageIndex, slot));

        private void PlayPvp()
        {
            // The button is disabled while locked; this guards against a stale click.
            if (!ProgressRules.IsPvpUnlocked(progress))
                return;
            PickSlotThen(PlayPvpWithSlot);
        }

        // Runs onPicked with the only complete slot, or asks which one when there are several.
        private void PickSlotThen(Action<int> onPicked)
        {
            var completeSlots = new List<int>();
            for (int slot = 1; slot <= TechniqueSlotStore.SlotCount; slot++)
            {
                if (TechniqueSlotStore.GetStatus(slot).State == TechniqueSlotState.Complete)
                    completeSlots.Add(slot);
            }

            if (completeSlots.Count == 0)
            {
                ShowNotice("완성된 무공이 없습니다", $"무공 제작에서 {BattleSimulator.TurnsPerBattle}턴을 모두 채워\n슬롯에 저장한 뒤 다시 오세요.");
                return;
            }

            if (completeSlots.Count == 1)
            {
                onPicked(completeSlots[0]);
                return;
            }

            pendingSlotAction = onPicked;
            for (int i = 0; i < slotPickButtons.Length; i++)
            {
                int slot = i + 1;
                slotPickButtons[i].gameObject.SetActive(completeSlots.Contains(slot));
                slotPickButtons[i].GetComponentInChildren<Text>(true).text = $"슬롯 {slot}";
            }
            slotPickTitle.text = "출전할 무공";
            slotPickBody.text = "완성된 무공 중 하나를 고르세요.";
            slotPickWorkshopButton.gameObject.SetActive(false);
            messageText.text = string.Empty;
            slotPickPanel.SetActive(true);
        }

        // Same popup without slot choices: a message plus the way to 무공 제작.
        private void ShowNotice(string title, string body)
        {
            pendingSlotAction = null;
            foreach (Button button in slotPickButtons)
                button.gameObject.SetActive(false);
            slotPickTitle.text = title;
            slotPickBody.text = body;
            slotPickWorkshopButton.gameObject.SetActive(true);
            slotPickPanel.SetActive(true);
        }

        private void OnSlotPicked(int slot)
        {
            slotPickPanel.SetActive(false);
            Action<int> action = pendingSlotAction;
            pendingSlotAction = null;
            action?.Invoke(slot);
        }

        private void CloseSlotPick()
        {
            pendingSlotAction = null;
            slotPickPanel.SetActive(false);
        }

        // Re-checked on load: the file may have changed since the status scan.
        // BattleSimulator requires a full sequence; report it here instead of letting it throw.
        private bool TryLoadPlayerSteps(int slot, out List<TechniqueStep> playerSteps)
        {
            if (TechniqueSlotStore.TryLoad(slot, out playerSteps) && playerSteps.Count == BattleSimulator.TurnsPerBattle)
                return true;

            ShowNotice("무공을 불러올 수 없습니다", $"슬롯 {slot}의 무공이 손상되었거나 미완성입니다.\n무공 제작에서 다시 저장하세요.");
            return false;
        }

        private void PlayStageWithSlot(int stageIndex, int slot)
        {
            if (!TryLoadPlayerSteps(slot, out List<TechniqueStep> playerSteps))
                return;

            Debug.Log($"[Stage] 스테이지 {stageIndex}: 슬롯 {slot} 무공으로 전투");
            StageDefinition stage = StageCatalog.Get(stageIndex);
            List<TechniqueStep> aiSteps = AiTechniqueGenerator.Generate(stage.Difficulty, stage.Seed);
            BattleResult result = BattleSimulator.Simulate(playerSteps, aiSteps);
            LogResult($"[Stage] 스테이지 {stage.StageIndex} ({stage.Difficulty})", result);

            string note = null;
            if (result.Outcome == BattleOutcome.WinA)
                note = RecordClear(stageIndex);

            messageText.text = note ?? string.Empty;
            flow.GoTo(GameScreen.Battle, () => replayController.StartReplay(result, playerSteps, aiSteps, note));
        }

        // Unlike stage clears, the PvP result is reported when the replay ends (or is closed early;
        // see BattleReplayController.StartReplay), the point a real backend would learn of it.
        private void PlayPvpWithSlot(int slot)
        {
            if (!TryLoadPlayerSteps(slot, out List<TechniqueStep> playerSteps))
                return;

            Debug.Log($"[Pvp] 슬롯 {slot} 무공으로 PvP 전투");
            IReadOnlyList<TechniqueStep> opponentSteps = pvpBackend.FetchRandomOpponent();
            BattleResult result = BattleSimulator.Simulate(playerSteps, opponentSteps);
            LogResult("[Pvp] PvP", result);

            messageText.text = string.Empty;
            flow.GoTo(GameScreen.Battle, () => replayController.StartReplay(result, playerSteps, opponentSteps, "비무", () =>
            {
                pvpBackend.ReportResult(result.Outcome);
                RefreshPvpRecord();
            }));
        }

        // Saved right away rather than after the replay, so quitting mid-replay keeps the clear.
        private string RecordClear(int stageIndex)
        {
            bool isNewClear = stageIndex > progress.HighestClearedStage;
            bool pvpWasUnlocked = ProgressRules.IsPvpUnlocked(progress);

            progress = ProgressRules.RecordStageCleared(progress, stageIndex);
            ProgressStore.Save(progress);
            RefreshStageButtons();

            if (!isNewClear)
                return $"스테이지 {stageIndex} 재클리어!";

            var sb = new StringBuilder($"스테이지 {stageIndex} 클리어!");
            if (StageCatalog.IsValidStageIndex(stageIndex + 1))
                sb.Append($"\n다음 스테이지({stageIndex + 1}) 해제!");
            else
                sb.Append("\n모든 스테이지 클리어!");
            if (!pvpWasUnlocked && ProgressRules.IsPvpUnlocked(progress))
                sb.Append("\n비무 해제!");
            return sb.ToString();
        }

        private static void LogResult(string title, BattleResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{title} 대전 결과");
            foreach (TurnEvent e in result.Events)
                sb.AppendLine($"턴 {e.TurnIndex + 1}: 플레이어({e.TypeA}) vs AI({e.TypeB}) - 판정:{e.Reason} - 플레이어 데미지 {e.DamageToA} AI 데미지 {e.DamageToB} - HP 플레이어:{e.HpAAfter} AI:{e.HpBAfter}");
            sb.AppendLine($"최종 결과: {result.Outcome} - 최종 HP 플레이어:{result.FinalHpA} AI:{result.FinalHpB}");
            Debug.Log(sb.ToString());
        }

        private static string DescribeDifficulty(AiDifficulty difficulty)
        {
            switch (difficulty)
            {
                case AiDifficulty.Easy: return "쉬움";
                case AiDifficulty.Normal: return "보통";
                case AiDifficulty.Hard: return "어려움";
                default: return difficulty.ToString();
            }
        }
    }
}
