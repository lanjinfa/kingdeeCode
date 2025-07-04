using Kingdee.BOS.Client.Core;
using System;
using System.Linq;
using System.Windows.Controls;
using System.IO.Ports;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Collections.Generic;
using System.Text;

namespace BOA.XJS.WpfCtl
{
    public class SmartCardReader : ContentControl, IKDCustomControl, IDynamicFormSupported
    {
        public IKDDynamicFormProxy FormProxy { get; set; }

        public IKDCustomControlProxy Proxy { get; set; }

        //private IKDControlProxy _Ctl;
        private IKDControlProxy _Label;
        private string _WeightText = "";

        //private MyCom mCom = new MyCom(); //实例化一个串口类

        protected void FireOnCustomEvent(CustomEventArgs e)
        {
            if (this.Proxy != null)
            {
                this.Proxy.FireCustomEvent(e);
            }
        }

        public void InitComponent()
        {
        }

        public void Release()
        {
            if (SerialCom.Com.IsOpen)
            {
                SerialCom.OpenState = false;
                SerialCom.Com.Close();
            }
            this.Content = null;
        }

        /// <summary>
        /// 该方法用来确认自定义控件是否成功更新
        /// </summary>
        public void WriteString()
        {
            //var label = this.FormProxy.ControlFactoryProxy.GetCmp("F_BOA_Label".ToUpper());
            //if (label != null)
            //{
            //    JObject fldState = new JObject();
            //    fldState["key"] = "F_BOA_Label".ToUpper();
            //    fldState["value"] = "称重重量：50";
            //    label.UpdateState(fldState);
            //}
            //var ctl = this.FormProxy.ControlFactoryProxy.GetCmp("F_BOA_QtyText".ToUpper());
            //if (ctl != null)
            //{
            //    JObject fldState = new JObject();
            //    fldState["key"] = "F_BOA_QtyText".ToUpper();
            //    fldState["value"] = "50";
            //    ctl.UpdateState(fldState);
            //}

            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                FireOnCustomEvent(new CustomEventArgs("", "Success", "v050"));
            }
            ));
        }

        /// <summary>
        /// 串口数据读取(新)
        /// </summary>
        /// <param name="portNo">串口号</param>
        public void GetDataNew()
        {
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                FireOnCustomEvent(new CustomEventArgs("", "Success", _WeightText));
            }));
        }

        /// <summary>
        /// 打开串口
        /// </summary>
        /// <param name="portNo"></param>
        public void OpenCom(string portNo)
        {
            SerialCom.Com_Name = portNo;// 串口号  COM1
            SerialCom.Com_Bound = 9600; // 波特率
            SerialCom.Com_DataBit = 8; // 数据位
            SerialCom.Com_StopBit = "1";// 停止位
            SerialCom.Com_Verify = "None";// 校验位
            SerialCom.Com = new SerialPort();

            //_Ctl = this.FormProxy.ControlFactoryProxy.GetCmp("F_BOA_QtyText".ToUpper());
            _Label = this.FormProxy.ControlFactoryProxy.GetCmp("F_BOA_Label".ToUpper());

            var msg = OpenCom();

            if (msg != "")
            {
                FireOnCustomEvent(new CustomEventArgs("", "fail", "msg:" + msg));
            }
        }

        /// <summary>
        /// 关闭串口
        /// </summary>
        public void CloseCom()
        {
            if (SerialCom.Com.IsOpen)
            {
                SerialCom.OpenState = false;
                SerialCom.Com.Close();
            }
        }

        /// <summary>
        /// 打开串口
        /// </summary>
        public string OpenCom()
        {
            string msg = "";
            try
            {
                SerialCom.OpenState = SerialCom.Com.IsOpen;
                if (SerialCom.OpenState == false)
                {
                    SerialCom.Com.PortName = SerialCom.Com_Name;
                    SerialCom.Com.BaudRate = SerialCom.Com_Bound;
                    SerialCom.Com.DataBits = SerialCom.Com_DataBit;
                    if (SerialCom.Com_StopBit == "1") SerialCom.Com.StopBits = StopBits.One;
                    if (SerialCom.Com_StopBit == "2") SerialCom.Com.StopBits = StopBits.Two;
                    if (SerialCom.Com_Verify == "None") SerialCom.Com.Parity = Parity.None;
                    if (SerialCom.Com_Verify == "Odd") SerialCom.Com.Parity = Parity.Odd;
                    if (SerialCom.Com_Verify == "Even") SerialCom.Com.Parity = Parity.Even;
                    SerialCom.Com.Open(); //打开串口
                    SerialCom.Com.Encoding = Encoding.GetEncoding("ASCII"); //设置编码格式
                    ThreadGetComData();
                }
                //else
                //{
                //    //SerialCom.Com.Close();
                //    //SerialCom.OpenState = false;
                //}
            }
            catch (Exception ex)
            {
                msg = ex.Message;
                if (SerialCom.Com.IsOpen)
                {
                    SerialCom.Com.Close();
                    SerialCom.OpenState = false;
                }
            }
            return msg;
        }

        /// <summary>
        /// 获取串口数据(后台线程)
        /// </summary>
        /// <returns></returns>
        public void ThreadGetComData()
        {
            //var msg = "";
            try
            {
                Thread thread = new Thread(GetComData); //实例化一个线程
                thread.IsBackground = true; //设置线程为后台线程
                thread.Start(); //启动线程
            }
            catch (Exception)
            {
                //msg = ex.Message;
                //SerialCom.OpenState = false;
                //SerialCom.Com.Close();//异常则关闭串口
            }
            //return msg;
        }

        /// <summary>
        /// 获取串口数据
        /// </summary>
        private void GetComData()
        {
            SerialCom.OpenState = true;
            while (SerialCom.OpenState)
            {
                Thread.Sleep(50);
                try
                {
                    int n = SerialCom.Com.BytesToRead;
                    byte[] buf = new byte[8];
                    SerialCom.Com.Read(buf, 0, 8);
                    //如果收到数据则输出
                    if (buf.Length > 0)
                    {
                        //if (SerialCom.Comdata.Count > 0)
                        //{
                        //    SerialCom.Comdata.Clear();
                        //}

                        //string str = Encoding.ASCII.GetString(buf);
                        string str = Encoding.Default.GetString(buf);
                        if (!str.Contains("\0"))
                        {
                            var str1 = str.Replace("=", "");

                            var weightData = str1.Reverse().ToArray();

                            var weightData1 = string.Join("", weightData).TrimStart('0');

                            //将串口数据，实时写入金蝶客户端控件
                            this.Dispatcher.Invoke(new Action(() =>
                            {
                                //if (_Ctl != null)
                                //{
                                //    JObject fldState = new JObject();
                                //    fldState["key"] = "F_BOA_QtyText".ToUpper();
                                //    fldState["value"] = weightData1;
                                //    _Ctl.UpdateState(fldState);
                                //}
                                if (_Label != null)
                                {
                                    JObject fldState = new JObject();
                                    fldState["key"] = "F_BOA_Label".ToUpper();
                                    fldState["value"] = $"称重重量：{weightData1}";
                                    _Label.UpdateState(fldState);
                                }
                                _WeightText = weightData1;
                            }));

                            //SerialCom.Comdata.Add(weightData1);
                        }
                    }
                }
                catch
                {
                    //SerialCom.OpenState = false;
                    //SerialCom.Com.Close();//异常则关闭串口
                }
            }
        }
    }
}
