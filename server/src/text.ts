// Türkçede I/ı ve İ/i çiftleri standart küçük harf dönüşümüyle karışır, bu yüzden elle yapılır.
// Unity tarafındaki TextNormalizer ile aynı kuralı uygular.
export function toLowerForLang(text: string, lang: string): string {
  let result = "";
  for (let i = 0; i < text.length; i++) {
    const c = text.charAt(i);
    if (lang === "tr" && c === "I") result += "ı";
    else if (lang === "tr" && c === "İ") result += "i";
    else result += c.toLowerCase();
  }
  return result;
}
