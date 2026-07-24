#nullable enable
using System;

namespace RemoteDesktopServer.Helpers
{
    public static class WpfCoordinateMapper
    {
        public static (bool IsValid, float NormX, float NormY) GetNormalizedCoordinates(
            double mouseX, double mouseY,
            double containerWidth, double containerHeight,
            double imageSourceWidth, double imageSourceHeight,
            bool isFill = false)
        {
            if (containerWidth <= 0 || containerHeight <= 0)
                return (false, 0f, 0f);

            if (isFill || imageSourceWidth <= 0 || imageSourceHeight <= 0)
            {
                float fillX = (float)Math.Clamp(mouseX / containerWidth, 0.0, 1.0);
                float fillY = (float)Math.Clamp(mouseY / containerHeight, 0.0, 1.0);
                return (true, fillX, fillY);
            }

            double aspectSource = imageSourceWidth / imageSourceHeight;
            double aspectContainer = containerWidth / containerHeight;

            double renderedWidth, renderedHeight;
            double offsetX, offsetY;

            if (aspectSource > aspectContainer)
            {
                // Width bounded, vertical letterboxing (top & bottom black bars)
                renderedWidth = containerWidth;
                renderedHeight = containerWidth / aspectSource;
                offsetX = 0;
                offsetY = (containerHeight - renderedHeight) / 2.0;
            }
            else
            {
                // Height bounded, horizontal pillarboxing (left & right black bars)
                renderedHeight = containerHeight;
                renderedWidth = containerHeight * aspectSource;
                offsetX = (containerWidth - renderedWidth) / 2.0;
                offsetY = 0;
            }

            double relativeX = mouseX - offsetX;
            double relativeY = mouseY - offsetY;

            if (relativeX < 0 || relativeX > renderedWidth || relativeY < 0 || relativeY > renderedHeight)
            {
                // Mouse is over black bars, but still clamp normalized coordinates
                float clampedX = (float)Math.Clamp(relativeX / renderedWidth, 0.0, 1.0);
                float clampedY = (float)Math.Clamp(relativeY / renderedHeight, 0.0, 1.0);
                return (true, clampedX, clampedY);
            }

            float normX = (float)(relativeX / renderedWidth);
            float normY = (float)(relativeY / renderedHeight);

            return (true, normX, normY);
        }
    }
}
