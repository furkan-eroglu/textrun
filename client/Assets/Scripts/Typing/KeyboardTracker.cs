using System;
using System.Collections.Generic;

namespace TextRun.Typing
{
    // Klavyenin (telefonda sistem klavyesi) metnini izler ve yalnızca DEĞİŞİKLİĞİ bildirir: yeni yazılan harfler ve silinen harfler.
    //
    // Klavyenin metni oyun tarafından ASLA üzerine yazılmaz (kelime geçişi dışında). Telefon klavyeleri (öneri, otomatik düzeltme,
    // yazım alanı) yazılan metni hemen kabul etmeyebilir; üzerine yazmak aynı harfin her karede yeniden sayılmasına ve girişin
    // kilitlenmesine yol açıyordu. Burada yanlış harfler klavye metninde kalır ama giriş tarafından yok sayılır.
    public class KeyboardTracker
    {
        string text = "";
        readonly List<bool> accepted = new List<bool>(); // klavye metnindeki her harf için: giriş tarafından kabul edildi mi

        public string Text => text;
        public int ResetCount { get; private set; }

        // Yeni kelime ya da klavye yeniden açıldığında: klavye metni boştur, geçmiş unutulur.
        public void Reset()
        {
            text = "";
            accepted.Clear();
            ResetCount++;
        }

        // raw: klavyenin şu anki metni. press: yeni harfi girişe verir, kabul edildiyse true döner.
        // undo: kabul edilmiş son harf silindi, girişten de bir harf geri alınmalı.
        public void Update(string raw, GameLanguage language, Func<char, bool> press, Action undo)
        {
            raw = raw ?? "";
            if (raw == text) return;

            // Ortak başlangıç (büyük/küçük harf ayrımı yok: otomatik büyük harf yapan klavyeler)
            int common = 0;
            while (common < raw.Length && common < text.Length &&
                   TextNormalizer.ToLower(raw[common], language) == TextNormalizer.ToLower(text[common], language)) common++;

            // Silinen (ya da başka metinle değişen) harfler: kabul edilmişlerse girişten de geri alınır
            while (accepted.Count > common)
            {
                bool wasAccepted = accepted[accepted.Count - 1];
                accepted.RemoveAt(accepted.Count - 1);
                if (wasAccepted) undo();
            }

            // Yeni harfler sırayla girişe verilir. Kelime bitip yeni kelimeye geçilirse (Reset) kalan harfler yeni kelimeye aittir.
            text = raw;
            int resets = ResetCount;
            for (int i = common; i < raw.Length; i++)
            {
                bool ok = press(raw[i]);
                if (ResetCount == resets) accepted.Add(ok);
            }

            if (ResetCount != resets)
            {
                text = "";
                accepted.Clear();
            }
        }
    }
}
