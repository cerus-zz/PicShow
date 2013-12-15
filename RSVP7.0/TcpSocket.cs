using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.IO;

namespace RSVP7._0
{
    public class CommandEventArgs : EventArgs
    {
        public char command { get; set; }
        public int number { get; set; }
    }

    public delegate void TcpEventHandler(Object sender, CommandEventArgs e);

    public class TcpSocket
    {        
        #region Socket

        public TcpSocket(String ip, int portnum)
        {
            localip = IPAddress.Parse(ip);
            hostEP = new IPEndPoint(localip, portnum);
        }
        public void hostAcceptMethod()
        {
            // 每次只接受一个客户端连接请求，如果来了新的，那么旧的就断掉
            // 这满足了实际的需要。如果要同时维持多个客户端连接，则需要用数组保存
            // 已经建立的连接，这样便于销毁
            Socket newhandler = null; 
            while (true)
            {
                try
                {
                    newhandler = listener.Accept();
                    if (null != destSocket)
                    {
                        // 先关闭旧连接
                        destSocket.Shutdown(SocketShutdown.Both);
                        destSocket.Close();
                        trReceive.Abort();                        
                    }
                    // 开启新处理线程
                    //IPEndPoint clientip = (IPEndPoint)destSocket.RemoteEndPoint;
                    //string msg = "客户端已连接！" + clientip.ToString();
                    //this.Invoke(new ShowStatusHandle(this.ShowStatus), msg); 
                    destSocket = newhandler;
                    trReceive = new Thread(new ParameterizedThreadStart(receiveMethod));
                    trReceive.Start(destSocket);
                    MessageBox.Show("a new client ");
                }
                catch
                {
                    break;
                }
            }
        }

        public void startHost()
        {
            try
            {                                       
                listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                listener.Bind(hostEP);
                listener.Listen(100);
                trServerAccept = new Thread(hostAcceptMethod);
                trServerAccept.Start();
            }
            catch (SocketException ex)
            {
                if (ex.ErrorCode == 10048)
                {
                    MessageBox.Show("端口已被占用!");
                }
                else
                    MessageBox.Show(ex.Message);
            }
        }

        public void endHost()
        {
            if (listener != null) { listener.Close(); }
            if (destSocket != null) destSocket.Close();
            if (trServerAccept != null) { trServerAccept.Abort(); trServerAccept.Join(); }
            if (trReceive != null) { trReceive.Abort(); trReceive.Join(); }
        }

        private void receiveMethod(object obj)
        {
            Socket soc = (Socket)obj;     
            try
            {
                while ((soc != null) && (soc.Connected))
                {
                    byte[] buffer = new byte[4000];
                    int receiveCount = soc.Receive(buffer);

                    //TODO: change control variables
                    if (receiveCount > 0)
                    {
                        Char command = (Char)buffer[0];
                        if ('F' == command)
                        {
                            // 返回了feedback结果，告诉PicShow显示结果
                            // 结果每幅图像的得分是以播放顺序返回的，故feedback的label和imagepath每次都要按原顺序的图像信息更新！！！因为排序后这些都被打乱了           
                            int count = 0;
                            for (int i = 0; i < receiveCount; i += 8)
                            {
                                byte[] buf = new byte[8];
                                for (int j = 0; j < 8; j++)
                                {
                                    buf[j] = buffer[1+ i + j];
                                }
                                buf = buf.Reverse().ToArray();
                                Config.feedback[count] = Config.originImage[count];
                                Config.feedback[count].score = System.BitConverter.ToDouble(buf, 0);
                                count++;
                            }
                            
                            FileStream sFile = new FileStream("D:\\result.txt", FileMode.Create | FileMode.Append);
                            StreamWriter sw = new StreamWriter(sFile);
                            sw.Write(count);
                            sw.Write(" &&&&\r\n");
                            for (int i = 0; i < Config.m_trialnum; ++i)
                            {
                                sw.Write(Config.feedback[i].label);
                                sw.Write("-");
                                sw.Write(Config.feedback[i].score);
                                sw.Write("\r\n");
                            }
                            sw.Write("\r\n");
                            sw.Flush();
                            // 按每个样本的得分降序排列
                            quick_sort(Config.feedback, 0, Config.m_trialnum - 1);
                            for (int i = 0; i < Config.m_trialnum; ++i)
                            {                                
                                sw.Write(Config.feedback[i].label);
                                sw.Write("-");
                                sw.Write(Config.feedback[i].score);
                                sw.Write("\r\n");
                            }
                            sw.Write("\r\n");
                            sw.Close();
                            sFile.Close();
                                                        
                            CommandEventArgs e = new CommandEventArgs();
                            // 通知界面显示本轮RSVP的结果
                            e.command = 'F';
                            e.number = Config.m_trialnum;
                            CommandHandler(this, e);

                            // 通知客户端发送结果给机器处理
                            e.command = 's';
                            e.number = 5;
                            CommandHandler(this, e);                            
                        }
                        else if ('A' == command)
                        {
                            // 告诉被试休息
                            CommandEventArgs e = new CommandEventArgs();
                            e.command = 'A';
                            CommandHandler(this, e);
                        }
                        else if ('B' == command)
                        {
                            // 训练中的休息，要显示剩下的训练轮数
                            byte[] buf = new byte[4];
                            for (int i = 0; i < 4; i++)
                                buf[i] = buffer[1 + i];
                            buf = buf.Reverse().ToArray();
                            int round = System.BitConverter.ToInt32(buf, 0);

                            CommandEventArgs e = new CommandEventArgs();
                            e.command = 'B';
                            e.number = round;
                            CommandHandler(this, e);
                        }
                        else if ('C' == command)
                        {
                            // 告诉被试这是一轮测试，等待测试结果
                            CommandEventArgs e = new CommandEventArgs();
                            e.command = 'C';
                            CommandHandler(this, e);
                        }
                        else if ('S' == command)
                        {
                            // 告诉被试，可以随时开始一轮实验
                            CommandEventArgs e = new CommandEventArgs();
                            e.command = 'S';
                            CommandHandler(this, e);
                        }
                        else
                        {
                            MessageBox.Show("Unknown Command!");
                        }
                    }// end if (receiveCount > 0)               
                    else
                    {
                        // 接受到的数据长度为0， 连接被断开了
                        //MessageBox.Show("a connection is closed");
                    }   
                }                
            }
            catch
            {
                // 客户端断开连接                   
            }
        }

        #endregion 

        #region VarDef & Otherfunc

        // quicksort
        private void quick_sort(Foo[] res, int low, int high)
        {
            int l = low, h = high;
            Foo tmp = new Foo();
            while (l < h)
            {
                while (l <= high && res[l].score >= res[low].score) ++l;   // 降序排列！！
                while (res[h].score < res[low].score) --h;
                if (l < h)
                {
                    tmp = res[h];
                    res[h] = res[l];
                    res[l] = tmp;
                }
            }

            tmp = res[low];
            res[low] = res[h];
            res[h] = tmp;

            if (low < h) quick_sort(res, low, h - 1);
            if (h < high) quick_sort(res, h + 1, high);
        }

        public void get_Handler(PicShow psw, TcpClient tc)
        {
            if (null != psw)
            {
                psw.add_Handler(this, null);
            }
            if (null != tc)
            {
                tc.add_Handler(this);
            }
        }

        public event TcpEventHandler CommandHandler;
        Socket listener = null;
        Socket destSocket = null;
        Thread trServerAccept = null;
        Thread trReceive = null;
        IPAddress localip;
        IPEndPoint hostEP;

        #endregion VarDef & Otherfunc
    }
}
