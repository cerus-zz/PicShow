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

            while (true)
            {
                try
                {
                    destSocket = socket.Accept();
                    IPEndPoint clientip = (IPEndPoint)destSocket.RemoteEndPoint;
                    //string msg = "客户端已连接！" + clientip.ToString();
                    //this.Invoke(new ShowStatusHandle(this.ShowStatus), msg);
                    trReceive = new Thread(new ParameterizedThreadStart(receiveMethod));
                    trReceive.Start(destSocket);
                    //break;
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
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Bind(hostEP);
                socket.Listen(100);
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
            if (socket != null) socket.Close();
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
                            //int size = (receiveCount-1) / 8;              
                            int count = 0;
                            for (int i = 0; i < receiveCount; i += 8)
                            {
                                byte[] buf = new byte[8];
                                for (int j = 0; j < 8; j++)
                                {
                                    buf[j] = buffer[1+ i + j];
                                }
                                buf = buf.Reverse().ToArray();
                                Config.feedback[count++].score = System.BitConverter.ToDouble(buf, 0);
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
                            e.command = 'F';
                            e.number = count;
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
                    
                }
            }
            catch
            {
                //reset(); after client cut the connect
                //MessageBox.Show("客户端已断开！");
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

        public void get_Handler(PicShow psw)
        {
            if (null != psw)
            {
                psw.add_Handler(this, null);
            }
        }

        public event TcpEventHandler CommandHandler; 
        Socket socket = null;
        Socket destSocket = null;
        Thread trServerAccept = null;
        Thread trReceive = null;
        IPAddress localip;
        IPEndPoint hostEP;

        #endregion VarDef & Otherfunc
    }
}
