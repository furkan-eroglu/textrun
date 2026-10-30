using System;
using System.Collections.Generic;
using UnityEngine;

namespace TextRun.Typing
{
    [Serializable]
    public class WordTier
    {
        public string name;
        public int min;
        public int max;
        public int weight;
    }

    [Serializable]
    public class LanguageEntry
    {
        public string code;
        public string file;
        public string allowedChars;
    }

    [Serializable]
    public class WordConfig
    {
        public int minLength;
        public int maxLength;
        public int recentWindow;
        public WordTier[] tiers;
        public LanguageEntry[] languages;
    }

    public static class GameLanguageExtensions
    {
        public static string Code(this GameLanguage language)
        {
            return language == GameLanguage.Turkish ? "tr" : "en";
        }
    }

    // Bir dilin kelime havuzu. Kelimeler uzunluk dilimlerine ayrılır, yarış listesi dilimlerin ağırlığına göre seçilir.
    // Kaynak dosyalar content/words klasöründedir; tools/words/sync.mjs bunları Resources/Words içine kopyalar.
    public class WordDatabase
    {
        static readonly Dictionary<GameLanguage, WordDatabase> cache = new Dictionary<GameLanguage, WordDatabase>();

        readonly WordConfig config;
        readonly List<string>[] tierWords;

        public int Count { get; private set; }
        public IReadOnlyList<string>[] Tiers => tierWords;

        WordDatabase(WordConfig config, string wordsText, GameLanguage language)
        {
            this.config = config;
            tierWords = new List<string>[config.tiers.Length];
            for (int i = 0; i < tierWords.Length; i++) tierWords[i] = new List<string>();

            var seen = new HashSet<string>();
            foreach (var raw in ParseWords(wordsText))
            {
                string word = TextNormalizer.ToLower(raw, language);
                if (word.Length < config.minLength || word.Length > config.maxLength) continue;
                if (!seen.Add(word)) continue;

                for (int t = 0; t < config.tiers.Length; t++)
                {
                    if (word.Length >= config.tiers[t].min && word.Length <= config.tiers[t].max)
                    {
                        tierWords[t].Add(word);
                        Count++;
                        break;
                    }
                }
            }
        }

        public static WordDatabase Load(GameLanguage language)
        {
            if (cache.TryGetValue(language, out var cached)) return cached;

            var configAsset = Resources.Load<TextAsset>("Words/config");
            var wordsAsset = Resources.Load<TextAsset>("Words/" + language.Code());
            if (configAsset == null || wordsAsset == null)
                throw new InvalidOperationException("Kelime dosyalari bulunamadi. 'node tools/words/sync.mjs' calistirin.");

            var db = FromText(JsonUtility.FromJson<WordConfig>(configAsset.text), wordsAsset.text, language);
            cache[language] = db;
            return db;
        }

        public static WordDatabase FromText(WordConfig config, string wordsText, GameLanguage language)
        {
            return new WordDatabase(config, wordsText, language);
        }

        // Boşlukla ayrılmış kelimeleri okur; # ile başlayan satırlar yorumdur.
        public static IEnumerable<string> ParseWords(string text)
        {
            foreach (var line in text.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed[0] == '#') continue;
                foreach (var word in trimmed.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
                    yield return word;
            }
        }

        // Aynı tohum aynı listeyi verir. Yakın zamanda kullanılan kelime tekrar seçilmez.
        public List<string> Generate(int count, int seed)
        {
            var random = new System.Random(seed);
            var result = new List<string>(count);
            var recent = new Queue<string>();
            var recentSet = new HashSet<string>();

            int totalWeight = 0;
            for (int t = 0; t < tierWords.Length; t++)
                if (tierWords[t].Count > 0) totalWeight += config.tiers[t].weight;
            if (totalWeight <= 0) throw new InvalidOperationException("Kelime havuzu bos.");

            while (result.Count < count)
            {
                var pool = tierWords[PickTier(random, totalWeight)];
                string word = pool[random.Next(pool.Count)];

                for (int attempt = 0; attempt < 20 && recentSet.Contains(word); attempt++)
                    word = pool[random.Next(pool.Count)];

                result.Add(word);
                recent.Enqueue(word);
                recentSet.Add(word);
                if (recent.Count > config.recentWindow) recentSet.Remove(recent.Dequeue());
            }
            return result;
        }

        int PickTier(System.Random random, int totalWeight)
        {
            int roll = random.Next(totalWeight);
            for (int t = 0; t < tierWords.Length; t++)
            {
                if (tierWords[t].Count == 0) continue;
                roll -= config.tiers[t].weight;
                if (roll < 0) return t;
            }
            return 0;
        }
    }
}
