using Kingdee.BOS.Client.Core;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;

namespace BOA.XJS.WpfCtl
{
    public static class SerialCom
    {
        /// <summary>
        /// 串口
        /// </summary>
        public static SerialPort Com { get; set; } //= new SerialPort();

        /// <summary>
        /// 串口名字
        /// </summary>
        public static string Com_Name { get; set; }

        /// <summary>
        /// 波特率
        /// </summary>
        public static int Com_Bound { get; set; }

        /// <summary>
        ///  数据位
        /// </summary>
        public static int Com_DataBit { get; set; }

        /// <summary>
        ///  校验位
        /// </summary>
        public static string Com_Verify { get; set; }

        /// <summary>
        /// 停止位
        /// </summary>
        public static string Com_StopBit { get; set; }

        /// <summary>
        /// 串口的打开状态标记位
        /// </summary>
        public static bool OpenState { get; set; }

        /// <summary>
        /// 数据显示
        /// </summary>
        //public static List<string> Comdata = new List<string>();
    }

    #region 注释不用

    //public class MyCom
    //{
    //    public IKDControlProxy Ctl { get; set; }

    //    /// <summary>
    //    /// 打开串口并读取数据(弃用)
    //    /// </summary>
    //    public string ComOpen()
    //    {
    //        if (SerialCom.OpenState == false)
    //        {
    //            SerialCom.Com = new SerialPort();
    //            SerialCom.Com.PortName = SerialCom.Com_Name;
    //            SerialCom.Com.BaudRate = SerialCom.Com_Bound;
    //            SerialCom.Com.DataBits = SerialCom.Com_DataBit;
    //            if (SerialCom.Com_StopBit == "1") SerialCom.Com.StopBits = StopBits.One;
    //            if (SerialCom.Com_StopBit == "2") SerialCom.Com.StopBits = StopBits.Two;
    //            if (SerialCom.Com_Verify == "None") SerialCom.Com.Parity = Parity.None;
    //            if (SerialCom.Com_Verify == "Odd") SerialCom.Com.Parity = Parity.Odd;
    //            if (SerialCom.Com_Verify == "Even") SerialCom.Com.Parity = Parity.Even;
    //            //SerialCom.Com.NewLine = "\r\n"; //接收或者发送数据回车显示
    //            return Comthread(); //启动线程
    //        }
    //        else
    //        {
    //            //SerialCom.Comdata.Add("关闭串口");
    //            SerialCom.Com.Close();
    //            SerialCom.OpenState = false;
    //            return "";
    //        }
    //    }

    //    /// <summary>
    //    /// 弃用
    //    /// </summary>
    //    private void ReadDada()
    //    {
    //        //SerialCom.Comdata.Add("打开串口完成");
    //        SerialCom.OpenState = true;
    //        while (SerialCom.OpenState)
    //        {
    //            Thread.Sleep(50);
    //            try
    //            {
    //                int n = SerialCom.Com.BytesToRead;
    //                byte[] buf = new byte[n];
    //                SerialCom.Com.Read(buf, 0, n);
    //                //如果收到数据则输出
    //                if (buf.Length > 0)
    //                {
    //                    //string str = Encoding.ASCII.GetString(buf);
    //                    string str = Encoding.Default.GetString(buf);
    //                    SerialCom.Comdata.Add(str);
    //                }
    //            }
    //            catch
    //            {
    //                SerialCom.OpenState = false;
    //                SerialCom.Com.Close();//异常则关闭串口
    //            }
    //        }
    //    }

    //    /// <summary>
    //    /// 弃用
    //    /// </summary>
    //    /// <returns></returns>
    //    private string Comthread()
    //    {
    //        string msg = "";
    //        try
    //        {
    //            SerialCom.Com.Open(); //打开串口
    //            SerialCom.Com.Encoding = Encoding.GetEncoding("ASCII"); //设置编码格式
    //            Thread thread = new Thread(ReadDada); //实例化一个线程
    //            thread.IsBackground = true; //设置线程为后台线程
    //            thread.Start(); //启动线程
    //        }
    //        catch (Exception ex)
    //        {
    //            SerialCom.OpenState = false;
    //            SerialCom.Com.Close();//异常则关闭串口
    //            msg = ex.Message;
    //        }
    //        return msg;
    //    }

    //    /// <summary>
    //    /// 打开串口
    //    /// </summary>
    //    public string OpenCom()
    //    {
    //        string msg = "";
    //        try
    //        {
    //            SerialCom.OpenState = SerialCom.Com.IsOpen;
    //            if (SerialCom.OpenState == false)
    //            {
    //                SerialCom.Com.PortName = SerialCom.Com_Name;
    //                SerialCom.Com.BaudRate = SerialCom.Com_Bound;
    //                SerialCom.Com.DataBits = SerialCom.Com_DataBit;
    //                if (SerialCom.Com_StopBit == "1") SerialCom.Com.StopBits = StopBits.One;
    //                if (SerialCom.Com_StopBit == "2") SerialCom.Com.StopBits = StopBits.Two;
    //                if (SerialCom.Com_Verify == "None") SerialCom.Com.Parity = Parity.None;
    //                if (SerialCom.Com_Verify == "Odd") SerialCom.Com.Parity = Parity.Odd;
    //                if (SerialCom.Com_Verify == "Even") SerialCom.Com.Parity = Parity.Even;
    //                SerialCom.Com.Open(); //打开串口
    //                SerialCom.Com.Encoding = Encoding.GetEncoding("ASCII"); //设置编码格式
    //                ThreadGetComData();
    //            }
    //            else
    //            {
    //                //SerialCom.Com.Close();
    //                //SerialCom.OpenState = false;
    //            }
    //        }
    //        catch (Exception ex)
    //        {
    //            msg = ex.Message;
    //            SerialCom.Com.Close();
    //            SerialCom.OpenState = false;
    //        }
    //        return msg;
    //    }

    //    /// <summary>
    //    /// 关闭串口
    //    /// </summary>
    //    public void CloseCom()
    //    {
    //        SerialCom.OpenState = false;
    //        SerialCom.Com.Close();
    //    }

    //    /// <summary>
    //    /// 获取串口数据(后台线程)
    //    /// </summary>
    //    /// <returns></returns>
    //    public string ThreadGetComData()
    //    {
    //        var msg = "";
    //        try
    //        {
    //            Thread thread = new Thread(GetComData); //实例化一个线程
    //            thread.IsBackground = true; //设置线程为后台线程
    //            thread.Start(); //启动线程
    //        }
    //        catch (Exception ex)
    //        {
    //            //msg = ex.Message;
    //            //SerialCom.OpenState = false;
    //            //SerialCom.Com.Close();//异常则关闭串口
    //        }
    //        return msg;
    //    }

    //    /// <summary>
    //    /// 获取串口数据
    //    /// </summary>
    //    private void GetComData()
    //    {
    //        SerialCom.OpenState = true;
    //        while (SerialCom.OpenState)
    //        {
    //            Thread.Sleep(50);
    //            try
    //            {
    //                int n = SerialCom.Com.BytesToRead;
    //                byte[] buf = new byte[8];
    //                SerialCom.Com.Read(buf, 0, 8);
    //                //如果收到数据则输出
    //                if (buf.Length > 0)
    //                {
    //                    if (SerialCom.Comdata.Count > 0)
    //                    {
    //                        SerialCom.Comdata.Clear();
    //                    }

    //                    //string str = Encoding.ASCII.GetString(buf);
    //                    string str = Encoding.Default.GetString(buf);
    //                    if (!str.Contains("\0"))
    //                    {
    //                        var str1 = str.Replace("=", "");

    //                        var weightData = str1.Reverse().ToArray();

    //                        var weightData1 = string.Join("", weightData).TrimStart('0');

    //                        if (Ctl != null)
    //                        {
    //                            JObject fldState = new JObject();
    //                            fldState["key"] = "F_BOA_QtyText".ToUpper();
    //                            fldState["value"] = weightData1;
    //                            Ctl.UpdateState(fldState);

    //                        }

    //                        SerialCom.Comdata.Add(weightData1);
    //                    }
    //                }
    //            }
    //            catch
    //            {
    //                //SerialCom.OpenState = false;
    //                //SerialCom.Com.Close();//异常则关闭串口
    //            }
    //        }
    //    }
    //}

    #endregion
}
