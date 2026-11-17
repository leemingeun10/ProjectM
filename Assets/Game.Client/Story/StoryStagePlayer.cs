using System;
using Game.Client.Battle;
using Game.Client.Flow;
using Game.Client.Stage;
using Game.Sim.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Story
{
    // Plays one story stage over the map (the prototype's stage view): cuts tapped through one by
    // one (art on top, the line below), then the battle, the cuts after it and the final choice,
    // as StoryRun orders them. The battle itself is the existing one, started through
    // StageSelectController.PlayStoryBattle; when the player leaves the battle screen the result
    // comes back here. Closing the battle, or "지도로", drops the stage without clearing it.
    public class StoryStagePlayer : MonoBehaviour
    {
        [SerializeField] private GameFlowController flow;
        [SerializeField] private StageSelectController battles;
        [SerializeField] private StoryArtCatalog art;
        // The overlay shown while a stage plays.
        [SerializeField] private GameObject root;
        [SerializeField] private Text headText;
        [SerializeField] private Button quitButton;
        [Header("Cut")]
        [SerializeField] private GameObject cutView;
        // Covers the whole cut view: a tap anywhere moves on.
        [SerializeField] private Button nextButton;
        [SerializeField] private Image artBackground;
        [SerializeField] private RawImage artImage;
        [SerializeField] private Text artCaption;
        [SerializeField] private Text speakerText;
        [SerializeField] private Text lineText;
        [Header("Panel (battle, defeat, choice, end)")]
        [SerializeField] private GameObject panelView;
        [SerializeField] private Text panelTitle;
        [SerializeField] private Text panelBody;
        [SerializeField] private Button primaryButton;
        [SerializeField] private Text primaryLabel;
        [SerializeField] private Button secondaryButton;
        [SerializeField] private Text secondaryLabel;

        private static readonly Color PlaceholderColor = new Color(0.78f, 0.78f, 0.77f);
        private static readonly Color BlackoutColor = new Color(0.067f, 0.078f, 0.09f);

        private StoryBook book;
        private StoryRun run;
        private Action<StoryRun> onFinished;
        private Action<StoryFlag> onFlagChosen;
        private Action onLeft;
        private Action primaryAction;
        private Action secondaryAction;

        public bool IsRunning => run != null;

        private void Awake()
        {
            if (run == null)
                root.SetActive(false);
        }

        private void OnEnable()
        {
            quitButton.onClick.AddListener(Leave);
            nextButton.onClick.AddListener(OnNext);
            primaryButton.onClick.AddListener(OnPrimary);
            secondaryButton.onClick.AddListener(OnSecondary);
        }

        private void OnDisable()
        {
            quitButton.onClick.RemoveListener(Leave);
            nextButton.onClick.RemoveListener(OnNext);
            primaryButton.onClick.RemoveListener(OnPrimary);
            secondaryButton.onClick.RemoveListener(OnSecondary);
        }

        // finished: the stage reached its end (record it). flagChosen: the final choice was made.
        // left: the player left early (nothing recorded).
        public void Play(StoryBook storyBook, int stageIndex, Action<StoryRun> finished, Action<StoryFlag> flagChosen, Action left)
        {
            book = storyBook;
            run = new StoryRun(book.stages[stageIndex], stageIndex);
            onFinished = finished;
            onFlagChosen = flagChosen;
            onLeft = left;
            root.SetActive(true);
            Show();
        }

        private void Show()
        {
            if (run.IsFinished)
            {
                StoryRun done = run;
                End();
                onFinished?.Invoke(done);
                return;
            }

            StoryStage stage = run.Stage;
            StoryStep step = run.Current;
            switch (step.Kind)
            {
                case StoryStepKind.Cut:
                    SetHead($"{run.CutNumber}/{run.CutTotal}컷");
                    ShowCut(step.Cut);
                    break;
                case StoryStepKind.Battle:
                    SetHead("전투");
                    ShowPanel(stage.enemy, string.IsNullOrEmpty(stage.system) ? string.Empty : $"시스템: {stage.system}",
                        "전투 시작", StartBattle, null, null);
                    break;
                case StoryStepKind.Lost:
                    SetHead("패배");
                    ShowPanel("패배", "이 스테이지는 이겨야 다음으로 넘어간다.",
                        "다시 싸우기", () => { run.Retry(); Show(); }, "지도로", Leave);
                    break;
                case StoryStepKind.Choice:
                    SetHead("마지막 선택");
                    StoryChoiceText choice = stage.choiceText;
                    ShowPanel(choice.title, choice.body,
                        choice.spare, () => Choose(StoryFlag.Jeong), choice.kill, () => Choose(StoryFlag.Sa));
                    break;
                case StoryStepKind.End:
                    SetHead(book.partEnd.title);
                    string body = (book.partEnd.body ?? string.Empty).Replace("{flag}", StoryMapController.FlagName(run.ChosenFlag));
                    ShowPanel(book.partEnd.title, body, "지도로", () => { run.Next(); Show(); }, null, null);
                    break;
            }
        }

        private void SetHead(string extra) => headText.text = $"{run.Stage.id} {run.Stage.name}, {extra}";

        private void ShowCut(StoryCut cut)
        {
            cutView.SetActive(true);
            panelView.SetActive(false);

            Texture2D image = cut.black ? null : art != null ? art.Find(cut.image) : null;
            artImage.texture = image;
            artImage.enabled = image != null;
            artBackground.color = cut.black ? BlackoutColor : PlaceholderColor;
            if (image != null)
                artCaption.text = string.Empty;
            else if (cut.black)
                artCaption.text = $"<b>암전</b>\n{cut.art}";
            else if (cut.Type == StoryCutType.Close)
                artCaption.text = $"<b>클로즈업 대화 자리</b>\n{cut.speaker}의 얼굴";
            else
                artCaption.text = $"<b>일러스트 자리</b>\n{cut.art}";
            artCaption.color = cut.black ? new Color(0.79f, 0.8f, 0.81f) : StoryMapPalette.InkSoft;

            bool close = cut.Type == StoryCutType.Close;
            speakerText.gameObject.SetActive(close);
            speakerText.text = close ? cut.speaker : string.Empty;
            speakerText.color = close && cut.speaker == book.protagonist ? StoryMapPalette.Jade : StoryMapPalette.Ink;
            lineText.text = cut.line;
        }

        private void ShowPanel(string title, string body, string primary, Action onPrimary, string secondary, Action onSecondary)
        {
            cutView.SetActive(false);
            panelView.SetActive(true);
            panelTitle.text = title;
            panelBody.text = body;
            primaryLabel.text = primary;
            primaryAction = onPrimary;
            secondaryButton.gameObject.SetActive(onSecondary != null);
            secondaryLabel.text = secondary ?? string.Empty;
            secondaryAction = onSecondary;
        }

        private void OnNext()
        {
            if (run == null || run.IsFinished || run.Current.Kind != StoryStepKind.Cut)
                return;
            run.Next();
            Show();
        }

        private void OnPrimary() => primaryAction?.Invoke();

        private void OnSecondary() => secondaryAction?.Invoke();

        private void Choose(StoryFlag flag)
        {
            run.Choose(flag);
            onFlagChosen?.Invoke(flag);
            Show();
        }

        private void StartBattle()
        {
            StoryRun battleRun = run;
            StoryStage stage = battleRun.Stage;
            string outcomeLabel = string.IsNullOrEmpty(stage.outcomeLabel) ? null : stage.outcomeLabel;
            battles.PlayStoryBattle(stage.aiStage, stage.Theme, stage.Look,
                stage.starter ? StarterTechnique.Create() : null,
                outcome => new BattleReplayController.ReplayExit
                {
                    OutcomeLabel = outcomeLabel,
                    ContinueLabel = "계속",
                    OnContinue = () => flow.GoTo(GameScreen.Story, () => OnBattleReturned(battleRun, outcome)),
                    CloseLabel = "지도로",
                    OnClose = () => flow.GoTo(GameScreen.Story, () => { if (run == battleRun) Leave(); }),
                });
        }

        private void OnBattleReturned(StoryRun battleRun, BattleOutcome outcome)
        {
            if (run != battleRun || run.IsFinished || run.Current.Kind != StoryStepKind.Battle)
                return;
            run.ReportBattle(outcome);
            Show();
        }

        // Back to the map without recording anything.
        private void Leave()
        {
            if (run == null)
                return;
            End();
            onLeft?.Invoke();
        }

        private void End()
        {
            run = null;
            primaryAction = null;
            secondaryAction = null;
            root.SetActive(false);
        }
    }
}
