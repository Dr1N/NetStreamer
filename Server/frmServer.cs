using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Server
{
    public partial class frmServer : Form
    {
        private BufferedGraphics _bg;
        private ScreenProcessor _screenProcessor = null;
        private ClientProcessor _clientProcessor = null;

        public frmServer()
        {
            InitializeComponent();
            this._screenProcessor = ScreenProcessor.Instance;
            IPAddress ip = IPAddress.Parse("192.168.1.100");
            this._clientProcessor = ClientProcessor.GetInstance(ip, 21999, this._screenProcessor);
            using (Graphics g = this.pnScreen.CreateGraphics())
            {
                this._bg = BufferedGraphicsManager.Current.Allocate(g, new Rectangle(0, 0, this.pnScreen.Width, this.pnScreen.Height));
            }
            
        }

        protected override void OnShown(EventArgs e)
        {
            this._screenProcessor.Start();
            this._clientProcessor.Start();
            base.OnShown(e);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            
        }

        private void button2_Click(object sender, EventArgs e)
        {

        }

        private void button3_Click(object sender, EventArgs e)
        {
            Action d = delegate () {
                while (true)
                {
                    this._bg.Graphics.Clear(Color.White);
                    using (Image img = _screenProcessor.CurrentScreenImage)
                    {
                        this._bg.Graphics.DrawImage(img, new Rectangle(0, 0, this.pnScreen.Width, this.pnScreen.Height));
                    }
                    using (Graphics g = this.pnScreen.CreateGraphics())
                    {
                        this._bg.Render(g);
                    }
                }
            };

            Task.Run(new Action(d));
        }
    }
}