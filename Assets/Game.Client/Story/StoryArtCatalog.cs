using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Client.Story
{
    // Story cut art by key (StoryCut.image). Textures are used as they are imported (shown on a
    // RawImage), so adding art never needs a re-import as Sprite.
    [CreateAssetMenu(fileName = "StoryArtCatalog", menuName = "ProjectM/Story Art Catalog")]
    public class StoryArtCatalog : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string key;
            public Texture2D image;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;

        // Null for an empty key or one not in the catalog: the cut shows its placeholder.
        public Texture2D Find(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;
            foreach (Entry entry in entries)
            {
                if (entry.key == key)
                    return entry.image;
            }
            return null;
        }
    }
}
