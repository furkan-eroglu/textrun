using System;
using System.Collections.Generic;

namespace TextRun.Typing
{
    // Klavyenin (telefonda sistem klavyesi) metnini izler ve yalnızca DEĞİŞİKLİĞİ bildirir: yeni yazılan harfler ve silinen harfler.
    //
    // Klavyenin metni oyun tarafından ASLA üzerine yazılmaz. Telefon klavyeleri (öneri, otomatik düzeltme, yazım alanı) yazılan
    // metni hemen kabul etmeyebilir; üzerine yazmak aynı harfin her karede yeniden sayılmasına ve girişin kilitlenmesine yol açıyordu.
    // Yanlış harfler ve geçmiş kelimelerin harfleri klavye metninde kalır ama giriş tarafından yok sayılır.
    public class KeyboardTracker
    {
        string text = "";
        readonly List<bool> accepted = new List<bool>(); // klavye metnindeki her harf için: şu anki kelimeye kabul edildi mi (silinirse geri alınır)

        public string Text => text;

        // Klavye metnini "zaten işlenmiş" kabul eder (yarış başlarken, klavye yeniden açılınca, sunucu kelimeyi geri alınca):
        // metindeki harfler yeni yazılmış sayılmaz, silinirse girişi etkilemez.
        public void Sync(string raw)
        {
            text = raw ?? "";
            accepted.Clear();
            for (int i = 0; i < text.Length; i++) accepted.Add(false);
        }

        // Kelime değişti: o ana kadarki harfler artık eski kelimeye aittir; silinirlerse yeni kelimeyi etkilememeli.
        public void Boundary()
        {
            for (int i = 0; i < accepted.Count; i++) accepted[i] = false;
        }

        // raw: klavyenin şu anki metni. press: yeni harfi girişe verir, giriş harfi kabul ettiyse true döner.
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

            // Yeni harfler sırayla girişe verilir. press içinde kelime gönderilirse Boundary() çağrılır, o ana kadarki harfler eski kelimeye ait olur.
            text = raw;
            for (int i = common; i < raw.Length; i++) accepted.Add(press(raw[i]));
        }
    }
}
