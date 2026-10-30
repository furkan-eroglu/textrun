using TextRun.Online;
using UnityEngine;

namespace TextRun.Race
{
    // Pistin üstünde ince bir yatay ilerleme şeridi: 5 oyuncunun renkli noktası başlangıçtan bitişe doğru ilerler.
    // 3D pist küçük kaldığında kimin önde olduğunu bir bakışta gösterir. Oyuncunun noktası daha büyük ve beyaz halkalıdır.
    public class RaceProgressStrip
    {
        const float DotSize = 14f;
        const float LocalDotSize = 20f;
        const int MaxLanes = 5;

        static Texture2D circle;

        readonly float[] shown = new float[MaxLanes];

        // localProgress: oyuncunun kendi ilerlemesi (0-1), sunucu cevabını beklemeden anında gösterilsin diye ayrı verilir; yoksa -1.
        public void Draw(Rect area, PlayerDto[] players, int wordCount, string localId, float localProgress)
        {
            if (players == null || wordCount <= 0) return;
            if (circle == null) circle = MakeCircle();

            float left = area.x + 12f;
            float right = area.xMax - 22f;
            float midY = area.y + area.height / 2f;

            // Okunabilirlik için yarı saydam koyu zemin ve çizgi
            DrawRect(new Rect(area.x, area.y, area.width, area.height), new Color(0.04f, 0.05f, 0.09f, 0.55f));
            DrawRect(new Rect(left, midY - 2f, right - left, 4f), new Color(1f, 1f, 1f, 0.35f));
            DrawFinishFlag(right + 3f, midY);

            // Önce rakipler, en son oyuncu çizilir (üstte kalsın)
            PlayerDto local = null;
            foreach (var p in players)
            {
                if (p.id == localId) { local = p; continue; }
                DrawDot(p, left, right, midY, wordCount, -1f, false);
            }
            if (local != null) DrawDot(local, left, right, midY, wordCount, localProgress, true);
        }

        void DrawDot(PlayerDto p, float left, float right, float midY, int wordCount, float overrideProgress, bool isLocal)
        {
            if (p.slot < 0 || p.slot >= MaxLanes) return;

            float target = p.finished && !p.disqualified ? 1f : Mathf.Clamp01((float)p.index / wordCount);
            if (overrideProgress >= 0f) target = Mathf.Max(target, Mathf.Clamp01(overrideProgress));

            // Yumuşak ilerleme; yalnızca çizim olayında güncellenir (OnGUI karede birkaç kez çağrılır)
            if (Event.current.type == EventType.Repaint)
                shown[p.slot] = Mathf.Lerp(shown[p.slot], target, 1f - Mathf.Exp(-10f * Time.deltaTime));

            float x = Mathf.Lerp(left, right, shown[p.slot]);
            Color color = OnlineRaceView.LaneColors[p.slot % OnlineRaceView.LaneColors.Length];

            if (isLocal)
            {
                DrawCircle(x, midY, LocalDotSize + 4f, Color.white);
                DrawCircle(x, midY, LocalDotSize, color);
            }
            else
            {
                DrawCircle(x, midY, DotSize + 2f, new Color(0f, 0f, 0f, 0.6f));
                DrawCircle(x, midY, DotSize, color);
            }
        }

        static void DrawCircle(float cx, float cy, float size, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(new Rect(cx - size / 2f, cy - size / 2f, size, size), circle);
            GUI.color = Color.white;
        }

        static void DrawRect(Rect r, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        // Bitişte küçük damalı bayrak
        static void DrawFinishFlag(float x, float midY)
        {
            const float cell = 5f;
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 2; col++)
                    DrawRect(new Rect(x + col * cell, midY - 7.5f + row * cell, cell, cell), (row + col) % 2 == 0 ? Color.white : Color.black);
        }

        static Texture2D MakeCircle()
        {
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            float r = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    float alpha = Mathf.Clamp01(r - d); // kenarda 1 piksellik yumuşatma
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            return tex;
        }
    }
}
