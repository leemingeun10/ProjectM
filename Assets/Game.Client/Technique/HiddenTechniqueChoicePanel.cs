using System;
using Game.Client.Ui;
using Game.Sim.Technique;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Technique
{
    // Drawing-screen panel offered when a pending stroke is recognised as a hidden technique:
    // "삼재검법 (내공 5)", the remaining 내공, one button per move and a cancel button. Picking a move
    // highlights it and raises MoveChosen; cancel raises Cancelled. TechniqueSequenceController
    // decides when it opens and what saving does with the choice.
    public class HiddenTechniqueChoicePanel : MonoBehaviour
    {
        [SerializeField] private Text titleText;
        [SerializeField] private Text powerText;
        [SerializeField] private Button horizontalButton;
        [SerializeField] private Button verticalButton;
        [SerializeField] private Button thrustButton;
        [SerializeField] private Button cancelButton;

        private Color normalFill;
        private bool initialized;

        public event Action<HiddenTechniqueMove> MoveChosen;
        public event Action Cancelled;

        public bool IsOpen => gameObject.activeSelf;
        public HiddenTechniqueMove Selected { get; private set; }

        // The panel starts inactive, so wiring happens on first use rather than in Awake.
        private void EnsureInitialized()
        {
            if (initialized)
                return;
            initialized = true;
            normalFill = horizontalButton.colors.normalColor;
            horizontalButton.onClick.AddListener(() => Choose(HiddenTechniqueMove.HorizontalSlash));
            verticalButton.onClick.AddListener(() => Choose(HiddenTechniqueMove.VerticalSlash));
            thrustButton.onClick.AddListener(() => Choose(HiddenTechniqueMove.Thrust));
            cancelButton.onClick.AddListener(() =>
            {
                Close();
                Cancelled?.Invoke();
            });
        }

        public void Open(string techniqueId, int cost, int remainingInternalPower)
        {
            EnsureInitialized();
            Selected = HiddenTechniqueMove.None;
            // Two lines: the panel is a narrow column beside the preview character.
            titleText.text = $"{HiddenTechniqueNames.Of(techniqueId)}\n(내공 {cost})";
            powerText.text = $"남은 내공 {remainingInternalPower}";
            RefreshHighlight();
            gameObject.SetActive(true);
        }

        public void Close()
        {
            Selected = HiddenTechniqueMove.None;
            gameObject.SetActive(false);
        }

        private void Choose(HiddenTechniqueMove move)
        {
            Selected = move;
            RefreshHighlight();
            MoveChosen?.Invoke(move);
        }

        private void RefreshHighlight()
        {
            InkStyle.SetFill(horizontalButton, Selected == HiddenTechniqueMove.HorizontalSlash ? InkTheme.Vermilion : normalFill);
            InkStyle.SetFill(verticalButton, Selected == HiddenTechniqueMove.VerticalSlash ? InkTheme.Vermilion : normalFill);
            InkStyle.SetFill(thrustButton, Selected == HiddenTechniqueMove.Thrust ? InkTheme.Vermilion : normalFill);
        }
    }
}
