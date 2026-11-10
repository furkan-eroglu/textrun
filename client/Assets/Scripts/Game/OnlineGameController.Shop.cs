using System;
using TextRun.Online;
using UnityEngine;

namespace TextRun.Game
{
    // Mağaza ekranı: her gün değişen 4 eşya, altınla ve elmasla birer sandık, elmasla yenileme.
    // Fiyatlar, günün eşyaları ve bakiyeler sunucudadır; burada yalnızca gösterilir ve "satın al" isteği gönderilir.
    public partial class OnlineGameController
    {
        ShopDto shop;                 // null = henüz okunmadı
        float shopFetchedAt;
        int shopSelected;             // önizlemede gösterilen eşya yuvası
        string shopNotice = "";
        bool shopBusy;
        bool shopReloading;

        static void DrawDiamond(Rect r)
        {
            var old = GUI.matrix;
            GUI.matrix = old * Matrix4x4.Translate(r.center) * Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, 45f)) * Matrix4x4.Translate(-r.center);
            float s = r.width * 0.72f;
            var body = new Rect(r.center.x - s / 2f, r.center.y - s / 2f, s, s);
            UiKit.Card(body, new Color(0.10f, 0.55f, 0.85f), 3f);
            UiKit.Card(new Rect(body.x + s * 0.12f, body.y + s * 0.12f, s * 0.76f, s * 0.76f), new Color(0.35f, 0.85f, 1f), 2f);
            UiKit.Card(new Rect(body.x + s * 0.2f, body.y + s * 0.2f, s * 0.3f, s * 0.3f), new Color(0.85f, 0.97f, 1f), 1f);
            GUI.matrix = old;
        }

        static void DrawCurrency(Rect r, string currency)
        {
            if (currency == "diamond") DrawDiamond(r);
            else DrawCoin(r);
        }

        void OpenShop()
        {
            shopNotice = "";
            shopSelected = 0;
            screen = Page.Shop;
            LoadShop();
        }

        void ApplyShop(ShopResultDto result)
        {
            if (result == null || result.shop == null) return;
            shop = result.shop;
            shopFetchedAt = Time.realtimeSinceStartup;
            if (result.profile != null) profile = result.profile;
            if (shop.slots == null) shop.slots = new ShopSlotDto[0];
            shopSelected = Mathf.Clamp(shopSelected, 0, Mathf.Max(0, shop.slots.Length - 1));
        }

        async void LoadShop()
        {
            if (net == null || shopReloading) return;
            shopReloading = true;
            try
            {
                ApplyShop(await net.GetShopAsync());
            }
            catch (Exception e)
            {
                Debug.LogWarning("Magaza okunamadi: " + e.Message);
                shopNotice = T("connectionError");
            }
            shopReloading = false;
        }

        // slot: 0-3 eşya, 4 altınla sandık, 5 elmasla sandık; -1 = elmasla yenile
        async void ShopAction(int slot, string currency)
        {
            if (net == null || shopBusy) return;
            shopBusy = true;
            shopNotice = "";
            try
            {
                ApplyShop(slot < 0 ? await net.ShopRefreshAsync() : await net.ShopBuyAsync(slot));
                if (slot < 0) shopSelected = 0;
                else sound.MatchFound();
            }
            catch (Exception e)
            {
                if (OnlineRaceClient.IsError(e, "not_enough")) shopNotice = T(currency == "diamond" ? "notEnoughDiamonds" : "notEnoughGold");
                else if (OnlineRaceClient.IsError(e, "already_bought")) LoadShop();
                else
                {
                    Debug.LogWarning("Magaza islemi basarisiz: " + e.Message);
                    shopNotice = T("connectionError");
                }
            }
            shopBusy = false;
        }

        static string Clock(int seconds)
        {
            seconds = Mathf.Max(0, seconds);
            return (seconds / 3600).ToString("00") + ":" + (seconds / 60 % 60).ToString("00") + ":" + (seconds % 60).ToString("00");
        }

        bool CanAfford(string currency, int price) => (currency == "diamond" ? profile.diamonds : profile.gold) >= price;

        // Fiyat düğmesi: para simgesi + fiyat. Bakiye yetmiyorsa soluk çizilir (dokununca uyarı verir).
        bool PriceButton(Rect r, string currency, int price, int fontSize = 18)
        {
            bool affordable = CanAfford(currency, price);
            bool clicked;
            string label = "      " + price;
            if (affordable) clicked = UiKit.SecondaryButton(r, label, fontSize);
            else
            {
                UiKit.DisabledButton(r, label, fontSize);
                clicked = GUI.Button(r, GUIContent.none, GUIStyle.none);
            }
            float icon = Mathf.Min(22f, r.height - 16f);
            float textWidth = 12f + price.ToString().Length * fontSize * 0.6f;
            DrawCurrency(new Rect(r.center.x - textWidth / 2f - icon * 0.75f, r.center.y - icon / 2f - 2f, icon, icon), currency);
            if (clicked && !affordable)
            {
                shopNotice = T(currency == "diamond" ? "notEnoughDiamonds" : "notEnoughGold");
                return false;
            }
            return clicked;
        }

        void DrawShop(float width, float height, float top)
        {
            FullViewport();
            EnsureInventoryStyles();

            float y = top + 8f;
            GUI.Label(new Rect(0f, y, width, 40f), T("shop"), headingStyle);
            y += 46f;

            // Bakiyeler: altın ve elmas
            float half = (width - 32f - 10f) / 2f;
            var goldBox = new Rect(16f, y, half, 40f);
            var diamondBox = new Rect(16f + half + 10f, y, half, 40f);
            UiKit.Card(goldBox, new Color(1f, 1f, 1f, 0.08f), 14f);
            UiKit.Card(diamondBox, new Color(1f, 1f, 1f, 0.08f), 14f);
            DrawCoin(new Rect(goldBox.x + 12f, goldBox.y + 8f, 24f, 24f));
            GUI.Label(new Rect(goldBox.x + 46f, goldBox.y, goldBox.width - 52f, 40f), profile.gold.ToString(), rowNameStyle);
            DrawDiamond(new Rect(diamondBox.x + 12f, diamondBox.y + 8f, 24f, 24f));
            GUI.Label(new Rect(diamondBox.x + 46f, diamondBox.y, diamondBox.width - 52f, 40f), profile.diamonds.ToString(), rowNameStyle);
            y += 48f;

            if (shop == null)
            {
                preview.Hide();
                GUI.Label(new Rect(20f, y + 40f, width - 40f, 30f), shopNotice.Length > 0 ? shopNotice : "...", smallCenterStyle);
                if (UiKit.SecondaryButton(new Rect(24f, height - 70f, width - 48f, 52f), T("back"), 20)) screen = Page.Menu;
                return;
            }

            // Geri sayım (gece 00.00) ve elmasla yenileme
            int left = shop.secondsLeft - Mathf.FloorToInt(Time.realtimeSinceStartup - shopFetchedAt);
            if (left <= 0) LoadShop();
            GUI.Label(new Rect(20f, y + 4f, width - 170f, 20f), T("refreshesIn"), smallStyle);
            GUI.Label(new Rect(20f, y + 24f, width - 170f, 24f), Clock(left), rowNameStyle);
            GUI.Label(new Rect(width - 136f, y - 2f, 120f, 18f), T("refresh"), smallCenterStyle);
            if (PriceButton(new Rect(width - 136f, y + 16f, 120f, 38f), "diamond", shop.refreshCost)) ShopAction(-1, "diamond");
            y += 60f;

            // Seçili eşya: karakterin üstünde önizleme + satın al
            var box = new Rect(16f, y, width - 32f, 132f);
            UiKit.Card(box, new Color(1f, 1f, 1f, 0.06f), 18f);
            if (shop.slots.Length == 0)
            {
                preview.Hide();
                GUI.Label(new Rect(box.x + 12f, box.y + 40f, box.width - 24f, 50f), T("shopEmpty"), smallCenterWrapStyle());
            }
            else
            {
                var chosen = shop.slots[shopSelected];
                Color rarity = RarityColor(chosen.rarity);
                preview.Show(new[] { chosen.itemId });
                if (preview.Texture != null) GUI.DrawTexture(new Rect(box.x + 4f, box.y + 2f, 104f, 128f), preview.Texture, ScaleMode.ScaleToFit, true);

                float tx = box.x + 112f, tw = box.width - 122f;
                GUI.Label(new Rect(tx, box.y + 10f, tw, 26f), ItemName(chosen.tr, chosen.en), itemNameStyle);
                var old = GUI.color;
                GUI.color = rarity;
                GUI.Label(new Rect(tx, box.y + 34f, tw, 20f), T("rarity_" + chosen.rarity) + " - " + T("cat_" + chosen.category), smallStyle);
                GUI.color = old;
                var buy = new Rect(tx, box.y + 68f, tw, 48f);
                if (chosen.bought) UiKit.DisabledButton(buy, T("bought"), 18);
                else if (PriceButton(buy, chosen.currency, chosen.price, 20)) ShopAction(chosen.slot, chosen.currency);
            }
            y += 140f;

            // Günün eşyaları (dokununca önizlemeye gelir)
            float cellWidth = half;
            for (int i = 0; i < shop.slots.Length; i++)
            {
                var slot = shop.slots[i];
                var cell = new Rect(16f + (i % 2) * (cellWidth + 10f), y + (i / 2) * 62f, cellWidth, 56f);
                Color rarity = RarityColor(slot.rarity);
                bool selected = i == shopSelected;
                UiKit.Card(cell, new Color(rarity.r, rarity.g, rarity.b, selected ? 0.42f : 0.20f), 12f);
                UiKit.Card(new Rect(cell.x, cell.y + 6f, 5f, cell.height - 12f), rarity, 2.5f);
                GUI.Label(new Rect(cell.x + 12f, cell.y + 4f, cell.width - 18f, 24f), ItemName(slot.tr, slot.en), itemNameStyle);
                if (slot.bought) GUI.Label(new Rect(cell.x + 12f, cell.y + 30f, cell.width - 18f, 20f), T("bought"), smallStyle);
                else
                {
                    DrawCurrency(new Rect(cell.x + 12f, cell.y + 31f, 18f, 18f), slot.currency);
                    GUI.Label(new Rect(cell.x + 36f, cell.y + 28f, cell.width - 42f, 24f), slot.price.ToString(), chestNameStyle);
                }
                if (GUI.Button(cell, GUIContent.none, GUIStyle.none)) { shopSelected = i; shopNotice = ""; }
            }
            y += ((shop.slots.Length + 1) / 2) * 62f + 4f;

            // Sandıklar: altınla (yenileme başına 1) ve elmasla
            DrawShopChest(new Rect(16f, y, half, 96f), shop.goldChest);
            DrawShopChest(new Rect(16f + half + 10f, y, half, 96f), shop.diamondChest);
            y += 102f;

            if (shopNotice.Length > 0) GUI.Label(new Rect(20f, y, width - 40f, 24f), shopNotice, smallCenterStyle);

            float backY = Mathf.Max(y + 26f, height - 66f);
            if (UiKit.SecondaryButton(new Rect(24f, backY, width - 48f, 50f), T("back"), 20)) screen = Page.Menu;
        }

        void DrawShopChest(Rect r, ShopChestDto chest)
        {
            if (chest == null) return;
            Color tint = TierColor(chest.tier);
            UiKit.Card(r, new Color(tint.r, tint.g, tint.b, 0.20f), 14f);
            DrawChestIcon(new Rect(r.x + 8f, r.y + 6f, 40f, 40f), chest.tier);
            GUI.Label(new Rect(r.x + 52f, r.y + 6f, r.width - 56f, 40f), T("chest_" + chest.tier), smallStyle);
            var button = new Rect(r.x + 8f, r.y + 50f, r.width - 16f, 40f);
            if (chest.bought) UiKit.DisabledButton(button, T("bought"), 17);
            else if (PriceButton(button, chest.currency, chest.price)) ShopAction(chest.slot, chest.currency);
        }

        // Geliştirme: mağazayı örnek veriyle gösterir (DevHarness)
        public void DevShowShop()
        {
            profile = new ProfileDto { name = "Furkan", level = 4, gold = 1250, diamonds = 320, chestBronze = 1 };
            shop = new ShopDto
            {
                secondsLeft = 9 * 3600 + 12 * 60 + 33, refreshCost = 20, gold = 1250, diamonds = 320,
                slots = new[]
                {
                    new ShopSlotDto { slot = 0, itemId = "hat_pop", category = "head", rarity = "rare", tr = "Popstar Şapkası", en = "Popstar Hat", currency = "gold", price = 1000 },
                    new ShopSlotDto { slot = 1, itemId = "shoes_wing", category = "shoes", rarity = "legendary", tr = "Kanatlı Ayakkabı", en = "Winged Shoes", currency = "diamond", price = 300 },
                    new ShopSlotDto { slot = 2, itemId = "top_hoodie", category = "top", rarity = "uncommon", tr = "Sokak Kapüşonu", en = "Street Hoodie", currency = "gold", price = 400, bought = true },
                    new ShopSlotDto { slot = 3, itemId = "glasses_cyber", category = "glasses", rarity = "epic", tr = "Siber Vizör", en = "Cyber Visor", currency = "gold", price = 2500 },
                },
                goldChest = new ShopChestDto { slot = 4, tier = "silver", currency = "gold", price = 500 },
                diamondChest = new ShopChestDto { slot = 5, tier = "gold", currency = "diamond", price = 100 },
            };
            shopFetchedAt = Time.realtimeSinceStartup;
            shopSelected = 0;
            shopNotice = "";
            screen = Page.Shop;
        }
    }
}
