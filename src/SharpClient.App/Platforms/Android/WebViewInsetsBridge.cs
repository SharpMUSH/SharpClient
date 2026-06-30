using System.Globalization;
using AndroidX.Core.View;
using AView = Android.Views.View;
using AWebView = Android.Webkit.WebView;

namespace SharpClient.App.Platforms.Android;

/// <summary>
/// Bridges Android WindowInsets into the Blazor/HTML layer as CSS custom properties.
///
/// WHY THIS EXISTS: on Android 15/16 edge-to-edge is enforced and the HTML draws under the status
/// bar / display cutout and behind the soft keyboard. The pure-web fixes (CSS
/// <c>env(safe-area-inset-*)</c> and <c>window.visualViewport</c>) only work on Android System
/// WebView &gt;= 136/139/144 — on older WebViews (the device hit this on 133) <c>env()</c> resolves to
/// 0 and <c>visualViewport</c> does not track the IME, so the top bar overlapped and the keyboard
/// hid the input bar. Reading the insets natively and pushing them in as CSS variables is
/// version-independent and is the approach Google documents for "content you own".
///
/// Sets <c>--sc-safe-top/-bottom/-left/-right</c> (system bars + display cutout) and
/// <c>--sc-keyboard-height</c> (IME); app.css consumes them via <c>max(env(...), var(--sc-safe-*))</c>
/// and subtracts <c>--sc-keyboard-height</c> from the shell height.
/// </summary>
internal static class WebViewInsetsBridge
{
    public static void Attach(AWebView webView)
    {
        ViewCompat.SetOnApplyWindowInsetsListener(webView, new Listener(webView));
        // Force an initial inset pass so the variables are set before the first keyboard/rotation.
        ViewCompat.RequestApplyInsets(webView);
    }

    private sealed class Listener : Java.Lang.Object, IOnApplyWindowInsetsListener
    {
        private readonly AWebView _webView;

        public Listener(AWebView webView) => _webView = webView;

        public WindowInsetsCompat? OnApplyWindowInsets(AView? view, WindowInsetsCompat? insets)
        {
            if (insets is null)
            {
                return insets;
            }

            var zero = AndroidX.Core.Graphics.Insets.Of(0, 0, 0, 0)!;
            var bars = insets.GetInsets(WindowInsetsCompat.Type.SystemBars() | WindowInsetsCompat.Type.DisplayCutout()) ?? zero;
            var ime = insets.GetInsets(WindowInsetsCompat.Type.Ime()) ?? zero;

            var density = view?.Resources?.DisplayMetrics?.Density ?? 1f;
            if (density <= 0f)
            {
                density = 1f;
            }

            string Px(int physical) => (physical / density).ToString("0.##", CultureInfo.InvariantCulture);

            // The IME inset already includes the bottom system bar; the keyboard height we want to
            // subtract from the layout is the part of the IME that exceeds the nav-bar inset.
            var keyboard = System.Math.Max(0, ime.Bottom - bars.Bottom);

            var js =
                "(function(s){" +
                $"s.setProperty('--sc-safe-top','{Px(bars.Top)}px');" +
                $"s.setProperty('--sc-safe-bottom','{Px(bars.Bottom)}px');" +
                $"s.setProperty('--sc-safe-left','{Px(bars.Left)}px');" +
                $"s.setProperty('--sc-safe-right','{Px(bars.Right)}px');" +
                $"s.setProperty('--sc-keyboard-height','{Px(keyboard)}px');" +
                "})(document.documentElement.style);";

            _webView.EvaluateJavascript(js, null);

            // Return the insets unconsumed; app.css applies the padding, so we deliberately do NOT
            // also pad the native view (that would double-count).
            return insets;
        }
    }
}
