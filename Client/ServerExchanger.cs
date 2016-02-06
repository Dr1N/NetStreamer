using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Client
{
    /// <summary>
    /// Обновление информации о состоянии рабочего стола сервера
    /// И отображение её в окне клиента (а не стоит ли это разделить)
    /// </summary>
    class ServerExchanger
    {
        #region CONST

        private readonly int BUFFER_SIZE = 1024 * 1024;
        private readonly char SEPARATOR = '|';
        private readonly Dictionary<string, ImageFormat> IMAGE_FROMATES = new Dictionary<string, ImageFormat>()
        {
            { "Bmp", ImageFormat.Bmp },
            { "Jpeg", ImageFormat.Jpeg },
            { "Png", ImageFormat.Png },
            { "Gif", ImageFormat.Gif }
        };

        #endregion

        #region FIELDS

        private static ServerExchanger _instance;
        private TcpClient _server;
        private Control _base;
        private Control _canvas;
        private ImageFormat _imageFormat;
        private bool _compress;

        #endregion

        #region PROPERTIES

        #endregion

        #region PUBLIC METHODS

        public static ServerExchanger GetInstance(TcpClient server, Control canvas)
        {
            if (_instance == null)
            {
                _instance = new ServerExchanger(server, canvas);
            }
            return _instance;
        }

        public void ServerExchange()
        {
            //Получить сетевой поток 

            NetworkStream serverNS = null;
            try
            {
                serverNS = this._server.GetStream();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Ошибка при получении сетевого потока сервера\n{0}", ex.Message);
                Debug.WriteLine("Завершаем обмен с сервером ");
                return;
            }

            //Получить параметры передачи

            try
            {
                Debug.WriteLine("Получаем параметры от сервера");

                this.SendRequest("GETPARAM|", serverNS);

                //Приём параметров

                string response = Encoding.UTF8.GetString(this.GetServerResponce(serverNS));
                string[] responseArr = response.Split(SEPARATOR);

                Debug.WriteLine("Ответ сервера: {0}", response);

                if(responseArr[0] == "PARAM")
                {
                    //Сохранение параметров в объекте

                    this._imageFormat = this.IMAGE_FROMATES[responseArr[1]];
                    this._compress = (responseArr[2] == "1") ? true : false;

                    Debug.WriteLine("Параметры сохранены");
                }
                else
                {
                    Debug.WriteLine("Не удалось получить параметры от сервера");
                    Debug.WriteLine("Завершаем обмен с сервером ");
                    throw new ServerExchangerException("Не удалось получить параметры от сервера");
                }

                Debug.WriteLine("Получили параметры от сервера");
            }
            catch(Exception ex)
            {
                Debug.WriteLine("Ошибка при получении параметров от сервера\n{0}", ex.Message);
                return;
            }

            //Начать обновление рабочего стола

            while (true)
            {
                try
                {
                    Debug.WriteLine("Получить изображение");

                    this.SendRequest("GETSCREEN|", serverNS);

                    byte[] responseHeaderBytes = this.GetServerResponce(serverNS);
                    string responseHeader = Encoding.UTF8.GetString(responseHeaderBytes);

                    Debug.WriteLine("Заголовок ответа: {0}", responseHeader);
                    
                    byte[] responseDate = this.GetServerResponce(serverNS);

                    Debug.WriteLine("Длина данных ответа: {0}", responseDate.Length);

                    Debug.WriteLine("Получено изображение, обработка 500мс");

                    Thread.Sleep(500);
                }
                catch(Exception ex)
                {
                    Debug.WriteLine("Ошибка при получении ответа сервера\n{0}", ex.Message);
                    break;
                }
            }

            Debug.WriteLine("Завершился поток обмена данными с сервером");

            this._server.Close();
        }

        #endregion

        #region PRIVATE METHODS

        private ServerExchanger(TcpClient server, Control canvas)
        {
            this._server = server;
            this._canvas = canvas;
        }

        private void SendRequest(string request, NetworkStream serverNS)
        {
            byte[] requestArr = Encoding.UTF8.GetBytes(request);
            serverNS.Write(BitConverter.GetBytes(requestArr.Length), 0, 4);
            serverNS.Write(requestArr, 0, requestArr.Length);
        }

        private int GetServerResponceLength(NetworkStream serverNS)
        {
            byte[] buffer = new byte[4];
            int cnt = serverNS.Read(buffer, 0, 4);
            if (cnt != 4)
            {
                string message = String.Format("Ошибка при получении длины ответа сервера. Получено байт: {0}", cnt);
                Debug.WriteLine(message);
                throw new ServerExchangerException(message);
            }
            return BitConverter.ToInt32(buffer, 0);
        }

        private byte[] GetServerResponce(NetworkStream serverNS)
        {
            int responceLength = this.GetServerResponceLength(serverNS);
            byte[] buffer = new byte[BUFFER_SIZE];
            using (MemoryStream ms = new MemoryStream())
            {
                int byteCounter = 0;
                do
                {
                    int cnt = serverNS.Read(buffer, 0, buffer.Length);
                    if (cnt == 0)
                    {
                        string message = String.Format("Ошибка при получении ответа сервера. Получено байт: 0 байт");
                        Debug.WriteLine(message);
                        throw new ServerExchangerException(message);
                    }
                    byteCounter += cnt;
                    ms.Write(buffer, 0, cnt);
                } while (responceLength > byteCounter);
                return ms.ToArray();
            }

            #endregion
        }
    }
}