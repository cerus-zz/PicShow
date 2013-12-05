﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.IO;
using System.Runtime.InteropServices; //并口操作需要调用API函数

namespace RSVP7._0
{
    public delegate void CloseEventHandler(Object sender, EventArgs e);

    public partial class PicShow : Form
    {
        # region Var Initialization      

        //全局变量声明或初始化
        Image[] picMap = new Image[300];  //用于存储要显示的图片
        int loop = 0;  // seq为图片数组下标从0开始；loop为没组内循环计数(mod picNum)；round为组外循环计数（mod trialnum）
        static int run = 0;
        System.Media.SoundPlayer musicplayer = new System.Media.SoundPlayer();     

        Thread countDown;                 // 倒计时线程
        Thread thr;                       // 用于显示图片播放声音和发送并口消息的线程        

        private delegate void showPic(int seq);  //定义一个代理用于子线程在主窗体中的控件中显示图片
        private delegate void count(string num, Image bg_image, 
            Graphics my_graphics, Brush my_brush, Font my_font); //用于显示倒计时的数字
        private delegate void clean(Graphics my_graphics);           //用于清空计数
        // 该事件，是让关闭该界面窗口的操作推迟到主界面（config）中，
        // 因为需要在主界面的实例中调用remove_Handler方法，避免事件残留！！！
        public event CloseEventHandler CloseHandler;

        //并口操作
        [DllImport("DLPORTIO.dll", EntryPoint = "DlPortWritePortUshort", ExactSpelling = false, CharSet = CharSet.Unicode, SetLastError = true)]
        static extern void DlPortWritePortUshort(uint Port, ushort Value);

        # endregion Var Initialization

        # region Generate random sequnce for display

        private void Loadimages(int runnum)
        {
            MessageBox.Show(Config.m_evtlabel[runnum]);
            GetImage(Convert.ToInt32(Config.m_evtlabel[runnum]));
            
            // 利用对随机数排序乱序播放次序
            disorder(Config.feedback, Config.m_trialnum);

            for (int i = 0; i < Config.m_trialnum; ++i)
            {
                picMap[i] = Image.FromFile(Config.feedback[i].imagepath);
            }
        }

        // 获取给定目标的播放图像子集
        private void GetImage(int targetLabel)
        {
            
            List<Foo> tmpTarPic = new List<Foo>();//存放目标图片信息
            List<Foo> tmpnTarPic = new List<Foo>();//存放非目标图片信息
            int index = 0;
            //遍历每一张图片，将图片分为目标图片和非目标图片，分别存在tmpTarPic和tmpnTarPic
            foreach (Foo theFoo in Config.allPic)
            {
                if (theFoo.label == targetLabel)
                    tmpTarPic.Add(theFoo);
                else
                    tmpnTarPic.Add(theFoo);
            }

            //罗列出目标图片
            int[] tmpTarLabel = new int[Config.m_targetnum];
            tmpTarLabel = myRan(tmpTarPic.Count, tmpTarPic.Count - 1, 0, Config.m_targetnum);
            foreach (int eTarLabel in tmpTarLabel)
            {
                Config.feedback[index++] = tmpTarPic[eTarLabel];
            }
            
            //罗列出非目标图片，放入nTargetPic
            int[] tmpnTarLabel = new int[Config.m_trialnum - Config.m_targetnum];
            tmpnTarLabel = myRan(tmpnTarPic.Count, tmpnTarPic.Count - 1, 0, Config.m_trialnum - Config.m_targetnum);
            foreach (int enTarLabel in tmpnTarLabel)
            {
                Config.feedback[index++] = tmpnTarPic[enTarLabel];
            }
             
        }

        //产生随机数
        private int[] myRan(int inNum, int upLimit, int lowLimit, int outNum)
        {
            int[] index = new int[inNum];
            int[] result = new int[outNum];

            for (int i = 0; i < inNum; i++)
                index[i] = i;

            Random ran = new Random();

            int site = inNum;
            int id;

            for (int j = 0; j < outNum; j++)
            {
                id = ran.Next(0, site - 1);                
                result[j] = index[id];
                index[id] = index[site - 1];
                site--;
            }

            return result;
        }

        // 乱序
        private void disorder(Foo[] arr, int size)
        {
            Random seed = new Random(System.Guid.NewGuid().GetHashCode() + 1 * 3456575);
            Foo tmp = new Foo();
            int rand = 0;
            for (int i = 0; i < size; ++i)
            {
                rand = seed.Next(i, size-1);
                tmp = arr[i];
                arr[i] = arr[rand];
                arr[rand] = tmp;
            }
        }
        //---------------------------------------------------------------------------

        # endregion

        public PicShow()
        {
            InitializeComponent();
           
            countDown = null;
            thr = null;           
        }

        private void PicShow_Load(object sender, EventArgs e)
        {
            /*
             * 获得屏幕像素大小，另一种方法：
             * Screen myscreen =Screen.PrimaryScreen;
             * ws_width = myscreen.Bounds.Width;
             * ws_height = myscreen.Bounds.Height;
             */
            int ws_width = Screen.GetWorkingArea(this).Width;
            int ws_height = Screen.GetWorkingArea(this).Height;
            this.StartPosition = FormStartPosition.Manual;
            this.Location = new Point(0, 0);
            this.ClientSize = new Size(ws_width, ws_height); //不知道this.Size与此有什么区别
            this.FormBorderStyle = FormBorderStyle.None;   //无边框模式
            this.BackColor = System.Drawing.Color.Gray;

            //pictureBox1
            pictureBox1.Size = new Size(200, 200);
            pictureBox1.Location = new Point(ws_width / 2 - pictureBox1.Size.Width / 2, ws_height / 2 - pictureBox1.Size.Height / 2);
            pictureBox1.BackColor = System.Drawing.Color.Gray;
            pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom; //图片显示方式，伸缩适应        
           //pictureBox1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle; // pitureBox的边框显示出来         
         
        }

        
        # region  Control
        private void closeThread()
        {
            // 终止旧线程
            try
            {
                if (null != countDown)
                {
                    countDown.Abort();
                    countDown.Join();
                    countDown = null;   //stopped状态下地线程不能使用.Start()方法重启故置空，等待开启新线程   
                }
            }
            catch
            {
                MessageBox.Show("终止异常线程！");
            }
            try
            {
                if (null != thr)
                {
                    thr.Abort();
                    thr.Join();
                    thr = null;   //stopped状态下地线程不能使用.Start()方法重启故置空，等待开启新线程  
                }
            }
            catch
            {
                MessageBox.Show("终止异常线程！");
            }
        }

        private void PicShow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {               
                Graphics ghs = this.CreateGraphics();
                ghs.Clear(this.BackColor);
                ghs.Dispose();
                pictureBox1.Visible = false;
                /*
                 * 开启新线程前，关闭旧线程
                 */
                closeThread();
                // 开启新线程
                if ( null == thr && countDown == null)
                {
                    countDown = new Thread(new ThreadStart(countRun));
                    countDown.Start();
                }                
            }
            else if (e.KeyCode == Keys.R)
            {                
                pictureBox1.Visible = false;
                drawCaption("开 始", new Rectangle(this.Size.Width / 2, 3, this.Size.Height / 2, 0), Color.YellowGreen);
                /*
                 * 终止显示图片线程并重置                 
                 */
                closeThread();
                pictureBox1.Image = null;

                // 如果是以外中断，那么该标签将会有用
                DlPortWritePortUshort(0x378, (ushort)(0));
                Thread.Sleep(1);
                DlPortWritePortUshort(0x378, (ushort)(252));    //程序运行的PC上，LPT1并口资源为0378~037F和0778~077F
                Thread.Sleep(10);
                DlPortWritePortUshort(0x378, (ushort)(0));
            }
            else if (e.KeyCode == Keys.Q)
            {
                closeThread();
                CloseHandler(this, new EventArgs());
            }
            else
                MessageBox.Show("开始：Enter；重置：R；退出：Q");

        }

        # endregion Control


        #region delegation

        private void drawNumber(string num, Image bg_image, 
            Graphics my_graphics, Brush my_brush, Font my_font)
        {
            //num = "+"; //如果要显示十字，就加这句好了
            //pictureBox1.Image = bg_image;
            my_graphics.DrawString(num, my_font, my_brush, this.Size.Width/2 - my_font.Height/3, this.Size.Height/2 - my_font.Height/2);
            if ("+" == num)
            {
                pictureBox1.Visible = true;
            }
        }

        private void clnNum(Graphics my_graphics)
        {
            my_graphics.Clear(Color.Gray);
        }        

        private void showPicture(int seq)
        {
            if (Config.m_auditory <= 0)
            {
                if (seq != 0)                                  //传入参数时图片数组下标已经增1了，故代表图片标号从1开始
                    pictureBox1.Image = picMap[seq-1];    //PicShow窗体pictureBox控件显示图片                 
                else
                    pictureBox1.Image = RSVP7._0.Properties.Resources.bg_gray;
            }
            if (Config.m_auditory >= 0)
                musicplayer.Play();
            //else pictureBox1.Image = null;
        }

        #endregion 

        # region CountDown & ImageShow
        // run() for countdown
        private void countRun()
        {
            //初始化
            Image bg_image;
            Graphics my_graphics;
            Brush my_brush;
            Font my_font;

            bg_image = RSVP7._0.Properties.Resources.bg_gray;
            my_graphics = this.CreateGraphics();
            my_brush = new SolidBrush(Color.Black);
            my_font = new Font("黑体", 150, FontStyle.Bold);

            count ct = new count(drawNumber);
            clean cln = new clean(clnNum);
            string str;
            //实验开始倒数
            for (int i = 5; i >= 1; i--)
            {
                str = i.ToString();
                this.Invoke(ct, new object[] { str, bg_image, my_graphics, my_brush, my_font });
                Thread.Sleep(1000);
                this.Invoke(cln, new object[] { my_graphics });
            }

            if (thr == null)
            {
                if (Config.m_auditory != 0)
                    this.Invoke(ct, new object[] { "+", bg_image, my_graphics, my_brush, my_font });
                thr = new Thread(new ThreadStart(thrRun));
                thr.Start();
            }
        }

        // run() for showing our images
        private void thrRun()
        {
            if (run >= Config.m_evtlabel.Length)
            {
                MessageBox.Show("no more RUN, Objects should be UPdated!");
                return;
            }
            try
            {
                // 终止旧倒计时线程，开启新线程
                countDown.Abort();
                countDown.Join();
                countDown = null;
            }
            catch
            {
                MessageBox.Show("终止异常de线程！");
            }

            showPic sp = new showPic(showPicture);           
    
            // 获取播放图像
            Loadimages(run);

            //要初始和恢复变量
            loop = 1;
            int trialnum = Config.m_trialnum;

            // 发送标志255表示开始
            //DlPortWritePortUshort(0x378, (ushort)(0));
            //Thread.Sleep(1);
            //DlPortWritePortUshort(0x378, (ushort)(255));
            //Thread.Sleep(10);
            //DlPortWritePortUshort(0x378, (ushort)(0));
            //Thread.Sleep(190);            
            
            while (true)
            {
                //musicplayer.SoundLocation = Soundname[coo];     //原来每种语义只有一种声音
                //获取图片和声音播放的数组下标  

                //显示图片，播放声音
                this.Invoke(sp, new object[] { loop });                       

                // TODO: 发送并口消息 
                // 即将对应的标签发送给信号采集器。训练阶段标签有意义，测试阶段，标签可能没有意义
                //DlPortWritePortUshort(0x378, (ushort)(0));
                //Thread.Sleep(1);                                  // 该行直接删掉是不行的，否则后面写入并口的label无法显示，原因暂且不知                
                //DlPortWritePortUshort(0x378, (ushort)(Config.feedback[loop].label));
                //Thread.Sleep(10);
                //DlPortWritePortUshort(0x378, (ushort)(0));

                // 图像显示一段时间，通过线程睡眠固定时间                
                // 每张图像显示完之后还有一段固定时间间歇，用于显示背景或者说不显示任何无效的图像
                Thread.Sleep(Config.m_durationT-11);          // 图片显示的时间，发送并口消息时已经睡了11ms,这里减去
                if (Config.m_tmbreak > 0)
                {
                    this.Invoke(sp, new object[] { 0 });
                    Thread.Sleep(Config.m_tmbreak);
                }

                if (loop >= trialnum)                         // loop从1开始，故应先判断，后决定加一
                    break;
                else
                    ++loop;
            }

            // 发送标志253表示结束
            this.Invoke(sp, new object[] { 0 });
            //DlPortWritePortUshort(0x378, (ushort)(0));
            //Thread.Sleep(1);
            //DlPortWritePortUshort(0x378, (ushort)(253));
            //Thread.Sleep(100);
            //DlPortWritePortUshort(0x378, (ushort)(0));
            run++;
        }
        #endregion

        # region Record click behavior
        private void PicShow_MouseClick(object sender, MouseEventArgs e)
        {
            //这个并口消息会不会干扰显示图像同时发的并口消息？           
            DlPortWritePortUshort(0x378, (ushort)(0));
            Thread.Sleep(1);
            DlPortWritePortUshort(0x378, (ushort)(250));    //程序运行的PC上，LPT1并口资源为0378~037F和0778~077F
            Thread.Sleep(10);
            DlPortWritePortUshort(0x378, (ushort)(0));
            MessageBox.Show("there is a object");
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            DlPortWritePortUshort(0x378, (ushort)(0));
            Thread.Sleep(1);
            DlPortWritePortUshort(0x378, (ushort)(250));    //程序运行的PC上，LPT1并口资源为0378~037F和0778~077F
        }//end thrRun;
        # endregion Record click behavior

        #region TcpCommand Handler

        // add command handler to a certian socket
        public void add_Handler(TcpSocket socket, TcpClient client)
        {
            if (null != socket)
            {
                socket.CommandHandler += Tcp_A_Handler;
                socket.CommandHandler += Tcp_B_Handler;
                socket.CommandHandler += Tcp_C_Handler;
                socket.CommandHandler += Tcp_S_Handler;
                socket.CommandHandler += Tcp_F_Handler;              
            }
            if (null != client)
            {
                client.CommandHandler += Client_A_Handler;
                client.CommandHandler += Client_B_Handler;
            }
        }

        // 删除委托是必要的，因为，即便窗口关闭了，委托依然会响应事件！！！
        public void remove_Handler(TcpSocket socket, TcpClient client)
        {
            if (null != socket)
            {
                socket.CommandHandler -= Tcp_A_Handler;
                socket.CommandHandler -= Tcp_B_Handler;
                socket.CommandHandler -= Tcp_C_Handler;
                socket.CommandHandler -= Tcp_S_Handler;
                socket.CommandHandler -= Tcp_F_Handler;
            }
            if (null != client)
            {
                client.CommandHandler -= Client_A_Handler;
                client.CommandHandler -= Client_B_Handler;
            }
        }

        private void drawCaption(string caption, Rectangle rect, Color color)
        {
            Graphics my_graphics = this.CreateGraphics();
            Brush my_brush = new SolidBrush(color);
            Font my_font = new Font("黑体", 80, FontStyle.Bold);

            pictureBox1.Visible = false;
            my_graphics.Clear(this.BackColor);
            //Rectangle rect = new Rectangle(pictureBox1.Location.X + 150, this.Height / 2 - my_font.Height, pictureBox1.Width, my_font.Height);
            my_graphics.DrawString(caption, my_font, my_brush, 
                new Rectangle(rect.X-(my_font.Height * rect.Y/2), rect.Width-my_font.Height/2,rect.Y * my_font.Height, my_font.Height));
            my_graphics.Dispose();
            my_font.Dispose();
            my_brush.Dispose();
        }

        // command handler functions
        private void Tcp_A_Handler(Object sender, CommandEventArgs e)
        {
            if ('A' == e.command)
            {                
                drawCaption("休 息", new Rectangle(this.Size.Width / 2, 3, this.Size.Height / 2, 0),Color.Green);
            }
        }

        private void Tcp_B_Handler(Object sender, CommandEventArgs e)
        {
            if ('B' == e.command)
            {                
                drawCaption("训练 结束", new Rectangle(this.Size.Width/2, 5, this.Size.Height/2, 0), Color.Yellow);
            }
        }

        private void Tcp_C_Handler(Object sender, CommandEventArgs e)
        {
            if ('C' == e.command)
            {                
                drawCaption("等待 结果", new Rectangle(this.Size.Width / 2, 5, this.Size.Height / 2, 0), Color.Violet);
            }
        }

        private void Tcp_S_Handler(Object sender, CommandEventArgs e)
        {
            if ('S' == e.command)
            {                
                drawCaption("开 始", new Rectangle(this.Size.Width / 2, 3, this.Size.Height / 2, 0), Color.YellowGreen);
            }
        }

        private void Tcp_F_Handler(Object sender, CommandEventArgs e)
        {
            if ('F' == e.command)
            {
                // 显示结果图片
                Graphics ghs = this.CreateGraphics();
                ghs.Clear(this.BackColor);
                int h_margin = 0;
                if (this.Size.Height > 100)
                {
                    h_margin = (this.Size.Height - 1000) / 2;   // 5行， 每行高200
                }
                int index = 0;
                Image img;
                for (int i = 0; i < 5; ++i)
                {
                    for (int j = 0; j < 4; ++j)
                    {
                        img = Image.FromFile(Config.feedback[index++].imagepath);
                        ghs.DrawImage(img, new Rectangle(this.Size.Width/2-(2-j)*200, h_margin + i*200, 200, 200));
                    }
                }
                ghs.Dispose();
                
            }
        }

        // 客户端接收到新的播放图像反馈，要求开始新的试验
        private void Client_A_Handler(Object sender, CommandEventArgs e)
        {
            if ('A' == e.command)
            {
                // 显示继续新的搜索
                drawCaption("重新搜索", new Rectangle(this.Size.Width / 2, 3, this.Size.Height / 2, 0), Color.YellowGreen);
            }
        }

        // 客户端接收到计算机从大图库搜索到的图像结果，进行显示
        private void Client_B_Handler(Object sender, CommandEventArgs e)
        {
            if ('B' == e.command)
            {
                // 显示结果图片
                Graphics ghs = this.CreateGraphics();
                ghs.Clear(this.BackColor);
                int h_margin = 0;
                if (this.Size.Height > 100)
                {
                    h_margin = (this.Size.Height - 1000) / 2;   // 5行， 每行高200
                }
                int index = 0;
                Image img;
                for (int i = 0; i < 5; ++i)
                {
                    for (int j = 0; j < 4; ++j)
                    {
                        img = Image.FromFile(Config.feedback[index++].imagepath);
                        ghs.DrawImage(img, new Rectangle(this.Size.Width / 2 - (2 - j) * 200, h_margin + i * 200, 200, 200));
                    }
                }
                ghs.Dispose();
            }
        }
        #endregion
    }
}
