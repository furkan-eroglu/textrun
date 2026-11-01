using System.Collections.Generic;
using TextRun.Typing;
using UnityEngine;

namespace TextRun.Game
{
    // Oyunun kendi ekran klavyesi. Telefonun sistem klavyesi yerine kullanılır: dokunulan tuş doğrudan oyuna gider.
    //
    // Neden: Sistem klavyeleri metni kendi kafasına göre değiştirir (otomatik düzeltme, öneri, yazım alanı); oyunun yazılanı
    // klavyenin metninden okuması harflerin karışmasına ve girişin kilitlenmesine yol açtı. Ayrıca klavye yüksekliği ve dili
    // cihaza göre değişiyordu. Burada düzen (Türkçe/İngilizce), boyut ve davranış oyunun elindedir.
    // Yanlış harfler oyun tarafından zaten yok sayıldığı için silme tuşu yoktur.
    public class OnScreenKeyboard
    {
        public struct Key
        {
            public Rect rect;
            public char character;
            public string label;
        }

        const float Gap = 4f;
        const float Margin = 4f;
        const int RowCount = 4; // 3 harf satırı + boşluk satırı

        static readonly Color PanelColor = new Color(0.03f, 0.05f, 0.13f, 0.98f);
        static readonly Color KeyFace = new Color(0.22f, 0.26f, 0.45f, 1f);
        static readonly Color KeyLip = new Color(0.09f, 0.11f, 0.24f, 1f);
        static readonly Color KeyPressed = new Color(0.27f, 0.64f, 1.00f, 1f);

        readonly Queue<char> presses = new Queue<char>();
        List<Key> keys = new List<Key>();
        GUIStyle style;
        GUIStyle spaceStyle;
        int highlightIndex = -1;
        float highlightUntil;

        float width = 360f;
        float scale = 2f;
        float safeBottom;

        public bool Visible { get; private set; }

        public void Open() => Visible = true;
        public void Close() => Visible = false;

        // Düzen satırları (küçük harf). Türkçe: Q klavye; İngilizce: QWERTY.
        public static string[] Rows(GameLanguage language)
        {
            return language == GameLanguage.Turkish
                ? new[] { "qwertyuıopğü", "asdfghjklşi", "zxcvbnmöç" }
                : new[] { "qwertyuiop", "asdfghjkl", "zxcvbnm" };
        }

        public static float KeyHeight(float guiWidth) => Mathf.Clamp(guiWidth * 0.12f, 40f, 56f);

        // Klavyenin kapladığı yükseklik (arayüz birimi); alttaki güvenli alan dahil
        public static float HeightUnits(float guiWidth, float safeBottomUnits)
        {
            return RowCount * KeyHeight(guiWidth) + (RowCount - 1) * Gap + 2f * Margin + safeBottomUnits;
        }

        // Ekran ölçüleri her karede bildirilir (OnGUI başında): klavye yüksekliği ve dokunma dönüşümü bunlara dayanır.
        public void SetMetrics(float guiWidth, float guiScale, float safeBottomUnits)
        {
            width = guiWidth;
            scale = Mathf.Max(0.01f, guiScale);
            safeBottom = Mathf.Max(0f, safeBottomUnits);
        }

        // Klavyenin ekrandan kapladığı yükseklik (piksel). Kapalıysa 0.
        public float AreaHeight => Visible ? HeightUnits(width, safeBottom) * scale : 0f;

        // Tuş dikdörtgenleri (arayüz birimi). Üst kenar: ekran yüksekliği - klavye yüksekliği.
        public static List<Key> BuildKeys(float guiWidth, float guiHeight, float safeBottomUnits, GameLanguage language, string spaceLabel)
        {
            var result = new List<Key>();
            var rows = Rows(language);
            float keyH = KeyHeight(guiWidth);
            float top = guiHeight - HeightUnits(guiWidth, safeBottomUnits) + Margin;

            int widest = 0;
            foreach (var row in rows) widest = Mathf.Max(widest, row.Length);
            float keyW = (guiWidth - 2f * Margin - (widest - 1) * Gap) / widest;

            for (int r = 0; r < rows.Length; r++)
            {
                string row = rows[r];
                float rowWidth = row.Length * keyW + (row.Length - 1) * Gap;
                float x = (guiWidth - rowWidth) / 2f;
                float y = top + r * (keyH + Gap);
                for (int i = 0; i < row.Length; i++)
                {
                    result.Add(new Key { rect = new Rect(x + i * (keyW + Gap), y, keyW, keyH), character = row[i], label = row[i].ToString() });
                }
            }

            float spaceW = guiWidth * 0.62f;
            result.Add(new Key
            {
                rect = new Rect((guiWidth - spaceW) / 2f, top + rows.Length * (keyH + Gap), spaceW, keyH),
                character = ' ',
                label = spaceLabel
            });
            return result;
        }

        // Dokunulan noktaya (arayüz birimi) denk gelen tuşun sırası; yok ise -1. Tuşlar arası boşluğa dokunulursa en yakın tuş seçilir.
        public static int HitTest(IList<Key> list, Vector2 point)
        {
            float slop = Gap / 2f + 1f;
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i].rect;
                var inflated = new Rect(r.x - slop, r.y - slop, r.width + 2f * slop, r.height + 2f * slop);
                if (inflated.Contains(point)) return i;
            }
            return -1;
        }

        // Her karede çağrılır: dokunuşları (telefonda çoklu parmak) ya da fare tıklamasını (bilgisayar) tuşlara çevirir.
        public void PollPointer()
        {
            if (!Visible || keys.Count == 0) return;

            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var touch = Input.GetTouch(i);
                    if (touch.phase == TouchPhase.Began) Press(touch.position);
                }
            }
            else if (!Application.isMobilePlatform && Input.GetMouseButtonDown(0))
            {
                Press(Input.mousePosition);
            }
        }

        void Press(Vector2 screenPixels)
        {
            var point = new Vector2(screenPixels.x / scale, (UnityEngine.Screen.height - screenPixels.y) / scale);
            int index = HitTest(keys, point);
            if (index < 0) return;

            presses.Enqueue(keys[index].character);
            highlightIndex = index;
            highlightUntil = Time.realtimeSinceStartup + 0.10f;
        }

        // Bilgisayarda fiziksel klavye: yazılan karakterler de aynı kuyruğa girer (telefonda fiziksel klavye olayı gelmez)
        public void HandleGuiEvent(Event e)
        {
            if (!Visible || Application.isMobilePlatform || e.type != EventType.KeyDown) return;
            if (e.character != '\0' && !char.IsControl(e.character)) presses.Enqueue(e.character);
        }

        public bool TryTake(out char c)
        {
            if (presses.Count > 0)
            {
                c = presses.Dequeue();
                return true;
            }
            c = '\0';
            return false;
        }

        // Yarış başlamadan (ışıklar yanarken) basılanlar sayılmaz
        public void Discard() => presses.Clear();

        public void Draw(float guiHeight, GameLanguage language, string spaceLabel)
        {
            if (!Visible) return;

            keys = BuildKeys(width, guiHeight, safeBottom, language, spaceLabel);
            if (Event.current.type != EventType.Repaint) return;

            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = false, clipping = TextClipping.Overflow };
                UiKit.SetAllTextColors(style, Color.white);
                spaceStyle = new GUIStyle(style) { fontSize = 15 };
                UiKit.SetAllTextColors(spaceStyle, new Color(1f, 1f, 1f, 0.75f));
            }
            style.fontSize = Mathf.RoundToInt(KeyHeight(width) * 0.5f);

            float panelTop = guiHeight - HeightUnits(width, safeBottom);
            UiKit.Card(new Rect(0f, panelTop, width, guiHeight - panelTop), PanelColor, 0f);

            bool highlighted = Time.realtimeSinceStartup < highlightUntil;
            for (int i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                bool down = highlighted && i == highlightIndex;
                var face = key.rect;
                if (down) face.y += 2f;

                UiKit.Card(new Rect(key.rect.x, key.rect.y + 3f, key.rect.width, key.rect.height), KeyLip, 9f);
                UiKit.Card(new Rect(face.x, face.y, face.width, face.height - 2f), down ? KeyPressed : KeyFace, 9f);
                GUI.Label(new Rect(face.x, face.y - 1f, face.width, face.height - 2f), key.label, key.character == ' ' ? spaceStyle : style);
            }
        }
    }
}
