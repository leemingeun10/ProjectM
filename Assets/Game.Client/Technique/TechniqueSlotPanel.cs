using System.Collections.Generic;
using Game.Sim.Technique;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Technique
{
    // Drawing-screen slot bar: picks which of the 5 technique slots is being edited, loads its
    // saved steps into the builder, saves the builder back to it, and deletes slots.
    public class TechniqueSlotPanel : MonoBehaviour
    {
        [SerializeField] private TechniqueSequenceController sequenceController;
        [SerializeField] private Button saveButton;
        [SerializeField] private Text saveButtonLabel;
        // One entry per slot (index 0 = slot 1); each select button needs a Text child.
        [SerializeField] private Button[] slotButtons = new Button[TechniqueSlotStore.SlotCount];
        [SerializeField] private Button[] deleteButtons = new Button[TechniqueSlotStore.SlotCount];
        [SerializeField] private Color selectedSlotColor = new Color(1f, 0.85f, 0.4f);

        private Color normalSlotColor;
        private int selectedSlot = 1;

        private void Awake()
        {
            normalSlotColor = slotButtons[0].image.color;
            for (int i = 0; i < slotButtons.Length; i++)
            {
                int slot = i + 1;
                slotButtons[i].onClick.AddListener(() => SelectSlot(slot));
                deleteButtons[i].onClick.AddListener(() => DeleteSlot(slot));
            }
        }

        private void Start()
        {
            SelectSlot(selectedSlot);
        }

        private void OnEnable()
        {
            saveButton.onClick.AddListener(Save);
        }

        private void OnDisable()
        {
            saveButton.onClick.RemoveListener(Save);
        }

        // Replaces whatever is being drawn with the slot's saved steps (or an empty sequence).
        private void SelectSlot(int slot)
        {
            selectedSlot = slot;
            if (TechniqueSlotStore.TryLoad(slot, out List<TechniqueStep> steps))
            {
                sequenceController.LoadSteps(steps);
                Debug.Log($"[TechniqueSlot] 슬롯 {slot} 선택: 저장된 {steps.Count}턴 불러옴");
            }
            else
            {
                sequenceController.ResetSequence();
                Debug.Log($"[TechniqueSlot] 슬롯 {slot} 선택: 비어있음");
            }
            RefreshUi();
        }

        private void Save()
        {
            TechniqueBuilder builder = sequenceController.Builder;
            if (builder.TurnCount == 0)
            {
                Debug.LogWarning($"[TechniqueSlot] 그린 턴이 없어 슬롯 {selectedSlot}에 저장하지 않았습니다.");
                return;
            }

            TechniqueSlotStore.Save(selectedSlot, builder);
            sequenceController.ResetSequence();
            RefreshUi();
        }

        private void DeleteSlot(int slot)
        {
            TechniqueSlotStore.Delete(slot);
            // Keep the screen in sync with the slot being edited, which is now empty.
            if (slot == selectedSlot)
                sequenceController.ResetSequence();
            RefreshUi();
        }

        private void RefreshUi()
        {
            for (int i = 0; i < slotButtons.Length; i++)
            {
                int slot = i + 1;
                TechniqueSlotStatus status = TechniqueSlotStore.GetStatus(slot);
                slotButtons[i].GetComponentInChildren<Text>(true).text = $"슬롯 {slot}\n<size=22>{status.Describe()}</size>";
                slotButtons[i].image.color = slot == selectedSlot ? selectedSlotColor : normalSlotColor;
                deleteButtons[i].interactable = status.State != TechniqueSlotState.Empty;
            }
            saveButtonLabel.text = $"슬롯 {selectedSlot} 저장";
        }
    }
}
