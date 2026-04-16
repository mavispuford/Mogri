namespace Mogri.Helpers;

public static class MathHelper
{
    public static (double AspectRatioDouble, string AspectRatioString, int Gcd, int AspectWidth, int AspectHeight) CalculateAspectRatio(double width, double height)
    {
        if (width <= 0 || height <= 0)
        {
            return (0, string.Empty, 0, 0, 0);
        }

        var aspectRatioDouble = width / height;
        var gcd = GreatestCommonDivisor((int)width, (int)height);
        var aspectWidth = (int)(width / gcd);
        var aspectHeight = (int)(height / gcd);
        var aspectRatioString = $"{aspectWidth}:{aspectHeight}";

        return (aspectRatioDouble, aspectRatioString, gcd, aspectWidth, aspectHeight);
    }

    public static int GreatestCommonDivisor(int a, int b)
    {
        return b == 0 ? a : GreatestCommonDivisor(b, a % b);
    }

    public static double ConstrainDimensionValue(double value)
    {
        return double.Clamp(Math.Round(value / Constants.ResolutionValueDivisor) * Constants.ResolutionValueDivisor, Constants.MinimumWidthHeight, Constants.MaximumWidthHeight);
    }

    public enum DimensionConstraint
    {
        UseMinimumWidthHeight,
        UseMaximumWidthHeight,
        ClosestMatch
    }

    public static (double Width, double Height) GetAspectCorrectConstrainedDimensions(double width, double height, double aspectWidth = 0, double aspectHeight = 0, DimensionConstraint dimensionConstraint = DimensionConstraint.UseMaximumWidthHeight)
    {
        if (width < 0 || height < 0)
        {
            return (0, 0);
        }

        if (aspectWidth == 0 || aspectHeight == 0)
        {
            var aspectRatioResult = CalculateAspectRatio(width, height);

            aspectWidth = aspectRatioResult.AspectWidth;
            aspectHeight = aspectRatioResult.AspectHeight;
        }

        var aspectRatio = aspectWidth / aspectHeight;
        var portrait = aspectWidth < aspectHeight;

        double targetWidth = width;
        double targetHeight = height;

        switch (dimensionConstraint)
        {
            case DimensionConstraint.UseMinimumWidthHeight:
                if (portrait)
                {
                    targetWidth = Constants.MinimumWidthHeight;
                    targetHeight = targetWidth / aspectRatio;
                }
                else
                {
                    targetHeight = Constants.MinimumWidthHeight;
                    targetWidth = targetHeight * aspectRatio;
                }

                break;
            case DimensionConstraint.UseMaximumWidthHeight:
                if (portrait)
                {
                    targetHeight = Constants.MaximumWidthHeight;
                    targetWidth = targetHeight * aspectRatio;
                }
                else
                {
                    targetWidth = Constants.MaximumWidthHeight;
                    targetHeight = targetWidth / aspectRatio;
                }
                break;
            case DimensionConstraint.ClosestMatch:
                var widthWithinBounds = width >= Constants.MinimumWidthHeight && width <= Constants.MaximumWidthHeight;
                var heightWithinBounds = height >= Constants.MinimumWidthHeight && height <= Constants.MaximumWidthHeight;

                var currentAspectRatio = CalculateAspectRatio(width, height);
                if (Math.Abs(currentAspectRatio.AspectRatioDouble - aspectRatio) > .001d)
                {
                    // Different aspect ratio was requested - Calculate using existing dimensions

                    if (portrait)
                    {
                        targetWidth = targetHeight * aspectRatio;
                    }
                    else
                    {
                        targetHeight = targetWidth / aspectRatio;
                    }

                    break;
                }


                if (widthWithinBounds && heightWithinBounds)
                {
                    // Target width/height were already set above the switch statement
                    break;
                }

                if (portrait)
                {
                    // Height is taller than max - Set it to max, then calculate target width
                    if (height > Constants.MaximumWidthHeight)
                    {
                        targetHeight = Constants.MaximumWidthHeight;
                        targetWidth = targetHeight * aspectRatio;
                    }
                    else if (height < Constants.MinimumWidthHeight)
                    {
                        // Height is less than minimum, instead calculate off the width
                        if (!widthWithinBounds)
                        {
                            targetWidth = Constants.MinimumWidthHeight;
                        }
                        else
                        {
                            targetWidth = width;
                        }

                        targetHeight = targetWidth / aspectRatio;
                    }
                }
                else
                {
                    // Width is wider than max - Set it to max, then calculate target height
                    if (width > Constants.MaximumWidthHeight)
                    {
                        targetWidth = Constants.MaximumWidthHeight;
                        targetHeight = targetWidth / aspectRatio;
                    }
                    else if (width < Constants.MinimumWidthHeight)
                    {
                        // Width is less than minimum, instead calculate off the height
                        if (!heightWithinBounds)
                        {
                            targetHeight = Constants.MinimumWidthHeight;
                        }
                        else
                        {
                            targetHeight = height;
                        }

                        targetWidth = targetHeight * aspectRatio;
                    }
                }


                break;
            default:
                break;
        }

        // Make sure resulting dimensions are multiples of Constants.ResolutionValueDivisor
        var constrainedWidth = ConstrainDimensionValue(targetWidth);
        var constrainedHeight = ConstrainDimensionValue(targetHeight);

        return (constrainedWidth, constrainedHeight);
    }
}
