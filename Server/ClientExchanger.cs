using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO.Compression;

namespace Server
{
    /// <summary>
    /// Класс обслуживает запросы клиента
    /// Работает в своём потоке
    /// </summary>
    class ClientExchanger
    {
        #region CONST
        
        private readonly int BUFFER_SIZE = 1024 * 1024;
        private readonly char SEPARATOR = '|';
        private readonly string PARAM_PROTOCOL = "PARAM{2}{0}{2}{1}{2}";
        private readonly string IMAGE_PROTOOL = "IMG{2}{0}{2}{1}{2}";

        #endregion

        #region FIELDS

        private ScreenProcessor _screenProcessor;               //Обработчик состояния рабочего стола
        private TcpClient _client;                              //Сокет для обмена данными с клиентом
        private string _clientAddress;                          //Строкове представление адреса клиента
        private ImageFormat _imageFormat = ImageFormat.Bmp;     //Формат передачи изображения
        private bool _compress = false;                         //Применять ли Zip сжатие к передоваемым данным

        #endregion
        
        #region PUBLIC METHODS

        public ClientExchanger(TcpClient client, ScreenProcessor sProcessor)
        {
            if (client == null)
            {
                Debug.WriteLine("Необходимо передать TcpClient для создания объекта");
                throw new ClientExchangerException("TcpClient ip is null");
            }
            if (sProcessor == null)
            {
                Debug.WriteLine("Необходимо передать ScreenProcessor для создания объекта");
                throw new ClientExchangerException("ScreenProcessor sProcessor is null");
            }

            this._client = client;
            this._screenProcessor = sProcessor;
            this._clientAddress = client.Client.RemoteEndPoint.ToString();
        }

        /// <summary>
        /// Обмен данными с клиентом
        /// Выполняется в рабочем потоке
        /// </summary>
        public void ClientExchange()
        {
            //Получение сетевого потока

            NetworkStream clientNS = null;
            try
            {
                clientNS = this._client.GetStream();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Ошибка при получении сетевого потока клиента {0}\n{1}\n{2}", this._clientAddress, ex.Message, ex.StackTrace);
                Debug.WriteLine("Завершаем обмен с клиентом {0}", this._clientAddress);
                this._client.Close();
                return;
            }

            //Обработка запросов

            byte[] buffer = new byte[BUFFER_SIZE];
            while (true)
            {
                try
                {
                    //Запрос клиента

                    Debug.WriteLine("Ожидаем запрос клиента {0}", this._clientAddress);
                    string clientRequest = this.GetClientRequest(clientNS);
                    Debug.WriteLine("Запрос клиента: {0} {1}", clientRequest, this._clientAddress);

                    //Обработка запроса

                    string[] clientRequestArray = clientRequest.Split(SEPARATOR);
                    switch (clientRequestArray[0])
                    {
                        case "GETSCREEN":
                            Debug.WriteLine("Получен GETSCREEN");
                            this.SendScreenToClient(clientNS);
                            Debug.WriteLine("Отправлен SCREEN");
                            break;
                        case "GETPARAM":
                            Debug.WriteLine("Получен GETPARAM");
                            this.SendParamToClient(clientNS);
                            Debug.WriteLine("Отправлен PARAM");
                            break;
                        default:
                            throw new ClientExchangerException(String.Format("Получена неизвестная команда от клиента: {0}", clientRequestArray[0]));
                    }
                }
                catch (ClientExchangerImageException ceiex)
                {
                    Debug.WriteLine("Не удалось отправить изображение клиенту {0}\n", this._clientAddress, ceiex.Message);
                    this.SendResponce("ERROR|NOIMAGE", clientNS);
                }
                catch (ClientExchangerException ceex)
                {
                    Debug.WriteLine("Ошибка соединения {0}\n{1}\n{2}", this._clientAddress, ceex.Message, ceex.StackTrace);
                    Debug.WriteLine("Закрываем соединение, завершаем поток {0}", this._clientAddress);
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Ошибка при обмене данными с клиентом {0}\n{1}\n{2}", this._clientAddress, ex.Message, ex.StackTrace);
                    Debug.WriteLine("Закрываем соединение, завершаем поток {0}", this._clientAddress);
                    break;
                }
            }
            this._client.Close();
        }

        #endregion

        #region PRIVATE METHODS

        private int GetClientRequestLength(NetworkStream clientNS)
        {
            byte[] buffer = new byte[4];
            int cnt = clientNS.Read(buffer, 0, 4);
            if(cnt != 4)
            {
                string message = String.Format("Ошибка при получении длины запроса клиента. Получено байт: {0} {1}", cnt, this._clientAddress);
                Debug.WriteLine(message);
                throw new ClientExchangerException(message);
            }
            return BitConverter.ToInt32(buffer, 0);
        }

        private string GetClientRequest(NetworkStream clientNS)
        {
            int requestLength = this.GetClientRequestLength(clientNS);
            byte[] buffer = new byte[BUFFER_SIZE];
            using (MemoryStream ms = new MemoryStream(requestLength))
            { 
                int byteCounter = 0;
                do
                {
                    int cnt = clientNS.Read(buffer, 0, buffer.Length);
                    if (cnt == 0)
                    {
                        string message = String.Format("Ошибка при получении запроса клиента. Получено байт: 0 байт {0}", this._clientAddress);
                        Debug.WriteLine(message);
                        throw new ClientExchangerException(message);
                    }
                    byteCounter += cnt;
                    ms.Write(buffer, 0, cnt);
                } while (requestLength > byteCounter);
                return Encoding.UTF8.GetString(ms.ToArray(), 0, byteCounter);
            }
        }

        private void SendScreenToClient(NetworkStream clientNS)
        {
            //Массив байт изображения

            byte[] screenImageBytes = new byte[0];
            Image screenImage = this._screenProcessor.CurrentScreenImage; //to-do(ref vs copy)
            screenImageBytes = this.GetBytesFromImage(screenImage);
            if(screenImageBytes == null || screenImageBytes.Length == 0)
            {
                string message = String.Format("Ошибка при получении массива байт изображения {0}", this._clientAddress);
                Debug.WriteLine(message);
                throw new ClientExchangerImageException(message);
            }

            Debug.WriteLine("Количество байт изображения: {0}. {1}", screenImageBytes.Length, this._clientAddress);

            //Заголовок

            string header = String.Format(this.IMAGE_PROTOOL, screenImage.Width, screenImage.Height, this.SEPARATOR);
            byte[] headerBytes = Encoding.UTF8.GetBytes(header);

            Debug.WriteLine("Заголовок ответа: {0}\tРазмер: {1} {2}", header, headerBytes.Length, this._clientAddress);
            Debug.WriteLine("Длина данных ответа: {0} {1}", screenImageBytes.Length, this._clientAddress);

            //Передача данных //to-do (check!)

            clientNS.Write(BitConverter.GetBytes(headerBytes.Length), 0, 4);                            //Размер заголовка

            Debug.WriteLine("Отправлена длина заголовка:{0}", headerBytes);

            clientNS.Write(headerBytes, 0, headerBytes.Length);                                         //Заголовок

            Debug.WriteLine("Отправлен заголовок:{0}", header);

            clientNS.Write(BitConverter.GetBytes(screenImageBytes.Length), 0, 4);                       //Размер данных

            Debug.WriteLine("Отправлена длина данных:{0}", screenImageBytes.Length);

            clientNS.Write(screenImageBytes, 0, screenImageBytes.Length);                               //Данные

            Debug.WriteLine("Отправлены данные");
        }

        private byte[] GetBytesFromImage(Image screenImage)
        {
            Random r = new Random();
            byte[] b = new byte[1024];
            r.NextBytes(b);
            return b;
            //to-do
            byte[] result = null;
            try
            { 
                if (this._compress)
                {
                    using (MemoryStream memStream = new MemoryStream())
                    {
                        using (GZipStream gZipStream = new GZipStream(memStream, CompressionMode.Compress))
                        {
                            screenImage.Save(gZipStream, this._imageFormat);
                            result = memStream.ToArray();
                        }
                    }
                }
                else
                {
                    using (MemoryStream memStream = new MemoryStream())
                    {
                        screenImage.Save(memStream, this._imageFormat);
                        result = memStream.ToArray();
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Ошибка конвертации изображения в массив байт {0}\n{1}\n{2}", this._clientAddress, ex.Message, ex.StackTrace);
                return null;
            }
        }

        private void SendParamToClient(NetworkStream clientNS)
        {
            string compress = (this._compress == true) ? "1" : "0";
            string response = String.Format(this.PARAM_PROTOCOL, this._imageFormat, compress, this.SEPARATOR);
            this.SendResponce(response, clientNS);
            Debug.WriteLine("Отправлены параметры клиенту: {0} {1}", response, this._clientAddress);
        }

        private void SendResponce(string response, NetworkStream clientNS)
        {
            byte[] responseArray = Encoding.UTF8.GetBytes(response);
            int responseLength = responseArray.Length;
            clientNS.Write(BitConverter.GetBytes(responseLength), 0, 4);
            clientNS.Write(responseArray, 0, responseArray.Length);
        }

        #endregion
    }
}