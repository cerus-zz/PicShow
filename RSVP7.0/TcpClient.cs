using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace RSVP7._0
{
    public class TcpClient
    {
        public TcpClient(String ip, int portnum)
        {
            HostIP = IPAddress.Parse(ip);
            point = new IPEndPoint(HostIP, portnum);
        }

        public void connectTohost()
        {
            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Connect(point);
                thread = new Thread(new ThreadStart(Process));
                thread.Start();
            }
            catch (Exception ey)
            {
                MessageBox.Show( ey.Message);
            }
        }

        public void disconnect()
        {
            try
            {
                if (null != socket)
                {
                    socket.Disconnect(false);  
                    socket.Close();
                }
                if (null != thread)
                {
                    thread.Abort();
                }
            }
            catch
            {
                MessageBox.Show("wrong with disconnect client\n");
            }
        }

        private void Process()
        {
            if (socket.Connected)
            {
                MessageBox.Show("** connected **");               
                try
                {
                    while (true)
                    {
                        byte[] receiveByte = new byte[8000];
                        socket.Receive(receiveByte, receiveByte.Length, 0);
                        string Info = Encoding.ASCII.GetString(receiveByte);
                        string[] tmp = Info.Split(';');

                        if ('a' == Info[0])
                        {                           
                            // 结果的图像相似度差，重新获取结果图像，用于播放，故用Config.originImage数组                 
                            if (tmp.Length - 2 != Config.m_trialnum)
                                MessageBox.Show("number of images come back from Server doesnot equal to Trial Number!");
                            else
                            {
                                for (int i = 1; i < tmp.Length-1; ++i)
                                {
                                    string[] hirach = tmp[i].Split('\\');
                                    if (hirach.Count() >= 2)
                                    {
                                        // 获取机器返回重新搜索的图集的路径，并依据文件编号获取相应的目标号
                                        Config.originImage[i - 1].label = Int32.Parse(hirach[hirach.Count()-2]);
                                        Config.originImage[i - 1].imagepath = Config.m_ImagePath + "\\" + hirach[hirach.Count() - 2] + "\\" + hirach[hirach.Count() - 1];
                                    }
                                }

                                CommandEventArgs e = new CommandEventArgs();
                                e.command = 'a';
                                e.number = tmp.Length - 2;
                                cmdHandler(this, e);
                            }

                        }
                        else if ('b' == Info[0])
                        {                      
                            // 机器返回的结果，用于展示，故用Config.feedback数组
                            for (int i = 1; i < tmp.Length-1; ++i)
                            {
                                string[] hirach = tmp[i].Split('\\');
                                if (hirach.Count() >= 2)
                                {
                                    Config.feedback[i - 1].imagepath = Config.m_ImagePath + "\\" + hirach[hirach.Count() - 2] + "\\" + hirach[hirach.Count() - 1];
                                }
                            }                           

                            try
                             {
                                CommandEventArgs e = new CommandEventArgs();
                                e.command = 'b';
                                e.number = tmp.Length - 2;
                                cmdHandler(this, e);                               
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(ex.ToString());
                            }
                        }
                        else
                        {
                            //do nothing                            
                        }
                    }
                }
                catch
                {
                    // 服务器端主动断开了连接，关闭客户端
                    socket.Close();
                    socket = null;
                }
            }

        }

        public void SendData(Object sender, CommandEventArgs e)
        {
            if ('s' == e.command)
            {
                try
                {
                    if (0 != e.number)
                    {
                        String content = null;
                        // 发送本轮的trial数，这样当发给服务器的图像相似度低需要需要重新搜索，则返回此数目的图像
                        content += Config.m_trialnum.ToString() + ";";
                        content += Config.m_evtlabel[Config.m_run-1].ToString() + ";";

                        for (int i = 0; i < e.number; ++i)
                        {
                            content += Config.feedback[i].imagepath;
                            content += ";";
                        }

                        byte[] sendBuffer = Encoding.ASCII.GetBytes(content.ToCharArray());
                        //byte[] sendBuffer = Encoding.BigEndianUnicode.GetBytes(content.ToCharArray());  // unicode
                        socket.Send(sendBuffer, sendBuffer.Length, 0);
                    }
                }
                catch { }
            }

        }

        #region variable

        public void get_Handler(PicShow psw)
        {
            if (null != psw)
            {
                psw.add_Handler(null, this);
            }
        }

        public void add_Handler(TcpSocket ts)
        {
            if (null != ts)
            {
                ts.CommandHandler += SendData;
            }
        }

        public void remove_Handler(TcpSocket ts)
        {
            if (null != ts)
            {
                ts.CommandHandler -= SendData;
            }
        }

        public event TcpEventHandler cmdHandler; 
        Thread thread = null;
        IPAddress HostIP;
        IPEndPoint point;
        Socket socket;

        #endregion
    }
}
