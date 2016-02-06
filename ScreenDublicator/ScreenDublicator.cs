using System;
using System.Drawing;
using System.Drawing.Imaging;

using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;
using System.Diagnostics;
using SharpDX.Mathematics.Interop;
using System.Runtime.InteropServices;

namespace ScreenDublicator
{
    public class ScreenDublicator : IDisposable
    {
        #region CONST

        private readonly int ADAPTER = 0;
        private readonly int MONITOR = 0;
        private readonly int TIMEOUT = 10000;
        private readonly int ATTEMPTS = 10;

        #endregion

        #region FIELDS

        private Device _device;
        private Texture2DDescription _textureDesc;
        private OutputDescription _outputDesc;
        private OutputDuplication _outputDublication;
        private Texture2D _desktopTexture;
        private OutputDuplicateFrameInformation _screenInfo;
        private int _width;
        private int _height;
        private bool _isDisposed = false;

        #endregion

        #region PUBLIC METHODS

        public ScreenDublicator()
        {
            Adapter adapter = null;
            Output output = null;
            Output1 output1 = null;
            try
            { 
                //Адаптер
                
                adapter = new Factory1().GetAdapter1(this.ADAPTER);
            
                //Устройство

                this._device = new Device(adapter);
                output = adapter.GetOutput(this.MONITOR);
            
                //Выход

                output1 = output.QueryInterface<Output1>();
                this._outputDesc = output.Description;
                this._width = this._outputDesc.DesktopBounds.Right - this._outputDesc.DesktopBounds.Left;
                this._height = this._outputDesc.DesktopBounds.Bottom - this._outputDesc.DesktopBounds.Top;
                this._textureDesc = new Texture2DDescription()
                {
                    CpuAccessFlags = CpuAccessFlags.Read,
                    BindFlags = BindFlags.None,
                    Format = Format.B8G8R8A8_UNorm,
                    Width = this._width,
                    Height = this._height,
                    OptionFlags = ResourceOptionFlags.None,
                    MipLevels = 1,
                    ArraySize = 1,
                    SampleDescription = { Count = 1, Quality = 0 },
                    Usage = ResourceUsage.Staging
                };
                this._outputDublication = output1.DuplicateOutput(this._device);

                //Текстура

                this._desktopTexture = new Texture2D(this._device, this._textureDesc);
            }
            catch(SharpDXException sdxex)
            {
                if (sdxex.ResultCode.Code == SharpDX.DXGI.ResultCode.NotCurrentlyAvailable.Result.Code)
                {
                    throw new ScreenDublicatorException("Достигнут предел приложений, работающих с API)");
                }
                else
                {
                    string message = String.Format("Не удалось создать дублирующий выход");
                    Debug.WriteLine(message + "\n{0}\n{1}", sdxex.Message, sdxex.StackTrace);
                    throw new ScreenDublicatorException(message);
                }
            }
            catch(Exception ex)
            {
                string message = String.Format("Ошибка при инициализации объекта\n{0}\n{1}", ex.Message, ex.StackTrace);
                throw new ScreenDublicatorException(message);
            }
            finally
            {
                if(adapter != null) { adapter.Dispose(); }
                if(output != null) { output.Dispose(); }
                if(output1 != null) { output1.Dispose(); }
            }
        }

        public void Dispose()
        {
            if (this._isDisposed) return;

            if (this._device != null)
            {
                this._device.Dispose();
            }
            if (this._outputDublication != null)
            {
                this._outputDublication.Dispose();
            }
            if (this._desktopTexture != null)
            {
                this._desktopTexture.Dispose();
            }
            if (this._desktopTexture != null)
            {
                this._desktopTexture.Dispose();
            }

            this._isDisposed = true;
        }

        public ScreenInfo GetScreenInformation()
        {
            ScreenInfo screenInfo = new ScreenInfo();
            SharpDX.DXGI.Resource screenResource = null;
            for (int i = 0; i < this.ATTEMPTS; i++)
            {
                try
                {
                    //Получаем кадр
                
                    this._outputDublication.AcquireNextFrame(this.TIMEOUT, out this._screenInfo, out screenResource);

                    using (Texture2D screenTexture2D = screenResource.QueryInterface<Texture2D>())
                    {
                        this._device.ImmediateContext.CopyResource(screenTexture2D, this._desktopTexture);
                    }
                    DataBox mapSource = this._device.ImmediateContext.MapSubresource(this._desktopTexture, 0, MapMode.Read, MapFlags.None);
               
                    //Создаём изображение

                    screenInfo.ScreenImage = this.GetBitmap(mapSource);

                    //Изменённые регионы
                
                    screenInfo.UpdatedRegions = this.GetDirtyRectangles();

                    //Курсор

                    PointerInfo pi = this.GetCursorInfo();
                    if (pi != null)
                    {
                        screenInfo.PointerInfo = pi;
                    }

                    this._device.ImmediateContext.UnmapSubresource(this._desktopTexture, 0);

                    return screenInfo;
                }
                catch (SharpDXException sdxex)
                {
                    if (sdxex.ResultCode.Code != SharpDX.DXGI.ResultCode.WaitTimeout.Result.Code)
                    {
                        Debug.WriteLine("Не удалось получить кадр. Превышен таймаут.");
                    }
                    else
                    {
                        string message = "Не удалось получить кадр. Ошибка в SharpDX";
                        Debug.WriteLine(message + "\n{0}\n{1}", sdxex.Message, sdxex.StackTrace);
                    }
                }
                catch (Exception ex)
                {
                    string message = "Ошибка при получении кадра";
                    Debug.WriteLine(message + "\n{0}\n{1}", ex.Message, ex.StackTrace);
                }
                finally
                {
                    if(screenResource != null) screenResource.Dispose();
                    this._outputDublication.ReleaseFrame();
                }
            }
            throw new ScreenDublicatorException(String.Format("Не удалось получить информацию о кадре. Попыток: {0}", this.ATTEMPTS));
        }

        #endregion

        #region PRIVATE METHODS

        /// <summary>
        /// Получить информацию о курсоре
        /// </summary>
        private PointerInfo GetCursorInfo()
        {
            if (this._screenInfo.LastMouseUpdateTime == 0)
            {
                return null;
            }

            PointerInfo pointerInfo = new PointerInfo();

            //Какие-то магические проверки

            bool updatePosition = true;
            if (!this._screenInfo.PointerPosition.Visible && (pointerInfo.WhoUpdatedPositionLast != this.MONITOR))
                updatePosition = false;
            if (this._screenInfo.PointerPosition.Visible && pointerInfo.Visible && (pointerInfo.WhoUpdatedPositionLast != this.MONITOR) && (pointerInfo.LastTimeStamp > this._screenInfo.LastMouseUpdateTime))
                updatePosition = false;

            //Позиция

            if (updatePosition)
            {
                pointerInfo.Position = new RawPoint() {
                    X = this._screenInfo.PointerPosition.Position.X,
                    Y = this._screenInfo.PointerPosition.Position.Y
                };
                pointerInfo.WhoUpdatedPositionLast = this.MONITOR;
                pointerInfo.LastTimeStamp = this._screenInfo.LastMouseUpdateTime;
                pointerInfo.Visible = this._screenInfo.PointerPosition.Visible;
            }

            //Форма

            if (this._screenInfo.PointerShapeBufferSize == 0)
            { 
                return null;
            }
            if (this._screenInfo.PointerShapeBufferSize > pointerInfo.BufferSize)
            {
                pointerInfo.PtrShapeBuffer = new byte[this._screenInfo.PointerShapeBufferSize];
                pointerInfo.BufferSize = this._screenInfo.PointerShapeBufferSize;
            }
            try
            {
                unsafe
                {
                    fixed (byte* ptrShapeBufferPtr = pointerInfo.PtrShapeBuffer)
                    {
                        this._outputDublication.GetFramePointerShape(
                            this._screenInfo.PointerShapeBufferSize, 
                            (IntPtr)ptrShapeBufferPtr, 
                            out pointerInfo.BufferSize, 
                            out pointerInfo.ShapeInfo);
                    }
                }
            }
            catch (SharpDXException ex)
            {
                if (ex.ResultCode.Failure)
                {
                    throw new ScreenDublicatorException("Ошибка при получении формы курсора");
                }
            }

            return pointerInfo;
        }

        /// <summary>
        /// Получить изменившиеся области
        /// </summary>
        /// <returns>Массив прямоугольников, где были изменения относительно предыдущего кадра</returns>
        private Rectangle[] GetDirtyRectangles()
        {
            Rectangle[] result = new Rectangle[0];
            if (this._screenInfo.TotalMetadataBufferSize > 0)
            {
                int dirtyRegionsLength = 0;
                RawRectangle[] rectangles = new RawRectangle[this._screenInfo.TotalMetadataBufferSize];
                this._outputDublication.GetFrameDirtyRects(rectangles.Length, rectangles, out dirtyRegionsLength);
                result = new Rectangle[dirtyRegionsLength / Marshal.SizeOf(typeof(RawRectangle))];
                for (int i = 0; i < result.Length; i++)
                {
                    result[i] = new Rectangle(
                        rectangles[i].Left, 
                        rectangles[i].Top,
                        rectangles[i].Right - rectangles[i].Left,
                        rectangles[i].Bottom - rectangles[i].Top
                    );
                }
            }
            return result;
        }

        /// <summary>
        /// Получить изображение рабочего стола из DataBox
        /// </summary>
        private Image GetBitmap(DataBox mapSource)
        {
            Bitmap bitmap = new Bitmap(this._width, this._height, PixelFormat.Format32bppArgb);
            Rectangle boundsRect = new Rectangle(0, 0, this._width, this._height);

            BitmapData mapDest = bitmap.LockBits(boundsRect, ImageLockMode.WriteOnly, bitmap.PixelFormat);
            IntPtr sourcePtr = mapSource.DataPointer;
            IntPtr destPtr = mapDest.Scan0;
            for (int y = 0; y < this._height; y++)
            {
                Utilities.CopyMemory(destPtr, sourcePtr, this._width * 4);
                sourcePtr = IntPtr.Add(sourcePtr, mapSource.RowPitch);
                destPtr = IntPtr.Add(destPtr, mapDest.Stride);
            }
            bitmap.UnlockBits(mapDest);

            return bitmap;
        }

        #endregion
    }
}