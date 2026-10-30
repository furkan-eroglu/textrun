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
            desktopText = "";
            if (!IsNative) return;

            TouchScreenKeyboard.hideInput = true;
            var type = kind == KeyboardKind.AsciiCapable ? TouchScreenKeyboardType.ASCIICapable : TouchScreenKeyboardType.Default;
            // otomatik düzeltme kapalı, tek satır, gizli değil
            native = TouchScreenKeyboard.Open("", type, false, false, false, false, "", 0);
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
