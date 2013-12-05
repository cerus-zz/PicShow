using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;

namespace RSVP7._0
{
    public partial class Config : Form
    {

        /*
         * 声明参数变量
         */
        public static int m_durationT;     // 每幅图片持续的时间
        public static int m_trialnum;      // 每次run的图像数
        public static int m_tmbreak;      // 每组图片显示之间的时间间隔
        public static int m_groups;        // 语义组数,其实就是单个语义的图片个数
        public static String m_evtlabel;      // 目标语义特定图片的标签，用于对比实验，"0"表示不使用
        public static int m_auditory;      // 是否添加听觉刺激以及是否单独听觉刺激;为1(>0)时为单独听觉刺激，为0时，视觉+听觉刺激;为-1(<0)时无听觉刺激
        public static int m_audi_groups;   // 单个语义的声音文件个数
                            
        public static int picNum;          //一组中应包含的图片数，其实就是包含的不同的语义数
        TcpSocket myServer = null;
        TcpClient myClient = null;
        public static string ip;
        public static int port;
        PicShow psw = null;
        public static Image[] picMap = new Image[500];  //用于存储要显示的图片
        public static string[] Soundname = new string[500];    //取决于语义的种类
        public struct Foo
        {

            public String imagepath { get; set; }             // 图像路径
            public double score { get; set; }                // 图像获得的分值，可能是距离或者其他标准
            public int label { get; set; }
        };
        public static Foo[] feedback = new Foo[500];

       // public static bool Isonshow = false;       // 显示图片的界面是否存在，用于socket线程反应前作判断
        bool IschooseFolder = false;
        bool IschooseFolder_audio = false;
        int picAmount = 0;//图片总数
        int audioAmount = 0; //声音数

        public Config()
        {
            InitializeComponent();
        }

        private void Config_Load(object sender, EventArgs e)
        {
            /*
             * 设置默认输入，避免空输入的异常
             */

            textBox1.Text = "500";
            textBox2.Text = "200";
            textBox3.Text = "500";
            textBox4.Text = "15";
            textBox5.Text = "0";
            textBox7.Text = "-1";
            textBox8.Text = "20";
            this.Text = "Display";     //窗体的Title
            Tbox_ip.Text  = "10.14.86.174";
            Tbox_port.Text= "10086";
            Tbox_ip_client.Text = "10.14.86.174";
            Tbox_port_client.Text = "1050";
        }

        #region Get Parameters
        private bool GetParam()
        {
            try
            {
                //获取单个图片显示时间
                m_durationT = int.Parse(textBox1.Text.ToString());                  
            }
            catch
            {
                MessageBox.Show("Duaration不能为空！");
                return false;
            }
            try
            {
                //获取循环轮数
                m_trialnum = int.Parse(textBox2.Text.ToString());                  
            
            }
            catch
            {
                MessageBox.Show("TrialNum不能为空！");
                return false;
            }
            try
            {
                //获取每张图片显示后的间歇时间
                m_tmbreak = int.Parse(textBox3.Text.ToString());                  
            }
            catch
            {
                MessageBox.Show("Interval不能为空！");
                return false;
            }
            try
            {
                //图片组数
                m_groups = int.Parse(textBox4.Text.ToString());
                
                if (IschooseFolder)
                {
                    if (0 == m_groups)
                    {
                        MessageBox.Show("Groups不能为0！");
                        return false;
                    }
                    else
                        picNum = picAmount / m_groups;
                }
            }
            catch
            {
                MessageBox.Show("Groups不能为空！");
                return false;
            }
            try
            {
                m_evtlabel = textBox5.Text.ToString();
            }
            catch
            {
                MessageBox.Show("Label 不能为空！");
                return false;
            }
            try
            {
                m_auditory = int.Parse(textBox7.Text.ToString());
            }
            catch
            {
                MessageBox.Show("Auditory 不能为空！");
                return false;
            }
            try
            {
                m_audi_groups = int.Parse(textBox8.Text.ToString());
                if(IschooseFolder_audio)
                {
                    if (m_audi_groups == 0)
                    {
                        MessageBox.Show("Audi-Groups不能为0！");
                        return false;
                    }
                    if(!IschooseFolder)
                        picNum = audioAmount / m_audi_groups;
                }               
            }
            catch
            {
                MessageBox.Show("Audi-Groups不能为空！");
                return false;
            }

            return true;
        }

        #endregion

        #region Action on Click

        private void button1_Click(object sender, EventArgs e)
        {
            /*
             * 添加图片
             */
            FolderDialog myfDialog = new FolderDialog();
            myfDialog.DisplayDialog();

            textBox6.Text = myfDialog.Path;
            if (textBox6.Text!="")
            {
                DirectoryInfo theFolder = new DirectoryInfo(myfDialog.Path);
                picAmount = 0;
                //遍历文件夹并将图片加入数组
                foreach (FileInfo NextFile in theFolder.GetFiles())
                {
                    //windowsXP图片文件中以缩略图浏览后，系统自动在该文件中生成Thumbs.db（缓存Windows Explorer的缩略图的文件）
                    if (NextFile.Name.ToString() == "Thumbs.db")
                        continue;
                    try
                    {
                        picMap[picAmount++] = Image.FromFile(NextFile.FullName.ToString());
                    }
                    catch
                    {
                        MessageBox.Show(picAmount.ToString()+" "+NextFile.FullName.ToString());
                    }
                }
                IschooseFolder = true;
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (GetParam())
            {
                if (m_auditory <= 0 && !IschooseFolder)      //picshow only or show with audio
                {
                    MessageBox.Show("先选择图片文件！");
                    return;
                }
                if (m_auditory >= 0 && !IschooseFolder_audio)
                {
                    MessageBox.Show("先选择声音文件！");
                    return;
                }

                psw = new PicShow();
                psw.Show();
                psw.CloseHandler += CloseWinform;
                psw.add_Handler(myServer, myClient);
            }                        
        }

        private void Duration(object sender, EventArgs e)
        {
           /*
            * 无用代码
            */
        }

        private void button3_Click(object sender, EventArgs e)
        {
            /*
             * add location of audio files
             */

            FolderDialog myfolderDlg = new FolderDialog();
            myfolderDlg.DisplayDialog();

            textBox9.Text = myfolderDlg.Path;
            if (textBox9.Text!="")
            {
                DirectoryInfo myfolder = new DirectoryInfo(myfolderDlg.Path);
                audioAmount = 0;
                //遍历声音文件夹，添加文件的名称                     
                foreach (FileInfo NextFile in myfolder.GetFiles())
                    Soundname[audioAmount++] = NextFile.FullName.ToString();

                IschooseFolder_audio = true;
            }    
        }

        // 打开服务器监听
        private void Btn_startServer_Click(object sender, EventArgs e)
        {
            if (null == myServer)
            {
                ip = Tbox_ip.Text;
                port = System.Int32.Parse(Tbox_port.Text);
                myServer = new TcpSocket(ip, port);
                myServer.startHost();
                Btn_startServer.Text = "Stop Server";
                myServer.get_Handler(psw);
            }
            else
            {
                myServer.endHost();
                myServer = null;
                Btn_startServer.Text = "Start Server";
            }
        }


        // 客户端连接到服务器，用于机器从大图库中搜取目标
        private void Btn_cntToclient_Click(object sender, EventArgs e)
        {
            if (null == myClient)
            {
                ip = Tbox_ip_client.Text;
                port = Int32.Parse(Tbox_port_client.Text);
                myClient = new TcpClient(ip, port);
                myClient.connectTohost();
                myClient.get_Handler(psw);
                Btn_cntToclient.Text = "DisConnect";
            }
            else
            {
                if (psw != null) psw.remove_Handler(null, myClient);
                myClient.disconnect();               
                myClient = null;
                Btn_cntToclient.Text = "Connect";
            }
        }
        #endregion 

        // 关闭PicShow委托
        // 需要删除PicShow实例中加到TcpSocket事件中的委托
        private void CloseWinform(Object sender, EventArgs e)
        {
            if (null != psw)
            {
                psw.CloseHandler -= CloseWinform;
                psw.remove_Handler(myServer, myClient);
                psw.Close();
            }
        }

    }
}
