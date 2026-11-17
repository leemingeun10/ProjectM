using System;
using System.Collections.Generic;
using Game.Client.Battle;
using Game.Client.Scenery;
using Game.Sim.Progress;
using UnityEngine;

namespace Game.Client.Story
{
    public enum StoryStageKind
    {
        Battle,
        Story,
        Boss,
        // A fight the story says is lost: it clears whatever the outcome.
        Loss,
    }

    public enum StoryClearRule
    {
        Win,
        Always,
    }

    public enum StoryCutType
    {
        Illust,
        // A dialogue close-up; for now a placeholder with the speaker's name.
        Close,
    }

    // Thrown by StoryBook.Parse for malformed JSON, schema violations or an unknown version.
    public class StoryDataException : Exception
    {
        public StoryDataException(string message) : base(message)
        {
        }
    }

    // The story map, read from a JSON TextAsset (Story/Data/story_part1.json). All story text
    // (names, lines, narration, choice wording) lives in that file, never in code, so it can be
    // rewritten without touching scripts. Field names match the JSON keys (JsonUtility).
    // Mirrors CHAPTERS / ST in Docs/StoryMapPrototype.html, plus the game-side fields
    // aiStage / look / theme / image / starter / outcomeLabel.
    [Serializable]
    public class StoryBook
    {
        public const int CurrentVersion = 1;

        public int version;
        // Speaker name of the protagonist; shown in jade like the prototype.
        public string protagonist;
        // Shown after the final choice. "{flag}" is replaced by the chosen side's name.
        public StoryText partEnd = new StoryText();
        public StoryChapter[] chapters;
        public StoryStage[] stages;

        public int StageCount => stages.Length;

        public static StoryBook Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
                throw new StoryDataException("Story JSON is empty.");

            StoryBook book;
            try
            {
                book = JsonUtility.FromJson<StoryBook>(json);
            }
            catch (ArgumentException e)
            {
                throw new StoryDataException("Invalid story JSON: " + e.Message);
            }

            if (book == null)
                throw new StoryDataException("Invalid story JSON: no root object.");
            book.Validate();
            return book;
        }

        public int IndexOf(string stageId)
        {
            for (int i = 0; i < stages.Length; i++)
            {
                if (stages[i].id == stageId)
                    return i;
            }
            return -1;
        }

        public StoryChapter Chapter(int ch) => chapters[ch - 1];

        // Indices (into stages) of one chapter's stages, in order.
        public List<int> StageIndicesOf(int ch)
        {
            var list = new List<int>();
            for (int i = 0; i < stages.Length; i++)
            {
                if (stages[i].ch == ch)
                    list.Add(i);
            }
            return list;
        }

        public static bool TryParseKind(string value, out StoryStageKind kind)
        {
            switch (value)
            {
                case "battle": kind = StoryStageKind.Battle; return true;
                case "story": kind = StoryStageKind.Story; return true;
                case "boss": kind = StoryStageKind.Boss; return true;
                case "loss": kind = StoryStageKind.Loss; return true;
                default: kind = default; return false;
            }
        }

        public static bool TryParseClear(string value, out StoryClearRule rule)
        {
            switch (value)
            {
                case "win": rule = StoryClearRule.Win; return true;
                case "always": rule = StoryClearRule.Always; return true;
                default: rule = default; return false;
            }
        }

        public static bool TryParseCutType(string value, out StoryCutType type)
        {
            switch (value)
            {
                case "illust": type = StoryCutType.Illust; return true;
                case "close": type = StoryCutType.Close; return true;
                default: type = default; return false;
            }
        }

        // Names of EnemyLook members other than Default ("Ronin", "BlackSwordsman", ...).
        public static bool TryParseLook(string value, out EnemyLook look)
        {
            if (!string.IsNullOrEmpty(value) && Enum.TryParse(value, false, out look)
                && Enum.IsDefined(typeof(EnemyLook), look) && look != EnemyLook.Default && look.ToString() == value)
                return true;
            look = default;
            return false;
        }

        // Names of ArenaTheme members ("Street", "TrainingHall", "MountHua").
        public static bool TryParseTheme(string value, out ArenaTheme theme)
        {
            if (!string.IsNullOrEmpty(value) && Enum.TryParse(value, false, out theme)
                && Enum.IsDefined(typeof(ArenaTheme), theme) && theme.ToString() == value)
                return true;
            theme = default;
            return false;
        }

        private void Validate()
        {
            if (version != CurrentVersion)
                throw new StoryDataException($"Unsupported story JSON version {version} (supported: {CurrentVersion}).");
            if (string.IsNullOrEmpty(protagonist))
                throw new StoryDataException("\"protagonist\" is required.");
            if (chapters == null || chapters.Length == 0)
                throw new StoryDataException("\"chapters\" must not be empty.");
            if (stages == null || stages.Length == 0)
                throw new StoryDataException("\"stages\" must not be empty.");

            for (int i = 0; i < chapters.Length; i++)
            {
                StoryChapter c = chapters[i];
                if (c == null || c.ch != i + 1)
                    throw new StoryDataException($"chapters[{i}].ch must be {i + 1}.");
                if (string.IsNullOrEmpty(c.title))
                    throw new StoryDataException($"Chapter {c.ch} has no title.");
            }

            var ids = new HashSet<string>();
            bool hasChoice = false;
            for (int i = 0; i < stages.Length; i++)
            {
                StoryStage s = stages[i];
                if (s == null || string.IsNullOrEmpty(s.id))
                    throw new StoryDataException($"stages[{i}] has no id.");
                string at = $"Stage {s.id}";
                if (!ids.Add(s.id))
                    throw new StoryDataException($"{at}: duplicate id.");
                if (s.ch < 1 || s.ch > chapters.Length)
                    throw new StoryDataException($"{at}: chapter {s.ch} does not exist.");

                // Stages run in order: chapter by chapter, n = 1, 2, ... within each.
                StoryStage prev = i > 0 ? stages[i - 1] : null;
                int expectedN = prev != null && prev.ch == s.ch ? prev.n + 1 : 1;
                if (prev != null && s.ch < prev.ch)
                    throw new StoryDataException($"{at}: stages must be ordered by chapter.");
                if (prev != null && s.ch > prev.ch + 1)
                    throw new StoryDataException($"{at}: chapter {prev.ch + 1} has no stages.");
                if (prev == null && s.ch != 1)
                    throw new StoryDataException($"{at}: the first stage must be in chapter 1.");
                if (s.n != expectedN)
                    throw new StoryDataException($"{at}: n must be {expectedN}.");
                if (string.IsNullOrEmpty(s.name))
                    throw new StoryDataException($"{at}: name is required.");
                if (string.IsNullOrEmpty(s.@event))
                    throw new StoryDataException($"{at}: event is required.");

                if (!TryParseKind(s.kind, out StoryStageKind kind))
                    throw new StoryDataException($"{at}: unknown kind \"{s.kind}\".");
                if (!TryParseClear(s.clear, out StoryClearRule clear))
                    throw new StoryDataException($"{at}: unknown clear rule \"{s.clear}\".");

                StoryClearRule expectedClear = kind == StoryStageKind.Battle || kind == StoryStageKind.Boss
                    ? StoryClearRule.Win : StoryClearRule.Always;
                if (clear != expectedClear)
                    throw new StoryDataException($"{at}: a {s.kind} stage must clear on \"{(expectedClear == StoryClearRule.Win ? "win" : "always")}\".");

                if (kind == StoryStageKind.Story)
                {
                    if (!string.IsNullOrEmpty(s.enemy) || s.aiStage != 0 || !string.IsNullOrEmpty(s.look)
                        || !string.IsNullOrEmpty(s.theme) || s.starter || !string.IsNullOrEmpty(s.outcomeLabel))
                        throw new StoryDataException($"{at}: a story stage has no battle fields.");
                }
                else
                {
                    if (string.IsNullOrEmpty(s.enemy))
                        throw new StoryDataException($"{at}: enemy is required.");
                    if (!StageCatalog.IsValidStageIndex(s.aiStage))
                        throw new StoryDataException($"{at}: aiStage {s.aiStage} is not a stage in StageCatalog (1..{StageCatalog.StageCount}).");
                    if (!TryParseLook(s.look, out _))
                        throw new StoryDataException($"{at}: unknown look \"{s.look}\".");
                    if (!TryParseTheme(s.theme, out _))
                        throw new StoryDataException($"{at}: unknown theme \"{s.theme}\".");
                }

                if (s.choice)
                {
                    hasChoice = true;
                    StoryChoiceText t = s.choiceText;
                    if (t == null || string.IsNullOrEmpty(t.title) || string.IsNullOrEmpty(t.spare) || string.IsNullOrEmpty(t.kill))
                        throw new StoryDataException($"{at}: a choice needs choiceText.title, spare and kill.");
                }

                if (s.cuts == null || s.cuts.Length == 0)
                    throw new StoryDataException($"{at}: cuts must not be empty.");
                for (int c = 0; c < s.cuts.Length; c++)
                {
                    StoryCut cut = s.cuts[c];
                    string cutAt = $"{at} cut {c + 1}";
                    if (cut == null || !TryParseCutType(cut.type, out StoryCutType type))
                        throw new StoryDataException($"{cutAt}: unknown type \"{cut?.type}\".");
                    if (string.IsNullOrEmpty(cut.line))
                        throw new StoryDataException($"{cutAt}: line is required.");
                    if (type == StoryCutType.Close && string.IsNullOrEmpty(cut.speaker))
                        throw new StoryDataException($"{cutAt}: a close-up needs a speaker.");
                    if (cut.after && kind == StoryStageKind.Story)
                        throw new StoryDataException($"{cutAt}: a story stage has no battle to come after.");
                }
            }

            for (int i = 0; i < chapters.Length; i++)
            {
                int count = 0;
                foreach (StoryStage s in stages)
                {
                    if (s.ch == i + 1)
                        count++;
                }
                if (chapters[i].nodes == null || chapters[i].nodes.Length != count)
                    throw new StoryDataException($"Chapter {i + 1} needs one map node per stage ({count}).");
            }

            if (hasChoice && (partEnd == null || string.IsNullOrEmpty(partEnd.title)))
                throw new StoryDataException("\"partEnd.title\" is required when a stage has a choice.");
        }
    }

    [Serializable]
    public class StoryText
    {
        public string title;
        public string body;
    }

    [Serializable]
    public class StoryChoiceText
    {
        public string title;
        public string body;
        // Button labels: spare = 정파 flag, kill = 사파 flag.
        public string spare;
        public string kill;
    }

    [Serializable]
    public class StoryChapter
    {
        public int ch;
        public string title;
        // Enemy named on the map (watermark).
        public string sub;
        // Map scenery key from the prototype (street / wall / peak); not drawn yet.
        public string scene;
        // One per stage, in a 360x600 viewBox with y growing downward (the prototype's SVG).
        public StoryPoint[] nodes;
    }

    [Serializable]
    public class StoryPoint
    {
        public int x;
        public int y;
    }

    [Serializable]
    public class StoryStage
    {
        public string id;
        public int ch;
        public int n;
        public string name;
        public string kind;
        public string clear;
        public string enemy;
        // StageCatalog index whose seeded AI technique the opponent uses (battle kinds only).
        public int aiStage;
        // EnemyLook / ArenaTheme member names (battle kinds only).
        public string look;
        public string theme;
        public string @event;
        public string system;
        public string[] tags;
        // Name and lines are provisional (shown as a note on the stage sheet).
        public bool prov;
        // With no complete technique slot, fight with StarterTechnique instead of sending the
        // player to 무공 제작 (1-1 only).
        public bool starter;
        // Replaces the outcome word on the result panel (e.g. "기절" for the forced loss of 1-5).
        public string outcomeLabel;
        public bool choice;
        public StoryChoiceText choiceText = new StoryChoiceText();
        public StoryCut[] cuts;

        public StoryStageKind Kind => StoryBook.TryParseKind(kind, out StoryStageKind k) ? k : throw new StoryDataException($"Stage {id}: unknown kind.");
        public StoryClearRule Clear => StoryBook.TryParseClear(clear, out StoryClearRule r) ? r : throw new StoryDataException($"Stage {id}: unknown clear rule.");
        public bool HasBattle => Kind != StoryStageKind.Story;
        public EnemyLook Look => StoryBook.TryParseLook(look, out EnemyLook l) ? l : throw new StoryDataException($"Stage {id}: no look.");
        public ArenaTheme Theme => StoryBook.TryParseTheme(theme, out ArenaTheme t) ? t : throw new StoryDataException($"Stage {id}: no theme.");
    }

    [Serializable]
    public class StoryCut
    {
        public string type;
        // Description of the intended art (shown on placeholders).
        public string art;
        // StoryArtCatalog key; empty = placeholder.
        public string image;
        public string speaker;
        public string line;
        // Shown after the battle.
        public bool after;
        // Blackout cut.
        public bool black;

        public StoryCutType Type => StoryBook.TryParseCutType(type, out StoryCutType t) ? t : StoryCutType.Illust;
    }
}
