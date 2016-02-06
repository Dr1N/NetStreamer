using System.Drawing;

namespace ScreenDublicator
{
    /// <summary>
    /// Информация о кадре
    /// </summary>
    public class ScreenInfo
    {
        public Image ScreenImage { get; internal set; }
        public Rectangle[] UpdatedRegions { get; internal set; }
        public PointerInfo PointerInfo { get; internal set; }
    }
}