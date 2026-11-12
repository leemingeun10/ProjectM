#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using Game.Client.Technique;
using Game.Sim.Shape;
using UnityEngine;

namespace Game.Client.Battle
{
    // TEMPORARY (editor and development builds only): plays the technique clips on demand and injects
    // TechniqueCasts into the battle replay so long technique turns can be checked on the real battle
    // screen before stroke recognition and AI settings supply casts. Delete once they do.
    //
    // Spawns itself after the first scene loads and shows a small "무공 미리보기" button in the top
    // left corner. Injected casts apply to every replay whose caller passed none, until cleared.
    public class TechniquePreviewTool : MonoBehaviour
    {
        private static readonly string[] TechniqueIds =
        {
            ShapeTemplates.TriangleId, ShapeTemplates.FlowerId, ShapeTemplates.SwordId, ShapeTemplates.CrescentId,
        };

        private const int Stage = 1;
        private const int TurnCount = 10;
        // Below the battle screen's HP bars (GUI units at 900 px screen height).
        private const float TopMargin = 110f;

        private static readonly List<TechniqueCast> injectedA = new List<TechniqueCast>();
        private static readonly List<TechniqueCast> injectedB = new List<TechniqueCast>();

        private bool open;
        private bool sideA = true;
        private int turnNumber = 1;
        private string status = string.Empty;

        public static IReadOnlyList<TechniqueCast> InjectedCasts(bool sideA) => sideA ? injectedA : injectedB;

        // One cast per side per turn: a new one for the same turn replaces the old.
        public static void Inject(bool sideA, TechniqueCast cast)
        {
            List<TechniqueCast> list = sideA ? injectedA : injectedB;
            list.RemoveAll(c => c.TurnIndex == cast.TurnIndex);
            list.Add(cast);
        }

        public static void ClearInjected()
        {
            injectedA.Clear();
            injectedB.Clear();
        }

        // Statics survive Play sessions when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ClearInjected();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Spawn()
        {
            var go = new GameObject(nameof(TechniquePreviewTool));
            DontDestroyOnLoad(go);
            go.AddComponent<TechniquePreviewTool>();
        }

        private void OnGUI()
        {
            float scale = Mathf.Max(1f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            if (!open)
            {
                if (GUI.Button(new Rect(8, TopMargin, 120, 32), "무공 미리보기"))
                    open = true;
                return;
            }

            GUILayout.BeginArea(new Rect(8, TopMargin, 360, 470), GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("무공 미리보기 (임시)");
            if (GUILayout.Button("닫기", GUILayout.Width(60)))
                open = false;
            GUILayout.EndHorizontal();

            sideA = GUILayout.Toolbar(sideA ? 0 : 1, new[] { "A (나)", "B (상대)" }) == 0;

            GUILayout.Space(6);
            GUILayout.Label("바로 재생");
            GUILayout.BeginHorizontal();
            foreach (string id in TechniqueIds)
            {
                if (GUILayout.Button(HiddenTechniqueNames.Of(id)))
                    PlayNow(id);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label("리플레이 턴에 주입 (1단계)");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("-", GUILayout.Width(32)))
                turnNumber = Mathf.Max(1, turnNumber - 1);
            GUILayout.Label($"턴 {turnNumber}", GUILayout.Width(60));
            if (GUILayout.Button("+", GUILayout.Width(32)))
                turnNumber = Mathf.Min(TurnCount, turnNumber + 1);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            foreach (string id in TechniqueIds)
            {
                if (GUILayout.Button(HiddenTechniqueNames.Of(id)))
                {
                    Inject(sideA, new TechniqueCast(turnNumber - 1, id, Stage));
                    status = $"{(sideA ? "A" : "B")} 턴 {turnNumber}: {HiddenTechniqueNames.Of(id)}";
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Label("A: " + Describe(injectedA));
            GUILayout.Label("B: " + Describe(injectedB));

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("주입 비우기"))
            {
                ClearInjected();
                status = "비움";
            }
            if (GUILayout.Button("리플레이 처음부터"))
                status = FindReplay() != null && FindReplay().PreviewRestart() ? "다시 재생" : "진행 중인 리플레이 없음";
            GUILayout.EndHorizontal();

            GUILayout.Label(status);
            GUILayout.EndArea();
        }

        private void PlayNow(string id)
        {
            BattleReplayController replay = FindReplay();
            if (replay == null || !TechniqueClipCatalog.TryGet(id, Stage, out TechniqueClip clip))
            {
                status = "전투 화면을 찾지 못함";
                return;
            }
            replay.PreviewCharacter(sideA).Play(clip.State);
            status = $"{(sideA ? "A" : "B")}: {clip.State} ({clip.Length:F2}s, 타격 {clip.Strike:F2}s)";
        }

        private static BattleReplayController FindReplay()
            => FindFirstObjectByType<BattleReplayController>(FindObjectsInactive.Include);

        private static string Describe(List<TechniqueCast> casts)
        {
            if (casts.Count == 0)
                return "-";
            var parts = new List<string>();
            foreach (TechniqueCast c in casts)
                parts.Add($"턴{c.TurnIndex + 1} {HiddenTechniqueNames.Of(c.TechniqueId)}");
            return string.Join(", ", parts);
        }
    }
}
#endif
