using ScreenDublicator;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;

using MyScreenDublicator = ScreenDublicator.ScreenDublicator;

namespace Server
{
    /// <summary>
    /// Класс получает и хранит информацию о состоянии рабочего стола.
    /// Работает в своём потоке, может обрабатывать запросы 
    /// в многопоточной среде
    /// </summary>
    class ScreenProcessor : IDisposable
    {
        #region CONST

        private int UDPATE_TIMEOUT = 50;

        #endregion

        #region FIELDS

        private static ScreenProcessor _instance = null;                        //Singleton
        private MyScreenDublicator _desktopDublicator = null;                   //Дубликатор рабочего стола (использует DirectX)
        private Image _currentScreenImage = null;                               //Текущее изображение рабочего стола
        private Point _currentCursorPosition = Point.Empty;                     //Текущее положение курсора
        private ReaderWriterLockSlim _rwLocker = new ReaderWriterLockSlim();    //Синхронизация доступа к ресурсам
        private bool _isDisposed = false;                                       //Признак уничтожения

        #endregion

        #region PROPERTIES

        /// <summary>
        /// Получить экземпляр объекта (Singleton).
        /// Потоконебезопасный
        /// </summary>
        public static ScreenProcessor Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ScreenProcessor();
                }
                return _instance;
            }
        }

        /// <summary>
        /// Текущее изображене рабочего стола
        /// </summary>
        public Image CurrentScreenImage
        {
            get
            {
               try
               {
                   this._rwLocker.EnterReadLock();
                   return new Bitmap(this._currentScreenImage);
               }
               catch(Exception ex)
               {
                   string messge = String.Format("Не удалось вернуть изображение\n{0}\n{1}", ex.Message, ex.StackTrace);
                   Debug.WriteLine(messge);
                   return null;
               }
               finally
               {
                   this._rwLocker.ExitReadLock();
               }
            }
        }

        /// <summary>
        /// Текущее положение курсора
        /// </summary>
        public Point CurrentCursorPosition
        {
            get
            {
                Debug.WriteLine("Запрос координат курсора");
                try
                {
                    this._rwLocker.EnterReadLock();
                    return this._currentCursorPosition;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Не удалось получить координаты курсора");
                    Debug.WriteLine(ex.Message);
                    return Point.Empty;
                }
                finally
                {
                    this._rwLocker.ExitReadLock();
                }
            }
        }

        #endregion

        #region PUBLIC METHODS

        public void Dispose()
        {
            if (this._isDisposed)
            {
                return;
            }

            if(this._desktopDublicator != null)
            {
                this._desktopDublicator.Dispose();
            }
            
            if(this._rwLocker != null)
            { 
                this._rwLocker.Dispose();
                this._rwLocker = null;
            }

            if(this._currentScreenImage != null)
            {
                this._currentScreenImage.Dispose();
            }
            this._isDisposed = true;

            Debug.WriteLine("ScreenProcessor Disposed");
        }

        /// <summary>
        /// Запустить потока обновления данных о состоянии рабочего стола
        /// </summary>
        public void Start()
        {
            if(this._isDisposed)
            {
                string message = String.Format("Запуск потока обновления кадра не возможен. Объект Disposed");
                Debug.WriteLine(message);
                throw new ScreenProcessorException(message);
            }
            try
            {
                Debug.WriteLine("Запускается поток обновления кадра");
                new Thread(this.UpdateFrame) { IsBackground = true, Name = "Update Frame Thread" }.Start();
                Debug.WriteLine("Поток обновления кадра запущен");
            }
            catch(Exception ex)
            {
                string message = String.Format("Ошибка при запуске потока обновления кадра\n{0}\n{1}", ex.Message, ex.StackTrace);
                Debug.WriteLine(message);
                throw new ScreenProcessorException(message);
            }
        }
       
        #endregion

        #region PRIVATE METHODS

        private ScreenProcessor()
        {
            this._desktopDublicator = new MyScreenDublicator();
        }

        /// <summary>
        /// Обновление состояния рабочего стола. Выполняется в рабочем потоке.
        /// </summary>
        private void UpdateFrame()
        {
            while (true)
            {
                try
                {
                    ScreenInfo screenInfo = this._desktopDublicator.GetScreenInformation();
                    this._rwLocker.EnterWriteLock();
                    if(this._currentScreenImage != null)
                    {
                        this._currentScreenImage.Dispose();
                    }
                    this._currentScreenImage = screenInfo.ScreenImage;
                }
                catch (ScreenProcessorException sdex)
                {
                    Debug.WriteLine("Не удалось обновить информацию о кадре (DesktopDuplicationException)\n{0}\n{1}", sdex.Message, sdex.StackTrace);
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Не удалось обновить информацию о кадре (Exception)\n{0}\n{1}", ex.Message, ex.StackTrace);
                    break;
                }
                finally
                {
                    this._rwLocker.ExitWriteLock();
                }
                Thread.Sleep(this.UDPATE_TIMEOUT);  //to-do
            }
            Debug.WriteLine("Поток обновления кадра завершился");
        }

        #endregion
    }
}