using System;
using System.Collections.Generic;
using TextRun.Common;
using TextRun.Online;
using TextRun.Typing;
using UnityEngine;

namespace TextRun.Game
{
    // Envanter ekranı: altın, açılmamış sandıklar, sahip olunan eşyalar ve sandık açma animasyonu.
    // Altın, sandık ve eşyalar sunucuda tutulur; burada yalnızca gösterilir ve "sandık aç" isteği gönderilir.
    public partial class OnlineGameController
    {
        static readonly string[] ChestTiers = { "bronze", "silver", "gold" };
        const int ItemsPerPage = 6;
        const float ChestShakeSeconds = 1.5f;

        InventoryDto inventory;       // null = henüz okunmadı
        int inventoryPage;
        GUIStyle itemNameStyle, chestNameStyle;

        // Sandık açma
        string openingTier;           // null = animasyon yok
        float openingStartedAt;
        ChestResultDto openedResult;
        bool openingFailed;
        bool revealSoundPlayed;

        // ---------- Renkler ve küçük çizimler ----------

        // Nadirlik renkleri her yerde aynıdır: efsanevi altın/sarı, destansı kırmızı, nadir mor, sıradışı mavi, yaygın yeşil
        static Color RarityColor(string rarity)
        {
            switch (rarity)
            {
                case "legendary": return new Color(1f, 0.82f, 0.22f);
                case "epic": return new Color(0.93f, 0.27f, 0.27f);
                case "rare": return new Color(0.66f, 0.42f, 0.96f);
                case "uncommon": return new Color(0.30f, 0.62f, 1f);
                default: return new Color(0.36f, 0.80f, 0.42f);
            }
        }

        static Color TierColor(string tier)
        {
            switch (tier)
            {
                case "gold": return new Color(1f, 0.80f, 0.26f);
                case "silver": return new Color(0.78f, 0.83f, 0.90f);
                default: return new Color(0.80f, 0.52f, 0.28f);
            }
        }

        string ItemName(string tr, string en) => language == GameLanguage.English ? en : tr;

        static void DrawCoin(Rect r)
        {
            UiKit.Card(r, new Color(0.80f, 0.55f, 0.08f), r.width / 2f);
            float inset = r.width * 0.12f;
            UiKit.Card(new Rect(r.x + inset, r.y + inset * 0.7f, r.width - inset * 2f, r.height - inset * 2f), new Color(1f, 0.84f, 0.30f), r.width / 2f);
            float core = r.width * 0.34f;
            UiKit.Card(new Rect(r.center.x - core / 2f, r.center.y - core / 2f - inset * 0.3f, core, core), new Color(1f, 0.93f, 0.62f), core / 2f);
        }

        // Sandık simgesi: sandık türüne göre renkli kapak, koyu gövde, kilit
        static void DrawChestIcon(Rect r, string tier)
        {
            Color baseColor = TierColor(tier);
            Color dark = new Color(baseColor.r * 0.52f, baseColor.g * 0.52f, baseColor.b * 0.52f);
            float w = r.width, h = r.height;
            UiKit.Card(new Rect(r.x + w * 0.08f, r.y + h * 0.40f, w * 0.84f, h * 0.52f), dark, w * 0.09f);
            UiKit.Card(new Rect(r.x + w * 0.04f, r.y + h * 0.10f, w * 0.92f, h * 0.38f), baseColor, w * 0.17f);
            UiKit.Card(new Rect(r.x + w * 0.12f, r.y + h * 0.15f, w * 0.76f, h * 0.10f), new Color(1f, 1f, 1f, 0.28f), w * 0.05f);
            UiKit.Card(new Rect(r.x + w * 0.08f, r.y + h * 0.42f, w * 0.84f, h * 0.07f), new Color(0.30f, 0.18f, 0.06f), 2f);
            UiKit.Card(new Rect(r.center.x - w * 0.09f, r.y + h * 0.38f, w * 0.18f, h * 0.20f), new Color(1f, 0.86f, 0.34f), w * 0.04f);
            UiKit.Card(new Rect(r.center.x - w * 0.025f, r.y + h * 0.45f, w * 0.05f, h * 0.07f), new Color(0.30f, 0.18f, 0.06f), 2f);
        }

        // ---------- Sayfa ----------

        void OpenInventory()
        {
            inventoryPage = 0;
            openingTier = null;
            screen = Page.Inventory;
            LoadInventory();
        }

        async void LoadInventory()
        {
            try
            {
                var fresh = await net.GetInventoryAsync();
                inventory = fresh;
                profile = await net.GetProfileAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning("Envanter okunamadi: " + e.Message);
            }
        }

        async void BeginOpenChest(string tier)
        {
            if (openingTier != null || net == null || profile.ChestCount(tier) <= 0) return;
            openingTier = tier;
            openingStartedAt = Time.realtimeSinceStartup;
            openedResult = null;
            openingFailed = false;
            revealSoundPlayed = false;
            try
            {
                var result = await net.OpenChestAsync(tier);
                openedResult = result;
                if (result.profile != null) profile = result.profile;
                LoadInventory();
            }
            catch (Exception e)
            {
                Debug.LogWarning("Sandik acilamadi: " + e.Message);
                openingFailed = true;
            }
        }

        void DrawInventory(float width, float height, float top)
        {
            FullViewport();

            if (openingTier != null)
            {
                DrawChestOpening(width, height, top);
                return;
            }

            if (itemNameStyle == null)
            {
                itemNameStyle = new GUIStyle(rowNameStyle) { fontSize = 15, fontStyle = FontStyle.Bold, wordWrap = false, clipping = TextClipping.Clip };
                UiKit.SetAllTextColors(itemNameStyle, Color.white);
                chestNameStyle = new GUIStyle(rowNameStyle) { fontSize = 17, wordWrap = false, clipping = TextClipping.Clip };
            }

            float y = top + 8f;
            GUI.Label(new Rect(0f, y, width, 40f), T("inventory"), headingStyle);
            y += 48f;

            // Altın
            var goldRow = new Rect(16f, y, width - 32f, 40f);
            UiKit.Card(goldRow, new Color(1f, 1f, 1f, 0.08f), 14f);
            DrawCoin(new Rect(goldRow.x + 12f, goldRow.y + 8f, 24f, 24f));
            GUI.Label(new Rect(goldRow.x + 46f, goldRow.y, goldRow.width - 60f, goldRow.height), profile.gold.ToString(), rowNameStyle);
            y += 48f;

            // Sandıklar: yalnızca sahip olunanlar gösterilir (hiç yoksa kısa bir not)
            if (profile.TotalChests == 0)
            {
                GUI.Label(new Rect(24f, y, width - 48f, 30f), T("noChests"), smallCenterStyle);
                y += 38f;
            }
            foreach (var tier in ChestTiers)
            {
                int count = profile.ChestCount(tier);
                if (count <= 0) continue;
                var row = new Rect(16f, y, width - 32f, 52f);
                UiKit.Card(row, new Color(TierColor(tier).r, TierColor(tier).g, TierColor(tier).b, 0.20f), 14f);
                DrawChestIcon(new Rect(row.x + 8f, row.y + 6f, 40f, 40f), tier);
                GUI.Label(new Rect(row.x + 56f, row.y, row.width - 156f, row.height), T("chest_" + tier) + "  x" + count, chestNameStyle);
                if (UiKit.SecondaryButton(new Rect(row.xMax - 96f, row.y + 6f, 88f, 40f), T("open"), 18)) BeginOpenChest(tier);
                y += 58f;
            }

            // Eşyalar (sayfalı liste, iki sütun)
            int total = inventory?.items?.Length ?? 0;
            GUI.Label(new Rect(20f, y, width - 40f, 24f), T("myItems") + " (" + total + ")", smallStyle);
            y += 28f;

            if (inventory == null) GUI.Label(new Rect(20f, y + 20f, width - 40f, 30f), "...", smallCenterStyle);
            else if (total == 0) GUI.Label(new Rect(24f, y + 12f, width - 48f, 44f), T("noItems"), smallCenterWrapStyle());
            else
            {
                int pages = Mathf.Max(1, Mathf.CeilToInt(total / (float)ItemsPerPage));
                inventoryPage = Mathf.Clamp(inventoryPage, 0, pages - 1);
                float cellWidth = (width - 32f - 10f) / 2f;
                for (int i = 0; i < ItemsPerPage; i++)
                {
                    int index = inventoryPage * ItemsPerPage + i;
                    if (index >= total) break;
                    var item = inventory.items[index];
                    var cell = new Rect(16f + (i % 2) * (cellWidth + 10f), y + (i / 2) * 56f, cellWidth, 50f);
                    Color rarity = RarityColor(item.rarity);
                    UiKit.Card(cell, new Color(rarity.r, rarity.g, rarity.b, 0.20f), 12f);
                    UiKit.Card(new Rect(cell.x, cell.y + 6f, 5f, cell.height - 12f), rarity, 2.5f);
                    GUI.Label(new Rect(cell.x + 12f, cell.y + 3f, cell.width - 18f, 24f), ItemName(item.tr, item.en), itemNameStyle);
                    var old = GUI.color;
                    GUI.color = rarity;
                    GUI.Label(new Rect(cell.x + 12f, cell.y + 26f, cell.width - 18f, 20f), T("rarity_" + item.rarity) + " - " + T("cat_" + item.category), smallStyle);
                    GUI.color = old;
                }
                y += 3 * 56f;

                if (pages > 1)
                {
                    if (inventoryPage > 0 && UiKit.SecondaryButton(new Rect(16f, y, 70f, 38f), "<", 20)) inventoryPage--;
                    GUI.Label(new Rect(0f, y, width, 38f), (inventoryPage + 1) + " / " + pages, smallCenterStyle);
                    if (inventoryPage < pages - 1 && UiKit.SecondaryButton(new Rect(width - 86f, y, 70f, 38f), ">", 20)) inventoryPage++;
                    y += 44f;
                }
            }

            float backY = Mathf.Max(y + 8f, height - 70f);
            if (UiKit.SecondaryButton(new Rect(24f, backY, width - 48f, 52f), T("back"), 20)) screen = Page.Menu;
        }

        // ---------- Sandık açma animasyonu ----------

        void DrawChestOpening(float width, float height, float top)
        {
            UiKit.Card(new Rect(0f, 0f, width, height), new Color(0.03f, 0.04f, 0.10f, 0.94f), 0f);

            float elapsed = Time.realtimeSinceStartup - openingStartedAt;
            bool revealing = openedResult != null && elapsed >= ChestShakeSeconds;

            if (openingFailed)
            {
                GUI.Label(new Rect(24f, height / 2f - 40f, width - 48f, 60f), T("connectionError"), centerStyle);
                if (UiKit.SecondaryButton(new Rect(24f, height / 2f + 40f, width - 48f, 56f), T("continue"), 22)) CloseChestOverlay();
                return;
            }

            if (!revealing)
            {
                // Sandık sallanır, giderek şiddetlenir
                float t = Mathf.Clamp01(elapsed / ChestShakeSeconds);
                float angle = Mathf.Sin(elapsed * 30f) * Mathf.Lerp(3f, 13f, t);
                float size = 150f + 16f * Mathf.Sin(elapsed * 9f) * t;
                var rect = new Rect(width / 2f - size / 2f, height / 2f - size / 2f - 30f, size, size);

                var oldMatrix = GUI.matrix;
                var pivot = rect.center;
                GUI.matrix = oldMatrix * Matrix4x4.Translate(pivot) * Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, angle)) * Matrix4x4.Translate(-pivot);
                DrawChestIcon(rect, openingTier);
                GUI.matrix = oldMatrix;

                GUI.Label(new Rect(0f, rect.yMax + 24f, width, 36f), T("chestOpening"), headingStyle);
                return;
            }

            // Eşya ortaya çıkar: nadirlik renginde parlayan kart
            float u = Mathf.Clamp01((elapsed - ChestShakeSeconds) / 0.45f);
            float pop = 1f + 0.18f * Mathf.Sin(u * Mathf.PI) - (1f - u) * 0.3f;
            Color rarity = RarityColor(openedResult.rarity);

            if (!revealSoundPlayed)
            {
                revealSoundPlayed = true;
                if (openedResult.rarity == "rare" || openedResult.rarity == "epic" || openedResult.rarity == "legendary") sound.MatchFound();
            }

            var card = new Rect(width / 2f - 135f, height / 2f - 150f, 270f, 250f);
            var oldM = GUI.matrix;
            GUI.matrix = oldM * Matrix4x4.Translate(card.center) * Matrix4x4.Scale(new Vector3(pop, pop, 1f)) * Matrix4x4.Translate(-card.center);

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 3.2f);
            for (int ring = 3; ring >= 1; ring--)
            {
                float grow = ring * 9f + pulse * 6f;
                UiKit.Card(new Rect(card.x - grow, card.y - grow, card.width + grow * 2f, card.height + grow * 2f), new Color(rarity.r, rarity.g, rarity.b, 0.10f), 26f + grow);
            }
            UiKit.Card(card, rarity, 24f);
            UiKit.Card(new Rect(card.x + 4f, card.y + 4f, card.width - 8f, card.height - 8f), new Color(0.07f, 0.08f, 0.18f), 21f);

            var old = GUI.color;
            GUI.color = new Color(rarity.r, rarity.g, rarity.b, 1f);
            GUI.Label(new Rect(card.x, card.y + 16f, card.width, 30f), openedResult.duplicate ? T("rarity_" + openedResult.rarity) : T("newItem"), rankStyle);
            GUI.color = old;

            DrawChestIcon(new Rect(card.center.x - 38f, card.y + 56f, 76f, 76f), openedResult.tier);
            GUI.color = rarity;
            GUI.Label(new Rect(card.x + 10f, card.y + 138f, card.width - 20f, 26f), T("rarity_" + openedResult.rarity) + " - " + T("cat_" + openedResult.category), smallCenterStyle);
            GUI.color = old;
            GUI.Label(new Rect(card.x + 10f, card.y + 164f, card.width - 20f, 34f), ItemName(openedResult.tr, openedResult.en), headingStyle);

            if (openedResult.duplicate)
                GUI.Label(new Rect(card.x + 14f, card.y + 202f, card.width - 28f, 40f), string.Format(T("duplicateNote"), openedResult.goldRefund), smallCenterWrapStyle());
            GUI.matrix = oldM;

            if (UiKit.SecondaryButton(new Rect(24f, card.yMax + 36f, width - 48f, 56f), T("continue"), 22)) CloseChestOverlay();
        }

        void CloseChestOverlay()
        {
            openingTier = null;
            openedResult = null;
            openingFailed = false;
        }

        // Geliştirme: sandık açma ekranını örnek veriyle gösterir (DevHarness)
        public void DevShowInventory(bool withItems, string openRarity = null, bool noChests = false)
        {
            profile = new ProfileDto { name = "Furkan", level = 4, gold = 1250, chestBronze = noChests ? 0 : 2, chestSilver = noChests ? 0 : 1, chestGold = 0, itemCount = 7 };
            var items = new List<ItemDto>();
            if (withItems)
            {
                items.Add(new ItemDto { id = "hat_cap", category = "head", rarity = "common", tr = "Şapka", en = "Cap" });
                items.Add(new ItemDto { id = "hair_afro", category = "head", rarity = "uncommon", tr = "Afro", en = "Afro" });
                items.Add(new ItemDto { id = "glasses_star", category = "glasses", rarity = "rare", tr = "Yıldız Gözlük", en = "Star Glasses" });
                items.Add(new ItemDto { id = "top_cape", category = "top", rarity = "epic", tr = "Pelerin", en = "Cape" });
                items.Add(new ItemDto { id = "hat_crown", category = "head", rarity = "legendary", tr = "Taç", en = "Crown" });
                items.Add(new ItemDto { id = "shoes_wing", category = "shoes", rarity = "legendary", tr = "Kanatlı Ayakkabı", en = "Winged Shoes" });
                items.Add(new ItemDto { id = "bottom_jeans", category = "bottom", rarity = "common", tr = "Kot Pantolon", en = "Jeans" });
            }
            inventory = new InventoryDto { items = items.ToArray() };
            inventoryPage = 0;
            screen = Page.Inventory;
            openingTier = null;
            if (openRarity != null)
            {
                openingTier = "silver";
                openingStartedAt = Time.realtimeSinceStartup - ChestShakeSeconds - 0.6f;
                openedResult = new ChestResultDto { tier = "silver", itemId = "top_cape", category = "top", rarity = openRarity, tr = "Pelerin", en = "Cape", duplicate = false, profile = profile };
                revealSoundPlayed = true;
            }
        }

        public void DevShowChestShaking()
        {
            DevShowInventory(true);
            openingTier = "bronze";
            openingStartedAt = Time.realtimeSinceStartup;
            openedResult = null;
            revealSoundPlayed = false;
        }
    }
}
