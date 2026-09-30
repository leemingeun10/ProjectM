using System.Collections.Generic;
using System.Text;
using Game.Client.Battle;
using Game.Client.Progress;
using Game.Client.Stroke;
using Game.Client.Technique;
using Game.Sim.Ai;
using Game.Sim.Battle;
using Game.Sim.Progress;
using Game.Sim.Technique;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Stage
{
    // PvE stage list: one button per StageCatalog entry, locked ones greyed out. Picking a stage
    // pits one of the player's complete technique slots (asking which when there are several)
    // against that stage's seeded AI, replays the fight, and records the clear on a win.
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
        private int pendingStageIndex;

        private void Awake()
        {
            progress = ProgressStore.Load();
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
            SetPanelOpen(false);
        }

        private void OnEnable()
        {
            openButton.onClick.AddListener(OpenPanel);
            closeButton.onClick.AddListener(ClosePanel);
            slotPickCancelButton.onClick.AddListener(CloseSlotPick);
        }

        private void OnDisable()
        {
            openButton.onClick.RemoveListener(OpenPanel);
            closeButton.onClick.RemoveListener(ClosePanel);
            slotPickCancelButton.onClick.RemoveListener(CloseSlotPick);
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

            pvpStatusText.text = ProgressRules.IsPvpUnlocked(progress)
                ? "PvP: 해제됨"
                : $"PvP: 잠김 (스테이지 {StageCatalog.PvpUnlockStageIndex} 클리어 시 해제)";
        }

        private void PlayStage(int stageIndex)
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
                PlayStageWithSlot(stageIndex, completeSlots[0]);
                return;
            }

            pendingStageIndex = stageIndex;
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
            PlayStageWithSlot(pendingStageIndex, slot);
        }

        private void CloseSlotPick() => slotPickPanel.SetActive(false);

        private void PlayStageWithSlot(int stageIndex, int slot)
        {
            // Re-checked on load: the file may have changed since the status scan.
            // BattleSimulator requires a full sequence; report it here instead of letting it throw.
            if (!TechniqueSlotStore.TryLoad(slot, out List<TechniqueStep> playerSteps)
                || playerSteps.Count != BattleSimulator.TurnsPerBattle)
            {
                messageText.text = $"슬롯 {slot} 무공을 불러올 수 없습니다.";
                return;
            }

            Debug.Log($"[Stage] 스테이지 {stageIndex}: 슬롯 {slot} 무공으로 전투");
            StageDefinition stage = StageCatalog.Get(stageIndex);
            List<TechniqueStep> aiSteps = AiTechniqueGenerator.Generate(stage.Difficulty, stage.Seed);
            BattleResult result = BattleSimulator.Simulate(playerSteps, aiSteps);
            LogResult(stage, result);

            string note = null;
            if (result.Outcome == BattleOutcome.WinA)
                note = RecordClear(stageIndex);

            messageText.text = note ?? string.Empty;
            SetPanelOpen(false);
            replayController.StartReplay(result, note);
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

        private static void LogResult(StageDefinition stage, BattleResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[Stage] 스테이지 {stage.StageIndex} ({stage.Difficulty}) 대전 결과");
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
