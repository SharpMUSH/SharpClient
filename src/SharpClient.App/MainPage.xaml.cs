namespace SharpClient.App;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();
#if ANDROID
		// Wire the native WindowInsets -> CSS-variable bridge once the platform WebView exists, so
		// status-bar/cutout insets and the soft-keyboard height reach the HTML layer even on Android
		// System WebViews too old for CSS env()/visualViewport (the device was on WebView 133).
		blazorWebView.BlazorWebViewInitialized += (_, e) =>
		{
			SharpClient.App.Platforms.Android.WebViewInsetsBridge.Attach(e.WebView);

			// Android WebView clamps CSS font-size to a MinimumFontSize (default 8px) and
			// MinimumLogicalFontSize (default 8px). The terminal fits the font down to a few px to pack
			// a high "Min columns" onto a narrow screen; the clamp silently rendered those at 8px, so
			// e.g. 90 columns never fit and NAWS stayed pinned at ~78. Drop the floor so the fitted size
			// is actually honoured. (measureGrid still measures the real rendered advance and reports
			// only the columns that fit, so nothing wraps if a value is genuinely too large.)
			if (e.WebView.Settings is { } settings)
			{
				settings.MinimumFontSize = 1;
				settings.MinimumLogicalFontSize = 1;
			}
		};
#endif
	}
}
