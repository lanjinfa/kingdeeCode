using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BOA.YZW.PayAndRec.ServicePlugIn.Helper
{
    public static class ConvertHelper
    {
        /// <summary>
        /// 保留n位小数，并四舍五入(默认2位小数)
        /// </summary>
        /// <param name="number"></param>
        /// <param name="ReservedDigits">保留的小数位</param>
        /// <returns></returns>
        public static decimal GetConvertNumValue(decimal number, int ReservedDigits = 2)
        {
            return Math.Round(number, ReservedDigits, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// 判当前时间，是否在 23:50:00 和 08:00:00 之间 ，在时间区间内不需要调用cbs接口
        /// </summary>
        /// <returns>true = 在时间范围内 ， false = 不在时间范围内</returns>
        public static bool CheckDateTime()
        {
            var dateTimeH = DateTime.Now.Hour;//当前小时
            var dateTimeM = DateTime.Now.Minute;//当前分钟
            if (dateTimeH >= 23 && dateTimeM >= 50)
            {
                return true;
            }
            if (dateTimeH <= 7 && dateTimeM <= 59)
            {
                return true;
            }
            return false;
        }
    }
}
