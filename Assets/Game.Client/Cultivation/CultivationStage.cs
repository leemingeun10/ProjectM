using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Game.Client.Cultivation
{
    // The 3D part of the 단련 screen: a private rig far from the battle stage with its own copy of
    // the character, a camera rendering into a RenderTexture (shown by a RawImage), a floor, the
    // 목각인형 prop and the qi effect. The Animator is driven by a PlayableGraph (no controller
    // asset): a looping seated idle, and one-shot drills that cross-fade in and back out to it.
    public class CultivationStage
    {
        private static readonly Vector3 Origin = new Vector3(-300f, 0f, 300f);
        private const float FadeSeconds = 0.35f;
        // Hips-to-post distance for the 목각인형; a straight punch just reaches the post.
        private const float DummyDistance = 0.65f;

        private readonly GameObject root;
        private readonly Camera camera;
        private readonly Transform character;
        private readonly Transform hips;
        private readonly PlayableGraph graph;
        private readonly AnimationMixerPlayable mixer;
        private readonly AnimationClipPlayable idle;
        private readonly GameObject dummy;
        // Close framing for the seated figure; drills (a handstand reaches ~1.9 m) pull back.
        private readonly Vector3 seatedFocus = Origin + new Vector3(0f, 0.55f, 0.1f);
        private readonly Vector3 drillFocus = Origin + new Vector3(0f, 0.9f, 0.1f);
        private static readonly Quaternion CameraYaw = Quaternion.Euler(0f, 30f, 0f);
        private static readonly Vector3 SeatedOffset = new Vector3(0f, 0.5f, 3.1f);
        private static readonly Vector3 DrillOffset = new Vector3(0f, 0.35f, 4.1f);

        private AnimationClipPlayable action;
        private AnimationClip actionClip;
        private Action actionDone;
        private bool actionActive;
        private float actionWeight;
        // The seated idle faces the camera; drills turn the body side-on (blended with the fade)
        // so push-ups and strikes read in profile instead of foreshortened.
        private Quaternion actionFacing = Quaternion.identity;
        private float shake;

        public RenderTexture Texture { get; }
        public QiAuraEffect Qi { get; }
        public bool IsPlayingAction => actionActive || actionWeight > 0f;

        public CultivationStage(GameObject characterPrefab, AnimationClip idleClip, Color background, Color floorColor)
        {
            root = new GameObject("CultivationStage");
            root.transform.position = Origin;

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "Floor";
            UnityEngine.Object.Destroy(floor.GetComponent<Collider>());
            floor.transform.SetParent(root.transform, false);
            floor.transform.localPosition = new Vector3(0f, -0.02f, 0f);
            floor.transform.localScale = new Vector3(3.2f, 0.02f, 3.2f);
            floor.GetComponent<Renderer>().material.color = floorColor;

            dummy = WoodenDummy.Create(root.transform, new Vector3(0f, 0f, DummyDistance));
            dummy.SetActive(false);

            GameObject characterGo = UnityEngine.Object.Instantiate(characterPrefab, Origin, Quaternion.identity, root.transform);
            character = characterGo.transform;
            Animator animator = characterGo.GetComponent<Animator>();
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            // Qi plays only while seated, so it sits at the seated hips height on the upright root.
            Qi = QiAuraEffect.Create(root.transform, new Vector3(0f, 0.45f, 0f));

            Texture = new RenderTexture(1024, 1024, 24) { antiAliasing = 4, name = "CultivationRT" };
            var camGo = new GameObject("CultivationCamera");
            camGo.transform.SetParent(root.transform, false);
            camera = camGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.fieldOfView = 36f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 30f;
            camera.targetTexture = Texture;
            PlaceCamera(0f, Vector3.zero);

            graph = PlayableGraph.Create("Cultivation");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(graph, "Animation", animator);
            mixer = AnimationMixerPlayable.Create(graph, 2);
            output.SetSourcePlayable(mixer);
            idle = AnimationClipPlayable.Create(graph, idleClip);
            graph.Connect(idle, 0, mixer, 0);
            mixer.SetInputWeight(0, 1f);

            SetActive(false);
        }

        public void SetActive(bool active)
        {
            // On scene unload (e.g. leaving Play mode) the rig can be destroyed before the screen's OnDisable.
            if (root == null)
                return;
            root.SetActive(active);
            if (active)
            {
                graph.Play();
            }
            else
            {
                graph.Stop();
                CancelAction();
            }
        }

        // Plays a one-shot drill, then fades back to the seated idle and calls onDone. facingYaw
        // turns the body for the drill (degrees, + = clockwise seen from above); the dummy, when
        // shown, stands in front of the turned body. footIK must be false for clips without IK
        // goal curves (the hand-keyed drills): the missing goals pull both feet to the body centre.
        public void PlayAction(AnimationClip clip, float facingYaw, bool footIK, bool showDummy, Action onDone)
        {
            CancelAction();
            action = AnimationClipPlayable.Create(graph, clip);
            action.SetApplyFootIK(footIK);
            graph.Connect(action, 0, mixer, 1);
            actionClip = clip;
            actionDone = onDone;
            actionActive = true;
            actionWeight = 0f;
            actionFacing = Quaternion.Euler(0f, facingYaw, 0f);
            dummy.transform.localPosition = actionFacing * new Vector3(0f, 0f, DummyDistance);
            dummy.transform.localRotation = actionFacing;
            dummy.SetActive(showDummy);
        }

        // Brief camera shake, e.g. on the tier-3 qi burst.
        public void Shake(float strength) => shake = Mathf.Max(shake, strength);

        public void Tick(float deltaTime)
        {
            if (action.IsValid())
            {
                if (actionActive && action.GetTime() >= actionClip.length)
                {
                    actionActive = false;
                    dummy.SetActive(false);
                    Action done = actionDone;
                    actionDone = null;
                    done?.Invoke();
                }

                actionWeight = Mathf.MoveTowards(actionWeight, actionActive ? 1f : 0f, deltaTime / FadeSeconds);
                mixer.SetInputWeight(0, 1f - actionWeight);
                mixer.SetInputWeight(1, actionWeight);

                if (!actionActive && actionWeight <= 0f)
                    DisconnectAction();
            }

            shake = Mathf.MoveTowards(shake, 0f, deltaTime * 1.5f);
            PlaceCamera(Mathf.SmoothStep(0f, 1f, actionWeight), shake > 0f ? UnityEngine.Random.insideUnitSphere * shake * 0.08f : Vector3.zero);
        }

        // Three-quarter front view; t blends from the seated framing (0) to the drill framing (1).
        private void PlaceCamera(float t, Vector3 jitter)
        {
            Vector3 focus = Vector3.Lerp(seatedFocus, drillFocus, t);
            camera.transform.position = focus + CameraYaw * Vector3.Lerp(SeatedOffset, DrillOffset, t) + jitter;
            camera.transform.LookAt(focus);
        }

        // Call from LateUpdate: keeps the hips over the mat so drills that travel (the clips have
        // root motion baked into the pose) stay in frame. Display only.
        public void ApplyLock()
        {
            float w = Mathf.SmoothStep(0f, 1f, actionWeight);
            character.rotation = Quaternion.Slerp(Quaternion.identity, actionFacing, w);
            if (hips == null)
                return;
            Vector3 drift = hips.position - character.position;
            drift.y = 0f;
            character.position = Origin - drift * actionWeight;
        }

        public void Dispose()
        {
            if (graph.IsValid())
                graph.Destroy();
            if (camera != null)
                camera.targetTexture = null;
            if (Texture != null)
            {
                Texture.Release();
                UnityEngine.Object.Destroy(Texture);
            }
            if (root != null)
                UnityEngine.Object.Destroy(root);
        }

        private void CancelAction()
        {
            actionActive = false;
            actionDone = null;
            actionWeight = 0f;
            if (dummy != null)
                dummy.SetActive(false);
            if (action.IsValid())
                DisconnectAction();
            mixer.SetInputWeight(0, 1f);
            if (Qi != null)
                Qi.Stop();
        }

        private void DisconnectAction()
        {
            graph.Disconnect(mixer, 1);
            action.Destroy();
            action = default;
            mixer.SetInputWeight(0, 1f);
        }
    }
}
