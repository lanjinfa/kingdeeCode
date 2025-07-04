using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace BOA.YZW.PayAndRec.ServicePlugIn.Helper
{
    public static class XmlHelper
    {
        /// <summary>
        /// 创建ERP支付经办请求参数
        /// </summary>
        /// <param name="xmlRow">一级节点，接口参数根节点为二级节点，添加在一级节点下</param>
        /// <returns></returns>
        public static XmlDocument CreateERPAYSAVXml(out XmlElement xmlRow)
        {
            var xmlDoc = new XmlDocument();//根节点
            var xdec = xmlDoc.CreateXmlDeclaration("1.0", "GBK", "");//xml描述节点
            xmlDoc.AppendChild(xdec);
            xmlRow = xmlDoc.CreateElement("CBSERPPGK");//一级节点
            var xmlInfo = xmlDoc.CreateElement("INFO");//二级节点
            var xmlFunName = xmlDoc.CreateElement("FUNNAM");//三级节点，接口名
            xmlFunName.InnerText = "ERPAYSAV";//三级节点值
            xmlInfo.AppendChild(xmlFunName);
            xmlRow.AppendChild(xmlInfo);
            xmlDoc.AppendChild(xmlRow);
            return xmlDoc;
        }

        /// <summary>
        /// 创建ERP代发经办参数
        /// </summary>
        /// <param name="parameterRootElement">接口参数根节点，所有接口字段参数作为三级节点添加到该节点下</param>
        /// <returns></returns>
        public static XmlDocument CreateERAGNOPRXml(out XmlElement xmlRow)
        {
            var xmlDoc = new XmlDocument();//根节点
            var xdec = xmlDoc.CreateXmlDeclaration("1.0", "GBK", "");//xml描述节点
            xmlDoc.AppendChild(xdec);
            xmlRow = xmlDoc.CreateElement("CBSERPPGK");//一级节点
            var xmlInfo = xmlDoc.CreateElement("INFO");//二级节点
            var xmlFunName = xmlDoc.CreateElement("FUNNAM");//三级节点，接口名
            xmlFunName.InnerText = "ERAGNOPR";//三级节点值
            xmlInfo.AppendChild(xmlFunName);
            xmlRow.AppendChild(xmlInfo);
            xmlDoc.AppendChild(xmlRow);
            return xmlDoc;
        }

        /// <summary>
        /// 创建查询待审批的结算记录
        /// </summary>
        /// <param name="xmlRow"></param>
        /// <returns></returns>
        public static XmlDocument CreateAPAUTQRYXml(out XmlElement xmlRow)
        {
            var xmlDoc = new XmlDocument();//根节点
            var xdec = xmlDoc.CreateXmlDeclaration("1.0", "GBK", "");//xml描述节点
            xmlDoc.AppendChild(xdec);
            xmlRow = xmlDoc.CreateElement("CBSERPPGK");//一级节点
            var xmlInfo = xmlDoc.CreateElement("INFO");//二级节点
            var xmlFunName = xmlDoc.CreateElement("FUNNAM");//三级节点，接口名
            xmlFunName.InnerText = "APAUTQRY";//三级节点值
            xmlInfo.AppendChild(xmlFunName);
            xmlRow.AppendChild(xmlInfo);
            xmlDoc.AppendChild(xmlRow);
            return xmlDoc;
        }

        /// <summary>
        /// 创建 当日查询收款单明细 参数
        /// </summary>
        /// <param name="lastSeq">最后一次流水号</param>
        /// <returns></returns>
        public static XmlDocument CreateERCURDTLXml(string lastSeq)
        {
            var xmlDoc = new XmlDocument();//根节点
            var xdec = xmlDoc.CreateXmlDeclaration("1.0", "GBK", "");//xml描述节点
            xmlDoc.AppendChild(xdec);
            var xmlRow = xmlDoc.CreateElement("CBSERPPGK");//一级节点
            var xmlInfo = xmlDoc.CreateElement("INFO");//二级节点
            var xmlFunName = xmlDoc.CreateElement("FUNNAM");//三级节点，接口名
            xmlFunName.InnerText = "ERCURDTL";//三级节点值
            xmlInfo.AppendChild(xmlFunName);
            xmlRow.AppendChild(xmlInfo);
            xmlDoc.AppendChild(xmlRow);
            var ercurdtla = xmlDoc.CreateElement("ERCURDTLA");//二级节点
            var itmdir = xmlDoc.CreateElement("ITMDIR");//借贷 (注： C = 贷 , 即为收款数据 )
            itmdir.InnerText = "C";
            ercurdtla.AppendChild(itmdir);

            var erdtlseqz = xmlDoc.CreateElement("ERDTLSEQZ");//二级节点
            var dtlseq = xmlDoc.CreateElement("DTLSEQ");//流水号
            dtlseq.InnerText = lastSeq;
            erdtlseqz.AppendChild(dtlseq);

            //var ercurdtlb = xmlDoc.CreateElement("ERCURDTLB");//二级节点
            //var actnbr = xmlDoc.CreateElement("ACTNBR");//外部账号
            //actnbr.InnerText = "755936022610402";
            //ercurdtlb.AppendChild(actnbr);
            xmlRow.AppendChild(ercurdtla);
            xmlRow.AppendChild(erdtlseqz);
            return xmlDoc;
        }

        /// <summary>
        /// 创建 查询历史明细数据 参数
        /// </summary>
        /// <param name="lastSeq">最后一次流水号</param>
        /// <param name="startTime">开始日期</param>
        /// <param name="endTime">结束日期</param>
        /// <returns></returns>
        public static XmlDocument CreateERQRYTRSXml(string lastSeq, string startTime, string endTime)
        {
            var xmlDoc = new XmlDocument();//根节点
            var xdec = xmlDoc.CreateXmlDeclaration("1.0", "GBK", "");//xml描述节点
            xmlDoc.AppendChild(xdec);
            var xmlRow = xmlDoc.CreateElement("CBSERPPGK");//一级节点
            var xmlInfo = xmlDoc.CreateElement("INFO");//二级节点
            var xmlFunName = xmlDoc.CreateElement("FUNNAM");//三级节点，接口名
            xmlFunName.InnerText = "ERQRYTRS";//三级节点值
            xmlInfo.AppendChild(xmlFunName);
            xmlRow.AppendChild(xmlInfo);
            xmlDoc.AppendChild(xmlRow);

            var ercurdtla = xmlDoc.CreateElement("ERQRYTRSA");//二级节点
            var BGNDAT = xmlDoc.CreateElement("BGNDAT");//开始日期
            BGNDAT.InnerText = startTime;
            ercurdtla.AppendChild(BGNDAT);
            var ENDDAT = xmlDoc.CreateElement("ENDDAT");//结束日期
            ENDDAT.InnerText = endTime;
            ercurdtla.AppendChild(ENDDAT);
            var itmdir = xmlDoc.CreateElement("ITMDIR");//借贷 (注： 2 = 贷 , 即为收款数据 )
            itmdir.InnerText = "2";
            ercurdtla.AppendChild(itmdir);

            var ERDTLSEQZ = xmlDoc.CreateElement("ERDTLSEQZ");//二级节点
            var DTLSEQ = xmlDoc.CreateElement("DTLSEQ");//流水号
            DTLSEQ.InnerText = lastSeq;
            ERDTLSEQZ.AppendChild(DTLSEQ);

            xmlRow.AppendChild(ercurdtla);
            xmlRow.AppendChild(ERDTLSEQZ);
            return xmlDoc;
        }

        /// <summary>
        /// 创建 ERP支付经办撤销 参数
        /// </summary>
        /// <param name="rEFNBRValue">企业参考业务号</param>
        /// <param name="oPRTYPValue">操作类型</param>
        /// <returns></returns>
        public static XmlDocument CreateERPAYCANXml(string rEFNBRValue, string oPRTYPValue)
        {
            var xmlDoc = new XmlDocument();//根节点
            var xdec = xmlDoc.CreateXmlDeclaration("1.0", "GBK", "");//xml描述节点
            xmlDoc.AppendChild(xdec);
            var xmlRow = xmlDoc.CreateElement("CBSERPPGK");//一级节点
            var xmlInfo = xmlDoc.CreateElement("INFO");//二级节点
            var xmlFunName = xmlDoc.CreateElement("FUNNAM");//三级节点，接口名
            xmlFunName.InnerText = "ERPAYCAN";//三级节点值
            xmlInfo.AppendChild(xmlFunName);
            xmlRow.AppendChild(xmlInfo);
            xmlDoc.AppendChild(xmlRow);
            //撤销参数拼接
            var canRow = xmlDoc.CreateElement("ERPAYCANX");//二级节点
            var refnbr = xmlDoc.CreateElement("REFNBR");//参考业务号
            refnbr.InnerText = $"{rEFNBRValue}";
            canRow.AppendChild(refnbr);
            var oprtyp = xmlDoc.CreateElement("OPRTYP");//操作类型
            oprtyp.InnerText = $"{oPRTYPValue}";
            canRow.AppendChild(oprtyp);
            var cacrsn = xmlDoc.CreateElement("CACRSN");//撤销原因
            cacrsn.InnerText = "金蝶系统付款单反审核";
            canRow.AppendChild(cacrsn);
            xmlRow.AppendChild(canRow);
            return xmlDoc;
        }

        /// <summary>
        /// 批量查询支付状态
        /// </summary>
        /// <param name="xmlRow"></param>
        /// <returns></returns>
        public static XmlDocument CreateERPAYSTAXml(out XmlElement xmlRow)
        {
            var xmlDoc = new XmlDocument();//根节点
            var xdec = xmlDoc.CreateXmlDeclaration("1.0", "GBK", "");//xml描述节点
            xmlDoc.AppendChild(xdec);
            xmlRow = xmlDoc.CreateElement("CBSERPPGK");//一级节点
            var xmlInfo = xmlDoc.CreateElement("INFO");//二级节点
            var xmlFunName = xmlDoc.CreateElement("FUNNAM");//三级节点，接口名
            xmlFunName.InnerText = "ERPAYSTA";//三级节点值
            xmlInfo.AppendChild(xmlFunName);
            xmlRow.AppendChild(xmlInfo);
            xmlDoc.AppendChild(xmlRow);
            return xmlDoc;
        }

        /// <summary>
        /// 加密原生xml内容
        /// </summary>
        /// <param name="xml">原生xml</param>
        /// <param name="key">密钥</param>
        ///  /// <param name="crc32_pwd">crc32密码</param>
        /// <param name="crc32_prefix">crc32前缀</param>
        /// <returns></returns>
        public static string CreateSendXml(string xml, string key, string crc32_pwd, string crc32_prefix)
        {
            try
            {
                //去除所有回车换行
                xml = xml.Replace("\n", "").Replace("\r", "");
                //格式判断
                //bool isiniPlus = false;
                //if (xml.IndexOf("elXmlIniPlus") > 0) { isiniPlus = true; }
                //根据格式组合外层xml文件
                XmlDocument xmlDoc = new XmlDocument();
                XmlDeclaration xdec = xmlDoc.CreateXmlDeclaration("1.0", "GBK", "");
                xmlDoc.AppendChild(xdec);
                XmlElement pkg = xmlDoc.CreateElement("PGK");
                xmlDoc.AppendChild(pkg);
                //if (isiniPlus)
                //{
                //    XmlElement pagtyp = xmlDoc.CreateElement("PAGTYP");
                //    pkg.AppendChild(pagtyp);
                //    pagtyp.InnerText = "1";
                //    XmlElement funnam = xmlDoc.CreateElement("FUNNAM");
                //    funnam.InnerText = funname;
                //    pkg.AppendChild(funnam);
                //}
                XmlElement data = xmlDoc.CreateElement("DATA");
                pkg.AppendChild(data);
                //由于xmlDocument本身不支持cdata嵌套，不得已先用一个占位符，然后在后面进行替换
                //测试中发现，用了占位符后校验也会失败，取消占位符相关代码。
                //再次尝试先算checkcode后再替换占位符，测试通过
                string occupant = "占位符___占位符";
                XmlCDataSection cdata = xmlDoc.CreateCDataSection(occupant);
                data.AppendChild(cdata);
                // 校验码
                string code = GetCheckSumWithCRC32(key, xml, crc32_pwd, crc32_prefix);
                XmlElement checkcode = xmlDoc.CreateElement("CHECKCODE");
                checkcode.InnerText = code;
                pkg.AppendChild(checkcode);
                return xmlDoc.OuterXml.Replace(occupant, xml.Replace("]]>", "]]]]><![CDATA[>"));
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        /// <summary>
        /// 创建CRC32的校验CODE
        /// </summary>
        /// <param name="key">密钥</param>
        /// <param name="xmlData">xml数据</param>
        /// <param name="crc32_pwd">crc32密码</param>
        /// <param name="crc32_prefix">crc32前缀</param>
        /// <returns></returns>
        public static string GetCheckSumWithCRC32(string key, string xmlData, string crc32_pwd, string crc32_prefix)
        {
            string str = crc32_pwd + key + xmlData;
            Crc32.language = "GBK";
            var result = "00000000" + (Crc32.Sum(str).ToString("X"));
            return crc32_prefix + result.Substring(result.Length - 8);
        }

        /// <summary>
        /// 解析接口返回值
        /// </summary>
        /// <param name="responseXml">接口返回值</param>
        /// <returns></returns>
        public static string GetResponseContent(string responseXml)
        {
            var xmldoc = new XmlDocument();
            xmldoc.LoadXml(responseXml);
            xmldoc.LoadXml(xmldoc.SelectSingleNode("/PGK/DATA").InnerText);
            var responseStr = string.Empty;
            var xml = xmldoc.OuterXml;
            foreach (XmlElement interfaceNode in xmldoc.SelectSingleNode("/CBSERPPGK").ChildNodes)
            {
                responseStr += interfaceNode.Name + "\r\n";
                foreach (XmlElement node in interfaceNode.ChildNodes)
                {
                    responseStr += node.Name + ":" + node.InnerText + "\r\n";
                }
                responseStr += "\r\n";
            }
            return responseStr;
        }

        /// <summary>
        /// CBS接口返回值
        /// 包含：ERP支付经办请求接口返回值、ERP代发经办接口返回值等
        /// </summary>
        /// <returns></returns>
        public static CBSERPPGK GetERPAYSAVResponse(string responseXml)
        {
            var xmldoc = new XmlDocument();
            xmldoc.LoadXml(responseXml);
            xmldoc.LoadXml(xmldoc.SelectSingleNode("/PGK/DATA").InnerText);
            var result = DESerializer<CBSERPPGK>(xmldoc.OuterXml);
            return result;
        }

        /// <summary>
        /// 序列化xml
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="strXML"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static T DESerializer<T>(string strXML) where T : class
        {
            try
            {
                using (StringReader sr = new StringReader(strXML))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(T));
                    return serializer.Deserialize(sr) as T;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("将XML转换成实体对象异常", ex);
            }
        }
    }
}
