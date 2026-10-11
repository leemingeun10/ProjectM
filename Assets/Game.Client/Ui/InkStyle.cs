using Game.Client.Stroke;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Ui
{
    public enum InkButtonKind
    {
        // Vermilion fill: the one main action on a screen.
        Primary,
        // Dark fill with a thin gold inner frame: navigation and secondary actions.
        Secondary,
        // Hanji paper card with dark text: list items such as stages.
        Card,
        // Small dark chip with a gold frame: "홈으로", "삭제" and other utility buttons.
        Chip,
    }

    public enum InkPanelKind
    {
        // Translucent ink panel with a gold inner frame.
        Dark,
        // Hanji paper panel with an ink inner frame.
        Hanji,
    }

    // Applies the ink-wash skin to legacy uGUI widgets. Fill colours go through the button's
    // ColorBlock (image colour stays white) so hover/press/disabled states are always distinct;
    // InkButtonLabel handles the label and frame for the disabled state.
    public static class InkStyle
    {
        public const string BorderName = "InkBorder";
        private const float BorderInset = 6f;
        private const float BorderThickness = 2f;

        public static void StyleButton(Button button, InkButtonKind kind)
        {
            Color fill, labelColor, borderColor;
            switch (kind)
            {
                case InkButtonKind.Primary:
                    fill = InkTheme.Vermilion;
                    labelColor = InkTheme.TextLight;
                    borderColor = new Color(1f, 0.9f, 0.75f, 0.35f);
                    break;
                case InkButtonKind.Card:
                    fill = InkTheme.Hanji;
                    labelColor = InkTheme.TextDark;
                    borderColor = InkTheme.Gold;
                    break;
                case InkButtonKind.Chip:
                    fill = InkTheme.InkPanel;
                    labelColor = InkTheme.TextLight;
                    borderColor = InkTheme.GoldDim;
                    break;
                default:
                    fill = InkTheme.InkPanelLight;
                    labelColor = InkTheme.TextLight;
                    borderColor = InkTheme.Gold;
                    break;
            }

            var image = button.targetGraphic as Image;
            if (image != null)
            {
                image.sprite = null;
                image.color = Color.white;
            }
            button.transition = Selectable.Transition.ColorTint;
            SetFill(button, fill);

            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.fontStyle = FontStyle.Bold;
                label.color = labelColor;
                label.supportRichText = true;
                EnsureSpacing(label, kind == InkButtonKind.Chip ? 2f : 5f);
            }

            UIRectOutline border = EnsureBorder(button.GetComponent<RectTransform>(), borderColor, kind == InkButtonKind.Chip ? 4f : BorderInset);

            InkButtonLabel stateLabel = button.GetComponent<InkButtonLabel>();
            if (stateLabel == null)
                stateLabel = button.gameObject.AddComponent<InkButtonLabel>();
            stateLabel.Configure(button, label, border, labelColor, borderColor);
        }

        // Changes the fill of an already-styled button, keeping hover/press/disabled distinct.
        public static void SetFill(Selectable button, Color fill)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = fill;
            colors.selectedColor = fill;
            colors.highlightedColor = Shade(fill, 1.12f);
            colors.pressedColor = Shade(fill, 0.68f);
            colors.disabledColor = InkTheme.DisabledFill;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }

        public static void StylePanel(Image panel, InkPanelKind kind)
        {
            panel.sprite = null;
            panel.color = kind == InkPanelKind.Hanji ? InkTheme.Hanji : InkTheme.InkPanel;
            EnsureBorder(panel.rectTransform, kind == InkPanelKind.Hanji ? InkTheme.TextDark * new Color(1f, 1f, 1f, 0.6f) : InkTheme.GoldDim, BorderInset);
        }

        public static void StyleTitle(Text text, int size, Color color, float spacing = 10f)
        {
            text.fontStyle = FontStyle.Bold;
            text.fontSize = size;
            text.color = color;
            text.supportRichText = true;
            EnsureSpacing(text, spacing);
        }

        public static void StyleBody(Text text, Color color, bool bold = false)
        {
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.color = color;
            text.supportRichText = true;
        }

        public static LetterSpacing EnsureSpacing(Text text, float spacing)
        {
            LetterSpacing effect = text.GetComponent<LetterSpacing>();
            if (effect == null)
                effect = text.gameObject.AddComponent<LetterSpacing>();
            effect.Spacing = spacing;
            return effect;
        }

        // A thin frame drawn just inside the rect, as the first child so content draws over it.
        public static UIRectOutline EnsureBorder(RectTransform target, Color color, float inset)
        {
            Transform existing = target.Find(BorderName);
            UIRectOutline outline;
            if (existing != null)
            {
                outline = existing.GetComponent<UIRectOutline>();
            }
            else
            {
                var go = new GameObject(BorderName, typeof(RectTransform), typeof(CanvasRenderer), typeof(UIRectOutline));
                go.transform.SetParent(target, false);
                go.transform.SetAsFirstSibling();
                outline = go.GetComponent<UIRectOutline>();
                // Layout groups on the target must not treat the frame as a child to arrange.
                go.AddComponent<LayoutElement>().ignoreLayout = true;
            }

            var rt = (RectTransform)outline.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            outline.raycastTarget = false;
            outline.color = color;
            outline.Thickness = BorderThickness;
            return outline;
        }

        private static Color Shade(Color c, float factor)
            => new Color(Mathf.Clamp01(c.r * factor), Mathf.Clamp01(c.g * factor), Mathf.Clamp01(c.b * factor), c.a);
    }
}
