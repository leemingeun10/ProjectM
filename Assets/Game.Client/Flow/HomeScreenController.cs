using Game.Client.Progress;
using Game.Client.Technique;
using Game.Sim.Progress;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Flow
{
    // Main hub: 무공 제작 (draw techniques), 수련 (stages) and 비무 (PvP). 비무 stays locked until
    // the PvP unlock stage is cleared, using the same ProgressRules check as the PvP lobby.
    public class HomeScreenController : MonoBehaviour
    {
        [SerializeField] private GameFlowController flow;
        [SerializeField] private Button workshopButton;
        [SerializeField] private Button trainingButton;
        [SerializeField] private Button sparringButton;
        [SerializeField] private Text workshopLabel;
        [SerializeField] private Text trainingLabel;
        [SerializeField] private Text sparringLabel;

        private void OnEnable()
        {
            workshopButton.onClick.AddListener(OpenWorkshop);
            trainingButton.onClick.AddListener(OpenTraining);
            sparringButton.onClick.AddListener(OpenSparring);
            Refresh();
        }

        private void OnDisable()
        {
            workshopButton.onClick.RemoveListener(OpenWorkshop);
            trainingButton.onClick.RemoveListener(OpenTraining);
            sparringButton.onClick.RemoveListener(OpenSparring);
        }

        private void OpenWorkshop() => flow.GoTo(GameScreen.Workshop);

        private void OpenTraining() => flow.GoTo(GameScreen.Training);

        private void OpenSparring()
        {
            // The button is disabled while locked; this guards against a stale click.
            if (ProgressRules.IsPvpUnlocked(ProgressStore.Load()))
                flow.GoTo(GameScreen.Sparring);
        }

        // Progress and slots may have changed on other screens, so re-read them on every visit.
        private void Refresh()
        {
            ProgressData progress = ProgressStore.Load();

            int complete = 0;
            for (int slot = 1; slot <= TechniqueSlotStore.SlotCount; slot++)
            {
                if (TechniqueSlotStore.GetStatus(slot).State == TechniqueSlotState.Complete)
                    complete++;
            }
            workshopLabel.text = $"무공 제작\n<size=26>완성된 무공 {complete} / {TechniqueSlotStore.SlotCount}</size>";

            int cleared = progress.HighestClearedStage;
            trainingLabel.text = $"수련\n<size=26>{(cleared > 0 ? $"스테이지 {cleared} 돌파" : "첫 수련을 시작하세요")}</size>";

            bool pvpUnlocked = ProgressRules.IsPvpUnlocked(progress);
            sparringButton.interactable = pvpUnlocked;
            sparringLabel.text = pvpUnlocked
                ? "비무\n<size=26>다른 무인과 겨루기</size>"
                : $"비무\n<size=26>스테이지 {StageCatalog.PvpUnlockStageIndex} 클리어 시 해제</size>";
        }
    }
}
