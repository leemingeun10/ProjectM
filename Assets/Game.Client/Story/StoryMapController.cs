using System.Collections;
using System.Collections.Generic;
using System.Text;
using Game.Client.Battle;
using Game.Client.Cultivation;
using Game.Client.Flow;
using Game.Client.Stage;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Story
{
    // The 스토리 screen's map (Docs/StoryMapPrototype.html): chapter tabs, the chapter's stage
    // nodes on a road, and a sheet describing the picked stage with a start button. Playing a
    // stage is handed to StoryStagePlayer; this owns the story save (storymap.json) and records a
    // stage when the player finishes it. All story text comes from the story JSON; only UI words
    // (buttons, field names) are written here.
    public class StoryMapController : MonoBehaviour
    {
        // The prototype's SVG viewBox the node coordinates are given in.
        private const float ViewWidth = 360f;
        private const float ViewHeight = 600f;
        private const float ToastSeconds = 1.8f;
        private const float ResetArmSeconds = 3f;
        // Node labels, in canvas units: gap from the marker's edge and half the distance between
        // the id line and the name line.
        private const float LabelGap = 16f;
        private const float LabelLine = 21f;

        [SerializeField] private TextAsset storyData;
        [SerializeField] private StoryStagePlayer player;
        [SerializeField] private Text progressText;
        [SerializeField] private RectTransform tabContainer;
        // Inactive template cloned once per chapter; needs a Text child.
        [SerializeField] private Button tabTemplate;
        [SerializeField] private RectTransform mapArea;
        [SerializeField] private StoryMapPaths paths;
        // Inactive template cloned per node: a Button with a StoryNodeMarker and two Text children
        // named "Id" and "Name".
        [SerializeField] private Button nodeTemplate;
        [SerializeField] private Text watermarkText;
        [SerializeField] private Text toastText;
        [SerializeField] private Button resetButton;
        [SerializeField] private Text resetLabel;
        [Header("Stage sheet")]
        [SerializeField] private GameObject sheetPanel;
        [SerializeField] private Button sheetScrim;
        [SerializeField] private Text sheetTitle;
        [SerializeField] private Text sheetKind;
        [SerializeField] private Text sheetBody;
        [SerializeField] private Text sheetProv;
        [SerializeField] private Button sheetCloseButton;
        [SerializeField] private Button sheetPlayButton;
        [SerializeField] private Text sheetPlayLabel;

        private readonly List<Button> tabs = new List<Button>();
        private readonly List<Button> nodes = new List<Button>();
        private StoryBook book;
        private StoryProgress progress;
        private int currentChapter = 1;
        private int sheetStage = -1;
        private bool resetArmed;
        private Coroutine toastRoutine;
        private Coroutine resetRoutine;
        // The map area size the nodes were laid out for; the map is redrawn when it changes
        // (screen rotation, resolution change, the canvas resizing after the screen opens).
        private Vector2 laidOutSize;

        public StoryBook Book => book;
        public StoryProgress Progress => progress;

        private void Awake()
        {
            tabTemplate.gameObject.SetActive(false);
            nodeTemplate.gameObject.SetActive(false);
            toastText.text = string.Empty;
            sheetPanel.SetActive(false);

            try
            {
                book = StoryBook.Parse(storyData != null ? storyData.text : null);
            }
            catch (StoryDataException e)
            {
                Debug.LogError($"[Story] 스토리 데이터를 읽을 수 없습니다: {e.Message}", this);
                book = null;
                return;
            }

            progress = StoryProgressStore.Load(book.StageCount);
            currentChapter = StoryProgressRules.CurrentChapter(book, progress);

            foreach (StoryChapter chapter in book.chapters)
            {
                Button tab = Instantiate(tabTemplate, tabContainer);
                tab.name = $"Tab{chapter.ch}";
                tab.gameObject.SetActive(true);
                int ch = chapter.ch;
                tab.onClick.AddListener(() => ShowChapter(ch));
                tabs.Add(tab);
            }

            int maxNodes = 0;
            foreach (StoryChapter chapter in book.chapters)
                maxNodes = Mathf.Max(maxNodes, chapter.nodes.Length);
            for (int i = 0; i < maxNodes; i++)
            {
                Button node = Instantiate(nodeTemplate, mapArea);
                node.name = $"Node{i + 1}";
                int slot = i;
                node.onClick.AddListener(() => OnNodeClicked(slot));
                nodes.Add(node);
            }
        }

        private void OnEnable()
        {
            sheetScrim.onClick.AddListener(CloseSheet);
            sheetCloseButton.onClick.AddListener(CloseSheet);
            sheetPlayButton.onClick.AddListener(PlaySheetStage);
            resetButton.onClick.AddListener(OnResetClicked);
            DisarmReset();
            // Coming back from a story battle: the player is still mid-stage and keeps the screen.
            if (book != null && !player.IsRunning)
                Refresh();
        }

        private void OnDisable()
        {
            sheetScrim.onClick.RemoveListener(CloseSheet);
            sheetCloseButton.onClick.RemoveListener(CloseSheet);
            sheetPlayButton.onClick.RemoveListener(PlaySheetStage);
            resetButton.onClick.RemoveListener(OnResetClicked);
            toastRoutine = null;
            resetRoutine = null;
            toastText.text = string.Empty;
            CloseSheet();
        }

        private void LateUpdate()
        {
            if (book != null && mapArea.rect.size != laidOutSize)
                RenderMap();
        }

        private void ShowChapter(int ch)
        {
            currentChapter = ch;
            Refresh();
        }

        private void Refresh()
        {
            if (book == null)
                return;

            var sb = new StringBuilder($"<b>{progress.Cleared}</b> / {book.StageCount} 클리어");
            if (progress.Flag != StoryFlag.None)
                sb.Append($"  <color=#{ColorUtility.ToHtmlStringRGB(StoryMapPalette.Jade)}><b>{FlagName(progress.Flag)}</b></color>");
            progressText.text = sb.ToString();

            for (int i = 0; i < tabs.Count; i++)
            {
                StoryChapter c = book.chapters[i];
                bool selected = c.ch == currentChapter;
                Text label = tabs[i].GetComponentInChildren<Text>(true);
                label.text = $"<size=26>{c.ch}장</size>\n{c.title}";
                label.color = selected ? StoryMapPalette.Ink : StoryMapPalette.InkSoft;
                Image underline = tabs[i].transform.Find("Underline")?.GetComponent<Image>();
                if (underline != null)
                    underline.enabled = selected;
            }

            RenderMap();
        }

        private void RenderMap()
        {
            StoryChapter chapter = book.Chapter(currentChapter);
            List<int> stageIndices = book.StageIndicesOf(currentChapter);
            watermarkText.text = chapter.sub;

            Rect rect = mapArea.rect;
            laidOutSize = rect.size;
            float scale = Mathf.Min(rect.width / ViewWidth, rect.height / ViewHeight);
            Vector2 ToLocal(StoryPoint p) => rect.center + new Vector2((p.x - ViewWidth * 0.5f) * scale, (ViewHeight * 0.5f - p.y) * scale);

            var roads = new List<(Vector2, Vector2, bool)>();
            for (int i = 0; i + 1 < stageIndices.Count; i++)
            {
                bool done = StoryProgressRules.StateOf(progress, stageIndices[i]) == StoryStageState.Cleared;
                roads.Add((ToLocal(chapter.nodes[i]), ToLocal(chapter.nodes[i + 1]), done));
            }
            paths.SetSegments(roads, scale);

            for (int i = 0; i < nodes.Count; i++)
            {
                Button node = nodes[i];
                bool used = i < stageIndices.Count;
                node.gameObject.SetActive(used);
                if (!used)
                    continue;

                StoryStage stage = book.stages[stageIndices[i]];
                StoryStageState state = StoryProgressRules.StateOf(progress, stageIndices[i]);
                var rt = (RectTransform)node.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = ToLocal(chapter.nodes[i]) - rect.center;
                rt.sizeDelta = new Vector2(64f, 64f) * scale;
                node.GetComponentInChildren<StoryNodeMarker>(true).Set(stage.Kind, state, scale);

                // Labels sit to the right of nodes on the left half, to the left otherwise.
                // Text keeps its size at any map scale, so only the gap from the marker scales.
                bool right = chapter.nodes[i].x < ViewWidth * 0.5f;
                float offset = (stage.Kind == StoryStageKind.Boss ? 26f : 17f) * scale + LabelGap;
                Color labelColor = state == StoryStageState.Locked ? StoryMapPalette.InkSoft : StoryMapPalette.Ink;
                PlaceLabel(node.transform.Find("Id") as RectTransform, stage.id, right, offset, LabelLine, true, labelColor);
                PlaceLabel(node.transform.Find("Name") as RectTransform, stage.name, right, offset, -LabelLine, false, labelColor);
                CanvasGroup group = node.GetComponent<CanvasGroup>();
                if (group != null)
                    group.alpha = state == StoryStageState.Locked ? 0.6f : 1f;
            }
        }

        private static void PlaceLabel(RectTransform label, string text, bool right, float offset, float y, bool bold, Color color)
        {
            if (label == null)
                return;
            label.anchorMin = label.anchorMax = new Vector2(0.5f, 0.5f);
            label.pivot = new Vector2(right ? 0f : 1f, 0.5f);
            label.anchoredPosition = new Vector2(right ? offset : -offset, y);
            Text t = label.GetComponent<Text>();
            t.alignment = right ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.color = color;
            t.text = text;
        }

        private void OnNodeClicked(int slot)
        {
            List<int> stageIndices = book.StageIndicesOf(currentChapter);
            if (slot < stageIndices.Count)
                OpenSheet(stageIndices[slot]);
        }

        private void OpenSheet(int stageIndex)
        {
            StoryStage stage = book.stages[stageIndex];
            StoryStageState state = StoryProgressRules.StateOf(progress, stageIndex);
            sheetStage = stageIndex;

            sheetTitle.text = $"{stage.id} {stage.name}";
            sheetKind.text = KindName(stage.Kind);

            var rows = new List<(string, string)>
            {
                ("클리어 조건", stage.Clear == StoryClearRule.Win ? "전투 승리" : stage.Kind == StoryStageKind.Loss ? "승패와 상관없이 진행" : "끝까지 보기"),
            };
            if (!string.IsNullOrEmpty(stage.enemy))
                rows.Add(("상대", stage.enemy));
            // Battles that must be won name the combat power they are tuned for, next to the player's.
            if (stage.recommendedPowerPermille > 0)
                rows.Add(("권장 전투력", $"{CombatPowerText.Format(stage.recommendedPowerPermille)} (내 전투력 {CombatPowerText.Format(BattleMatchups.PlayerStats(CultivationStore.Load()))})"));
            rows.Add(("사건", stage.@event));
            if (!string.IsNullOrEmpty(stage.system))
                rows.Add(("시스템", stage.system));
            if (stage.tags != null && stage.tags.Length > 0)
                rows.Add(("태그", string.Join(", ", stage.tags)));
            rows.Add(("컷 수", $"{stage.cuts.Length}컷"));

            string soft = ColorUtility.ToHtmlStringRGB(StoryMapPalette.InkSoft);
            var sb = new StringBuilder();
            foreach ((string key, string value) in rows)
            {
                if (sb.Length > 0)
                    sb.Append('\n');
                sb.Append($"<color=#{soft}>{key}</color>   {value}");
            }
            sheetBody.text = sb.ToString();

            sheetProv.gameObject.SetActive(stage.prov);
            sheetProv.text = stage.prov ? "스테이지 이름과 대사는 가제다." : string.Empty;

            sheetPlayButton.interactable = state != StoryStageState.Locked;
            sheetPlayLabel.text = state == StoryStageState.Locked ? "앞 스테이지를 먼저 클리어"
                : state == StoryStageState.Cleared ? "다시 보기"
                : "스테이지 시작";
            sheetPanel.SetActive(true);
        }

        private void CloseSheet()
        {
            sheetStage = -1;
            sheetPanel.SetActive(false);
        }

        private void PlaySheetStage()
        {
            int stageIndex = sheetStage;
            CloseSheet();
            if (stageIndex < 0 || !StoryProgressRules.CanPlay(progress, stageIndex, book.StageCount))
                return;
            player.Play(book, stageIndex, OnStageFinished, OnFlagChosen, Refresh);
        }

        // Saved the moment it is picked, like the prototype.
        private void OnFlagChosen(StoryFlag flag)
        {
            progress = StoryProgressRules.WithFlag(progress, flag);
            StoryProgressStore.Save(progress);
        }

        private void OnStageFinished(StoryRun run)
        {
            bool first = StoryProgressRules.IsFirstClear(progress, run.StageIndex);
            if (first)
            {
                progress = StoryProgressRules.RecordFinished(progress, run.StageIndex, book.StageCount);
                StoryProgressStore.Save(progress);
                currentChapter = StoryProgressRules.CurrentChapter(book, progress);
                ShowToast($"{run.Stage.id} 클리어");
            }
            Refresh();
        }

        private void OnResetClicked()
        {
            if (!resetArmed)
            {
                resetArmed = true;
                resetLabel.text = "진행 기록을 지운다";
                resetRoutine = StartCoroutine(DisarmLater());
                return;
            }

            DisarmReset();
            progress = default;
            StoryProgressStore.Save(progress);
            currentChapter = 1;
            Refresh();
            ShowToast("진행 기록을 지웠다");
        }

        private IEnumerator DisarmLater()
        {
            yield return new WaitForSecondsRealtime(ResetArmSeconds);
            resetRoutine = null;
            DisarmReset();
        }

        private void DisarmReset()
        {
            if (resetRoutine != null)
                StopCoroutine(resetRoutine);
            resetRoutine = null;
            resetArmed = false;
            resetLabel.text = "처음부터";
        }

        private void ShowToast(string message)
        {
            if (toastRoutine != null)
                StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(Toast(message));
        }

        private IEnumerator Toast(string message)
        {
            toastText.text = message;
            yield return new WaitForSecondsRealtime(ToastSeconds);
            toastText.text = string.Empty;
            toastRoutine = null;
        }

        public static string KindName(StoryStageKind kind)
        {
            switch (kind)
            {
                case StoryStageKind.Story: return "이야기";
                case StoryStageKind.Boss: return "보스전";
                case StoryStageKind.Loss: return "패배 전용 전투";
                default: return "전투";
            }
        }

        public static string FlagName(StoryFlag flag) => flag == StoryFlag.Jeong ? "정파" : flag == StoryFlag.Sa ? "사파" : string.Empty;
    }
}
