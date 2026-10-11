using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Ui
{
    // Keeps a button's or toggle's label (and optional border) in step with its interactable state, so a
    // locked button reads as locked by palette rather than by ad-hoc grey text in each controller.
    // The button's own ColorTint handles the fill; this handles what the tint cannot reach.
    public class InkButtonLabel : MonoBehaviour
    {
        [SerializeField] private Selectable button;
        [SerializeField] private Graphic label;
        [SerializeField] private Graphic border;
        [SerializeField] private Color labelColor = Color.white;
        [SerializeField] private Color borderColor = Color.white;

        private bool? shownInteractable;

        public void Configure(Selectable target, Graphic targetLabel, Graphic targetBorder, Color enabledLabel, Color enabledBorder)
        {
            button = target;
            label = targetLabel;
            border = targetBorder;
            labelColor = enabledLabel;
            borderColor = enabledBorder;
            shownInteractable = null;
            Apply();
        }

        // Called when the enabled colours change at runtime (e.g. a selected slot).
        public void SetLabelColor(Color color)
        {
            labelColor = color;
            shownInteractable = null;
        }

        private void OnEnable() => Apply();

        private void LateUpdate() => Apply();

        private void Apply()
        {
            if (button == null)
                return;
            bool interactable = button.IsInteractable();
            if (shownInteractable == interactable)
                return;
            shownInteractable = interactable;

            if (label != null)
                label.color = interactable ? labelColor : InkTheme.DisabledText;
            if (border != null)
                border.color = interactable ? borderColor : InkTheme.DisabledText * new Color(1f, 1f, 1f, 0.5f);
        }
    }
}
