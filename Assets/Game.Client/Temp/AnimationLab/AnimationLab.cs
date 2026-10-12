using System;
using System.Collections.Generic;
using Game.Client.Ui;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Temp.AnimationLab
{
    // TEMPORARY animation test page. Self-contained on purpose so it can be removed by deleting
    // this folder and the "AnimationLab (temp)" scene object: nothing else references it.
    // At start it adds an entry button to the home screen and builds its own overlay canvas.
    // Clip slots with no clip assigned (e.g. AI clips not generated yet) show as disabled.
    public class AnimationLab : MonoBehaviour
    {
        [Serializable]
        public class LabClip
        {
            public string label;
            public AnimationClip clip;
        }

        [Tooltip("Where the entry button is added (the home screen root).")]
        [SerializeField] private RectTransform entryParent;
        [SerializeField] private GameObject characterPrefab;
        [SerializeField] private AnimationClip idleClip;
        [SerializeField] private List<LabClip> clips = new List<LabClip>();

        private static readonly float[] Speeds = { 1f, 0.5f, 0.25f };
        private static readonly string[] ViewNames = { "측면", "정면", "45°", "후면" };

        private Font font;
        private GameObject page;
        private AnimationLabPreview preview;
        private RawImage previewImage;
        private Text infoText;
        private Slider scrubSlider;
        private Text pauseLabel;
        private Text speedLabel;
        private Text repeatLabel;
        private Text viewLabel;
        private readonly List<int> buttonClipIndex = new List<int>();
        private readonly List<Button> clipButtons = new List<Button>();
        private int speedIndex;
        private bool updatingSlider;

        private void Start()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildEntryButton();
            BuildPage();
            page.SetActive(false);
        }

        private void OnDestroy() => preview?.Dispose();

        private void Update()
        {
            if (preview == null || !page.activeSelf)
                return;

            preview.Tick(Time.deltaTime);

            AnimationClip clip = preview.CurrentClip;
            float time = Mathf.Min(preview.CurrentTime, clip.length);
            infoText.text = $"{ClipLabel(preview.CurrentIndex)}   <color=#A39A88>{clip.name} · {time:0.00} / {clip.length:0.00}초 · {Mathf.RoundToInt(clip.length * clip.frameRate)}프레임</color>";

            updatingSlider = true;
            scrubSlider.value = clip.length > 0f ? time / clip.length : 0f;
            updatingSlider = false;

            for (int i = 0; i < clipButtons.Count; i++)
                InkStyle.SetFill(clipButtons[i], buttonClipIndex[i] == preview.CurrentIndex ? InkTheme.Vermilion : InkTheme.InkPanelLight);
        }

        private void Open()
        {
            if (preview == null)
            {
                var list = new List<AnimationClip> { idleClip };
                foreach (LabClip c in clips)
                {
                    if (c.clip != null)
                        list.Add(c.clip);
                }
                preview = new AnimationLabPreview(characterPrefab, list);
                previewImage.texture = preview.Texture;
            }

            page.SetActive(true);
            preview.SetActive(true);
            RefreshControls();
        }

        private void Close()
        {
            preview.SetActive(false);
            page.SetActive(false);
        }

        private void PlayClip(int previewIndex)
        {
            preview.SetPaused(false);
            preview.PlayIndex(previewIndex);
            RefreshControls();
        }

        private string ClipLabel(int previewIndex)
        {
            if (previewIndex == 0)
                return "대기";
            int n = 0;
            foreach (LabClip c in clips)
            {
                if (c.clip == null)
                    continue;
                if (++n == previewIndex)
                    return c.label;
            }
            return "";
        }

        private void RefreshControls()
        {
            pauseLabel.text = preview.Paused ? "재생" : "일시정지";
            speedLabel.text = $"속도 {Speeds[speedIndex]:0.##}x";
            repeatLabel.text = preview.Repeat ? "반복 켬" : "반복 끔";
            viewLabel.text = $"시점 {ViewNames[(int)preview.CurrentView]}";
        }

        // ---- UI construction -------------------------------------------------------------

        private void BuildEntryButton()
        {
            Button button = MakeButton(entryParent, "애니 테스트 (임시)", InkButtonKind.Chip, 30, Open);
            Place((RectTransform)button.transform, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(420f, 90f));
        }

        private void BuildPage()
        {
            page = new GameObject("AnimationLabCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            page.transform.SetParent(transform, false);
            var canvas = page.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = page.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0f;
            var rootRt = (RectTransform)page.transform;

            Image background = MakeImage(rootRt, "Background", InkTheme.Ink);
            Stretch(background.rectTransform);

            Button exit = MakeButton(rootRt, "〈 나가기", InkButtonKind.Secondary, 32, Close);
            Place((RectTransform)exit.transform, new Vector2(0f, 1f), new Vector2(160f, -80f), new Vector2(240f, 90f));

            Text title = MakeText(rootRt, "Title", "애니메이션 테스트", 44, InkTheme.TextLight, TextAnchor.MiddleCenter);
            InkStyle.StyleTitle(title, 44, InkTheme.TextLight, 6f);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(90f, -80f), new Vector2(600f, 80f));

            previewImage = new GameObject("Preview", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            previewImage.transform.SetParent(rootRt, false);
            Place(previewImage.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -620f), new Vector2(920f, 920f));
            InkStyle.EnsureBorder(previewImage.rectTransform, InkTheme.Gold, -4f);

            infoText = MakeText(rootRt, "Info", "", 30, InkTheme.TextLight, TextAnchor.MiddleCenter);
            infoText.fontStyle = FontStyle.Bold;
            Place(infoText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -1120f), new Vector2(1000f, 60f));

            scrubSlider = MakeSlider(rootRt);
            Place((RectTransform)scrubSlider.transform, new Vector2(0.5f, 1f), new Vector2(0f, -1185f), new Vector2(920f, 44f));
            scrubSlider.onValueChanged.AddListener(v =>
            {
                if (updatingSlider)
                    return;
                preview.SetPaused(true);
                preview.Scrub(v);
                RefreshControls();
            });

            // Clip buttons: idle first, then every configured slot (disabled when empty).
            var entries = new List<(string label, int previewIndex)> { ("대기", 0) };
            int next = 1;
            foreach (LabClip c in clips)
                entries.Add((c.clip != null ? c.label : $"{c.label}\n<size=22>미생성</size>", c.clip != null ? next++ : -1));

            const int columns = 3;
            for (int i = 0; i < entries.Count; i++)
            {
                int previewIndex = entries[i].previewIndex;
                Button b = MakeButton(rootRt, entries[i].label, InkButtonKind.Secondary, 30, () => PlayClip(previewIndex));
                int row = i / columns;
                int col = i % columns;
                Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2((col - 1) * 340f, -1290f - row * 112f), new Vector2(320f, 96f));
                b.interactable = previewIndex >= 0;
                if (previewIndex >= 0)
                {
                    clipButtons.Add(b);
                    buttonClipIndex.Add(previewIndex);
                }
            }

            float controlsY = -1290f - ((entries.Count + columns - 1) / columns) * 112f - 30f;
            pauseLabel = MakeControl(rootRt, 0, controlsY, () => preview.SetPaused(!preview.Paused));
            speedLabel = MakeControl(rootRt, 1, controlsY, () =>
            {
                speedIndex = (speedIndex + 1) % Speeds.Length;
                preview.SetSpeed(Speeds[speedIndex]);
            });
            repeatLabel = MakeControl(rootRt, 2, controlsY, () => preview.Repeat = !preview.Repeat);
            viewLabel = MakeControl(rootRt, 3, controlsY, () => preview.SetView((AnimationLabPreview.View)(((int)preview.CurrentView + 1) % ViewNames.Length)));

            Text hint = MakeText(rootRt, "Hint", "격자 간격 0.5m · 슬라이더를 끌면 일시정지 후 해당 프레임으로 이동", 24, InkTheme.TextMuted, TextAnchor.MiddleCenter);
            Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, controlsY - 85f), new Vector2(1000f, 40f));
        }

        private Text MakeControl(RectTransform parent, int slot, float y, Action onClick)
        {
            Button b = MakeButton(parent, "", InkButtonKind.Chip, 28, () =>
            {
                onClick();
                RefreshControls();
            });
            Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(-375f + slot * 250f, y), new Vector2(234f, 90f));
            return b.GetComponentInChildren<Text>();
        }

        private Button MakeButton(RectTransform parent, string label, InkButtonKind kind, int fontSize, Action onClick)
        {
            Image image = MakeImage(parent, "Button", Color.white);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Text text = MakeText(image.rectTransform, "Text", label, fontSize, InkTheme.TextLight, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            InkStyle.StyleButton(button, kind);
            button.onClick.AddListener(() => onClick());
            return button;
        }

        private Slider MakeSlider(RectTransform parent)
        {
            var go = new GameObject("Scrub", typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);
            var slider = go.GetComponent<Slider>();

            Image track = MakeImage((RectTransform)go.transform, "Track", InkTheme.Charcoal);
            Stretch(track.rectTransform);
            track.rectTransform.anchorMin = new Vector2(0f, 0.35f);
            track.rectTransform.anchorMax = new Vector2(1f, 0.65f);

            var fillArea = new GameObject("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            Stretch((RectTransform)fillArea.transform);
            Image fill = MakeImage((RectTransform)fillArea.transform, "Fill", InkTheme.GoldDim);
            Stretch(fill.rectTransform);
            fill.rectTransform.anchorMin = new Vector2(0f, 0.35f);
            fill.rectTransform.anchorMax = new Vector2(0f, 0.65f);

            var handleArea = new GameObject("HandleArea", typeof(RectTransform));
            handleArea.transform.SetParent(go.transform, false);
            Stretch((RectTransform)handleArea.transform);
            Image handle = MakeImage((RectTransform)handleArea.transform, "Handle", InkTheme.Vermilion);
            handle.rectTransform.sizeDelta = new Vector2(26f, 0f);
            handle.rectTransform.anchorMin = new Vector2(0f, 0f);
            handle.rectTransform.anchorMax = new Vector2(0f, 1f);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            return slider;
        }

        private Image MakeImage(RectTransform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private Text MakeText(RectTransform parent, string name, string content, int size, Color color, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }
    }
}
