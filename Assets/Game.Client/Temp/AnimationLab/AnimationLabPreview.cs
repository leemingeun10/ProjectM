using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Game.Client.Temp.AnimationLab
{
    // TEMPORARY (animation test page): a private preview rig far away from the battle stage.
    // Spawns its own copy of the character, its own camera rendering into a RenderTexture and a
    // floor grid, and drives the Animator through a PlayableGraph so no AnimatorController asset
    // is needed. Clips cross-fade, so what is judged here is the clip itself plus a normal blend.
    //
    // LockInPlace: the AI clips are Humanoid with "Bake Into Pose (XZ)" on, so their forward
    // travel is part of the body pose and applyRootMotion = false does not stop it. When locked,
    // the character is shifted back each frame after the pose is evaluated so the hips stay over
    // the origin (feet may slide, like an in-place clip). Display only: clip assets are untouched,
    // and unlocking puts the character straight back at the origin with the original motion.
    public class AnimationLabPreview
    {
        private static readonly Vector3 Origin = new Vector3(300f, 0f, 300f);
        private const float FadeSeconds = 0.15f;
        private const float RepeatIdleSeconds = 0.5f;
        private const float CameraDistance = 4.2f;

        public enum View { Side, Front, ThreeQuarter, Back }

        private readonly GameObject root;
        private readonly Camera camera;
        private readonly Animator animator;
        private readonly Transform character;
        private readonly Transform hips;
        private readonly PlayableGraph graph;
        private readonly AnimationMixerPlayable mixer;
        private readonly List<AnimationClipPlayable> playables = new List<AnimationClipPlayable>();
        private readonly List<AnimationClip> clips = new List<AnimationClip>();
        private readonly float[] weights;
        private readonly TrailRenderer trail;

        private int current;
        private float repeatWait = -1f;
        private int pendingRepeat;

        public RenderTexture Texture { get; }
        public float Speed { get; private set; } = 1f;
        public bool Paused { get; private set; }
        public bool Repeat { get; set; }
        public View CurrentView { get; private set; } = View.Side;
        public bool LockInPlace { get; set; } = true;
        // Horizontal hips offset from the origin that the lock removed this frame (metres).
        public float LockedDrift { get; private set; }
        public AnimationClip CurrentClip => clips[current];
        public int CurrentIndex => current;
        public float CurrentTime => (float)playables[current].GetTime();

        // clipList[0] must be the idle clip: finished one-shot clips fade back to it.
        public AnimationLabPreview(GameObject characterPrefab, IList<AnimationClip> clipList)
        {
            root = new GameObject("AnimationLabPreview");
            root.transform.position = Origin;

            BuildFloor(root.transform);

            GameObject characterGo = Object.Instantiate(characterPrefab, Origin, Quaternion.identity, root.transform);
            character = characterGo.transform;
            animator = characterGo.GetComponent<Animator>();
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            trail = AttachSword(animator);

            Texture = new RenderTexture(1024, 1024, 24) { antiAliasing = 4, name = "AnimationLabRT" };
            var camGo = new GameObject("AnimationLabCamera");
            camGo.transform.SetParent(root.transform, false);
            camera = camGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.86f, 0.83f, 0.76f);
            camera.fieldOfView = 34f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 30f;
            camera.targetTexture = Texture;
            SetView(View.Side);

            graph = PlayableGraph.Create("AnimationLab");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(graph, "Animation", animator);
            mixer = AnimationMixerPlayable.Create(graph, clipList.Count);
            output.SetSourcePlayable(mixer);

            weights = new float[clipList.Count];
            for (int i = 0; i < clipList.Count; i++)
            {
                AnimationClip clip = clipList[i];
                clips.Add(clip);
                AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clip);
                playables.Add(playable);
                graph.Connect(playable, 0, mixer, i);
            }

            weights[0] = 1f;
            ApplyWeights();
            SetActive(false);
        }

        public void SetActive(bool active)
        {
            root.SetActive(active);
            if (active)
            {
                graph.Play();
                PlayIndex(0);
            }
            else
            {
                graph.Stop();
            }
        }

        public void PlayIndex(int index)
        {
            current = index;
            repeatWait = -1f;
            playables[index].SetTime(0);
            playables[index].SetTime(0);
            if (trail != null)
                trail.Clear();
        }

        public void SetSpeed(float speed)
        {
            Speed = speed;
            ApplySpeed();
        }

        public void SetPaused(bool paused)
        {
            Paused = paused;
            ApplySpeed();
        }

        // Scrubbing jumps straight to the pose (no blend) so a single frame can be inspected.
        public void Scrub(float normalized)
        {
            float time = Mathf.Clamp01(normalized) * CurrentClip.length;
            playables[current].SetTime(time);
            for (int i = 0; i < weights.Length; i++)
                weights[i] = i == current ? 1f : 0f;
            ApplyWeights();
        }

        public void SetView(View view)
        {
            CurrentView = view;
            float yaw;
            switch (view)
            {
                case View.Front: yaw = 0f; break;
                case View.ThreeQuarter: yaw = 45f; break;
                case View.Back: yaw = 180f; break;
                default: yaw = 90f; break;
            }

            // The character faces +Z and lunges along it, so aim a little ahead of the feet.
            Vector3 focus = Origin + new Vector3(0f, 1.0f, 0.5f);
            Vector3 offset = Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0.25f, CameraDistance);
            camera.transform.position = focus + offset;
            camera.transform.LookAt(focus);
        }

        public void Tick(float deltaTime)
        {
            if (Paused)
                return;

            float scaled = deltaTime * Speed;
            AnimationClip clip = CurrentClip;
            bool oneShot = current != 0 && !clip.isLooping;

            if (oneShot && repeatWait < 0f && playables[current].GetTime() >= clip.length)
            {
                // Finished: hand back to idle, and in repeat mode replay after a short rest.
                int finished = current;
                current = 0;
                repeatWait = Repeat ? RepeatIdleSeconds : -1f;
                pendingRepeat = finished;
            }
            else if (current == 0 && repeatWait >= 0f)
            {
                repeatWait -= scaled;
                if (repeatWait < 0f && Repeat && pendingRepeat > 0)
                    PlayIndex(pendingRepeat);
            }

            float step = scaled / FadeSeconds;
            for (int i = 0; i < weights.Length; i++)
                weights[i] = Mathf.MoveTowards(weights[i], i == current ? 1f : 0f, step);
            ApplyWeights();
        }

        // Call from LateUpdate: the graph has evaluated this frame's pose by then.
        public void ApplyLock()
        {
            if (hips == null || !LockInPlace)
            {
                character.position = Origin;
                LockedDrift = 0f;
                return;
            }

            // The hips' offset from the character root depends only on the pose, not on where the
            // root currently is, so this cancels the drift without feedback from last frame's shift.
            Vector3 drift = hips.position - character.position;
            drift.y = 0f;
            character.position = Origin - drift;
            LockedDrift = drift.magnitude;
        }

        public void Dispose()
        {
            if (graph.IsValid())
                graph.Destroy();
            // On scene teardown (e.g. leaving Play mode) the rig may already be destroyed.
            if (camera != null)
                camera.targetTexture = null;
            if (Texture != null)
            {
                Texture.Release();
                Object.Destroy(Texture);
            }
            if (root != null)
            {
                // Destroy is deferred to the end of the frame; hide the rig now so a replacement
                // preview built in the same frame (character switch) does not overlap it.
                root.SetActive(false);
                Object.Destroy(root);
            }
        }

        private void ApplySpeed()
        {
            float speed = Paused ? 0f : Speed;
            foreach (AnimationClipPlayable playable in playables)
                playable.SetSpeed(speed);
        }

        // Normalised so the blend never dips the pose toward the T-pose mid-fade.
        private void ApplyWeights()
        {
            float sum = 0f;
            foreach (float w in weights)
                sum += w;
            for (int i = 0; i < weights.Length; i++)
                mixer.SetInputWeight(i, sum > 0f ? weights[i] / sum : (i == current ? 1f : 0f));
        }

        // Same placeholder blade as the battle characters (TempSword), plus a short trail so the
        // swing arc is readable. Placed in the character's frame while it is still in its bind
        // T-pose (called right after Instantiate), not in the hand bone's local axes: those differ
        // between rigs (e.g. the AI-generated characters), while the T-pose frame does not. For
        // Character_TPose this gives exactly the old hand-local placement (blade pointing forward,
        // grip 0.0767 m past the wrist, 0.8 m long).
        private static TrailRenderer AttachSword(Animator animator)
        {
            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (hand == null)
                return null;

            Transform body = animator.transform;
            GameObject sword = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sword.name = "LabSword";
            Object.Destroy(sword.GetComponent<Collider>());
            sword.transform.SetPositionAndRotation(
                hand.position + body.right * 0.0767f + body.forward * 0.4f,
                Quaternion.LookRotation(body.forward, body.up));
            sword.transform.SetParent(hand, true);
            float handScale = hand.lossyScale.x;
            sword.transform.localScale = new Vector3(0.05f, 0.05f, 0.8f) / handScale;
            sword.GetComponent<Renderer>().material.color = new Color(0.62f, 0.6f, 0.58f);

            var tip = new GameObject("LabSwordTip");
            tip.transform.SetParent(sword.transform, false);
            tip.transform.localPosition = new Vector3(0f, 0f, 0.5f);
            TrailRenderer trail = tip.AddComponent<TrailRenderer>();
            trail.time = 0.22f;
            trail.minVertexDistance = 0.02f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.07f), new Keyframe(1f, 0f));
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
                trail.material = new Material(shader);
            trail.startColor = new Color(0.1f, 0.1f, 0.1f, 0.7f);
            trail.endColor = new Color(0.1f, 0.1f, 0.1f, 0f);
            return trail;
        }

        // A floor with 0.5 m lines, so foot sliding and lunge distance are visible.
        private static void BuildFloor(Transform parent)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "LabFloor";
            Object.Destroy(floor.GetComponent<Collider>());
            floor.transform.SetParent(parent, false);
            floor.transform.localPosition = new Vector3(0f, -0.02f, 0.5f);
            floor.transform.localScale = new Vector3(6f, 0.02f, 6f);
            floor.GetComponent<Renderer>().material.color = new Color(0.72f, 0.67f, 0.58f);

            for (int i = -4; i <= 4; i++)
            {
                AddLine(parent, new Vector3(i * 0.5f, 0f, 0.5f), new Vector3(0.012f, 0.004f, 4.4f));
                AddLine(parent, new Vector3(0f, 0f, 0.5f + i * 0.5f), new Vector3(4.4f, 0.004f, 0.012f));
            }
        }

        private static void AddLine(Transform parent, Vector3 position, Vector3 scale)
        {
            GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = "LabFloorLine";
            Object.Destroy(line.GetComponent<Collider>());
            line.transform.SetParent(parent, false);
            line.transform.localPosition = position;
            line.transform.localScale = scale;
            line.GetComponent<Renderer>().material.color = new Color(0.45f, 0.41f, 0.35f);
        }
    }
}
