using System.Collections.Generic;
using UnityEngine;

namespace TextRun.Race
{
    // Küçük küplerden yazı: pano, tabela ve zemindeki yazılar için. Her çözünürlükte keskin görünür, yazı tipi dosyası gerektirmez.
    // Harfler 5x7 pikseldir (1 = dolu), yukarıdan aşağıya.
    public static class PixelFont
    {
        static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
        {
            { 'A', new[] { "01110", "10001", "10001", "11111", "10001", "10001", "10001" } },
            { 'B', new[] { "11110", "10001", "10001", "11110", "10001", "10001", "11110" } },
            { 'E', new[] { "11111", "10000", "10000", "11110", "10000", "10000", "11111" } },
            { 'F', new[] { "11111", "10000", "10000", "11110", "10000", "10000", "10000" } },
            { 'H', new[] { "10001", "10001", "10001", "11111", "10001", "10001", "10001" } },
            { 'I', new[] { "11111", "00100", "00100", "00100", "00100", "00100", "11111" } },
            { 'K', new[] { "10001", "10010", "10100", "11000", "10100", "10010", "10001" } },
            { 'L', new[] { "10000", "10000", "10000", "10000", "10000", "10000", "11111" } },
            { 'N', new[] { "10001", "11001", "10101", "10011", "10001", "10001", "10001" } },
            { 'O', new[] { "01110", "10001", "10001", "10001", "10001", "10001", "01110" } },
            { 'P', new[] { "11110", "10001", "10001", "11110", "10000", "10000", "10000" } },
            { 'R', new[] { "11110", "10001", "10001", "11110", "10100", "10010", "10001" } },
            { 'S', new[] { "01111", "10000", "10000", "01110", "00001", "00001", "11110" } },
            { 'T', new[] { "11111", "00100", "00100", "00100", "00100", "00100", "00100" } },
            { 'U', new[] { "10001", "10001", "10001", "10001", "10001", "10001", "01110" } },
            { 'V', new[] { "10001", "10001", "10001", "10001", "10001", "01010", "00100" } },
            { 'X', new[] { "10001", "10001", "01010", "00100", "01010", "10001", "10001" } },
            { 'Z', new[] { "11111", "00001", "00010", "00100", "01000", "10000", "11111" } },
            { '0', new[] { "01110", "10001", "10011", "10101", "11001", "10001", "01110" } },
            { '1', new[] { "00100", "01100", "00100", "00100", "00100", "00100", "01110" } },
            { '2', new[] { "01110", "10001", "00001", "00010", "00100", "01000", "11111" } },
            { '3', new[] { "11110", "00001", "00001", "01110", "00001", "00001", "11110" } },
            { '4', new[] { "00010", "00110", "01010", "10010", "11111", "00010", "00010" } },
            { '5', new[] { "11111", "10000", "11110", "00001", "00001", "10001", "01110" } },
            { '6', new[] { "01110", "10000", "10000", "11110", "10001", "10001", "01110" } },
        };

        // Yazının piksel cinsinden genişliği (harfler arası 1 piksel boşluk)
        public static int WidthInPixels(string text) => text.Length * 6 - 1;

        // Yazıyı ortası "center" olacak şekilde küplerden kurar. right/up: yazının ilerleme ve yukarı yönleri (eksenlere paralel
        // birim vektörler), pixel: bir pikselin kenarı, thickness: yüzeyden kabarıklık.
        public static void Add(MeshBatch batch, string text, Color color, Vector3 center, Vector3 right, Vector3 up, float pixel, float thickness = 0.03f)
        {
            var normal = Vector3.Cross(right, up);
            var size = Abs(right) * pixel + Abs(up) * pixel + Abs(normal) * thickness;
            float left = -(WidthInPixels(text) - 1) / 2f;
            int cursor = 0;
            foreach (char c in text)
            {
                if (Glyphs.TryGetValue(c, out var glyph))
                {
                    for (int row = 0; row < 7; row++)
                        for (int col = 0; col < 5; col++)
                            if (glyph[row][col] == '1')
                                batch.AddBox(color, center + right * ((left + cursor + col) * pixel) + up * ((3 - row) * pixel), size);
                }
                cursor += 6;
            }
        }

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }
}
