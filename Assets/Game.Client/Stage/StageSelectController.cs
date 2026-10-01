using System;
using System.Collections.Generic;
using System.Text;
using Game.Client.Battle;
using Game.Client.Progress;
using Game.Client.Pvp;
using Game.Client.Stroke;
using Game.Client.Technique;
using Game.Sim.Ai;
using Game.Sim.Battle;
using Game.Sim.Progress;
using Game.Sim.Pvp;
using Game.Sim.Technique;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Stage
{
    // PvE stage list: one button per StageCatalog entry, locked ones greyed out. Picking a stage
    // pits one of the player's complete technique slots (asking which when there are several)
    // against that stage's seeded AI, replays the fight, and records the clear on a win.
    // The PvP button next to the stage list runs the same slot pick against an opponent from
    // IPvpBackend and reports the outcome once the replay ends.
    // Game.sim decides everything; this only wires UI and files.
    public class StageSelectController : MonoBehaviour
    {
        private static readonly Color LockedLabelColor = new Color(0.2f, 0.2f, 0.2f, 0.35f);

        [SerializeField] private GameObject stagePanel;
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private RectTransform stageButtonContainer;
        // Inactive template cloned once per stage; must have a Text child for the label.
        [SerializeField] private Button stageButtonTemplate;
        [SerializeField] private Text pvpStatusText;
        [SerializeField] private Button pvpButton;
        // Outside the stage panel so the record stays on screen while drawing and replaying.
        [SerializeField] private Text pvpRecordHudText;
        [SerializeField] private Text messageText;
        [SerializeField] private StrokeInputController strokeInput;
        [SerializeField] private BattleReplayController replayController;
        // Shown over the stage list when more than one slot is complete.
        [SerializeField] private GameObject slotPickPanel;
        // One entry per slot (index 0 = slot 1); each needs a Text child.
        [SerializeField] private Button[] slotPickButtons = new Button[TechniqueSlotStore.SlotCount];
        [SerializeField] private Button slotPickCancelButton;

        private readonly List<Button> stageButtons = new List<Button>();
        private ProgressData progress;
        private Color unlockedLabelColor;
        private Action<int> pendingSlotAction;
        // Swap point for a real server: nothing else here knows which backend is in use.
        private IPvpBackend pvpBackend;

        private void Awake()
        {
            progress = ProgressStore.Load();
            pvpBackend = new LocalFakePvpBackend();
            stageButtonTemplate.gameObject.SetActive(false);
            unlockedLabelColor = stageButtonTemplate.GetComponentInChildren<Text>(true).color;

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
            RefreshStageButtons();
            RefreshPvpRecord();
            SetPanelOpen(false);
        }

        private void OnEnable()
        {
            openButton.onClick.AddListener(OpenPanel);
            closeButton.onClick.AddListener(ClosePanel);
            slotPickCancelButton.onClick.AddListener(CloseSlotPick);
            pvpButton.onClick.AddListener(PlayPvp);
        }

        private void OnDisable()
        {
            openButton.onClick.RemoveListener(OpenPanel);
            closeButton.onClick.RemoveListener(ClosePanel);
            slotPickCancelButton.onClick.RemoveListener(CloseSlotPick);
            pvpButton.onClick.RemoveListener(PlayPvp);
        }

        private void OpenPanel() => SetPanelOpen(true);

        private void ClosePanel() => SetPanelOpen(false);

        // The panel covers the drawing area, so stroke input is paused while it is open.
        private void SetPanelOpen(bool open)
        {
            stagePanel.SetActive(open);
            slotPickPanel.SetActive(false);
            strokeInput.enabled = !open;
        }

        private void RefreshStageButtons()
        {
            for (int i = 0; i < stageButtons.Count; i++)
            {
                StageDefinition stage = StageCatalog.Stages[i];
                bool unlocked = ProgressRules.IsStageUnlocked(progress, stage.StageIndex);
                bool cleared = stage.StageIndex <= progress.HighestClearedStage;

                Button button = stageButtons[i];
                button.interactable = unlocked;
                Text label = button.GetComponentInChildren<Text>(true);
                label.text = cleared ? $"{stage.StageIndex}\n<size=24>클리어</size>"
                    : unlocked ? $"{stage.StageIndex}\n<size=24>{DescribeDifficulty(stage.Difficulty)}</size>"
                    : $"{stage.StageIndex}\n<size=24>잠김</size>";
                label.color = unlocked ? unlockedLabelColor : LockedLabelColor;
            }

            bool pvpUnlocked = ProgressRules.IsPvpUnlocked(progress);
            pvpButton.interactable = pvpUnlocked;
            Text pvpLabel = pvpButton.GetComponentInChildren<Text>(true);
            pvpLabel.text = pvpUnlocked
                ? "PvP 대전"
                : $"PvP 잠김\n<size=24>스테이지 {StageCatalog.PvpUnlockStageIndex} 클리어 시 해제</size>";
            pvpLabel.color = pvpUnlocked ? unlockedLabelColor : LockedLabelColor;
        }

        private void RefreshPvpRecord()
        {
            PvpRecord record = pvpBackend.GetRecord();
            string text = $"PvP 전적: {record.Wins}승 {record.Losses}패 {record.Draws}무";
            pvpStatusText.text = text;
            pvpRecordHudText.text = text;
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
                messageText.text = $"완성된 무공 슬롯이 없습니다.\n{BattleSimulator.TurnsPerBattle}턴을 채워 슬롯에 저장하세요.";
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
            messageText.text = string.Empty;
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

            messageText.text = $"슬롯 {slot} 무공을 불러올 수 없습니다.";
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
            SetPanelOpen(false);
            replayController.StartReplay(result, note);
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
            SetPanelOpen(false);
            replayController.StartReplay(result, "PvP 대전", () =>
            {
                pvpBackend.ReportResult(result.Outcome);
                RefreshPvpRecord();
            });
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
                sb.Append("\nPvP 해제!");
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
