using System;
using TextRun.Online;
using UnityEngine;

namespace TextRun.Game
{
    // Mağaza ekranı: her gün değişen 4 eşya (resimli), altınla ve elmasla birer sandık, elmasla yenileme (onay sorar).
    // Fiyatlar, günün eşyaları ve bakiyeler sunucudadır; burada yalnızca gösterilir ve "satın al" isteği gönderilir.
    public partial class OnlineGameController
    {
        ShopDto shop;                 // null = henüz okunmadı
        float shopFetchedAt;
        string shopNotice = "";
        bool shopBusy;
        bool shopReloading;

        // Onay penceresi (açıkken mağazanın yerine çizilir)
        string confirmText;           // null = kapalı
        string confirmCurrency;
        int confirmPrice;
        Action confirmYes;

        Texture2D refreshIcon;
        GUIStyle shopNameStyle, shopLeftStyle, shopPriceStyle;

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

        // Yenileme simgesi: ucu oklu, tam kapanmayan halka (beyaz; kodla bir kez çizilir)
        static Texture2D MakeRefreshIcon()
        {
            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            float half = size / 2f;
            const float inner = 0.50f, outer = 0.78f;      // halka kalınlığı (yarıçapın oranı)
            const float gapFrom = 20f, gapTo = 80f;        // halkanın açık kaldığı açı aralığı (derece)

            // Ok başı: boşluğun bittiği uçta, halkaya teğet
            float tipAngle = gapFrom * Mathf.Deg2Rad;
            float mid = (inner + outer) / 2f * half;
            var arcEnd = new Vector2(half + Mathf.Cos(tipAngle) * mid, half + Mathf.Sin(tipAngle) * mid);
            var tangent = new Vector2(-Mathf.Sin(tipAngle), Mathf.Cos(tipAngle));
            var radial = new Vector2(Mathf.Cos(tipAngle), Mathf.Sin(tipAngle));
            Vector2 a = arcEnd + tangent * (half * 0.42f), b = arcEnd + radial * (half * 0.34f), c = arcEnd - radial * (half * 0.34f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float radius = Vector2.Distance(p, new Vector2(half, half)) / half;
                    float angle = Mathf.Repeat(Mathf.Atan2(p.y - half, p.x - half) * Mathf.Rad2Deg, 360f);
                    bool onRing = radius >= inner && radius <= outer && !(angle > gapFrom && angle < gapTo);
                    bool inHead = SameSide(p, a, b, c) && SameSide(p, b, c, a) && SameSide(p, c, a, b);
                    texture.SetPixel(x, y, onRing || inHead ? Color.white : Color.clear);
                }
            }
            texture.Apply();
            return texture;
        }

        static bool SameSide(Vector2 p, Vector2 from, Vector2 to, Vector2 reference)
        {
            float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;
            return Cross(to - from, p - from) * Cross(to - from, reference - from) >= 0f;
        }

        void OpenShop()
        {
            shopNotice = "";
            confirmText = null;
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
                string boughtItem = slot >= 0 && shop != null && slot < shop.slots.Length ? shop.slots[slot].itemId : null;
                ApplyShop(slot < 0 ? await net.ShopRefreshAsync() : await net.ShopBuyAsync(slot));
                MarkNewItem(boughtItem);
                sound.MatchFound();
                if (slot >= 0 && slot < 4) { inventory = null; inventoryTriedAt = -999f; } // yeni eşya: envanter yeniden okunur
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

        // Para simgesi + fiyat, verilen alanın ortasına
        void DrawPrice(Rect r, string currency, int price, float alpha = 1f)
        {
            string text = price.ToString();
            float icon = Mathf.Min(20f, r.height - 10f);
            float textWidth = shopPriceStyle.CalcSize(new GUIContent(text)).x;
            float total = icon + 6f + textWidth;
            float x = r.center.x - total / 2f;
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            DrawCurrency(new Rect(x, r.center.y - icon / 2f - 1f, icon, icon), currency);
            GUI.Label(new Rect(x + icon + 6f, r.y - 1f, textWidth + 4f, r.height), text, shopPriceStyle);
            GUI.color = old;
        }

        // Fiyat düğmesi. Bakiye yetmiyorsa soluk çizilir ve dokununca uyarı verir; yetiyorsa true döner.
        bool PriceButton(Rect r, string currency, int price)
        {
            bool affordable = CanAfford(currency, price);
            bool clicked;
            if (affordable) clicked = UiKit.SecondaryButton(r, "", 18);
            else
            {
                UiKit.DisabledButton(r, "", 18);
                clicked = GUI.Button(r, GUIContent.none, GUIStyle.none);
            }
            DrawPrice(new Rect(r.x, r.y, r.width, r.height - 6f), currency, price, affordable ? 1f : 0.5f);
            if (clicked && !affordable)
            {
                shopNotice = T(currency == "diamond" ? "notEnoughDiamonds" : "notEnoughGold");
                return false;
            }
            return clicked;
        }

        void AskConfirm(string text, string currency, int price, Action yes)
        {
            confirmText = text;
            confirmCurrency = currency;
            confirmPrice = price;
            confirmYes = yes;
            shopNotice = "";
        }

        void DrawConfirm(float width, float height)
        {
            var card = new Rect(20f, height / 2f - 130f, width - 40f, 250f);
            UiKit.Card(new Rect(card.x - 3f, card.y - 3f, card.width + 6f, card.height + 6f), new Color(0.30f, 0.60f, 1f, 0.55f), 25f);
            UiKit.Card(card, new Color(0.08f, 0.10f, 0.22f), 22f);
            GUI.Label(new Rect(card.x + 18f, card.y + 20f, card.width - 36f, 86f), confirmText, smallCenterWrapStyle(18));
            DrawPrice(new Rect(card.x, card.y + 108f, card.width, 40f), confirmCurrency, confirmPrice);

            float half = (card.width - 36f - 12f) / 2f;
            if (UiKit.SecondaryButton(new Rect(card.x + 18f, card.y + 170f, half, 58f), T("giveUp"), 20)) confirmText = null;
            if (UiKit.PrimaryButton(new Rect(card.x + 18f + half + 12f, card.y + 170f, half, 58f), T("yes"), 20))
            {
                var action = confirmYes;
                confirmText = null;
                action?.Invoke();
            }
        }

        void DrawShop(float width, float height, float top)
        {
            FullViewport();
            EnsureInventoryStyles();
            preview.Hide();
            if (shopNameStyle == null)
            {
                shopNameStyle = new GUIStyle(rowNameStyle) { fontSize = 14, fontStyle = FontStyle.Bold, wordWrap = false, clipping = TextClipping.Overflow, alignment = TextAnchor.MiddleCenter };
                UiKit.SetAllTextColors(shopNameStyle, Color.white);
                shopLeftStyle = new GUIStyle(shopNameStyle) { alignment = TextAnchor.MiddleLeft };
                shopPriceStyle = new GUIStyle(rowNameStyle) { fontSize = 18, fontStyle = FontStyle.Bold, wordWrap = false, alignment = TextAnchor.MiddleLeft };
                UiKit.SetAllTextColors(shopPriceStyle, Color.white);
                refreshIcon = MakeRefreshIcon();
            }

            float y = top + 8f;
            GUI.Label(new Rect(0f, y, width, 40f), T("shop"), headingStyle);
            y += 46f;

            if (confirmText != null)
            {
                DrawConfirm(width, height);
                return;
            }

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
                GUI.Label(new Rect(20f, y + 40f, width - 40f, 30f), shopNotice.Length > 0 ? shopNotice : "...", smallCenterStyle);
                if (UiKit.SecondaryButton(new Rect(24f, height - 64f, width - 48f, 50f), T("back"), 20)) screen = Page.Menu;
                return;
            }

            // Geri sayım (gece 00.00) ve elmasla yenileme düğmesi (yenileme simgesi + bedel)
            int left = shop.secondsLeft - Mathf.FloorToInt(Time.realtimeSinceStartup - shopFetchedAt);
            if (left <= 0) LoadShop();
            GUI.Label(new Rect(20f, y, width - 170f, 20f), T("refreshesIn"), smallStyle);
            GUI.Label(new Rect(20f, y + 19f, width - 170f, 26f), Clock(left), rowNameStyle);
            var refresh = new Rect(width - 136f, y + 2f, 120f, 44f);
            bool refreshClicked = UiKit.SecondaryButton(refresh, "", 18);
            GUI.DrawTexture(new Rect(refresh.x + 12f, refresh.y + 7f, 24f, 24f), refreshIcon);
            DrawPrice(new Rect(refresh.x + 36f, refresh.y, refresh.width - 40f, refresh.height - 6f), "diamond", shop.refreshCost);
            if (refreshClicked)
            {
                if (!CanAfford("diamond", shop.refreshCost)) shopNotice = T("notEnoughDiamonds");
                else AskConfirm(string.Format(T("confirmRefresh"), shop.refreshCost), "diamond", shop.refreshCost, () => ShopAction(-1, "diamond"));
            }
            y += 54f;

            // Kalan yüksekliğe göre kart boyu: 4 eşya (2x2) + 2 sandık + geri düğmesi her ekrana sığar
            float free = height - 64f - y - 28f;
            float chestHeight = Mathf.Clamp(free * 0.24f, 104f, 116f);
            float cardHeight = Mathf.Clamp((free - chestHeight - 16f) / 2f, 150f, 190f);

            if (shop.slots.Length == 0)
                GUI.Label(new Rect(28f, y + 30f, width - 56f, 60f), T("shopEmpty"), smallCenterWrapStyle());

            for (int i = 0; i < shop.slots.Length; i++)
            {
                var slot = shop.slots[i];
                var card = new Rect(16f + (i % 2) * (half + 10f), y + (i / 2) * (cardHeight + 8f), half, cardHeight);
                Color rarity = RarityColor(slot.rarity);
                UiKit.Card(card, new Color(rarity.r, rarity.g, rarity.b, 0.20f), 14f);
                UiKit.Card(new Rect(card.x, card.y, card.width, 5f), rarity, 2.5f);

                float iconSize = card.height - 106f;
                DrawItemIcon(new Rect(card.center.x - iconSize / 2f, card.y + 11f, iconSize, iconSize), slot.itemId, slot.rarity);
                GUI.Label(new Rect(card.x + 2f, card.y + 13f + iconSize, card.width - 4f, 20f), ItemName(slot.tr, slot.en), shopNameStyle);
                var old = GUI.color;
                GUI.color = rarity;
                GUI.Label(new Rect(card.x + 2f, card.y + 31f + iconSize, card.width - 4f, 24f), T("rarity_" + slot.rarity), smallCenterStyle);
                GUI.color = old;

                var button = new Rect(card.x + 8f, card.yMax - 46f, card.width - 16f, 40f);
                if (slot.bought) UiKit.DisabledButton(button, T("bought"), 16);
                else if (PriceButton(button, slot.currency, slot.price))
                {
                    var chosen = slot;
                    AskConfirm(string.Format(T("confirmBuy"), ItemName(chosen.tr, chosen.en)), chosen.currency, chosen.price, () => ShopAction(chosen.slot, chosen.currency));
                }
            }
            y += 2f * (cardHeight + 8f);

            // Sandıklar: altınla (yenileme başına 1) ve elmasla
            DrawShopChest(new Rect(16f, y, half, chestHeight), shop.goldChest, T("dailyOnce"));
            DrawShopChest(new Rect(16f + half + 10f, y, half, chestHeight), shop.diamondChest, null);
            y += chestHeight + 4f;

            if (shopNotice.Length > 0) GUI.Label(new Rect(20f, y, width - 40f, 22f), shopNotice, smallCenterStyle);

            if (UiKit.SecondaryButton(new Rect(24f, height - 64f, width - 48f, 50f), T("back"), 20)) screen = Page.Menu;
        }

        void DrawShopChest(Rect r, ShopChestDto chest, string note)
        {
            if (chest == null) return;
            Color tint = TierColor(chest.tier);
            UiKit.Card(r, new Color(tint.r, tint.g, tint.b, 0.20f), 14f);
            UiKit.Card(new Rect(r.x, r.y, r.width, 5f), tint, 2.5f);
            float iconSize = r.height - 74f;
            DrawChestIcon(new Rect(r.x + 10f, r.y + 10f, iconSize, iconSize), chest.tier);
            float tx = r.x + iconSize + 14f, tw = r.xMax - 4f - tx;
            GUI.Label(new Rect(tx, r.y + 10f, tw, 20f), T("chest_" + chest.tier), shopLeftStyle);
            if (note != null) GUI.Label(new Rect(tx, r.y + 30f, tw, 24f), note, smallStyle);

            var button = new Rect(r.x + 8f, r.yMax - 46f, r.width - 16f, 40f);
            if (chest.bought) UiKit.DisabledButton(button, T("bought"), 16);
            else if (PriceButton(button, chest.currency, chest.price))
                AskConfirm(string.Format(T("confirmBuy"), T("chest_" + chest.tier)), chest.currency, chest.price, () => ShopAction(chest.slot, chest.currency));
        }

        // Geliştirme: mağazayı örnek veriyle gösterir (DevHarness)
        public void DevShowShop(bool confirm = false)
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
                    new ShopSlotDto { slot = 3, itemId = "cape_royal", category = "cape", rarity = "epic", tr = "Kraliyet Pelerini", en = "Royal Cape", currency = "gold", price = 2500 },
                },
                goldChest = new ShopChestDto { slot = 4, tier = "silver", currency = "gold", price = 500 },
                diamondChest = new ShopChestDto { slot = 5, tier = "gold", currency = "diamond", price = 100 },
            };
            shopFetchedAt = Time.realtimeSinceStartup;
            shopNotice = "";
            confirmText = null;
            screen = Page.Shop;
            if (confirm) AskConfirm(string.Format(T("confirmRefresh"), 20), "diamond", 20, null);
        }
    }
}
