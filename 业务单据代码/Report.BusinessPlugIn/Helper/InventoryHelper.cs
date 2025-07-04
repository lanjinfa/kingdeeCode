using Kingdee.BOS;
using Kingdee.BOS.JSON;
using Kingdee.BOS.ServiceFacade.KDServiceFx;
using Kingdee.BOS.WebApi.Client;
using Kingdee.BOS.WebApi.FormService;
using Kingdee.K3.SCM.WebApi.ServicesStub;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BOA.DJJX.Report.BusinessPlugIn.Helper
{
    /// <summary>
    /// 即时库存帮助类
    /// </summary>
    public static class InventoryHelper
    {
        /// <summary>
        /// 获取即时库存相关信息
        /// (注：地址，管理员账号密码，账套标识这三个数据若被修改，这边的数据也要对应的修改)
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="orgNumber">组织编码</param>
        /// <param name="materialNumber">物料编码</param>
        /// <param name="stockNumber">仓库编码</param>
        /// <returns></returns>
        public static InventoryDto GetInventoryInfo(Context context, string orgNumber, string materialNumber, string stockNumber = "")
        {
            var dbId = context.DBId;
            string serverUrl;
            string passWord;
            if (dbId == "611cb4cb19f0f3")//本地测试环境
            {
                serverUrl = "http://121.36.225.84/k3cloud/";
                passWord = "xingna!111";
            }
            else//大金正式环境
            {
                serverUrl = "http://k.takam.com/k3cloud/";
                passWord = "takam@2022";
            }
            var client = new K3CloudApiClient(serverUrl);
            var bLogin = client.Login(dbId, "Administrator", passWord, 2052);
            if (!bLogin)
            {
                return null;
            }
            var jObj = new JSONObject();
            jObj.Add("fstockorgnumbers", orgNumber);//组织编码，多个用,隔开
            jObj.Add("fmaterialnumbers", materialNumber);//物料编码
            if (stockNumber != "")
            {
                jObj.Add("fstocknumbers", stockNumber);//仓库编码
            }
            //jObj.Add("flotnumbers", "");//批号编码
            //jObj.Add("isshowauxprop", true);//是否查询辅助属性，查询辅助属性对性能有影响
            jObj.Add("isshowstockloc", true);//是否查询仓位，查询仓位对性能有影响
            jObj.Add("pageindex", 1);//当前页
            jObj.Add("pagerows", 1000);//每页显示行数
            var result = client.Execute<string>("Kingdee.K3.SCM.WebApi.ServicesStub.InventoryQueryService.GetInventoryData,Kingdee.K3.SCM.WebApi.ServicesStub",
                                           new object[] { jObj.ToString() });
            return JsonConvert.DeserializeObject<InventoryDto>(result);
        }
    }

    /// <summary>
    /// 即时库存接口返回值
    /// </summary>
    public class InventoryDto
    {
        /// <summary>
        /// 总行数
        /// </summary>
        public int RowCount { get; set; }

        /// <summary>
        /// 调用结果
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 信息
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 返回值
        /// </summary>
        public List<InventoryData> Data { get; set; }
    }

    public class InventoryData
    {
        /// <summary>
        /// 即时库存內码
        /// </summary>
        public string FId { get; set; }

        /// <summary>
        /// 库存组织id
        /// </summary>
        public long FStockOrgId { get; set; }

        /// <summary>
        /// 仓库id
        /// </summary>
        public long FStockId { get; set; }

        /// <summary>
        /// 仓位id
        /// </summary>
        public long FStockLocId { get; set; }

        /// <summary>
        /// 基本单位id
        /// </summary>
        public long FBaseUnitId { get; set; }

        /// <summary>
        /// 基本单位数量
        /// </summary>
        public decimal FBaseQty { get; set; } = 0;

        /// <summary>
        /// 辅单位数量
        /// </summary>
        public decimal FSecQty { get; set; } = 0;

        /// <summary>
        /// 物料内码
        /// </summary>
        public long FMaterialId { get; set; }

        /// <summary>
        /// 辅单位
        /// </summary>
        public long FSecUnitId { get; set; }

        /// <summary>
        /// 可用量(基本单位)
        /// </summary>
        public decimal FBaseAvbQty { get; set; } = 0;

        /// <summary>
        /// 预留量(基本单位)
        /// </summary>
        public decimal FBASELOCKQTY { get; set; }
    }
}
