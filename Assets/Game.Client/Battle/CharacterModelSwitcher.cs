using System;
using System.Collections.Generic;
using Game.Client.Flow;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Game.Client.Battle
{
    // Dresses a fighter in one of several models (EnemyLook). The model built into this object
    // (its own Animator, skinned meshes and the sword under its right hand) is Default; any other
    // look instantiates that look's prefab as a child, gives it this object's Animator controller,
    // moves the sword into its right hand, and hands its Animator to CharacterMotionPlayer.
    //
    // Like InkWashBackground, battles pick the look themselves (StageSelectController) once the
    // screen has faded; every other screen shows EnemyLooks.Workshop.
    [RequireComponent(typeof(CharacterMotionPlayer))]
    public class CharacterModelSwitcher : MonoBehaviour
    {
        [Serializable]
        public struct LookModel
        {
            public EnemyLook look;
            [Tooltip("Humanoid prefab saved in its bind T-pose, root at the feet, facing +Z.")]
            public GameObject prefab;
        }

        // Sword placement relative to the right hand, in the fighter's frame while the model is in
        // its bind T-pose (the same rule as the animation lab, which works across rigs). For the
        // built-in Character_TPose this matches the scene's TempSword exactly.
        private const float SwordRightOffset = 0.0767f;
        private const float SwordForwardOffset = 0.4f;

        [SerializeField] private GameFlowController flow;
        [Tooltip("The blade (with the trail tip) under the built-in model's right hand.")]
        [SerializeField] private Transform sword;
        [SerializeField] private List<LookModel> models = new List<LookModel>();

        private CharacterMotionPlayer motionPlayer;
        private Animator builtInAnimator;
        private RigBuilder builtInRig;
        private SkinnedMeshRenderer[] builtInRenderers;
        private Transform swordHome;
        private Vector3 swordHomePosition;
        private Quaternion swordHomeRotation;
        private Vector3 swordHomeScale;
        private Vector3 swordWorldScale;

        private GameObject currentModel;
        private EnemyLook? currentLook;

        public EnemyLook? CurrentLook => currentLook;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Start()
        {
            if (currentLook == null)
                Apply(EnemyLooks.Workshop);
        }

        private void OnEnable()
        {
            if (flow != null)
                flow.ScreenEntered += OnScreenEntered;
        }

        private void OnDisable()
        {
            if (flow != null)
                flow.ScreenEntered -= OnScreenEntered;
        }

        private void OnScreenEntered(GameScreen screen)
        {
            if (screen != GameScreen.Battle)
                Apply(EnemyLooks.Workshop);
        }

        public void Apply(EnemyLook look)
        {
            EnsureInitialized();
            if (currentLook == look)
                return;

            GameObject prefab = PrefabFor(look);
            if (look != EnemyLook.Default && prefab == null)
                Debug.LogWarning($"[CharacterModelSwitcher] '{name}' has no prefab for {look}; using the built-in model.", this);

            if (currentModel != null)
            {
                Destroy(currentModel);
                currentModel = null;
            }

            Animator target = prefab == null ? ShowBuiltIn() : ShowPrefab(prefab);
            motionPlayer.UseAnimator(target);
            currentLook = look;
        }

        private void EnsureInitialized()
        {
            if (motionPlayer != null)
                return;

            motionPlayer = GetComponent<CharacterMotionPlayer>();
            builtInAnimator = GetComponent<Animator>();
            builtInRig = GetComponent<RigBuilder>();
            builtInRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (sword != null)
            {
                swordHome = sword.parent;
                swordHomePosition = sword.localPosition;
                swordHomeRotation = sword.localRotation;
                swordHomeScale = sword.localScale;
                swordWorldScale = sword.lossyScale;
            }
        }

        private GameObject PrefabFor(EnemyLook look)
        {
            foreach (LookModel model in models)
            {
                if (model.look == look)
                    return model.prefab;
            }
            return null;
        }

        private Animator ShowBuiltIn()
        {
            SetBuiltInVisible(true);
            if (sword != null)
            {
                sword.SetParent(swordHome, false);
                sword.localPosition = swordHomePosition;
                sword.localRotation = swordHomeRotation;
                sword.localScale = swordHomeScale;
            }
            return builtInAnimator;
        }

        private Animator ShowPrefab(GameObject prefab)
        {
            // Keeps the prefab's own local scale (the generated models are scaled at the root).
            currentModel = Instantiate(prefab, transform);
            currentModel.name = prefab.name;
            currentModel.transform.localPosition = Vector3.zero;
            currentModel.transform.localRotation = Quaternion.identity;

            var animator = currentModel.GetComponent<Animator>();
            animator.runtimeAnimatorController = builtInAnimator.runtimeAnimatorController;
            animator.applyRootMotion = false;
            animator.cullingMode = builtInAnimator.cullingMode;

            // Still in the prefab's bind pose: the Animator has not evaluated yet.
            AttachSword(animator);
            SetBuiltInVisible(false);
            return animator;
        }

        private void AttachSword(Animator animator)
        {
            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (sword == null || hand == null)
            {
                Debug.LogWarning($"[CharacterModelSwitcher] '{name}': no sword or right hand on {animator.name}; sword left in place.", this);
                return;
            }

            Transform body = transform;
            sword.SetParent(hand, true);
            sword.SetPositionAndRotation(
                hand.position + body.right * SwordRightOffset + body.forward * SwordForwardOffset,
                Quaternion.LookRotation(body.forward, body.up));
            // Same blade size in the world whatever the bone scale (the trail tip rides on it).
            Vector3 parentScale = hand.lossyScale;
            sword.localScale = new Vector3(swordWorldScale.x / parentScale.x, swordWorldScale.y / parentScale.x, swordWorldScale.z / parentScale.x);
        }

        // The built-in model's renderers and Animator are switched off rather than destroyed so
        // Default can come back; its RigBuilder must follow its Animator.
        private void SetBuiltInVisible(bool visible)
        {
            foreach (SkinnedMeshRenderer renderer in builtInRenderers)
                renderer.enabled = visible;
            builtInAnimator.enabled = visible;
            if (builtInRig != null)
                builtInRig.enabled = visible;
        }
    }
}
