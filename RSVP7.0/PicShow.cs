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
    public partial class PicShow : Form
    {
        # region Var Initialization
        int[] RandNum = new int[200];        //大小要求取决于一组图片的张数或者同一语义图片重复的次数
        int[] Sequence = new int[500];       //存储同一语义重复显示的随机顺序
        int[] OrderforSem = new int[500];      //定义同一语义图片内部出现的随机顺序
        int[] OrderforMusic = new int[500];    //定义同一语义声音内部出现的随机顺序       

        //全局变量声明或初始化
        int seq = 0, seq_m = 0, loop = 0, round = 0;  // seq为图片数组下标从0开始；loop为没组内循环计数(mod picNum)；round为组外循环计数（mod trialnum）
        int sead;
        System.Media.SoundPlayer musicplayer = new System.Media.SoundPlayer();
        Graphics m_tmpgr;                   //用于1000ms图片显示间隔，而图片显示500ms时，后500ms显示背景
        //

        Thread countDown;                 //倒计时线程
        Thread thr;                       //用于显示图片播放声音和发送并口消息的线程        

        private delegate void showPic(int seq);  //定义一个代理用于子线程在主窗体中的控件中显示图片
        private delegate void count(string num, Image bg_image, 
            Graphics my_graphics, Brush my_brush, Font my_font); //用于显示倒计时的数字
        private delegate void clean(Graphics my_graphics);           //用于清空计数

        //并口操作
        [DllImport("DLPORTIO.dll", EntryPoint = "DlPortWritePortUshort", ExactSpelling = false, CharSet = CharSet.Unicode, SetLastError = true)]
        static extern void DlPortWritePortUshort(uint Port, ushort Value);

        # endregion Var Initialization

        # region Generate random
        /*
         * 利用对随机数排序产生随机顺序
         */
        private void QuickSort(int[] Order, int bar, int inlen, int low, int high)
        {
            int t_low = low;
            int t_high = high;
            int piovtkey = RandNum[low];
            int imit = Order[bar * inlen + low];

            while (low < high)
            {
                while (low < high && piovtkey <= RandNum[high])
                    high--;
                if (low < high)
                {
                    RandNum[low] = RandNum[high];
                    Order[bar * inlen + low] = Order[bar * inlen + high];
                    low++;
                }
                while (low < high && piovtkey > RandNum[low])
                    low++;
                if (low < high)
                {
                    RandNum[high] = RandNum[low];
                    Order[bar * inlen + high] = Order[bar * inlen + low];
                    high--;
                }
            }
            RandNum[low] = piovtkey;
            Order[bar * inlen + low] = imit;
            piovtkey = low;

            if (t_low < piovtkey) QuickSort(Order, bar, inlen, t_low, piovtkey - 1);
            if (t_high > piovtkey) QuickSort(Order, bar, inlen, piovtkey + 1, t_high);
        }

        private void RanSeq(int bar, int inlen, int[] Order)  //对不同组的顺序数实现随机化，因此bar为每组界限，inlen指每组长度
        {
            Random seed = new Random(System.Guid.NewGuid().GetHashCode() + sead * 3456575);
            for (int i = 0; i < inlen; i++)
            {
                RandNum[i] = seed.Next(1, 100000);
                Order[bar * inlen + i] = i;     //顺序数
            }

            QuickSort(Order, bar, inlen, 0, inlen - 1);  //通过记录一组随机数的排序来打乱顺序数
        }
        private void RanSeq_group(int bar, int inlen, int trialnum, int[] Order)  //由于每个语义的每个图片都可能重复不止一次，trialnum为总共一个语义重复的次数
        {     
            if ((Config.m_evtlabel != 0) && (bar == (Config.m_evtlabel - 1) / Config.m_groups))
            {
                for (int i = 0; i < trialnum; ++i)
                    Order[bar * trialnum + i] = Config.m_evtlabel - 1;
            }
            else
            {
                if (trialnum < inlen)
                    trialnum = inlen;  //inlen指总共可循环的图片数，trialnum指实际需要循环次数，所以如果trialnum更小，则图片数是够的，没必要更少

                Random seed = new Random(System.Guid.NewGuid().GetHashCode() + sead * 3464575);
                int j;
                for (int i = 0; i < trialnum; ++i)
                {
                    j = i % inlen;
                    RandNum[i] = seed.Next(1, 100000);
                    Order[bar * trialnum + i] = bar * inlen + j;
                }

                QuickSort(Order, bar, trialnum, 0, trialnum - 1);
            }
        }
        //---------------------------------------------------------------------------

        # endregion Generate random

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
            this.ClientSize = new Size(ws_width,ws_height); //不知道this.Size与此有什么区别
            this.FormBorderStyle = FormBorderStyle.None;   //无边框模式
            this.BackColor = System.Drawing.Color.Gray;

            //pictureBox1
            pictureBox1.Size = new Size(400,400);
            pictureBox1.Location = new Point(ws_width/2-pictureBox1.Size.Width/2,ws_height/2-pictureBox1.Size.Height/2);            
            pictureBox1.BackColor = System.Drawing.Color.Gray;
            pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage; //图片显示方式，伸缩适应
           // pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
            //pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
           // pictureBox1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle; // pitureBox的边框显示出来
         
        }

        # region  Control
        private void PicShow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                /*
                 * 开始倒数并显示图片
                 */                
                if(thr != null)
                    MessageBox.Show("先按R终止之前的线程！");
                else if (countDown == null)
                {
                    countDown = new Thread(new ThreadStart(countRun));
                    countDown.Start();
                }                
            }
            else if (e.KeyCode == Keys.R)
            {
                /*
                 * 终止显示图片线程并重置                 
                 */
                if (countDown != null)
                {
                    try
                    {
                        //终止旧线程，开启新线程              
                        countDown.Abort();
                        countDown.Join();
                        countDown = null;   //stopped状态下地线程不能使用.Start()方法重启故置空，等待开启新线程                    
                    }
                    catch
                    {
                        MessageBox.Show("终止异常线程！");
                    }
                }
                if (thr != null)
                {
                    try
                    {
                        //终止旧线程，开启新线程              
                        thr.Abort();
                        thr.Join();
                        thr = null;   //stopped状态下地线程不能使用.Start()方法重启故置空，等待开启新线程                    
                    }
                    catch
                    {
                        MessageBox.Show("终止异常线程！");
                    }
                }
                pictureBox1.Image = null;

                //DlPortWritePortUshort(0x378, (ushort)(0));  //并口写0，防止重新开始时，上次的数据又被写入
            }
            else if (e.KeyCode == Keys.Q)
            {
                this.Close();
                Config.Isonshow = false;
            }
            else
                MessageBox.Show("开始：Enter；重置：R；退出：Q");

        }

        # endregion Control


        private void cdTime(string num, Image bg_image, 
            Graphics my_graphics, Brush my_brush, Font my_font)
        {
            //num = "+"; //如果要显示十字，就加这句好了
            pictureBox1.Image = bg_image;
            my_graphics.DrawString(num, my_font, my_brush, (pictureBox1.Size.Width / 2), (pictureBox1.Size.Height / 2 - my_font.Height/3));
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
                    pictureBox1.Image = Config.picMap[seq-1];    //PicShow窗体pictureBox控件显示图片                 
                else
                    pictureBox1.Image = RSVP7._0.Properties.Resources.bg_gray;
            }
            if (Config.m_auditory >= 0)
                musicplayer.Play();
            //else pictureBox1.Image = null;
        }

        # region CountDown & ImageShow
        private void countRun()
        {
            //初始化
            Image bg_image;
            Graphics my_graphics;
            Brush my_brush;
            Font my_font;

            bg_image = RSVP7._0.Properties.Resources.bg_gray;
            my_graphics = Graphics.FromImage(bg_image);
            m_tmpgr = my_graphics;
            my_brush = new SolidBrush(Color.Black);
            my_font = new Font("黑体", 150, FontStyle.Bold);

            count ct = new count(cdTime);
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

        private void thrRun()
        {
            try
            {
                //终止旧倒计时线程，开启新线程
                countDown.Abort();
                countDown.Join();
                countDown = null;
            }
            catch
            {
                MessageBox.Show("终止异常de线程！");
            }

            showPic sp = new showPic(showPicture);


            int label, sentinel, trialnum, bar;
            bar = 0;
            trialnum = Config.m_trialnum;
            sentinel = -1;
            sead = 0;
            while ((trialnum--) != 0)           //提前生成好图片显示次序
            {
                while (true)
                {
                    RanSeq(bar, Config.picNum, Sequence);
                    sead++;
                    if (Sequence[bar * Config.picNum] != sentinel)   //防止语义相同的图片连续显示，
                        break;
                }
                sentinel = Sequence[bar * Config.picNum + Config.picNum - 1];
                bar = (bar + 1) % Config.m_trialnum;
            }

            if(Config.m_auditory<=0)
            {
                trialnum = Config.picNum;
                bar = 0;
                while ((trialnum--) != 0)
                {

                    RanSeq_group(bar, Config.m_groups, Config.m_trialnum, OrderforSem);
                    sead++;

                    bar = (bar + 1) % Config.picNum;
                }
            }           
            
            //这里默认声音的数目和图片是一样的，否则要另外重新定义参数
            if(Config.m_auditory>=0)
            {
                trialnum = Config.picNum;
                bar = 0;
                while ((trialnum--) != 0)
                {

                    RanSeq_group(bar, Config.m_audi_groups, Config.m_trialnum, OrderforMusic);
                    sead++;

                    bar = (bar + 1) % Config.picNum;
                }                
            }

            //要初始和恢复变量
            round = loop = 0;
            trialnum = Config.m_trialnum;

            // 发送标志255表示开始
            //DlPortWritePortUshort(0x378, (ushort)(0));
            //Thread.Sleep(1);
            //DlPortWritePortUshort(0x378, (ushort)(255));
            //Thread.Sleep(20);

            while (true)
            {
                //seq = Sequence[round*picNum+loop] + trial*picNum;
                int coo = Sequence[round * Config.picNum + loop];
                //musicplayer.SoundLocation = Soundname[coo];     //原来每种语义只有一种声音
                //获取图片和声音播放的数组下标
                if (Config.m_auditory <= 0)
                {
                    seq = OrderforSem[(Config.m_trialnum > Config.m_groups ? Config.m_trialnum : Config.m_groups) * coo + round];
                    Config.feedback[round * Config.picNum + loop].seq = seq;
                }
                if (Config.m_auditory >= 0)
                {
                    seq_m = OrderforMusic[(Config.m_trialnum > Config.m_audi_groups ? Config.m_trialnum : Config.m_audi_groups) * coo + round];
                    musicplayer.SoundLocation = Config.Soundname[seq_m];
                }

                //显示图片，播放声音
                this.Invoke(sp, new object[] { seq+1 });                       

                //TODO: 发送并口消息   
                if (Config.m_auditory <= 0)
                {
                    label = (int)(seq / Config.m_groups) + 1;                     // 将图片位置下标转换为图片的目标标签号，从1开始
                    //label = seq + 1;
                }
                else
                    label = (int)(seq_m / Config.m_audi_groups) + 1;

                //DlPortWritePortUshort(0x378, (ushort)(0));
                //Thread.Sleep(1);                                  //该行直接删掉是不行的，否则后面写入并口的label无法显示，原因暂且不知                
                //DlPortWritePortUshort(0x378, (ushort)(label));
                //Thread.Sleep(10);
                //DlPortWritePortUshort(0x378, (ushort)(0));

                if (loop != (Config.picNum - 1))
                    Thread.Sleep(Config.m_durationT-1);         //图片显示的时间，发送并口消息时已经睡了1ms,这里减去

                else if ((Config.picNum - 1) == loop)
                {
                    trialnum--;
                    round = (round + 1) % Config.m_trialnum;

                    if (0 == trialnum)
                    {
                        //MessageBox.Show("Over!");
                        break;
                    }
                    else
                        Thread.Sleep(Config.m_interval-1);
                }
                /*
                 *纯听觉刺激被改成了1000ms,刺激是500ms
                 *因此视觉刺激这里，图片显示500ms后，也要显示背景500ms
                 *
                 * 如果要改掉这点，可以先删掉下面这段代码，然后再将showPicture()
                 * 中的if(seq!=0) else 代码段给去掉，其他地方都不用变                
                 */
                if (Config.m_auditory <= 0)
                {
                    this.Invoke(sp, new object[] { 0 });
                    Thread.Sleep(500);
                }
                //---------------------------------------------------------

                loop = (loop + 1) % Config.picNum;
            }

            // 发送标志253表示结束
            //DlPortWritePortUshort(0x378, (ushort)(0));
            //Thread.Sleep(250);                
            //DlPortWritePortUshort(0x378, (ushort)(253));
        }
        # endregion CountDown & ImageShow

        # region Record click behavior
        private void PicShow_MouseClick(object sender, MouseEventArgs e)
        {
            //这个并口消息会不会干扰显示图像同时发的并口消息？           
            //DlPortWritePortUshort(0x378, (ushort)(0));
            //Thread.Sleep(1);                                   
            //DlPortWritePortUshort(0x378, (ushort)(250));    //程序运行的PC上，LPT1并口资源为0378~037F和0778~077F
            //Thread.Sleep(10);
            //DlPortWritePortUshort(0x378, (ushort)(0));
            //MessageBox.Show("there is a object");
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            //DlPortWritePortUshort(0x378, (ushort)(0));
            //Thread.Sleep(1);
            //DlPortWritePortUshort(0x378, (ushort)(250));    //程序运行的PC上，LPT1并口资源为0378~037F和0778~077F
        }//end thrRun;
        # endregion Record click behavior

    }
}
