using MobileDiffusion.Interfaces.ViewModels;

namespace MobileDiffusion.Views;

public class BasePage : ContentPage
{
	public BasePage()
	{
    }

	protected override void OnAppearing()
	{
		base.OnAppearing();

		if (BindingContext is IPageViewModel pageViewModel)
		{
			pageViewModel.OnAppearing();
		}
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();

        if (BindingContext is IPageViewModel pageViewModel)
        {
            pageViewModel.OnDisappearing();
        }
    }
}