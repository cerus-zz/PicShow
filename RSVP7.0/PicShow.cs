using System;
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
        System.Media.SoundPlayer musicplayer = new System.Media.SoundPlayer();     

        Thread countDown;                 // 倒计时线程
        Thread thr;                       // 用于显示图片播放声音和发送并口消息的线程        

        private delegate void showPic(int seq);  //定义一个代理用于子线程在主窗体中的控件中显示图片
        //private delegate void count(string num, Image bg_image, 
        //    Graphics my_graphics, Brush my_brush, Font my_font); //用于显示倒计时的数字
        private delegate void count(string num, SizeF amend, Color color, float fontsize); //用于显示倒计时的数字
        private delegate void clean();           //用于清空计数
        // 该事件，是让关闭该界面窗口的操作推迟到主界面（config）中，
        // 因为需要在主界面的实例中调用remove_Handler方法，避免事件残留！！！
        public event CloseEventHandler CloseHandler;
        private delegate void flowImage(Point location, Size size, int count);
        private delegate void remove_flowImage();
                    
        //并口操作
        [DllImport("DLPORTIO.dll", EntryPoint = "DlPortWritePortUshort", ExactSpelling = false, CharSet = CharSet.Unicode, SetLastError = true)]
        static extern void DlPortWritePortUshort(uint Port, ushort Value);

        # endregion Var Initialization
       
        public PicShow()
        {
            InitializeComponent();
           
            countDown = null;
            thr = null;
            Config.m_run = 0;     // 每次开始播放界面时，轮数总是从0开始
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
            //this.ClientSize = new Size(800, 800);
            this.FormBorderStyle = FormBorderStyle.None;   //无边框模式
            this.BackColor = System.Drawing.Color.Gray;

            //pictureBox1
            pictureBox1.Size = new Size(400, 400);
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
                this.Controls.RemoveByKey("flowimages");
                Graphics ghs = this.CreateGraphics();            
                ghs.Clear(this.BackColor);
                ghs.Dispose();
                pictureBox1.Visible = false;
                /*
                 * 开启新线程前，关闭旧线程
                 */
                closeThread();
                // 开启新线程，同时加载播放图像
                if ( null == thr && countDown == null)
                {   
                    countDown = new Thread(new ThreadStart(countRun));
                    countDown.Start();                                  
                }                
            }
            else if (e.KeyCode == Keys.R)
            {                
                this.Controls.RemoveByKey("flowimages");
                pictureBox1.Visible = false;
                Graphics tmp = this.CreateGraphics();
                tmp.Clear(this.BackColor);
                tmp.Dispose();
                drawCaption("开 始", new SizeF(0,0), Color.Cyan, 80);
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
            else if (e.KeyCode == Keys.Space)
            {
                this.Controls.RemoveByKey("flowimages");
                pictureBox1.Visible = false;
                Graphics ghs = this.CreateGraphics();
                ghs.Clear(this.BackColor);
                Image img;
                
                DirectoryInfo myFolder = new DirectoryInfo(Config.m_objInstanceLoc);
                DirectoryInfo[] tmpSubFile = myFolder.GetDirectories();
                FileInfo[] tmpPic = tmpSubFile[Config.m_evtlabel[Config.m_run]-1].GetFiles();   // 目标标签从1开始！寻找第多少子文件夹，索引从0开始

                // 显示caption和目标图像事例
                drawCaption("本轮 目标", new SizeF(0, -200), Color.BurlyWood, 30);          
                for (int i = 0; i < 2; ++i)
                {
                    if (".db" != tmpPic[i].Extension)
                    {
                        img = Image.FromFile(tmpPic[i].FullName);
                        ghs.DrawImage(img, new Rectangle(this.Size.Width/2 - (1-i)*200 - 10+i*20, this.Size.Height/2 - 200/2, 200, 200));
                    }

                }
                ghs.Dispose();
            }
            else if (e.KeyCode == Keys.J)
            {
                //Graphics ghs = this.CreateGraphics();
                //ghs.Clear(this.BackColor);
              
                //// handler似乎在触发它事件的线程里运行
                //// 如一个TcpSocket实例中触发事件，该handler运行，那么此时handler运行在TcpSocket的线程中！！！
                //// 而不是PicShow实例所处的线程中。
                //// 故当我创建一个临时控件，并试图添加到PicShow的实例中时，发生错误，因为临时控件在TcpSocket实例的线程中创建的。
                ////
                //// 因此，这个地方我使用了代理来完成在PicShow中用flowLayoutPanel显示结果图像的功能。
                //try
                //{
                //    flowImage flg = new flowImage(display);
                //    this.Invoke(flg, new object[] { new Point(this.Width/2 - 800/2, 150), new Size(800, 800), 20 });
                //}
                //catch (Exception ex)
                //{
                //    MessageBox.Show(ex.ToString());
                //}


                //// title
                //Font my_font = new Font("黑体", 40, FontStyle.Bold);
                //SizeF sz = ghs.MeasureString("EEG搜索图像的结果 >>>    准确率：", my_font);
                //drawCaption("EEG搜索图像的结果 >>>    准确率：", new SizeF(-(this.Width / 2 - sz.Width / 2) + 10, -(this.Height / 2 - sz.Height / 2) + 50), Color.BurlyWood, 40);

                //// target 
                //my_font = new Font("黑体", 30, FontStyle.Bold);
                //sz = ghs.MeasureString("本轮目标", my_font);
                //drawCaption("本轮目标", new SizeF(-(this.Width / 2 - sz.Width / 2) + 70, -(this.Height / 2 - sz.Height / 2) + 250), Color.Blue, 40);
                //Image img;
                //DirectoryInfo myFolder = new DirectoryInfo(Config.m_objInstanceLoc);
                //DirectoryInfo[] tmpSubFile = myFolder.GetDirectories();
                //FileInfo[] tmpPic = tmpSubFile[Config.m_evtlabel[Config.m_run] - 1].GetFiles();   // 目标标签从1开始！寻找第多少子文件夹，索引从0开始            
                //for (int i = 0; i < 2; ++i)
                //{
                //    if (".db" != tmpPic[i].Extension)
                //    {
                //        img = Image.FromFile(tmpPic[i].FullName);
                //        ghs.DrawImage(img, new Rectangle(20 +i*170, 270 + Convert.ToInt32(sz.Height), 150, 150));
                //    }

                //}
                //// roc
                //Pen anpen = new Pen(Color.Coral);
                //anpen.Width = 4;
                //Plot myplot = new Plot(ghs, new Point(this.Width - 350, 500), 300, anpen);
                //myplot.Plotaxis();
                //float[] x = { 1, 1, -1, 1, 1, 1, -1, -1, 1, -1, 1, -1, 1, -1, -1, -1, 1, -1, 1, -1 };
                //float[] y = { 0.9f, 0.8f, 0.7f, 0.6f, 0.55f, 0.54f, 0.53f, 0.52f, 0.51f, 0.505f, 0.4f, 0.39f, 0.38f, 0.37f, 0.36f, 0.35f, 0.34f, 0.33f, 0.30f, 0.1f };
                //PointF[] org = new PointF[20];
                //for (int i = 0; i < 20; ++i)
                //{

                //    org[i].X = x[i];
                //    org[i].Y = y[i];
                //}
                //float auc = myplot.PlotRoc(org, 20, 1);
             
                //sz = ghs.MeasureString("AUC = " + auc.ToString(), my_font);
                //drawCaption("AUC = " + auc.ToString(), new SizeF(new SizeF(-(this.Width / 2 - sz.Width / 2) + this.Width - 350, -(this.Height / 2 - sz.Height / 2) + 550)), Color.Coral, 30);              
                
                //ghs.Dispose();
            }
            else
                MessageBox.Show("开始：Enter；重置：R；退出：Q");

        }

        # endregion Control

        # region Generate random sequnce for display

        private void Loadimages(int runnum)
        {
            //MessageBox.Show(Config.m_evtlabel[runnum]);
            GetImage(Config.m_evtlabel[runnum]);

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
                rand = seed.Next(i, size - 1);
                tmp = arr[i];
                arr[i] = arr[rand];
                arr[rand] = tmp;
            }
        }
        //---------------------------------------------------------------------------

        # endregion

        #region delegation

        // 在窗体某个位置（中央）显示字符串
        private void drawCaption(string caption, SizeF amend, Color color, float fontsize)
        {
            // amend.Width     --> amend of x axis of location
            // amend.Height    --> amend of y axis of locationa
            // 默认是字符串在窗体的中间，如果想要改变位置，则需要修正amend

            Graphics my_graphics = this.CreateGraphics();
            Brush my_brush = new SolidBrush(color);
            Font my_font = new Font("黑体", fontsize, FontStyle.Bold);

            pictureBox1.Visible = false;
           // pictureBox1.Image = RSVP7._0.Properties.Resources.bg_gray;
           
             //获取字符串的宽度和长度
            SizeF sizef = my_graphics.MeasureString(caption, my_font);        
            my_graphics.DrawString(caption, my_font, my_brush,
                new RectangleF(this.Width / 2 - sizef.Width / 2 + amend.Width, this.Height / 2 - sizef.Height / 2 + amend.Height, sizef.Width, sizef.Height));
            my_graphics.Dispose();
            my_font.Dispose();
            my_brush.Dispose();

            if ("+" == caption)
            {
                pictureBox1.Visible = true;
            }
        }

        private void clnNum()
        {
            Graphics my_graphics = this.CreateGraphics();
            my_graphics.Clear(Color.Gray);
            my_graphics.Dispose();
        }        

        private void showPicture(int seq)
        {
            if (Config.m_auditory <= 0)
            {
                if (seq != 0)                                  //传入参数时图片数组下标已经增1了，故代表图片标号从1开始
                {
                    pictureBox1.Image = picMap[seq - 1];    //PicShow窗体pictureBox控件显示图片                 
                }
                else
                {
                    pictureBox1.Image = RSVP7._0.Properties.Resources.bg_gray;
                }
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
            count ct = new count(drawCaption);
            clean cln = new clean(clnNum);
            string str;
            //实验开始倒数
            for (int i = 5; i >= 1; i--)
            {
                str = i.ToString();
                this.Invoke(ct, new object[] { str, new SizeF(0,0), Color.Black, 150 });
                Thread.Sleep(1000);
                this.Invoke(cln, new object[] { });
            }

            if (thr == null)
            {
                if (Config.m_auditory != 0)
                    this.Invoke(ct, new object[] { "+", new SizeF(0,0), Color.Black, 150 });                
                //this.Invoke(cln, new object[] { });
                thr = new Thread(new ThreadStart(thrRun));
                thr.Start();
            }
        }

        // run() for showing our images
        private void thrRun()
        {
            if (Config.m_run >= Config.m_evtlabel.Length)
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
    
            // 获取播放图像 ---- 本来觉得放在这里符合逻辑，容易理解，但是生成随机顺序并加载图像的速度有点慢，所以前移到倒数线程开始之后！！！
            Loadimages(Config.m_run);
         
            //要初始和恢复变量
            loop = 0;
            int trialnum = Config.m_trialnum;

            // 发送标志255表示开始
            DlPortWritePortUshort(0x378, (ushort)(0));
            Thread.Sleep(1);
            DlPortWritePortUshort(0x378, (ushort)(255));
            Thread.Sleep(10);
            DlPortWritePortUshort(0x378, (ushort)(0));
            Thread.Sleep(190);            
            
            while (true)
            {
                //musicplayer.SoundLocation = Soundname[coo];     //原来每种语义只有一种声音
                //获取图片和声音播放的数组下标  

                //显示图片，播放声音
                //(委托出现问题了，没有找到原因，这里直接操做界面是可以的)
                //this.Invoke(sp, new object[] { loop });          
                pictureBox1.Image = picMap[loop];
                
                int label = Config.feedback[loop].label;
                // TODO: 发送并口消息 
                // 即将对应的标签发送给信号采集器。训练阶段标签有意义，测试阶段，标签可能没有意义
                DlPortWritePortUshort(0x378, (ushort)(0));
                Thread.Sleep(1);                                  // 该行直接删掉是不行的，否则后面写入并口的label无法显示，原因暂且不知                
                DlPortWritePortUshort(0x378, (ushort)(label));
                Thread.Sleep(10);
                DlPortWritePortUshort(0x378, (ushort)(0));


                // 图像显示一段时间，通过线程睡眠固定时间                
                // 每张图像显示完之后还有一段固定时间间歇，用于显示背景或者说不显示任何无效的图像
                Thread.Sleep(Config.m_durationT-11);          // 图片显示的时间，发送并口消息时已经睡了11ms,这里减去
                if (Config.m_tmbreak > 0)
                {
                    pictureBox1.Image = RSVP7._0.Properties.Resources.bg_gray; // 不显示任何图片，显示背景
                    Thread.Sleep(Config.m_tmbreak);
                }

                if (loop >= trialnum)                         // loop从1开始，故应先判断，后决定加一
                    break;
                else
                    ++loop;
            }

            // 发送标志253表示结束
            pictureBox1.Image = RSVP7._0.Properties.Resources.bg_gray;
            DlPortWritePortUshort(0x378, (ushort)(0));
            Thread.Sleep(1);
            DlPortWritePortUshort(0x378, (ushort)(253));
            Thread.Sleep(100);
            DlPortWritePortUshort(0x378, (ushort)(0));

            // 本轮实验完整结束，轮数加一
            Config.m_run++;                      
        }
        #endregion

        # region Record click behavior
        private void PicShow_MouseClick(object sender, MouseEventArgs e)
        {
            //这个并口消息会不会干扰显示图像同时发的并口消息？           
            DlPortWritePortUshort(0x378, (ushort)(0));
            Thread.Sleep(1);
            DlPortWritePortUshort(0x378, (ushort)(250));    //程序运行的PC上，LPT1并口资源为0378~037F和0778~077F
            //Thread.Sleep(10);
            //DlPortWritePortUshort(0x378, (ushort)(0));
            //MessageBox.Show("there is a object");
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
                client.cmdHandler += Client_A_Handler;
                client.cmdHandler += Client_B_Handler;
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
                client.cmdHandler -= Client_A_Handler;
                client.cmdHandler -= Client_B_Handler;
            }
        }        

        // command handler functions
        private void Tcp_A_Handler(Object sender, CommandEventArgs e)
        {
            if ('A' == e.command)
            {
                remove_flowImage rfi = new remove_flowImage(remove_display);
                this.Invoke(rfi, new object[] { });
                Graphics tmp = this.CreateGraphics();
                tmp.Clear(this.BackColor);
                tmp.Dispose();
                drawCaption("休 息", new SizeF(0,0), Color.DarkKhaki, 80);
            }
        }

        private void Tcp_B_Handler(Object sender, CommandEventArgs e)
        {
            if ('B' == e.command)
            {
                remove_flowImage rfi = new remove_flowImage(remove_display);
                this.Invoke(rfi, new object[] { });
                Graphics tmp = this.CreateGraphics();
                tmp.Clear(this.BackColor);
                tmp.Dispose();
                drawCaption("训练 结束", new SizeF(0, 0), Color.Chartreuse, 80);
            }
        }

        private void Tcp_C_Handler(Object sender, CommandEventArgs e)
        {
            if ('C' == e.command)
            {
                remove_flowImage rfi = new remove_flowImage(remove_display);
                this.Invoke(rfi, new object[] { });
                Graphics tmp = this.CreateGraphics();
                tmp.Clear(this.BackColor);
                tmp.Dispose();
                drawCaption("等待 结果", new SizeF(0,0), Color.Coral, 80);
            }
        }

        private void Tcp_S_Handler(Object sender, CommandEventArgs e)
        {
            if ('S' == e.command)
            {
                remove_flowImage rfi = new remove_flowImage(remove_display);
                this.Invoke(rfi, new object[] { });
                Graphics tmp = this.CreateGraphics();
                tmp.Clear(this.BackColor);
                tmp.Dispose();
                drawCaption("开 始", new SizeF(0,0), Color.Cyan, 80);
            }
        }

        private void Tcp_F_Handler(Object sender, CommandEventArgs e)
        {
            if ('F' == e.command)
            {
                Graphics ghs = this.CreateGraphics();
                ghs.Clear(this.BackColor);

                 //handler似乎在触发它事件的线程里运行
                 //如一个TcpSocket实例中触发事件，该handler运行，那么此时handler运行在TcpSocket的线程中！！！
                 //而不是PicShow实例所处的线程中。
                 //故当我创建一个临时控件，并试图添加到PicShow的实例中时，发生错误，因为临时控件在TcpSocket实例的线程中创建的。
                
                 //因此，这个地方我使用了代理来完成在PicShow中用flowLayoutPanel显示结果图像的功能。
                try
                {
                    flowImage flg = new flowImage(display);
                    this.Invoke(flg, new object[] { new Point(this.Width / 2 - 800 / 2, 150), new Size(800, 750), 20 });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }


                // title               
                // 这时统计结果都是与前一轮有关，轮数已经加一了，故下面要减一！！
                int objLabel = Config.m_evtlabel[Config.m_run - 1];

                float accuracy = 0;
                for (int i = 0; i < Config.m_trialnum; ++i)
                {
                    if ((Config.feedback[i].score > 0 && Config.feedback[i].label == objLabel) || (Config.feedback[i].score <= 0 && Config.feedback[i].label != objLabel))
                        accuracy += 1;
                }
                accuracy = 100.0f * accuracy / (float)Config.m_trialnum;
                Font my_font = new Font("黑体", 40, FontStyle.Bold);
                SizeF sz = ghs.MeasureString("EEG搜索图像的结果 >>>    准确率：" + accuracy.ToString() + "%", my_font);
                drawCaption("EEG搜索图像的结果 >>>    准确率：" + accuracy.ToString() + "%", new SizeF(-(this.Width / 2 - sz.Width / 2) + 10, -(this.Height / 2 - sz.Height / 2) + 50), Color.BurlyWood, 40);

                // target 
                my_font = new Font("黑体", 30, FontStyle.Bold);
                sz = ghs.MeasureString("本轮目标", my_font);
                drawCaption("本轮目标", new SizeF(-(this.Width / 2 - sz.Width / 2) + 70, -(this.Height / 2 - sz.Height / 2) + 250), Color.Blue, 40);
                Image img;
                DirectoryInfo myFolder = new DirectoryInfo(Config.m_objInstanceLoc);
                DirectoryInfo[] tmpSubFile = myFolder.GetDirectories();
                FileInfo[] tmpPic = tmpSubFile[objLabel - 1].GetFiles();
                for (int i = 0; i < 2; ++i)
                {
                    if (".db" != tmpPic[i].Extension)
                    {
                        img = Image.FromFile(tmpPic[i].FullName);
                        ghs.DrawImage(img, new Rectangle(20 + i * 170, 270 + Convert.ToInt32(sz.Height), 150, 150));
                    }

                }
                // roc
                Pen anpen = new Pen(Color.Coral);
                anpen.Width = 4;
                Plot myplot = new Plot(ghs, new Point(this.Width - 310, 500), 300, anpen);
                myplot.Plotaxis();
                float auc = myplot.PlotRoc(Config.feedback, e.number, objLabel);
                sz = ghs.MeasureString("AUC = " + auc.ToString(), my_font);
                drawCaption("AUC = " + auc.ToString(), new SizeF(new SizeF(-(this.Width / 2 - sz.Width / 2) + this.Width - 310, -(this.Height / 2 - sz.Height / 2) + 550)), Color.Coral, 30);

                ghs.Dispose();
            }
        }

        // 客户端接收到新的播放图像反馈，要求开始新的试验
        private void Client_A_Handler(Object sender, CommandEventArgs e)
        {
            if ('a' == e.command)
            {         
                try
                {
                    remove_flowImage rfi = new remove_flowImage(remove_display);
                    this.Invoke(rfi, new object[] { });
                    Graphics tmp = this.CreateGraphics();
                    tmp.Clear(this.BackColor);
                    tmp.Dispose();
                    // 显示继续新的搜索
                    Config.m_run -= 1;              // 需要重新搜索前一轮的目标，故轮数回拨！！！
                    count ct = new count(drawCaption);
                    this.Invoke(ct, new object[] {"重新 搜索", new SizeF(0, 0), Color.DarkSalmon, 80});                 
                }
                catch (Exception ee)
                {
                    MessageBox.Show(ee.ToString());
                }
                
            }
        }

        // 客户端接收到计算机从大图库搜索到的图像结果，进行显示
        private void Client_B_Handler(Object sender, CommandEventArgs e)
        {
            if ('b' == e.command)
            {
                remove_flowImage rfi = new remove_flowImage(remove_display);
                this.Invoke(rfi, new object[] {});
                Graphics ghs = this.CreateGraphics();
                ghs.Clear(this.BackColor);
                
                // 显示结果图片
                try
                {
                    flowImage flg = new flowImage(display);
                    this.Invoke(flg, new object[] { new Point(this.Width / 2 - 800 / 2, 150), new Size(800, 800), e.number });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }


                ghs.Dispose();               
            }
        }

        // 代理实现函数，展示EEG搜索结果图像
        private void display(Point location, Size size, int count)
        {
            FlowLayoutPanel fllp = new FlowLayoutPanel() { Name = "flowimages" };
            this.Controls.Add(fllp);     // 将fllp加入到当前的窗体PicShow（psw)中
            //fllp.Name = "flowimages";    // 用于从窗体中删除该控件时的key (this.Controls.RemoveByKey("flowimages");)以达到清屏的目的

            for (int i = 0; i < count; i++)
            {
                PictureBox tmp = new PictureBox();
                tmp.Size = new Size(150, 150);
                tmp.SizeMode = PictureBoxSizeMode.Zoom;
                tmp.Image = Image.FromFile(Config.feedback[i].imagepath);
                //tmp.Image = Image.FromFile("G:\\Face\\01\\39.JPG");                
                fllp.Controls.Add(tmp);
            }

            fllp.Size = size;
            fllp.Location = location;
            fllp.AutoScroll = true;
            fllp.FlowDirection = FlowDirection.LeftToRight;         
            fllp.Visible = true;            
        }

        // 代理实现函数，从PicShow的窗体中删除flowLayoutPanel的实例
        private void remove_display()
        {
            try
            {
                int index = this.Controls.IndexOfKey("flowimages");              
                if (-1 != index)
                {
                    this.Controls.RemoveByKey("flowimages");
                }
            }
            catch
            {
                MessageBox.Show("wrong with delete flowlayoutpanel");
            }
           
            Graphics ghs = this.CreateGraphics();
            ghs.Clear(this.BackColor);
            ghs.Dispose();
        }
        #endregion
    }
}
