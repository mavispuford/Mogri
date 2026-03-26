namespace Mogri.Views;

public partial class AboutPage : BasePage
{
	private Random _random = new ();

	public AboutPage()
	{
		InitializeComponent();
	}

    private async void Logo_Tapped(object? sender, TappedEventArgs e)
	{
		var shrinkSizeX = double.Clamp(_random.NextDouble(), .6, .8);
		var growTimeX = int.Clamp(_random.Next(), 200, 400);
		var shrinkSizeY = double.Clamp(_random.NextDouble(), .6, .8);
		var growTimeY = int.Clamp(_random.Next(), 200, 400);

		// Define local async functions for each axis
		var animateX = async () =>
		{
			await LogoImage.ScaleXToAsync(shrinkSizeX, 50, Easing.SpringIn);
			await LogoImage.ScaleXToAsync(1, (uint)growTimeX, Easing.BounceOut);
		};

		var animateY = async () =>
		{
			await LogoImage.ScaleYToAsync(shrinkSizeY, 50, Easing.SpringIn);
			await LogoImage.ScaleYToAsync(1, (uint)growTimeY, Easing.BounceOut);
		};

		// Run both animations concurrently on the UI thread
		await Task.WhenAll(animateX(), animateY());
	}

    private async void Logo_PanUpdated(object? sender, PanUpdatedEventArgs e)
	{
		switch (e.StatusType)
		{
			case GestureStatus.Running:
				// Limit the translation
				double maxTranslation = 500;
				double translationX = Math.Clamp(e.TotalX, -maxTranslation, maxTranslation);
				double translationY = Math.Clamp(e.TotalY, -maxTranslation, maxTranslation);
				
				// Stretch effect based on distance
				double distance = Math.Sqrt(translationX * translationX + translationY * translationY);
				double stretch = Math.Clamp(distance / 200, 0, 0.3); // max 1.3 scale

				LogoImage.TranslationX = translationX;
				LogoImage.TranslationY = translationY;
				LogoImage.ScaleX = 1 + (translationX != 0 ? stretch * Math.Abs(translationX / maxTranslation) : 0);
				LogoImage.ScaleY = 1 + (translationY != 0 ? stretch * Math.Abs(translationY / maxTranslation) : 0);
				break;

			case GestureStatus.Completed:
			case GestureStatus.Canceled:
				// Jiggle back to original scale and translation
				await Task.WhenAll(
					LogoImage.TranslateToAsync(0, 0, 400, Easing.SpringOut),
					LogoImage.ScaleXToAsync(1, 400, Easing.SpringOut),
					LogoImage.ScaleYToAsync(1, 400, Easing.SpringOut)
				);
				break;
		}
	}
}