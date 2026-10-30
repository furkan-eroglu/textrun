using System.Collections.Generic;

namespace TextRun.Typing
{
    // Prototip için küçük kelime listeleri. Gerçek veritabanı Faz 5'te hazırlanacak.
    public static class SampleWords
    {
        static readonly string[] Turkish =
        {
            "elma", "kalem", "pilot", "sarı", "yumurta", "okul", "öğretmen", "araba", "güneş", "çiçek",
            "ağaç", "şeker", "gözlük", "ışık", "ördek", "ılık", "köprü", "bülbül", "çorba", "üzmüş",
            "dağ", "kuş", "şehir", "göl", "rüzgar", "tavşan", "örtü", "ağız", "çakmak", "yağmur",
            "kış", "yaz", "sınıf", "küçük", "büyük", "çocuk", "yıldız", "bahçe", "gökyüzü", "ıspanak",
            "iğne", "ısı", "ilaç", "içecek", "pencere", "müzik", "nehir", "sandalye", "kağıt", "telefon"
        };

        static readonly string[] English =
        {
            "apple", "pencil", "pilot", "yellow", "egg", "school", "teacher", "car", "sun", "flower",
            "tree", "sugar", "glasses", "light", "duck", "bridge", "soup", "grape", "mountain", "bird",
            "city", "lake", "wind", "rabbit", "cover", "mouth", "lighter", "rain", "winter", "summer",
            "class", "small", "big", "child", "star", "garden", "sky", "window", "music", "river",
            "chair", "paper", "phone", "water", "bread", "table", "cloud", "green", "story", "ocean"
        };

        // Aynı tohum aynı diziyi üretir; yarışta herkese aynı liste gideceği için bu önemlidir.
        public static List<string> Generate(GameLanguage language, int count, int seed)
        {
            var pool = language == GameLanguage.Turkish ? Turkish : English;
            var random = new System.Random(seed);
            var result = new List<string>(count);
            string last = null;
            while (result.Count < count)
            {
                var word = pool[random.Next(pool.Length)];
                if (word == last) continue;
                result.Add(word);
                last = word;
            }
            return result;
        }
    }
}
