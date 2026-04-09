using MyAndroidApp.Services;

namespace MyAndroidApp;

public partial class App : Application
{
	private readonly Task _preloadTask;

	public App(MemberDatabase database)
	{
		InitializeComponent();

		MainPage = new AppShell();
		_preloadTask = PreloadDataAsync(database);
	}

	private static async Task PreloadDataAsync(MemberDatabase database)
	{
		try
		{
			await database.PreloadAsync();
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"アプリ起動時のプリロードに失敗: {ex.Message}");
		}
	}
}
