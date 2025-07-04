using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace BOA.YZW.PayAndRec.ServicePlugIn.Helper
{
    /// <summary>
    /// ERP支付经办请求
    /// </summary>
    public class CBSERPPGK
    {
        public INFO INFO { get; set; }

        /// <summary>
        /// ERP支付经办请求返回值
        /// </summary>
        //[XmlElement("APPAYSAVZ", IsNullable = false)]
        //public List<APPAYSAVZ> APPAYSAVZ { get; set; }
        public APPAYSAVZ APPAYSAVZ { get; set; }

        ///// <summary>
        ///// ERP代发经办
        ///// </summary>
        //public APSALSAVY APSALSAVY { get; set; }

        ///// <summary>
        ///// ERP代发经办
        ///// </summary>
        //public APSALSAVZ APSALSAVZ { get; set; }

        public SYCOMRETZ SYCOMRETZ { get; set; }

        /// <summary>
        /// ERP支付经办撤销
        /// </summary>
        public ERPAYCANZ ERPAYCANZ { get; set; }

        /// <summary>
        /// 批量查询支付状态
        /// </summary>
        [XmlElement("ERPAYSTAZ", IsNullable = false)]
        public List<ERPAYSTAZ> ERPAYSTAZ { get; set; }

        /// <summary>
        /// 收款业务查询
        /// </summary>
        [XmlElement("ERCURDTLZ", IsNullable = false)]
        public List<ERCURDTLZ> ERCURDTLZ { get; set; }

        /// <summary>
        /// 历史数据对接
        /// </summary>
        [XmlElement("EREXPTRSZ", IsNullable = false)]
        public List<EREXPTRSZ> EREXPTRSZ { get; set; }

        /// <summary>
        /// 历史数据（大于1000条该字段值不为空）
        /// </summary>
        public ERDTLSEQZ ERDTLSEQZ { get; set; }
    }

    /// <summary>
    /// ERP代发经办
    /// </summary>
    public class APSALSAVY
    {
        /// <summary>
        /// 业务流水号
        /// </summary>
        public string BUSNBR { get; set; }

        /// <summary>
        /// 明细原序号
        /// </summary>
        public string EXTTX1 { get; set; }

        /// <summary>
        /// 明细最后的序号
        /// </summary>
        public string SQRNBR { get; set; }
    }

    /// <summary>
    /// ERP代发经办
    /// </summary>
    public class APSALSAVZ
    {
        /// <summary>
        /// 业务流水号
        /// </summary>
        public string BUSNBR { get; set; }
    }

    public class INFO
    {
        public string ERPTYP { get; set; }

        /// <summary>
        /// 错误消息
        /// </summary>
        public string ERRMSG { get; set; } = "";

        /// <summary>
        /// 接口名
        /// </summary>
        public string FUNNAM { get; set; }

        /// <summary>
        /// 返回码
        /// </summary>
        public string RETCOD { get; set; }
    }

    /// <summary>
    /// ERP支付经办
    /// </summary>
    public class APPAYSAVZ
    {
        /// <summary>
        /// 业务流水号
        /// </summary>
        public string BUSNBR { get; set; } = "";

        /// <summary>
        /// 错误码，0000000表示成功
        /// </summary>
        public string ERRCOD { get; set; } = "";

        /// <summary>
        /// 错误消息
        /// </summary>
        public string ERRMSG { get; set; } = "";

        /// <summary>
        /// 支付时间
        /// </summary>
        public string PAYTIM { get; set; }

        /// <summary>
        /// 记录号
        /// </summary>
        public string RECNUM { get; set; }

        /// <summary>
        /// 客户参考业务号
        /// </summary>
        public string REFNBR { get; set; }

        /// <summary>
        /// 备注信息
        /// </summary>
        public string REMARK { get; set; }
    }

    public class SYCOMRETZ
    {
        public string ERRCOD { get; set; }

        public string ERRDTL { get; set; }

        public string ERRMSG { get; set; }
    }

    /// <summary>
    /// ERP支付经办撤销
    /// </summary>
    public class ERPAYCANZ
    {
        /// <summary>
        /// 业务流水号
        /// </summary>
        public string BUSNBR { get; set; }

        /// <summary>
        /// 错误码
        /// </summary>
        public string ERRCOD { get; set; } = "";

        /// <summary>
        /// 错误消息
        /// </summary>
        public string ERRMSG { get; set; } = "";

        /// <summary>
        /// 企业业务参考号
        /// </summary>
        public string REFNBR { get; set; }
    }

    /// <summary>
    /// 批量查询支付状态
    /// </summary>
    public class ERPAYSTAZ
    {
        /// <summary>
        /// 业务流水号
        /// </summary>
        public string BUSNBR { get; set; } = "";

        /// <summary>
        /// 企业参考业务号
        /// </summary>
        public string REFNBR { get; set; } = "";

        /// <summary>
        /// 错误码  0000000表示成功，否则表示失败
        /// </summary>
        public string ERRCOD { get; set; } = "";

        /// <summary>
        /// 记录状态 
        /// 0查无此记录（状态可疑），
        /// 1：支付成功，2：支付失败，3：未完成 ，4：银行退票
        /// </summary>
        public string STATUS { get; set; } = "";

        /// <summary>
        /// 业务状态
        /// </summary>
        public string OPTSTU { get; set; }

        ///// <summary>
        ///// 备注信息
        ///// </summary>
        //public string REMARK { get; set; }

        public string CLTACC { get; set; }

        /// <summary>
        /// 摘要
        /// </summary>
        public string EXTTX1 { get; set; }

        /// <summary>
        /// 业务类型
        /// </summary>
        public string OPRTYP { get; set; }

        /// <summary>
        /// 支付时间
        /// </summary>
        public string PAYTIM { get; set; }
    }

    /// <summary>
    /// ERP查询当日明细(ERCURDTL)
    /// </summary>
    public class ERCURDTLZ
    {
        /// <summary>
        /// 金额
        /// </summary>
        public string ACTBAL { get; set; }

        /// <summary>
        /// 账户名称 
        /// </summary>
        public string ACTNAM { get; set; }

        /// <summary>
        /// 银行账号
        /// </summary>
        public string ACTNBR { get; set; } = "";

        /// <summary>
        /// 账户流水号
        /// </summary>
        public string ACTSEQ { get; set; }

        /// <summary>
        /// 银行流水号
        /// </summary>
        public string BNKFLW { get; set; } = "";

        /// <summary>
        /// 银行号
        /// </summary>
        public string BNKNBR { get; set; }

        /// <summary>
        /// 银行交易时间
        /// </summary>
        public string BNKTIM { get; set; } = "";

        /// <summary>
        /// 银行接口类型
        /// </summary>
        public string BNKTYP { get; set; }

        /// <summary>
        /// 币种
        /// </summary>
        public string CCYNBR { get; set; }

        /// <summary>
        /// 32位客户号
        /// </summary>
        public string CLTSEQ { get; set; }

        /// <summary>
        /// 明细流水号
        /// </summary>
        public string DTLSEQ { get; set; } = "";

        /// <summary>
        /// 借贷
        /// </summary>
        public string ITMDIR { get; set; } = "";

        /// <summary>
        /// 用途
        /// </summary>
        public string NUSAGE { get; set; } = "";

        /// <summary>
        /// 原始流水号
        /// </summary>
        public string ORISEQ { get; set; }

        /// <summary>
        /// 对方账号
        /// </summary>
        public string OTHACT { get; set; } = "";

        /// <summary>
        /// 对方户名
        /// </summary>
        public string OTHNAM { get; set; } = "";

        /// <summary>
        /// 对方开户行
        /// </summary>
        public string OTHOPN { get; set; } = "";

        /// <summary>
        /// 支付类型
        /// </summary>
        public string PAYCTG { get; set; }

        /// <summary>
        /// 业务参考号
        /// </summary>
        public string REFCOD { get; set; } = "";

        /// <summary>
        /// 字典翻译后的摘要
        /// </summary>
        public string TRANSM { get; set; }

        /// <summary>
        /// 发生额
        /// </summary>
        public string TRSBAL { get; set; } = "";

        /// <summary>
        /// 交易代码
        /// </summary>
        public string TRSCOD { get; set; }

        /// <summary>
        /// 摘要
        /// </summary>
        public string TXTDSM { get; set; }

        /// <summary>
        /// 更新时间
        /// </summary>
        public string UPDTIM { get; set; }

        /// <summary>
        /// 起息日期
        /// </summary>
        public string VLTDAT { get; set; }
    }

    /// <summary>
    /// 历史明细数据对接
    /// </summary>
    public class EREXPTRSZ
    {
        /// <summary>
        /// 关联内部业务流水号
        /// </summary>
        public string ACMBUS { get; set; }

        /// <summary>
        /// 余额
        /// </summary>
        public string ACTBAL { get; set; }

        /// <summary>
        /// 账户名称
        /// </summary>
        public string ACTNAM { get; set; }

        /// <summary>
        /// 银行账号
        /// </summary>
        public string ACTNBR { get; set; } = "";

        /// <summary>
        /// 账务对方邮箱
        /// </summary>
        public string ATGEML { get; set; }

        /// <summary>
        /// 账务对方全称
        /// </summary>
        public string ATGNAM { get; set; }

        /// <summary>
        /// 累积退款金额 
        /// </summary>
        public string BAUNUM { get; set; }

        /// <summary>
        /// 票据号
        /// </summary>
        public string BILNUM { get; set; }

        /// <summary>
        /// 商品名称
        /// </summary>
        public string BIZNAM { get; set; }

        /// <summary>
        /// 客商名称
        /// </summary>
        public string BMINAM { get; set; }

        /// <summary>
        /// 客商编号
        /// </summary>
        public string BMINBR { get; set; }

        /// <summary>
        /// 银行流水号
        /// </summary>
        public string BNKFLW { get; set; } = "";

        /// <summary>
        /// 银行交易日期时间
        /// </summary>
        public string BNKTIM { get; set; } = "";

        /// <summary>
        /// 银行接口类型
        /// </summary>
        public string BNKTYP { get; set; }

        /// <summary>
        /// 交易总金额 
        /// </summary>
        public string BUTNUM { get; set; }

        /// <summary>
        /// 买家人民币资金账号
        /// </summary>
        public string BUYACC { get; set; }

        /// <summary>
        /// 币种
        /// </summary>
        public string CCYNBR { get; set; }

        /// <summary>
        /// 32位客户号
        /// </summary>
        public string CLTSEQ { get; set; }

        /// <summary>
        /// 当日明细流水号
        /// </summary>
        public string CURSEQ { get; set; }

        /// <summary>
        /// 直连流水号
        /// </summary>
        public string DCCSEQ { get; set; }

        /// <summary>
        /// 流水号 
        /// </summary>
        public string DTLSEQ { get; set; } = "";

        /// <summary>
        /// 结算业务参考号
        /// </summary>
        public string ERPNBR { get; set; }

        /// <summary>
        /// 备注
        /// </summary>
        public string EXTTX2 { get; set; }

        /// <summary>
        /// 交易服务费率
        /// </summary>
        public string FEERAT { get; set; }

        /// <summary>
        /// 企业识别码
        /// </summary>
        public string FRMCOD { get; set; }

        /// <summary>
        /// 母子公司账号
        /// </summary>
        public string GSBACC { get; set; }

        /// <summary>
        /// 母子公司账号
        /// </summary>
        public string GSBBBK { get; set; }

        /// <summary>
        /// 母子公司名称 
        /// </summary>
        public string GSBNAM { get; set; }

        /// <summary>
        /// 借贷
        /// </summary>
        public string ITMDIR { get; set; } = "";

        /// <summary>
        /// 款项性质
        /// </summary>
        public string MONTYP { get; set; }

        /// <summary>
        /// 商户订单号
        /// </summary>
        public string MORDER { get; set; }

        /// <summary>
        /// 充值网银流水号
        /// </summary>
        public string NBNKNO { get; set; }

        /// <summary>
        /// 用途
        /// </summary>
        public string NUSAGE { get; set; } = "";

        /// <summary>
        /// 原始流水号
        /// </summary>
        public string ORISEQ { get; set; }

        /// <summary>
        /// 对方账号
        /// </summary>
        public string OTHACT { get; set; } = "";

        /// <summary>
        /// 对方户名 
        /// </summary>
        public string OTHNAM { get; set; } = "";

        /// <summary>
        /// 对方开户行
        /// </summary>
        public string OTHOPN { get; set; } = "";

        /// <summary>
        /// 支付类型
        /// </summary>
        public string PAYCTG { get; set; }

        /// <summary>
        /// 结算业务流水号
        /// </summary>
        public string PAYNBR { get; set; }

        /// <summary>
        /// 结算方式
        /// </summary>
        public string PAYTYP { get; set; }

        /// <summary>
        /// 附言
        /// </summary>
        public string PSTSCP { get; set; }

        /// <summary>
        /// 协议类型
        /// </summary>
        public string PTCTYP { get; set; }

        /// <summary>
        /// 回单个性化信息
        /// </summary>
        public string RCPINF { get; set; }

        /// <summary>
        /// 参考业务号
        /// </summary>
        public string REFCOD { get; set; } = "";

        /// <summary>
        /// 工行退票原因
        /// </summary>
        public string RFDMSG { get; set; }

        /// <summary>
        /// 关联客户号
        /// </summary>
        public string RLTCLT { get; set; }

        /// <summary>
        /// 卖家人民币资金账号
        /// </summary>
        public string SALACC { get; set; }

        /// <summary>
        /// 卖家姓名 
        /// </summary>
        public string SALNAM { get; set; }

        /// <summary>
        /// 交易服务费 
        /// </summary>
        public string SERFEE { get; set; }

        /// <summary>
        /// 备注1
        /// </summary>
        public string SPCRM1 { get; set; }

        /// <summary>
        /// 备注2
        /// </summary>
        public string SPCRM2 { get; set; }

        /// <summary>
        /// 备注3
        /// </summary>
        public string SPCRM3 { get; set; }

        /// <summary>
        /// 备注4
        /// </summary>
        public string SPCRM4 { get; set; }

        /// <summary>
        /// 订单号
        /// </summary>
        public string TORDER { get; set; }

        /// <summary>
        /// 支付宝交易号
        /// </summary>
        public string TRANUM { get; set; }

        /// <summary>
        /// 发生额 
        /// </summary>
        public string TRSBAL { get; set; } = "";

        /// <summary>
        /// 交易代码
        /// </summary>
        public string TRSCOD { get; set; }

        /// <summary>
        /// 摘要
        /// </summary>
        public string TXTDSM { get; set; }

        /// <summary>
        /// 款项性质说明 
        /// </summary>
        public string TYPNAM { get; set; }

        /// <summary>
        /// 更新时间
        /// </summary>
        public string UPDTIM { get; set; }

        /// <summary>
        /// 记账标志 
        /// </summary>
        public string VCDSTS { get; set; }

        /// <summary>
        /// 起息日 
        /// </summary>
        public string VLTDAT { get; set; }

        /// <summary>
        /// 来源
        /// </summary>
        public string WHRFRM { get; set; }
    }

    /// <summary>
    /// 历史明细数据对接 
    /// 超过1000笔数据则会在输出报文中返回ERDTLSEQZ
    /// 此时应该把ERDTLSEQZ中的DTLSEQ, 连带第一次查询时候的查询条件再次发送到后台，直到ERDTLSEQZ返回为空时，才说明数据全部取完
    /// </summary>
    public class ERDTLSEQZ
    {
        /// <summary>
        /// 流水号
        /// </summary>
        public string DTLSEQ { get; set; } = "0";
    }
}
