using Mopups.Services;
using SampleMopups.XAML;

namespace SampleMaui.XAML;

public partial class TestPage : ContentPage
{
	public TestPage()
	{
		InitializeComponent();
	}

	private async void OnButtonClicked(object sender, EventArgs e)
	{
		await MopupService.Instance.PushAsync(new AswinPage());
	}
}