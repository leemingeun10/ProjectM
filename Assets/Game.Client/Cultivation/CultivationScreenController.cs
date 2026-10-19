using System.Collections;
using Game.Client.Flow;
using Game.Client.Ui;
using Game.Sim.Cultivation;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Cultivation
{
    // 단련 screen: shows 외공/내공 and their tiers, and lets the player train one of them once a
    // day. The character sits in meditation on a 3D stage; 외공 plays a drill picked by the current
    // 외공 tier, 내공 plays the qi effect at the current 내공 tier. The result is saved the moment a
    // button is pressed (so leaving mid-show cannot train twice) and revealed when the show ends.
    //
    // The screen's UI is built in code under this object on first enable, so the scene only holds
    // the root, this component and its references.
    public class CultivationScreenController : MonoBehaviour
    {
        [SerializeField] private GameFlowController flow;
        [SerializeField] private GameObject characterPrefab;
        [Tooltip("Looping seated (가부좌) idle.")]
        [SerializeField] private AnimationClip meditationClip;
        [Tooltip("외공 drill per tier: 1 push-ups, 2 wooden dummy strikes, 3 one-arm handstand push-ups.")]
        [SerializeField] private AnimationClip[] externalDrills = new AnimationClip[3];
        [Tooltip("Per drill: apply foot IK. On for AI-generated clips (they carry IK goal curves), off for hand-keyed ones.")]
        [SerializeField] private bool[] externalDrillFootIK = new bool[3];
        [Tooltip("Leave empty to use the built-in legacy font.")]
        [SerializeField] private Font font;

        private const float GainSeconds = 1.6f;
        private const float TierUpSeconds = 2.6f;
        // Drills turn the body side-on to the camera (which sits 30° to the character's left).
        private const float DrillFacing = -65f;

        private CultivationStage stage;
        private RectTransform root;
        private Text externalValue;
        private Text externalTier;
        private Text internalValue;
        private Text internalTier;
        private Button externalButton;
        private Button internalButton;
        private Text statusText;
        private Text gainText;
        private GameObject tierUpBanner;
        private Text tierUpText;
        private Image flash;
        private Coroutine show;

        private void OnEnable()
        {
            if (root == null)
                Build();
            stage.SetActive(true);
            gainText.gameObject.SetActive(false);
            tierUpBanner.SetActive(false);
            flash.color = Clear(flash.color);
            Refresh(CultivationStore.Load());
        }

        private void OnDisable()
        {
            // Leaving mid-show: the result is already saved, so just stop the presentation.
            if (show != null)
                StopCoroutine(show);
            show = null;
            stage?.SetActive(false);
        }

        private void OnDestroy() => stage?.Dispose();

        private void Update()
        {
            stage?.Tick(Time.deltaTime);
            if (flash != null && flash.color.a > 0f)
                flash.color = new Color(flash.color.r, flash.color.g, flash.color.b, Mathf.MoveTowards(flash.color.a, 0f, Time.deltaTime * 1.2f));
        }

        private void LateUpdate() => stage?.ApplyLock();

        private void TrainExternal() => StartTraining(external: true);

        private void TrainInternal() => StartTraining(external: false);

        private void StartTraining(bool external)
        {
            if (show != null)
                return;

            int today = CultivationStore.Today();
            CultivationStats before = CultivationStore.Load();
            // The buttons are disabled once trained; this guards against a stale click.
            if (CultivationRules.HasTrainedToday(before, today))
            {
                Refresh(before);
                return;
            }

            CultivationStats after = external
                ? CultivationRules.AddExternalPower(before, today)
                : CultivationRules.AddInternalPower(before, today);
            CultivationStore.Save(after);
            show = StartCoroutine(Show(external, before, after));
        }

        private IEnumerator Show(bool external, CultivationStats before, CultivationStats after)
        {
            externalButton.interactable = false;
            internalButton.interactable = false;
            statusText.text = external ? "외공을 단련하는 중…" : "내공을 운기하는 중…";
            // The gain is revealed after the show, so start from the pre-training numbers.
            SetStat(externalValue, externalTier, before.ExternalPower);
            SetStat(internalValue, internalTier, before.InternalPower);

            int tierBefore = CultivationRules.GetTier(external ? before.ExternalPower : before.InternalPower);
            if (external)
            {
                AnimationClip drill = externalDrills != null && externalDrills.Length >= tierBefore ? externalDrills[tierBefore - 1] : null;
                if (drill != null)
                {
                    bool done = false;
                    bool footIK = externalDrillFootIK != null && externalDrillFootIK.Length >= tierBefore && externalDrillFootIK[tierBefore - 1];
                    stage.PlayAction(drill, DrillFacing, footIK, showDummy: tierBefore == 2, () => done = true);
                    while (!done)
                        yield return null;
                    // Let the fade back to the seated idle finish before the numbers appear.
                    while (stage.IsPlayingAction)
                        yield return null;
                }
                else
                {
                    Debug.LogWarning($"[Cultivation] 외공 {tierBefore}단계 동작 클립이 지정되지 않았습니다.");
                    yield return new WaitForSeconds(1f);
                }
            }
            else
            {
                float length = stage.Qi.Play(tierBefore, () =>
                {
                    // Tier 3 bursts out of the preview: flash the whole screen gold and shake.
                    flash.color = new Color(InkTheme.Gold.r, InkTheme.Gold.g, InkTheme.Gold.b, 0.55f);
                    stage.Shake(1f);
                });
                yield return new WaitForSeconds(length);
            }

            string name = external ? "외공" : "내공";
            Refresh(after);
            gainText.text = $"{name} +{CultivationRules.DailyGain}";
            gainText.gameObject.SetActive(true);
            yield return FloatUp(gainText.rectTransform, GainSeconds);
            gainText.gameObject.SetActive(false);

            int tierAfter = CultivationRules.GetTier(external ? after.ExternalPower : after.InternalPower);
            if (tierAfter > tierBefore)
            {
                tierUpText.text = $"{name} 성취가 {tierAfter}단계에 이르렀습니다";
                tierUpBanner.SetActive(true);
                yield return new WaitForSeconds(TierUpSeconds);
                tierUpBanner.SetActive(false);
            }

            show = null;
        }

        private static IEnumerator FloatUp(RectTransform rt, float seconds)
        {
            Vector2 start = rt.anchoredPosition;
            var text = rt.GetComponent<Text>();
            Color color = text.color;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                rt.anchoredPosition = start + new Vector2(0f, 90f * Mathf.SmoothStep(0f, 1f, k));
                rt.localScale = Vector3.one * (k < 0.15f ? Mathf.Lerp(0.6f, 1.1f, k / 0.15f) : Mathf.Lerp(1.1f, 1f, (k - 0.15f) / 0.85f));
                text.color = new Color(color.r, color.g, color.b, k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f);
                yield return null;
            }
            rt.anchoredPosition = start;
            rt.localScale = Vector3.one;
            text.color = color;
        }

        private void Refresh(CultivationStats stats)
        {
            SetStat(externalValue, externalTier, stats.ExternalPower);
            SetStat(internalValue, internalTier, stats.InternalPower);

            bool trained = CultivationRules.HasTrainedToday(stats, CultivationStore.Today());
            externalButton.interactable = !trained;
            internalButton.interactable = !trained;
            statusText.text = trained
                ? "오늘의 수련을 마쳤습니다"
                : "하루 한 번, 외공과 내공 중 하나를 수련할 수 있습니다";
        }

        private static void SetStat(Text value, Text tier, int power)
        {
            int t = CultivationRules.GetTier(power);
            value.text = power.ToString();
            int next = t == 1 ? CultivationRules.Tier2Threshold : t == 2 ? CultivationRules.Tier3Threshold : -1;
            tier.text = next < 0 ? $"{t}단계 · 대성" : $"{t}단계 · 다음 단계까지 {next - power}";
        }

        private static Color Clear(Color c) => new Color(c.r, c.g, c.b, 0f);

        // ---- UI construction -------------------------------------------------------------

        private void Build()
        {
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            root = (RectTransform)transform;
            stage = new CultivationStage(characterPrefab, meditationClip, InkTheme.Charcoal, InkTheme.Hex(0x3A3630));

            Image background = MakeImage(root, "Background", InkTheme.Ink);
            Stretch(background.rectTransform);

            Button home = MakeButton(root, "〈 홈으로", InkButtonKind.Chip, 30, () => flow.GoTo(GameScreen.Home));
            Place((RectTransform)home.transform, new Vector2(0f, 1f), new Vector2(150f, -200f), new Vector2(220f, 84f));

            Text title = MakeText(root, "Title", "단련", 56, InkTheme.TextLight, TextAnchor.MiddleCenter);
            InkStyle.StyleTitle(title, 56, InkTheme.TextLight, 14f);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(400f, 90f));

            var preview = new GameObject("Stage", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            preview.transform.SetParent(root, false);
            preview.texture = stage.Texture;
            preview.raycastTarget = false;
            Place(preview.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -720f), new Vector2(880f, 880f));
            InkStyle.EnsureBorder(preview.rectTransform, InkTheme.Gold, -4f);

            BuildStatPanel(new Vector2(-235f, -1290f), "외공", "外功 · 몸을 단련한 힘", out externalValue, out externalTier);
            BuildStatPanel(new Vector2(235f, -1290f), "내공", "內功 · 쌓아 올린 기", out internalValue, out internalTier);

            externalButton = MakeButton(root, "외공 수련", InkButtonKind.Primary, 40, TrainExternal);
            Place((RectTransform)externalButton.transform, new Vector2(0.5f, 1f), new Vector2(-235f, -1500f), new Vector2(440f, 130f));
            internalButton = MakeButton(root, "내공 수련", InkButtonKind.Primary, 40, TrainInternal);
            Place((RectTransform)internalButton.transform, new Vector2(0.5f, 1f), new Vector2(235f, -1500f), new Vector2(440f, 130f));

            statusText = MakeText(root, "Status", "", 30, InkTheme.TextMuted, TextAnchor.MiddleCenter);
            Place(statusText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -1625f), new Vector2(1000f, 60f));

            gainText = MakeText(root, "Gain", "", 84, InkTheme.Gold, TextAnchor.MiddleCenter);
            InkStyle.StyleTitle(gainText, 84, InkTheme.Gold, 6f);
            gainText.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            Place(gainText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -620f), new Vector2(900f, 140f));

            Image banner = MakeImage(root, "TierUp", Color.white);
            InkStyle.StylePanel(banner, InkPanelKind.Hanji);
            // Low in the stage view, below the seated figure's head and chest.
            Place(banner.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -1060f), new Vector2(860f, 130f));
            tierUpText = MakeText(banner.rectTransform, "Text", "", 40, InkTheme.TextDark, TextAnchor.MiddleCenter);
            InkStyle.StyleTitle(tierUpText, 40, InkTheme.TextDark, 4f);
            Stretch(tierUpText.rectTransform);
            tierUpBanner = banner.gameObject;

            flash = MakeImage(root, "Flash", Clear(InkTheme.Gold));
            flash.raycastTarget = false;
            Stretch(flash.rectTransform);
        }

        private void BuildStatPanel(Vector2 position, string name, string subtitle, out Text value, out Text tier)
        {
            Image panel = MakeImage(root, name, Color.white);
            InkStyle.StylePanel(panel, InkPanelKind.Dark);
            Place(panel.rectTransform, new Vector2(0.5f, 1f), position, new Vector2(440f, 230f));

            Text label = MakeText(panel.rectTransform, "Name", name, 36, InkTheme.Gold, TextAnchor.MiddleCenter);
            InkStyle.StyleTitle(label, 36, InkTheme.Gold, 10f);
            Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -42f), new Vector2(400f, 50f));

            Text sub = MakeText(panel.rectTransform, "Subtitle", subtitle, 22, InkTheme.TextMuted, TextAnchor.MiddleCenter);
            Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(400f, 30f));

            value = MakeText(panel.rectTransform, "Value", "0", 60, InkTheme.TextLight, TextAnchor.MiddleCenter);
            InkStyle.StyleTitle(value, 60, InkTheme.TextLight, 2f);
            Place(value.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -136f), new Vector2(400f, 70f));

            tier = MakeText(panel.rectTransform, "Tier", "", 26, InkTheme.TextMuted, TextAnchor.MiddleCenter);
            Place(tier.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -196f), new Vector2(400f, 36f));
        }

        private Button MakeButton(RectTransform parent, string label, InkButtonKind kind, int fontSize, UnityEngine.Events.UnityAction onClick)
        {
            Image image = MakeImage(parent, "Button", Color.white);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Text text = MakeText(image.rectTransform, "Text", label, fontSize, InkTheme.TextLight, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            InkStyle.StyleButton(button, kind);
            button.onClick.AddListener(onClick);
            return button;
        }

        private static Image MakeImage(RectTransform parent, string name, Color color)
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
