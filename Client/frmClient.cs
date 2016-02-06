using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Client
{
    public partial class frmClient : Form
    {
        private IPAddress _serverIp = IPAddress.Parse("192.168.1.100");
        private int _serverPort = 21999;
        private TcpClient _server;
        private ServerExchanger _serverExchanger;

        public frmClient()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            StartExchangeAndUpdate();
        }

        private void StartExchangeAndUpdate()
        {
            Debug.WriteLine("Устанавливаем соединение с сервером");
            try
            {
                this._server = new TcpClient(this._serverIp.ToString(), this._serverPort);
                this._serverExchanger = ServerExchanger.GetInstance(_server, this.pnCanvas);
                new Thread(this._serverExchanger.ServerExchange).Start();
            }
            catch (SocketException sex)
            {
                Debug.WriteLine("Не удалось установить соединение с сервером.\n{0}", sex.Message);
                MessageBox.Show("Не удалось установить соединение с сервером", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (ServerExchangerException seex)
            {
                Debug.WriteLine("Не удалось установить соединение с сервером.\n{0}", seex.Message);
                MessageBox.Show("Не удалось установить соединение с сервером", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (ArgumentNullException anex)
            {
                Debug.WriteLine("Не удалось установить соединение с сервером.\n{0}", anex.Message);
                MessageBox.Show("Не удалось установить соединение с сервером", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (ThreadStartException tsex)
            {
                Debug.WriteLine("Не удалось установить соединение с сервером.\n{0}", tsex.Message);
                MessageBox.Show("Не удалось установить соединение с сервером", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (OutOfMemoryException omex)
            {
                Debug.WriteLine("Не удалось установить соединение с сервером.\n{0}", omex.Message);
                MessageBox.Show("Не удалось установить соединение с сервером", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Ошибка при установлении соединения с сервером.\n{0}", ex.Message);
                MessageBox.Show("Ошибка при установлении соединения с сервером", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}