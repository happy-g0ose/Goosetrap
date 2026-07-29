using System.Drawing;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace Goosetrap.Extensions
{
    public static class IconEx
    {
        public static Icon GetSized(this Icon icon, int width, int height) => new(icon, new Size(width, height));

        public static ImageSource GetImageSource(this Icon icon, bool handleException = true)
        {
            using MemoryStream stream = new();
            icon.Save(stream);
            stream.Seek(0, SeekOrigin.Begin);

            try
            {
                var decoder = new IconBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                BitmapFrame bestFrame = null;
                int maxArea = 0;
                foreach (var frame in decoder.Frames)
                {
                    int area = frame.PixelWidth * frame.PixelHeight;
                    if (area > maxArea)
                    {
                        maxArea = area;
                        bestFrame = frame;
                    }
                }
                return bestFrame ?? decoder.Frames[0];
            }
            catch (Exception ex)
            {
                if (handleException)
                {
                    App.Logger.WriteException("IconEx::GetImageSource", ex);
                    Frontend.ShowMessageBox(string.Format(Strings.Dialog_IconLoadFailed, ex.Message));
                    return BootstrapperIcon.IconGoosetrap.GetIcon().GetImageSource(false);
                }
                throw;
            }
        }
    }
}
