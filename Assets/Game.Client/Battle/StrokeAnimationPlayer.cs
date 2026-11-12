using System.Collections.Generic;
using Game.Client.Stroke;
using Game.Client.Technique;
using Game.Sim.Battle;
using Game.Sim.Stroke;
using UnityEngine;

namespace Game.Client.Battle
{
    // Drawing preview: plays the motion a completed stroke would fight with (CombatClipSelector) on character A.
    // A new stroke mid-swing restarts playback from the new clip.
    //
    // When the counter toggle is on, plays the counter move (as if it landed). This is only a
    // preview of the motion for a stroke drawn in counter mode, NOT a counter success/failure judgement.
    // Battle playback is driven separately by BattleReplayController.
    public class StrokeAnimationPlayer : MonoBehaviour
    {
        [SerializeField] private StrokeInputController strokeInput;
        [SerializeField] private CharacterMotionPlayer motionPlayer;
        [SerializeField] private TechniqueSequenceController sequenceController;

        // Fail once, loudly, on unwired inspector fields instead of throwing on every stroke.
        // Disabling in Awake means OnEnable/OnDisable never run for this component.
        private void Awake()
        {
            var missing = new List<string>();
            if (strokeInput == null) missing.Add(nameof(strokeInput));
            if (motionPlayer == null) missing.Add(nameof(motionPlayer));
            if (sequenceController == null) missing.Add(nameof(sequenceController));

            if (missing.Count == 0)
                return;

            Debug.LogError($"[StrokeAnimationPlayer] '{name}' has unassigned field(s): {string.Join(", ", missing)}. Component disabled.", this);
            enabled = false;
        }

        private void OnEnable()
        {
            strokeInput.StrokeCompleted += OnStrokeCompleted;
        }

        private void OnDisable()
        {
            strokeInput.StrokeCompleted -= OnStrokeCompleted;
        }

        private void OnStrokeCompleted(StrokeResult result, IReadOnlyList<StrokePoint> points)
        {
            // A stroke drawn in counter mode fights as Counter whatever its shape.
            BattleStepType type = sequenceController.IsCounterOn ? BattleStepType.Counter
                : result.Type == StrokeType.Thrust ? BattleStepType.Thrust : BattleStepType.Slash;
            motionPlayer.Play(CombatClipSelector.StateFor(type, points, counterLanded: true));
        }
    }
}
