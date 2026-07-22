using System;
using System.Drawing;
using System.Windows.Forms;

namespace RemoteDesktopServer.Helpers
{
    public static class CoordinateMapper
    {
        /// <summary>
        /// Calculates normalized coordinates (0.0 to 1.0) inside a PictureBox with SizeMode = Zoom.
        /// Handles letterboxing and pillarboxing precisely.
        /// </summary>
        public static bool GetNormalizedCoordinates(
            PictureBox pb, 
            int mouseX, 
            int mouseY, 
            out float normX, 
            out float normY)
        {
            normX = 0f;
            normY = 0f;

            if (pb.Image == null || pb.ClientRectangle.Width == 0 || pb.ClientRectangle.Height == 0)
                return false;

            int imgWidth = pb.Image.Width;
            int imgHeight = pb.Image.Height;
            int boxWidth = pb.ClientRectangle.Width;
            int boxHeight = pb.ClientRectangle.Height;

            float imgAspect = (float)imgWidth / imgHeight;
            float boxAspect = (float)boxWidth / boxHeight;

            float drawnWidth, drawnHeight;
            float offsetX, offsetY;

            if (imgAspect > boxAspect)
            {
                // Fills width, letterboxed top and bottom
                drawnWidth = boxWidth;
                drawnHeight = boxWidth / imgAspect;
                offsetX = 0;
                offsetY = (boxHeight - drawnHeight) / 2f;
            }
            else
            {
                // Fills height, pillarboxed left and right
                drawnHeight = boxHeight;
                drawnWidth = boxHeight * imgAspect;
                offsetY = 0;
                offsetX = (boxWidth - drawnWidth) / 2f;
            }

            float relX = mouseX - offsetX;
            float relY = mouseY - offsetY;

            // Check if click was inside actual image boundaries (excluding black bars)
            if (relX < 0 || relX > drawnWidth || relY < 0 || relY > drawnHeight)
            {
                return false;
            }

            normX = Math.Max(0f, Math.Min(1.0f, relX / drawnWidth));
            normY = Math.Max(0f, Math.Min(1.0f, relY / drawnHeight));
            return true;
        }
    }
}
