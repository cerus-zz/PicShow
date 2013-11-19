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
    class TcpSocket
    {        
        #region Socket
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
                int portNum = 10086;
                // get ip address automaticly
                //IPHostEntry ipHost = Dns.GetHostEntry(Dns.GetHostName());
                //IPAddress[] ipAddrlist = ipHost.AddressList;
                IPAddress localip = null;
                //foreach (IPAddress Ip in ipAddrlist)
                //{
                //    string tp = Ip.ToString();
                //    if (tp[0] != ':')
                //    {
                //        localip = Ip;
                //        break;
                //    }

                //}
                localip = IPAddress.Parse("10.14.86.111");
                IPEndPoint hostEP = new IPEndPoint(localip, portNum);

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
            if (trServerAccept != null) trServerAccept.Abort();
            if (trReceive != null) trReceive.Abort();
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
                            int size = (receiveCount) / 8;
                            double[] res = new double[size + 2];
                            int c = 0;
                            for (int i = 0; i < receiveCount; i += 8)
                            {
                                byte[] buf = new byte[8];
                                for (int j = 0; j < 8; j++)
                                {
                                    buf[j] = buffer[1+ i + j];
                                }
                                buf = buf.Reverse().ToArray();
                                res[c++] = System.BitConverter.ToDouble(buf, 0);
                            }
                            FileStream sFile = new FileStream("D:\\result.txt", FileMode.Create | FileMode.Append);
                            StreamWriter sw = new StreamWriter(sFile);

                            for (int i = 0; i < size; ++i)
                            {
                                sw.Write(res[i]);
                                sw.Write(" ");
                            }
                            sw.Write("\r\n");
                            sw.Flush();
                            sw.Close();
                            sFile.Close();
                        }
                        else if ('A' == command)
                        {
                            MessageBox.Show(command.ToString());
                        }
                        else if ('B' == command)
                        {
                            byte[] buf = new byte[4];
                            for (int i = 0; i < 4; i++)
                                buf[i] = buffer[1 + i];
                            buf = buf.Reverse().ToArray();
                            int round = System.BitConverter.ToInt32(buf, 0);
                            MessageBox.Show(command.ToString() + " " + round.ToString());
                        }
                        else if ('C' == command)
                        {
                            MessageBox.Show(command.ToString());
                        }
                        else if ('S' == command)
                        {
                            MessageBox.Show(command.ToString());
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

        public void Getresult(Config.Foo[] map)
        {
            for (int i=0; i < size; ++i)
            {
                map[i].score = result[i];
            }
        }
        Socket socket = null;
        Socket destSocket = null;
        Thread trServerAccept = null;
        Thread trReceive = null;
        int size = 0;
        double[] result = new double[400];          // 用于接收结果的数组，已知结果会是double类型的数值
    }
}
