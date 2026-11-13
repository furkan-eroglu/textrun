using System;
using System.Collections.Generic;
using TextRun.Audio;
using TextRun.Common;
using TextRun.Online;
using TextRun.Typing;
using UnityEngine;

namespace TextRun.Game
{
    // Envanter ekranı: kategori sekmeleri (kafa, üst, alt, ayakkabı, pelerin), sandıklar ve sandık açma animasyonu.
    // Altın, sandık ve eşyalar sunucuda tutulur; burada yalnızca gösterilir ve istekler gönderilir.
    public partial class OnlineGameController
    {
        static readonly string[] ChestTiers = { "bronze", "silver", "gold" };
        static readonly string[] Categories = { "head", "top", "bottom", "shoes", "cape" };
        const int ChestTab = 5;
        const int GridColumns = 4;

        InventoryDto inventory;       // null = henüz okunmadı
        float inventoryTriedAt = -999f;
        int inventoryTab;             // 0-4 kategoriler (Categories sırasıyla), 5 sandıklar
        GUIStyle itemNameStyle, chestNameStyle, tinyNameStyle, tabStyle;
        readonly OutfitPreview preview = new OutfitPreview();

        // Yeni kazanılan, henüz bakılmamış eşyalar: menüdeki Envanter düğmesinde ve sekmelerde sayı, eşyada "YENİ" işareti.
        // İlgili sekme açılıp kapatılınca (ya da envanterden çıkınca) görülmüş sayılır. Cihazda saklanır.
        const string NewItemsKey = "textrun.newitems";
        HashSet<string> newItems;

        HashSet<string> NewItems
        {
            get
            {
                if (newItems == null)
                {
                    newItems = new HashSet<string>();
                    foreach (var id in PlayerPrefs.GetString(NewItemsKey, "").Split(','))
                        if (id.Length > 0) newItems.Add(id);
                }
                return newItems;
            }
        }

        void SaveNewItems()
        {
            PlayerPrefs.SetString(NewItemsKey, string.Join(",", new List<string>(NewItems).ToArray()));
            PlayerPrefs.Save();
        }

        void MarkNewItem(string itemId)
        {
            if (!string.IsNullOrEmpty(itemId) && NewItems.Add(itemId)) SaveNewItems();
        }

        int NewItemCount(string category)
        {
            int count = 0;
            foreach (var id in NewItems)
                if (Characters.Outfits.CategoryOf(id) == category) count++;
            return count;
        }

        // Bir kategorideki (null = hepsi) yeni işaretlerini kaldırır
        void ClearNewItems(string category)
        {
            if (NewItems.Count == 0) return;
            if (category == null) NewItems.Clear();
            else NewItems.RemoveWhere(id => Characters.Outfits.CategoryOf(id) == category);
            SaveNewItems();
        }

        // Envanter sekmesi değişirken ya da envanterden çıkarken: bakılan kategorinin işaretleri silinir
        void LeaveInventoryTab()
        {
            if (inventoryTab >= 0 && inventoryTab < Categories.Length) ClearNewItems(Categories[inventoryTab]);
        }

        static void DrawBadge(Rect at, int count, GUIStyle style)
        {
            UiKit.Card(at, new Color(0.93f, 0.25f, 0.25f), at.height / 2f);
            GUI.Label(at, count.ToString(), style);
        }

        // Sandık açma
        const float ChestDropSeconds = 0.45f;
        const float ChestShakeSeconds = RaceAudio.ChestRiserSeconds;
        string openingTier;           // null = animasyon yok
        float openingStartedAt;
        ChestResultDto openedResult;
        bool openingFailed;
        float revealAt = -1f;         // eşyanın ortaya çıktığı an (-1 = henüz çıkmadı)
        float nextRattleAt;
        bool riserPlayed;
        bool chestToMenu;             // animasyon bitince menüye dön (reklam sandığı)

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

        static int RarityLevel(string rarity) => rarity == "legendary" ? 2 : rarity == "epic" || rarity == "rare" ? 1 : 0;

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

        // Eşyanın resmi: nadirlik renginde zemin üstünde (resim hazır değilse yalnızca zemin)
        static void DrawItemIcon(Rect r, string itemId, string rarity, float backAlpha = 0.24f)
        {
            Color tint = RarityColor(rarity);
            UiKit.Card(r, new Color(tint.r * 0.5f, tint.g * 0.5f, tint.b * 0.5f, backAlpha + 0.2f), 12f);
            UiKit.Card(new Rect(r.x + 2f, r.y + 2f, r.width - 4f, r.height - 4f), new Color(tint.r, tint.g, tint.b, backAlpha), 10f);
            var texture = ItemIcons.Get(itemId);
            if (texture != null) GUI.DrawTexture(new Rect(r.x + 2f, r.y + 2f, r.width - 4f, r.height - 4f), texture, ScaleMode.ScaleToFit, true);
        }

        void EnsureInventoryStyles()
        {
            if (itemNameStyle != null) return;
            itemNameStyle = new GUIStyle(rowNameStyle) { fontSize = 15, fontStyle = FontStyle.Bold, wordWrap = true, clipping = TextClipping.Clip, alignment = TextAnchor.UpperLeft };
            UiKit.SetAllTextColors(itemNameStyle, Color.white);
            chestNameStyle = new GUIStyle(rowNameStyle) { fontSize = 17, wordWrap = false, clipping = TextClipping.Clip };
            tinyNameStyle = new GUIStyle(rowNameStyle) { fontSize = 11, fontStyle = FontStyle.Bold, wordWrap = true, clipping = TextClipping.Overflow, alignment = TextAnchor.UpperCenter };
            UiKit.SetAllTextColors(tinyNameStyle, Color.white);
            tabStyle = new GUIStyle(rowNameStyle) { fontSize = 13, fontStyle = FontStyle.Bold, wordWrap = false, alignment = TextAnchor.MiddleCenter };
            UiKit.SetAllTextColors(tabStyle, Color.white);
        }

        // ---------- Sayfa ----------

        void OpenInventory()
        {
            inventoryTab = profile.TotalChests > 0 ? ChestTab : 0;
            if (profile.TotalChests == 0)
                for (int i = 0; i < Categories.Length; i++)
                    if (NewItemCount(Categories[i]) > 0) { inventoryTab = i; break; }
            openingTier = null;
            screen = Page.Inventory;
            LoadInventory();
        }

        async void LoadInventory()
        {
            inventoryTriedAt = Time.realtimeSinceStartup;
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

        void DrawInventory(float width, float height, float top)
        {
            FullViewport();
            EnsureInventoryStyles();

            if (openingTier != null)
            {
                preview.Hide();
                DrawChestOpening(width, height, top);
                return;
            }

            float y = top + 8f;
            GUI.Label(new Rect(0f, y, width, 40f), T("inventory"), headingStyle);
            y += 46f;

            // Sekmeler: beş kategori + sandıklar
            float tabWidth = (width - 24f - 5f * 4f) / 6f;
            for (int i = 0; i <= ChestTab; i++)
            {
                var tab = new Rect(12f + i * (tabWidth + 4f), y, tabWidth, 36f);
                bool selected = i == inventoryTab;
                UiKit.Card(tab, selected ? new Color(0.20f, 0.52f, 1f) : new Color(1f, 1f, 1f, 0.10f), 10f);
                string label = i == ChestTab ? T("tab_chests") : T("tab_" + Categories[i]);
                GUI.Label(tab, label, tabStyle);
                // Köşede kırmızı rozet: açılmamış sandık sayısı ya da o kategorideki yeni eşya sayısı
                int badgeCount = i == ChestTab ? profile.TotalChests : NewItemCount(Categories[i]);
                if (badgeCount > 0) DrawBadge(new Rect(tab.xMax - 15f, tab.y - 6f, 19f, 19f), badgeCount, tabStyle);
                if (GUI.Button(tab, GUIContent.none, GUIStyle.none) && i != inventoryTab) { LeaveInventoryTab(); inventoryTab = i; sound.Click(); }
            }
            y += 44f;

            if (inventoryTab == ChestTab) DrawChestList(width, y);
            else
            {
                DrawCategory(width, height, y, Categories[inventoryTab]);
                DrawBodyStrip(width, height - 116f);
            }

            if (UiKit.SecondaryButton(new Rect(24f, height - 64f, width - 48f, 50f), T("back"), 20)) { LeaveInventoryTab(); screen = Page.Menu; }
        }

        // Sandıklar: yalnızca sahip olunanlar gösterilir (hiç yoksa kısa bir not)
        void DrawChestList(float width, float y)
        {
            preview.Hide();
            if (profile.TotalChests == 0)
            {
                GUI.Label(new Rect(24f, y + 30f, width - 48f, 30f), T("noChests"), smallCenterStyle);
                return;
            }
            foreach (var tier in ChestTiers)
            {
                int count = profile.ChestCount(tier);
                if (count <= 0) continue;
                var row = new Rect(16f, y, width - 32f, 72f);
                UiKit.Card(row, new Color(TierColor(tier).r, TierColor(tier).g, TierColor(tier).b, 0.20f), 14f);
                DrawChestIcon(new Rect(row.x + 10f, row.y + 8f, 56f, 56f), tier);
                GUI.Label(new Rect(row.x + 76f, row.y, row.width - 180f, row.height), T("chest_" + tier) + "  x" + count, chestNameStyle);
                if (UiKit.SecondaryButton(new Rect(row.xMax - 100f, row.y + 14f, 90f, 44f), T("open"), 18)) BeginOpenChest(tier);
                y += 80f;
            }
        }

        bool IsWorn(string id)
        {
            if (inventory?.equipped == null) return false;
            return Array.IndexOf(inventory.equipped, id) >= 0;
        }

        // Bir kategori: üstte karakter (giydikleriyle), altta o kategoride sahip olunan eşyaların resimli ızgarası.
        // Eşyaya dokunmak giyer, giyili eşyaya dokunmak çıkarır.
        void DrawCategory(float width, float height, float y, string category)
        {
            var owned = new List<ItemDto>();
            if (inventory?.items != null)
                foreach (var item in inventory.items)
                    if (item.category == category) owned.Add(item);

            float cellWidth = (width - 32f - (GridColumns - 1) * 8f) / GridColumns;
            float cellHeight = cellWidth + 34f;
            int rows = Mathf.Max(1, Mathf.CeilToInt(owned.Count / (float)GridColumns));
            float gridHeight = rows * (cellHeight + 6f);
            float previewHeight = Mathf.Clamp(height - 124f - y - gridHeight - 12f, 100f, 210f);

            var box = new Rect(16f, y, width - 32f, previewHeight);
            UiKit.Card(box, new Color(1f, 1f, 1f, 0.06f), 18f);
            preview.Show(FullLook(), category == "cape" ? 180f : 0f);
            if (preview.Texture != null) GUI.DrawTexture(new Rect(box.center.x - previewHeight * 0.4f, box.y, previewHeight * 0.8f, previewHeight), preview.Texture, ScaleMode.ScaleToFit, true);
            y += previewHeight + 10f;

            if (inventory == null) { GUI.Label(new Rect(20f, y + 20f, width - 40f, 30f), "...", smallCenterStyle); return; }
            if (owned.Count == 0) { GUI.Label(new Rect(28f, y + 14f, width - 56f, 50f), T("noItemsHere"), smallCenterWrapStyle()); return; }

            for (int i = 0; i < owned.Count; i++)
            {
                var item = owned[i];
                var cell = new Rect(16f + (i % GridColumns) * (cellWidth + 8f), y + (i / GridColumns) * (cellHeight + 6f), cellWidth, cellHeight);
                bool worn = IsWorn(item.id);
                var icon = new Rect(cell.x, cell.y, cellWidth, cellWidth);
                if (worn) UiKit.Card(new Rect(icon.x - 3f, icon.y - 3f, icon.width + 6f, icon.height + 6f), Color.white, 14f);
                DrawItemIcon(icon, item.id, item.rarity, worn ? 0.55f : 0.24f);
                if (worn)
                {
                    // Giyili işareti: köşede yeşil yuvarlak
                    UiKit.Card(new Rect(icon.xMax - 20f, icon.y + 4f, 16f, 16f), Color.white, 8f);
                    UiKit.Card(new Rect(icon.xMax - 18f, icon.y + 6f, 12f, 12f), new Color(0.20f, 0.80f, 0.35f), 6f);
                }
                if (NewItems.Contains(item.id))
                {
                    var tag = new Rect(icon.x + 3f, icon.y + 3f, 34f, 16f);
                    UiKit.Card(tag, new Color(0.93f, 0.25f, 0.25f), 6f);
                    GUI.Label(tag, T("newTag"), tinyNameStyle);
                }
                GUI.Label(new Rect(cell.x - 4f, icon.yMax + 3f, cellWidth + 8f, 32f), ItemName(item.tr, item.en), tinyNameStyle);
                if (GUI.Button(cell, GUIContent.none, GUIStyle.none)) { sound.Click(); ToggleWear(item); }
            }
        }

        // Karakterin tam görünümü: takılı eşyalar + beden işaretleri (ten rengi, cinsiyet)
        string[] FullLook()
        {
            var look = new List<string>(inventory?.equipped ?? new string[0]);
            if (inventory != null)
            {
                if (inventory.skin > 0) look.Add("skin_" + inventory.skin);
                if (inventory.gender == "f") look.Add("gender_f");
            }
            return look.ToArray();
        }

        // Envanterin altındaki sabit şerit: üç ten rengi ve cinsiyet seçimi
        void DrawBodyStrip(float width, float y)
        {
            if (inventory == null) return;
            for (int tone = 0; tone < Characters.CharacterBuilder.SkinTones.Length; tone++)
            {
                var spot = new Rect(18f + tone * 46f, y + 3f, 38f, 38f);
                if (tone == inventory.skin) UiKit.Card(new Rect(spot.x - 3f, spot.y - 3f, spot.width + 6f, spot.height + 6f), Color.white, 22f);
                UiKit.Card(new Rect(spot.x - 1f, spot.y - 1f, spot.width + 2f, spot.height + 2f), new Color(0f, 0f, 0f, 0.5f), 20f);
                UiKit.Card(spot, Characters.CharacterBuilder.SkinTones[tone], 19f);
                if (GUI.Button(spot, GUIContent.none, GUIStyle.none) && tone != inventory.skin) { sound.Click(); SetBody(tone, inventory.gender); }
            }

            float left = 18f + 3f * 46f + 8f;
            int current = inventory.gender == "f" ? 1 : 0;
            int picked = UiKit.Segmented(new Rect(left, y, width - 16f - left, 44f), new[] { T("male"), T("female") }, current, 16);
            if (picked >= 0 && picked != current) SetBody(inventory.skin, picked == 1 ? "f" : "m");
        }

        // Ekran hemen güncellenir, sunucu arkada kaydeder (hata olursa envanter yeniden okunur)
        async void SetBody(int skin, string gender)
        {
            if (inventory == null || net == null) return;
            inventory.skin = skin;
            inventory.gender = gender;
            try
            {
                await net.SetBodyAsync(skin, gender);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Beden kaydedilemedi: " + e.Message);
                LoadInventory();
            }
        }

        // Giy / çıkar: ekran hemen güncellenir, sunucu arkada kaydeder (hata olursa envanter yeniden okunur)
        async void ToggleWear(ItemDto item)
        {
            if (inventory == null || net == null) return;
            bool wasWorn = IsWorn(item.id);

            var next = new List<string>();
            foreach (var id in inventory.equipped ?? new string[0])
                if (Characters.Outfits.CategoryOf(id) != item.category) next.Add(id);
            if (!wasWorn) next.Add(item.id);
            inventory.equipped = next.ToArray();

            try
            {
                await net.EquipAsync(wasWorn ? null : item.id, item.category);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Esya takilamadi: " + e.Message);
                LoadInventory();
            }
        }

        // ---------- Sandık açma ----------

        void StartChestOverlay(string tier, bool toMenu)
        {
            openingTier = tier;
            openingStartedAt = Time.realtimeSinceStartup;
            openedResult = null;
            openingFailed = false;
            revealAt = -1f;
            nextRattleAt = ChestDropSeconds;
            riserPlayed = false;
            chestToMenu = toMenu;
        }

        async void BeginOpenChest(string tier)
        {
            if (openingTier != null || net == null || profile.ChestCount(tier) <= 0) return;
            StartChestOverlay(tier, false);
            try
            {
                var result = await net.OpenChestAsync(tier);
                openedResult = result;
                if (!result.duplicate) MarkNewItem(result.itemId);
                if (result.profile != null) profile = result.profile;
                LoadInventory();
            }
            catch (Exception e)
            {
                Debug.LogWarning("Sandik acilamadi: " + e.Message);
                openingFailed = true;
            }
        }

        // Günlük reklam sandığı: menüden açılır, animasyon bitince menüye dönülür.
        // NOT: reklam SDK'sı eklenince önce reklam gösterilecek; şimdilik doğrudan açılır.
        async void BeginAdChest()
        {
            if (openingTier != null || net == null || profile.guest || AdChestSecondsLeft() > 0) return;
            StartChestOverlay("silver", true);
            screen = Page.Inventory;
            try
            {
                var result = await net.ClaimAdChestAsync();
                openedResult = result;
                if (!result.duplicate) MarkNewItem(result.itemId);
                if (result.profile != null) profile = result.profile;
                LoadInventory();
            }
            catch (Exception e)
            {
                Debug.LogWarning("Reklam sandigi acilamadi: " + e.Message);
                openingFailed = true;
            }
        }

        static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t) - 1f;
            return 1f + 2.70158f * t * t * t + 1.70158f * t * t;
        }

        static void DrawSpinning(Texture texture, Vector2 center, float size, float angle, Color color)
        {
            var old = GUI.matrix;
            var oldColor = GUI.color;
            GUI.matrix = old * Matrix4x4.Translate(center) * Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, angle)) * Matrix4x4.Translate(-center);
            GUI.color = color;
            GUI.DrawTexture(new Rect(center.x - size / 2f, center.y - size / 2f, size, size), texture);
            GUI.color = oldColor;
            GUI.matrix = old;
        }

        // Aşamalar: sandık yukarıdan düşer -> giderek şiddetlenen sallanma (tıkırtı + gerilim sesi, arkada büyüyen ışık)
        // -> beyaz patlama -> nadirlik renginde dönen ışınlar, saçılan parıltılar ve büyüyerek gelen eşya.
        void DrawChestOpening(float width, float height, float top)
        {
            UiKit.Card(new Rect(0f, 0f, width, height), new Color(0.02f, 0.03f, 0.08f, 0.96f), 0f);

            float now = Time.realtimeSinceStartup;
            float elapsed = now - openingStartedAt;
            bool repaint = Event.current.type == EventType.Repaint;
            var center = new Vector2(width / 2f, height * 0.42f);

            if (openingFailed)
            {
                GUI.Label(new Rect(24f, height / 2f - 40f, width - 48f, 60f), T("connectionError"), centerStyle);
                if (UiKit.SecondaryButton(new Rect(24f, height / 2f + 40f, width - 48f, 56f), T("continue"), 22)) CloseChestOverlay();
                return;
            }

            if (openedResult != null) ItemIcons.Get(openedResult.itemId); // resim açılış anına hazır olsun

            if (revealAt < 0f)
            {
                float shake = Mathf.Clamp01((elapsed - ChestDropSeconds) / ChestShakeSeconds);
                if (repaint && elapsed >= ChestDropSeconds)
                {
                    if (!riserPlayed) { riserPlayed = true; sound.ChestRiser(); }
                    if (elapsed >= nextRattleAt)
                    {
                        sound.ChestRattle(Mathf.Lerp(0.9f, 1.8f, shake));
                        nextRattleAt = elapsed + Mathf.Lerp(0.26f, 0.07f, shake);
                    }
                    if (shake >= 1f && openedResult != null)
                    {
                        revealAt = now;
                        sound.ChestOpen(RarityLevel(openedResult.rarity));
                    }
                }

                // Arkada büyüyen ışık
                Color tier = TierColor(openingTier);
                DrawSpinning(Pictograms.Rays(), center, 220f + 240f * shake, elapsed * 50f, new Color(tier.r, tier.g, tier.b, 0.15f + 0.5f * shake));

                float drop = Mathf.Clamp01(elapsed / ChestDropSeconds);
                float scale = EaseOutBack(drop) * (1f + 0.07f * Mathf.Sin(elapsed * 15f) * shake);
                float angle = Mathf.Sin(elapsed * 38f) * Mathf.Lerp(1.5f, 15f, shake * shake);
                var jitter = new Vector2(Mathf.Sin(elapsed * 51f), Mathf.Cos(elapsed * 43f)) * (5f * shake);
                var pivot = center + jitter + new Vector2(0f, -(1f - drop) * (1f - drop) * 260f);
                const float size = 170f;

                var oldMatrix = GUI.matrix;
                GUI.matrix = oldMatrix * Matrix4x4.Translate(pivot) * Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, angle)) * Matrix4x4.Scale(new Vector3(scale, scale, 1f)) * Matrix4x4.Translate(-pivot);
                DrawChestIcon(new Rect(pivot.x - size / 2f, pivot.y - size / 2f, size, size), openingTier);
                GUI.matrix = oldMatrix;

                // Kapaktan sızan ışık: sallanma arttıkça parlar
                if (shake > 0.35f)
                    UiKit.Card(new Rect(pivot.x - 70f, pivot.y - 12f, 140f, 10f), new Color(1f, 0.97f, 0.75f, (shake - 0.35f) * 1.4f), 5f);
                return;
            }

            // ---- Eşya ortaya çıktı ----
            float r = now - revealAt;
            Color rarity = RarityColor(openedResult.rarity);
            int level = RarityLevel(openedResult.rarity);

            float raySize = Mathf.Min(width * 1.7f, 620f) * Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(r / 0.4f));
            DrawSpinning(Pictograms.Rays(), center, raySize, r * 34f, new Color(rarity.r, rarity.g, rarity.b, 0.55f));
            DrawSpinning(Pictograms.Rays(), center, raySize * 0.7f, -r * 22f + 13f, new Color(1f, 1f, 1f, 0.16f + 0.08f * level));

            // Saçılan parıltılar (nadir eşyada daha çok)
            int sparkles = 18 + level * 12;
            for (int i = 0; i < sparkles; i++)
            {
                float life = Mathf.Repeat(r * (0.55f + (i % 4) * 0.09f) + i * 0.137f, 1f);
                if (r < 0.9f) life = Mathf.Min(life, r / 0.9f); // ilk an hepsi merkezden fırlar
                float direction = i * 137.5f * Mathf.Deg2Rad;
                float distance = life * (150f + (i % 5) * 34f);
                var at = center + new Vector2(Mathf.Cos(direction), Mathf.Sin(direction)) * distance;
                float dot = (7f - (i % 3) * 1.8f) * (1f - life * 0.5f);
                Color tint = i % 3 == 0 ? Color.white : rarity;
                var oldM = GUI.matrix;
                GUI.matrix = oldM * Matrix4x4.Translate(at) * Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, 45f + r * 120f)) * Matrix4x4.Translate(-at);
                UiKit.Card(new Rect(at.x - dot / 2f, at.y - dot / 2f, dot, dot), new Color(tint.r, tint.g, tint.b, 1f - life), 1.5f);
                GUI.matrix = oldM;
            }

            // Eşya: büyüyerek gelir, hafifçe nefes alır
            float pop = EaseOutBack(r / 0.5f) * (1f + 0.03f * Mathf.Sin(r * 3.4f));
            float iconSize = 176f * pop;
            float pulse = 0.5f + 0.5f * Mathf.Sin(r * 3.2f);
            for (int ring = 3; ring >= 1; ring--)
            {
                float grow = iconSize + ring * 16f + pulse * 10f;
                UiKit.Card(new Rect(center.x - grow / 2f, center.y - grow / 2f, grow, grow), new Color(rarity.r, rarity.g, rarity.b, 0.10f), grow / 2f);
            }
            UiKit.Card(new Rect(center.x - iconSize / 2f - 4f, center.y - iconSize / 2f - 4f, iconSize + 8f, iconSize + 8f), rarity, 22f);
            UiKit.Card(new Rect(center.x - iconSize / 2f, center.y - iconSize / 2f, iconSize, iconSize), new Color(0.07f, 0.08f, 0.18f), 18f);
            DrawItemIcon(new Rect(center.x - iconSize / 2f, center.y - iconSize / 2f, iconSize, iconSize), openedResult.itemId, openedResult.rarity, 0.3f);

            var old = GUI.color;
            float textAlpha = Mathf.Clamp01((r - 0.25f) / 0.3f);
            GUI.color = new Color(rarity.r, rarity.g, rarity.b, textAlpha);
            GUI.Label(new Rect(0f, center.y - 150f, width, 34f), openedResult.duplicate ? T("rarity_" + openedResult.rarity) : T("newItem"), rankStyle);
            GUI.Label(new Rect(10f, center.y + 132f, width - 20f, 24f), T("rarity_" + openedResult.rarity) + " - " + T("cat_" + openedResult.category), smallCenterStyle);
            GUI.color = new Color(1f, 1f, 1f, textAlpha);
            GUI.Label(new Rect(10f, center.y + 98f, width - 20f, 36f), ItemName(openedResult.tr, openedResult.en), headingStyle);
            if (openedResult.duplicate)
                GUI.Label(new Rect(24f, center.y + 158f, width - 48f, 40f), string.Format(T("duplicateNote"), openedResult.goldRefund), smallCenterWrapStyle());
            GUI.color = old;

            // Beyaz patlama (en üstte)
            if (r < 0.45f) UiKit.Card(new Rect(0f, 0f, width, height), new Color(1f, 1f, 1f, 1f - r / 0.45f), 0f);

            if (r > 0.9f && UiKit.SecondaryButton(new Rect(24f, height - 84f, width - 48f, 56f), T("continue"), 22)) CloseChestOverlay();
        }

        void CloseChestOverlay()
        {
            if (openedResult != null && !openedResult.duplicate && !chestToMenu)
            {
                int tab = Array.IndexOf(Categories, openedResult.category);
                if (tab >= 0 && profile.TotalChests == 0) inventoryTab = tab; // sandık kalmadıysa yeni eşyanın sekmesine geç
            }
            openingTier = null;
            openedResult = null;
            openingFailed = false;
            if (chestToMenu)
            {
                chestToMenu = false;
                screen = Page.Menu;
            }
        }

        // ---------- Geliştirme (DevHarness) ----------

        static ItemDto DevItem(string id, string rarity, string tr) => new ItemDto { id = id, category = Characters.Outfits.CategoryOf(id), rarity = rarity, tr = tr, en = tr };

        // Envanteri örnek veriyle gösterir. tab: 0-4 kategoriler, 5 sandıklar
        public void DevShowInventory(int tab, bool noChests = false)
        {
            profile = new ProfileDto { name = "Furkan", level = 4, gold = 1250, diamonds = 320, chestBronze = noChests ? 0 : 2, chestSilver = noChests ? 0 : 1, itemCount = 14 };
            inventory = new InventoryDto
            {
                items = new[]
                {
                    DevItem("hat_cap", "common", "Spor Şapka"), DevItem("hair_afro", "uncommon", "Afro"), DevItem("glasses_sun", "uncommon", "Güneş Gözlüğü"),
                    DevItem("hat_pop", "rare", "Popstar Şapkası"), DevItem("hat_pirate", "epic", "Korsan Şapkası"), DevItem("hat_crown", "legendary", "Kral Tacı"),
                    DevItem("top_hawaii", "rare", "Hawaii Gömleği"), DevItem("top_ninja", "epic", "Ninja Kıyafeti"),
                    DevItem("bottom_jeans", "common", "Mavi Kot"), DevItem("bottom_flame", "epic", "Alevli Pantolon"),
                    DevItem("shoes_sneaker", "common", "Spor Ayakkabı"), DevItem("shoes_skate", "epic", "Tekerlekli Paten"),
                    DevItem("cape_hero", "rare", "Kahraman Pelerini"), DevItem("cape_wings", "legendary", "Melek Kanatları"),
                },
                equipped = new[] { "hat_pop", "top_hawaii", "bottom_jeans", "shoes_sneaker", "cape_hero" },
            };
            inventoryTab = tab;
            openingTier = null;
            screen = Page.Inventory;
        }

        // Sandık açılışını belirli bir anda dondurmadan gösterir: revealed=false sallanma ortası, true eşya ortaya çıkmış
        public void DevShowChest(bool revealed, string rarity = "epic")
        {
            DevShowInventory(ChestTab);
            StartChestOverlay("silver", false);
            if (!revealed)
            {
                openingStartedAt = Time.realtimeSinceStartup - ChestDropSeconds - ChestShakeSeconds * 0.7f;
                riserPlayed = true;
                nextRattleAt = 999f;
                return;
            }
            openedResult = new ChestResultDto { tier = "silver", itemId = rarity == "legendary" ? "cape_wings" : "top_ninja", category = rarity == "legendary" ? "cape" : "top", rarity = rarity, tr = rarity == "legendary" ? "Melek Kanatları" : "Ninja Kıyafeti", en = "Item", profile = profile };
            openingStartedAt = Time.realtimeSinceStartup - 10f;
            revealAt = Time.realtimeSinceStartup - 1.2f;
            riserPlayed = true;
        }

        public void DevSetAdChestTimer(int seconds) { profile = new ProfileDto { name = "Furkan", level = 4, gold = 1250, diamonds = 320, xpIntoLevel = 110, xpForNext = 220, adChestSeconds = seconds }; }

        public void DevSetBody(int skin, string gender) { if (inventory != null) { inventory.skin = skin; inventory.gender = gender; } }

        public void DevMarkNew(params string[] ids) { foreach (var id in ids) MarkNewItem(id); }

        public bool DevSuppressGui;

        // Galeri için: istemcinin çizebildiği tüm eşya kimlikleri
        public static string[] DevAllOutfitIds() => Characters.Outfits.AllIds();
    }
}
