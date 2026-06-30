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
			SharpClient.App.Platforms.Android.WebViewInsetsBridge.Attach(e.WebView);
#endif
	}
}
