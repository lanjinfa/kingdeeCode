using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BOA.SBT.Report.PlugIns.Helper
{
    /// <summary>
    /// 数值转换
    /// </summary>
    public static class ConvertHelper
    {
        /// <summary>
        /// 保留n位小数，并四舍五入
        /// </summary>
        /// <param name="number"></param>
        /// <param name="ReservedDigits">保留的小数位</param>
        /// <returns></returns>
        public static decimal GetConvertNumValue(decimal number, int ReservedDigits = 2)
        {
            return Math.Round(number, ReservedDigits, MidpointRounding.AwayFromZero);
        }
    }
}
