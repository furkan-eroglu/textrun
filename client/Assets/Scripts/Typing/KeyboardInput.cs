using UnityEngine;

namespace TextRun.Typing
{
    public enum KeyboardKind { Default, AsciiCapable }

    // Telefonda sistemin yerel klavyesini, bilgisayarda (editör) fiziksel klavyeyi kullanır.
    public class KeyboardInput
    {
        TouchScreenKeyboard native;
        string desktopText = "";

        public bool IsNative => Application.isMobilePlatform;

        // Klavye her (yeniden) açıldığında artar: giriş izleyicisi klavye metninin sıfırlandığını anlar.
        public int Generation { get; private set; }

        public string Text
        {
            get
            {
                if (!IsNative) return desktopText;
                return native != null ? native.text : "";
            }
            set
            {
                if (!IsNative) desktopText = value;
                else if (native != null) native.text = value;
            }
        }

        public bool IsOpen => !IsNative || (native != null && native.status == TouchScreenKeyboard.Status.Visible);

        // Yalnızca editörde: telefon klavyesinin kaplayacağı yeri taklit eder (ekranın yaklaşık %38'i; iPhone ~%36, Android ~%40).
        public static bool SimulateInEditor;
        const float SimulatedHeightFraction = 0.38f;

        // Klavyenin ekrandan kapladığı yükseklik (piksel). Kapalıysa 0.
        public float AreaHeight
        {
            get
            {
                if (!IsNative) return SimulateInEditor ? Screen.height * SimulatedHeightFraction : 0f;
                return IsOpen ? TouchScreenKeyboard.area.height : 0f;
            }
        }

        public string StatusText
        {
            get
            {
                if (!IsNative) return "masaustu";
                return native == null ? "hic acilmadi" : native.status.ToString();
            }
        }

        public void Open(KeyboardKind kind)
        {
            Generation++;
            desktopText = "";
            if (!IsNative) return;

            TouchScreenKeyboard.hideInput = true;
            var type = kind == KeyboardKind.AsciiCapable ? TouchScreenKeyboardType.ASCIICapable : TouchScreenKeyboardType.Default;
            // otomatik düzeltme kapalı, tek satır, gizli değil
            native = TouchScreenKeyboard.Open("", type, false, false, false, false, "", 0);
        }

        // Klavyeyi kapatır (oyuncu yarışı bitirince artık yazacak bir şey kalmaz).
        // Bazı Android cihazlarda active=false klavyeyi gizlemez; bu yüzden sistemden de gizlemesi istenir.
        public void Close()
        {
            if (!IsNative) return;
            if (native != null)
            {
                native.active = false;
                native = null;
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            HideAndroidKeyboard();
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static void HideAndroidKeyboard()
        {
            try
            {
                var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                var activity = player.GetStatic<AndroidJavaObject>("currentActivity"); // arayüz iş parçacığında kullanılacağı için serbest bırakılmaz
                activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    try
                    {
                        var service = activity.Call<AndroidJavaObject>("getSystemService", "input_method");
                        var decor = activity.Call<AndroidJavaObject>("getWindow").Call<AndroidJavaObject>("getDecorView");
                        service.Call<bool>("hideSoftInputFromWindow", decor.Call<AndroidJavaObject>("getWindowToken"), 0);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning("Klavye gizlenemedi: " + e.Message);
                    }
                }));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Klavye gizlenemedi: " + e.Message);
            }
        }
#endif

        // İki metnin ortak başlangıç uzunluğu; harf büyüklüğü dile göre (Türkçe I/ı, İ/i) yok sayılır.
        // Klavye metni beklenmedik biçimde değiştiğinde (öneri, otomatik düzeltme) yazılan yeni harfleri bulmak için kullanılır.
        public static int CommonPrefixLength(string raw, string entered, GameLanguage language)
        {
            int n = 0;
            while (n < raw.Length && n < entered.Length && TextNormalizer.ToLower(raw[n], language) == entered[n]) n++;
            return n;
        }

        // Editörde OnGUI olaylarından karakter toplar.
        public void HandleGuiEvent(Event e)
        {
            if (IsNative || e.type != EventType.KeyDown) return;

            if (e.keyCode == KeyCode.Backspace)
            {
                if (desktopText.Length > 0) desktopText = desktopText.Substring(0, desktopText.Length - 1);
            }
            else if (e.character != '\0' && !char.IsControl(e.character))
            {
                desktopText += e.character;
            }
        }
    }
}
