using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Server
{
    /// <summary>
    /// Класс создаёт серверный сокет, ожидает соединений клиентов
    /// Запускает обмен данными с клиентами протокол TCP
    /// </summary>
    class ClientProcessor : IDisposable
    {
        #region FIELDS

        private static ClientProcessor _instance;           //Singleton
        private ScreenProcessor _screenProcessor;           //Обработчик состояния рабочего стола(для передачи рабочим потокам обмена данными)
        private IPAddress _serverAddress;                   //Адрес сервера
        private int _serverPort;                            //Порт сервера
        private TcpListener _serverSocket;                  //Сокет для соединений клиентов
        private bool _isDisposed;

        #endregion

        #region PUBLIC METHODS

        public static ClientProcessor GetInstance(IPAddress ip, int port, ScreenProcessor sProcessor)
        {
            if (_instance == null)
            {
                _instance = new ClientProcessor(ip, port, sProcessor);
            }
            return _instance;
        }

        public void Dispose()
        {
            if(this._isDisposed)
            {
                return;
            }
            if(this._serverSocket != null)
            { 
                try
                { 
                    this._serverSocket.Stop();
                }
                catch (SocketException sex)
                {
                    Debug.WriteLine("Ошибка при закрытии сокета (Dispose): {0}", sex.Message);
                }
                this._serverSocket = null;
            }
            this._isDisposed = true;
            Debug.WriteLine("Объект ClientProcessor освободил ресурсы");
        }

        public void Start()
        {
            if (this._isDisposed)
            {
                string message = String.Format("Запуск потока ожидания клиентов. Объект Disposed");
                Debug.WriteLine(message);
                throw new ClientProcessorException(message);
            }

            try
            {
                Debug.WriteLine("Запуск потока ожидания соединений клиентов");
                this._serverSocket = new TcpListener(this._serverAddress, this._serverPort);
                this._serverSocket.Start();
                new Thread(AcceptClients) { IsBackground = true }.Start();
                Debug.WriteLine("Прослушивание сокета запущено {0}", this._serverSocket.LocalEndpoint);
            }
            catch (Exception ex)
            {
                string message = String.Format("Ошибка создания и запуска потока прослушивания сокета\n{0}\n{1}", ex.Message, ex.StackTrace);
                Debug.WriteLine(message);
                throw new ClientProcessorException(message);
            }
        }

        #endregion

        #region PRIVATE METHODS

        private ClientProcessor(IPAddress ip, int port, ScreenProcessor sProcessor)
        {
            if (port <= 0 || port > 65536)
            {
                Debug.WriteLine("Недопустимое значение порта (port = )" + port);
                throw new ClientProcessorException("Недопустимое значение порта (port = )" + port);
            }
            if (ip == null)
            {
                Debug.WriteLine("Необходимо передать IPAddress для создания объекта");
                throw new ClientProcessorException("IPAddress ip is null");
            }
            if (sProcessor == null)
            {
                Debug.WriteLine("Необходимо передать ScreenProcessor для создания объекта");
                throw new ClientProcessorException("ScreenProcessor sProcessor is null");
            }

            this._serverAddress = ip;
            this._serverPort = port;
            this._screenProcessor = sProcessor;
        }
        
        /// <summary>
        /// Метод ожидает соединения клиентов и создаёт потоки обработки соединений 
        /// Работает в своём в потоке
        /// </summary>
        private void AcceptClients()
        {
            //Цикл ожидания соединений

            Debug.WriteLine("Поток ожидания соединений запустился");
            while (true)
            {
                try
                {
                    TcpClient client = this._serverSocket.AcceptTcpClient();
                    Debug.WriteLine("Соединение установлено с {0}", client.Client.RemoteEndPoint.ToString());

                    // Запуск потока обработки запросов клиента

                    ClientExchanger clientExchanger = new ClientExchanger(client, this._screenProcessor);
                    new Thread(clientExchanger.ClientExchange).Start();
                    Debug.WriteLine("Поток обмена с клиентом {0} запустился", client.Client.RemoteEndPoint.ToString());
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Ошибка при ожидании соединений или запуска потока обмена данными с клиентом\n{0}\n{1}", ex.Message, ex.StackTrace);
                    break;
                }
            }
            Debug.WriteLine("Поток ожидания соединений завершился");
            if(this._serverSocket != null)
            { 
                try
                { 
                    this._serverSocket.Stop();
                    this._serverSocket = null;
                }
                catch(Exception ex)
                {
                    Debug.WriteLine("Исключение при остановке прослушивания сокета");
                    Debug.WriteLine(ex.Message);
                }
                Debug.WriteLine("Серверный сокет закрыт");
            }
        }
        #endregion
    }
}