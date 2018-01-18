using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CandyCoreCS
{
    public class ToledoS
    {
        // 串口相关
        public SerialPort serialPort = new SerialPort();//串口
        //public Boolean isOpen = false;//串口是否打开


        private StringBuilder builder = null;
        private Boolean Listening = false;//是否没有执行完invoke相关操作  
        private List<byte> buffer = new List<byte>(4096);//默认分配1页内存，并始终限制不允许超过
        private byte[] bufferData = new byte[16];//截取一个完整数据段

        // 秤的数据
        private String scaleName = "defaultScale";//秤的名称
        private bool isStable = false;//是否稳定
        private bool isNegative = false;//是否负数
        private double grossWeight = 0;//毛重
        private double netWeight = 0;//净重
        private double tareWeight = 0;//皮重

        public void setSetting(String portName,int baudRate,int dataBits,Parity parity,StopBits stopBits)
        {
            serialPort.PortName = portName;
            serialPort.BaudRate = baudRate;
            serialPort.DataBits = dataBits;
            serialPort.Parity = parity;
            serialPort.StopBits = stopBits;
        }

        public void setScaleName(String scaleName)
        {
            this.scaleName = scaleName;
            LogHelper.WriteLog(typeof(ToledoS), "设置秤名称" + scaleName);
        }

        public void startRead()
        {
            serialPort.Encoding = Encoding.GetEncoding("ASCII");
            Thread serialPortthread;
            serialPortthread = new Thread(new ThreadStart(begin));
            serialPortthread.Start();
        }

        private void begin()
        {
            while (true)
            {
                openSerialPort();
                Thread.CurrentThread.Join(500);//阻止设定时间
            }
        }

        private void openOrCloseSerialPort()
        { 
            if (serialPort.IsOpen)
            {
                openSerialPort();
            }
            else
            {
                closeSerialPort();
            }
        }

        public void openSerialPort()
        {
            try
            { 
            if (serialPort == null) return;
            if (!serialPort.IsOpen)
            {
                serialPort.Open();////打开端口，进行监控
                serialPort.DataReceived += new SerialDataReceivedEventHandler(serialPort_DataReceived);//这个事件为最关键点，一旦端口收到信号，就会触发该事件，这个事件就是真正读取信号，以便做接下的业务。  
            }
            }catch(Exception ex)
            {
                serialPort.Close();
                throw ex;
            }
        }

        public void closeSerialPort()
        {
            if (serialPort == null) return;
            if (serialPort.IsOpen)
            {
                serialPort.Close();//关闭端口，停止监控
                //serialPort.DataReceived = null;
            }
        }

        // 串口接收到数据后处理的方法
        private void serialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (!serialPort.IsOpen)
                {
                    return;
                }
                try
                {
                    int count = serialPort.BytesToRead;//先记录下来，避免某种原因，人为的原因，操作几次之，缓存不一致间时间长
                    byte[] buf = new byte[count];//声明一个临时数组存储当前来的串口数据
                    serialPort.Read(buf, 0, count);//读取缓冲数据

                    //连续3k数据没分析，应该没啥用了。 
                    //避免数据太多分析时候时间长导致效率明显下降 
                    if (buffer.Count > 1500)
                        buffer.Clear();

                    buffer.AddRange(buf);
                    //<协议解析>  
                    bool data_1_catched = false;//缓存记录数据是否捕获到  
                    ////因为要访问ui资源，所以需要使用invoke方式同步ui。
                    //this.Invoke((EventHandler)(delegate
                    //{
                    while (buffer.Count >= 2)
                    {
                        if (buffer[0] == 2 || buffer[0] == 130)//传输数据有帧头，用于判断  托利多熊猫是以S开头的                       
                        {
                            if (buffer.Count < 16) //数据区尚未接收完整  
                            {
                                break;
                            }

                            buffer.CopyTo(0, bufferData, 0, 16);//复制一条完整数据到具体的数据缓存  
                            data_1_catched = true;
                            buffer.RemoveRange(0, 16);//正确分析一条数据，从缓存中移除数据。  
                        }
                        else
                        {
                            buffer.RemoveAt(0);
                        }
                    }

                    if (data_1_catched)
                    {
                        builder = new StringBuilder();
                        //数组第二位转换为2进制进行判断，稳定和正负数
                        string judge = Convert.ToString(bufferData[2], 2).PadLeft(8, '0');
                        char[] jdg = judge.ToCharArray();
                        //从右到坐，第二位：1负数，0是整数
                        if (jdg[6] == 49)
                        {
                            isNegative = true;
                        }
                        else
                        {
                            isNegative = false;
                        }
                        //从右到坐，第四位：1不稳定，0是稳定
                        if (jdg[4] == 48)
                        {
                            isStable = true;
                        }
                        else
                        {
                            isStable = false;
                        }

                        for (int k = 5; k < 10; k++)
                        {
                            if (bufferData[k] > 128 && bufferData[k] - 128 != 32)
                            {
                                builder.Append((char)(bufferData[k] - 128));
                            }
                            else if (bufferData[k] != 32)
                            {
                                builder.Append((char)(bufferData[k]));
                            }
                        }

                        //for (int q = 10; q < 16; q++)
                        //{
                        //    if (bufferData[q] > 128 && bufferData[q] - 128 != 32)
                        //    {
                        //        builderSkin.Append((char)(bufferData[q] - 128));
                        //    }
                        //    else if (bufferData[q] != 32)
                        //    {
                        //        Console.WriteLine((char)(bufferData[q]));
                        //        builderSkin.Append((char)(bufferData[q]));
                        //    }
                        //}

                        //if (Format.IsNum(builder.ToString().Trim()))
                        //{
                            
                            grossWeight =  Convert.ToDouble(builder.ToString().Trim()) ;
                            if (isNegative)
                            {
                                 grossWeight =  -1* grossWeight;
                            }
                             
                            if (Status.scaleGrossWeight.ContainsKey(scaleName))
                            {
                            Status.scaleGrossWeight[scaleName] = grossWeight;
                            }
                            else
                            {
                            Status.scaleGrossWeight.Add(scaleName,grossWeight);
                        } 
                        //}
                        builder.Clear();//清除字符串构造器的内容
                    }

                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    //throw ex;
                }
                finally
                {
                    //Listening = false;//我用完了，ui可以关闭串口了。  
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                //throw ex;
            }




            //string s = "";
            //int count = serialPort.BytesToRead;

            //byte[] data = new byte[count];
            //serialPort.Read(data, 0, count);

            //foreach (byte item in data)
            //{
            //    s += Convert.ToChar(item);
            //}

            //if (this.InvokeRequired)//由于是非创建线程访问<span style="font-family:Arial, Helvetica, sans-serif;">textBox1，所以要使用代理句柄。要不然会抛异常，这点需要特别注意</span>  
            //{
            //    this.Invoke(new MethodInvoker(delegate { this.txtData.Text = s; }));
            //}
            //else
            //{
            //    this.txtData.Text = s;
            //}
        }


        public Boolean isOpen()
        {
            return serialPort.IsOpen;
        }

        public String getBufferString()
        {
                StringBuilder re = new StringBuilder();
            foreach(byte item in buffer)
            {
                re.Append(item);
            }
            return re.ToString();
        }


    }
}
