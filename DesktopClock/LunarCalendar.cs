using System;
using System.Collections.Generic;

namespace DesktopClock;

public class LunarCalendar
{
    private static readonly int[] LunarInfo = {
        0x04bd8, 0x04ae0, 0x0a570, 0x054d5, 0x0d260, 0x0d950, 0x16554, 0x056a0, 0x09ad0, 0x055d2, // 1900-1909
        0x04ae0, 0x0a5b6, 0x0a4d0, 0x0d250, 0x1d255, 0x0b540, 0x0d6a0, 0x0ada2, 0x095b0, 0x14977, // 1910-1919
        0x04970, 0x0a4b0, 0x0b4b5, 0x06a50, 0x06d40, 0x1ab54, 0x02b60, 0x09570, 0x052f2, 0x04970, // 1920-1929
        0x06566, 0x0d4a0, 0x0ea50, 0x06e95, 0x05ad0, 0x02b60, 0x186e3, 0x092e0, 0x1c8d7, 0x0c950, // 1930-1939
        0x0d4a0, 0x1d8a6, 0x0b550, 0x056a0, 0x1a5b4, 0x025d0, 0x092d0, 0x0d2b2, 0x0a950, 0x0b557, // 1940-1949
        0x06ca0, 0x0b550, 0x15355, 0x04da0, 0x0a5b0, 0x14573, 0x052b0, 0x0a9a8, 0x0e950, 0x06aa0, // 1950-1959
        0x0aea6, 0x0ab50, 0x04b60, 0x0aae4, 0x0a570, 0x05260, 0x0f263, 0x0d950, 0x05b57, 0x056a0, // 1960-1969
        0x096d0, 0x04dd5, 0x04ad0, 0x0a4d0, 0x0d4d4, 0x0d250, 0x0d558, 0x0b540, 0x0b6a0, 0x195a6, // 1970-1979
        0x095b0, 0x049b0, 0x0a974, 0x0a4b0, 0x0b27a, 0x06a50, 0x06d40, 0x0af46, 0x0ab60, 0x09570, // 1980-1989
        0x04af5, 0x04970, 0x064b0, 0x074a3, 0x0ea50, 0x06b58, 0x05ac0, 0x0ab60, 0x096d5, 0x092e0, // 1990-1999
        0x0c960, 0x0d954, 0x0d4a0, 0x0da50, 0x07552, 0x056a0, 0x0abb7, 0x025d0, 0x092d0, 0x0cab5, // 2000-2009
        0x0a950, 0x0b4a0, 0x0baa4, 0x0ad50, 0x055d9, 0x04ba0, 0x0a5b0, 0x15176, 0x052b0, 0x0a930, // 2010-2019
        0x07954, 0x06aa0, 0x0ad50, 0x05b52, 0x04b60, 0x0a6e6, 0x0a4e0, 0x0d260, 0x0ea65, 0x0d530, // 2020-2029
        0x05aa0, 0x076a3, 0x096d0, 0x04afb, 0x04ad0, 0x0a4d0, 0x1d0b6, 0x0d250, 0x0d520, 0x0dd45, // 2030-2039
        0x0b5a0, 0x056d0, 0x055b2, 0x049b0, 0x0a577, 0x0a4b0, 0x0aa50, 0x1b255, 0x06d20, 0x0ada0, // 2040-2049
        0x14b63, 0x09370, 0x049f8, 0x04970, 0x064b0, 0x168a6, 0x0ea50, 0x06aa0, 0x1a6c4, 0x0aae0, // 2050-2059
        0x092e0, 0x0d2e3, 0x0c960, 0x0d557, 0x0d4a0, 0x0da50, 0x05d55, 0x056a0, 0x0a6d0, 0x055d4, // 2060-2069
        0x052d0, 0x0a9b8, 0x0a950, 0x0b4a0, 0x0b6a6, 0x0ad50, 0x055a0, 0x0aba4, 0x0a5b0, 0x052b0, // 2070-2079
        0x0b273, 0x06930, 0x07337, 0x06aa0, 0x0ad50, 0x14b55, 0x04b60, 0x0a570, 0x054e4, 0x0d160, // 2080-2089
        0x0e968, 0x0d520, 0x0daa0, 0x16aa6, 0x056d0, 0x04ae0, 0x0a9d4, 0x0a4d0, 0x0d150, 0x0f252, // 2090-2099
        0x0d520 // 2100
    };

    private static readonly string[] HeavenlyStems = { "甲", "乙", "丙", "丁", "戊", "己", "庚", "辛", "壬", "癸" };
    private static readonly string[] EarthlyBranches = { "子", "丑", "寅", "卯", "辰", "巳", "午", "未", "申", "酉", "戌", "亥" };
    private static readonly string[] Zodiacs = { "鼠", "牛", "虎", "兔", "龙", "蛇", "马", "羊", "猴", "鸡", "狗", "猪" };
    private static readonly string[] LunarMonthNames = { "正", "二", "三", "四", "五", "六", "七", "八", "九", "十", "冬", "腊" };
    private static readonly string[] LunarDayNames = { "初一", "初二", "初三", "初四", "初五", "初六", "初七", "初八", "初九", "初十",
        "十一", "十二", "十三", "十四", "十五", "十六", "十七", "十八", "十九", "二十",
        "廿一", "廿二", "廿三", "廿四", "廿五", "廿六", "廿七", "廿八", "廿九", "三十" };

    private static readonly string[] SolarTermNames = {
        "小寒", "大寒", "立春", "雨水", "惊蛰", "春分", "清明", "谷雨",
        "立夏", "小满", "芒种", "夏至", "小暑", "大暑", "立秋", "处暑",
        "白露", "秋分", "寒露", "霜降", "立冬", "小雪", "大雪", "冬至"
    };

    private static readonly (int Month, int Day)[] SolarTermApproxDates = {
        (1, 6), (1, 20), (2, 4), (2, 19), (3, 6), (3, 21), (4, 5), (4, 20),
        (5, 6), (5, 21), (6, 6), (6, 21), (7, 7), (7, 23), (8, 7), (8, 23),
        (9, 8), (9, 23), (10, 8), (10, 23), (11, 7), (11, 22), (12, 7), (12, 22)
    };

    private static readonly Dictionary<string, (int Month, int Day)> SolarTerms1901_2100 = new()
    {
        // Key: "yyyyMMdd" compressed to (month, day) lookup
    };

    private const int BaseYear = 1900;
    private static readonly DateTime BaseDate = new(1900, 1, 31);

    public static LunarResult GetLunarInfo(DateTime gregorianDate)
    {
        int offset = (int)(gregorianDate - BaseDate).TotalDays;
        if (offset < 0 || offset > 73350) // 1900-01-31 ~ 2100-12-31 approx
            return new LunarResult { Error = "日期超出范围" };

        int lunarYear = BaseYear;
        int daysInLunarYear = 0;

        // Calculate the lunar year
        while (lunarYear < BaseYear + LunarInfo.Length)
        {
            daysInLunarYear = GetLunarYearDays(lunarYear);
            if (offset < daysInLunarYear)
                break;
            offset -= daysInLunarYear;
            lunarYear++;
        }

        if (lunarYear >= BaseYear + LunarInfo.Length)
            return new LunarResult { Error = "日期超出范围" };

        // Calculate the lunar month
        int leapMonth = GetLeapMonth(lunarYear);
        bool isLeap = false;
        int lunarMonth = 1;
        int daysInMonth = 0;

        // Iterate through months (13 max: 12 regular + 1 leap)
        for (int month = 1; month <= 12; month++)
        {
            daysInMonth = GetLunarMonthDays(lunarYear, month);
            if (offset < daysInMonth)
            {
                lunarMonth = month;
                isLeap = false;
                break;
            }
            offset -= daysInMonth;

            // If this month is the leap month, process the leap month
            if (leapMonth == month)
            {
                int leapDays = GetLeapMonthDays(lunarYear);
                if (offset < leapDays)
                {
                    lunarMonth = month;
                    isLeap = true;
                    daysInMonth = leapDays;
                    break;
                }
                offset -= leapDays;
            }

            lunarMonth = month + 1;
        }

        if (lunarMonth > 12)
        {
            lunarMonth = 12;
            daysInMonth = GetLunarMonthDays(lunarYear, 12);
            if (offset >= daysInMonth)
                offset = daysInMonth - 1;
        }

        int lunarDay = offset + 1;
        int stemBranchIndex = (lunarYear - 4) % 60;
        if (stemBranchIndex < 0) stemBranchIndex += 60;
        int stemIndex = stemBranchIndex % 10;
        int branchIndex = stemBranchIndex % 12;

        string yearName = HeavenlyStems[stemIndex] + EarthlyBranches[branchIndex];
        string zodiac = Zodiacs[(lunarYear - 4) % 12];
        string monthName = (isLeap ? "闰" : "") + LunarMonthNames[lunarMonth - 1] + "月";
        string dayName = lunarDay <= 30 ? LunarDayNames[lunarDay - 1] : lunarDay + "日";

        string holiday = GetHoliday(lunarYear, lunarMonth, lunarDay, isLeap, gregorianDate);
        string solarTerm = GetSolarTerm(gregorianDate);

        return new LunarResult
        {
            Year = lunarYear,
            Month = lunarMonth,
            Day = lunarDay,
            IsLeap = isLeap,
            YearName = yearName,
            Zodiac = zodiac,
            MonthName = monthName,
            DayName = dayName,
            Holiday = holiday,
            SolarTerm = solarTerm,
            FullString = yearName + " " + zodiac + "年 " + monthName + dayName,
            Error = null
        };
    }

    private static int GetLunarYearDays(int year)
    {
        int idx = year - BaseYear;
        if (idx < 0 || idx >= LunarInfo.Length) return 0;
        int sum = 0;
        for (int i = 1; i <= 12; i++)
            sum += GetLunarMonthDays(year, i);
        int leapMonth = GetLeapMonth(year);
        if (leapMonth > 0)
            sum += GetLeapMonthDays(year);
        return sum;
    }

    private static int GetLeapMonth(int year)
    {
        int idx = year - BaseYear;
        if (idx < 0 || idx >= LunarInfo.Length) return 0;
        return LunarInfo[idx] & 0xf;
    }

    private static int GetLunarMonthDays(int year, int month)
    {
        int idx = year - BaseYear;
        if (idx < 0 || idx >= LunarInfo.Length) return 29;
        if (month < 1 || month > 12) return 29;
        return (LunarInfo[idx] & (1 << (month + 3))) != 0 ? 30 : 29;
    }

    private static int GetLeapMonthDays(int year)
    {
        int idx = year - BaseYear;
        if (idx < 0 || idx >= LunarInfo.Length) return 29;
        return (LunarInfo[idx] & 0x10000) != 0 ? 30 : 29;
    }

    private static string GetSolarTerm(DateTime date)
    {
        for (int i = 0; i < SolarTermApproxDates.Length; i++)
        {
            var (m, d) = SolarTermApproxDates[i];
            // Solar terms can vary by ±1 day; check a 3-day window
            var termDate = new DateTime(date.Year, m, d);
            if ((date - termDate).Duration().Days <= 1)
                return SolarTermNames[i];
        }
        return string.Empty;
    }

    private static string GetHoliday(int lunarYear, int lunarMonth, int lunarDay, bool isLeap, DateTime gregorian)
    {
        if (isLeap) return string.Empty;

        if (lunarMonth == 1 && lunarDay == 1) return "春节";
        if (lunarMonth == 1 && lunarDay == 15) return "元宵节";
        if (lunarMonth == 5 && lunarDay == 5) return "端午节";
        if (lunarMonth == 7 && lunarDay == 7) return "七夕节";
        if (lunarMonth == 7 && lunarDay == 15) return "中元节";
        if (lunarMonth == 8 && lunarDay == 15) return "中秋节";
        if (lunarMonth == 9 && lunarDay == 9) return "重阳节";
        if (lunarMonth == 12 && lunarDay == 30 && GetLunarMonthDays(lunarYear, 12) == 30) return "除夕";
        if (lunarMonth == 12 && lunarDay == 29 && GetLunarMonthDays(lunarYear, 12) == 29) return "除夕";

        // Qingming Festival - April 5th (solar calendar)
        if (gregorian.Month == 4 && gregorian.Day == 5) return "清明节";
        // Laba Festival - 12th month, 8th day
        if (lunarMonth == 12 && lunarDay == 8) return "腊八节";
        // Dongzhi (Winter Solstice) - solar term
        if (gregorian.Month == 12 && (gregorian.Day == 21 || gregorian.Day == 22)) return "冬至";

        return string.Empty;
    }
}

public class LunarResult
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int Day { get; set; }
    public bool IsLeap { get; set; }
    public string YearName { get; set; } = string.Empty;
    public string Zodiac { get; set; } = string.Empty;
    public string MonthName { get; set; } = string.Empty;
    public string DayName { get; set; } = string.Empty;
    public string Holiday { get; set; } = string.Empty;
    public string SolarTerm { get; set; } = string.Empty;
    public string FullString { get; set; } = string.Empty;
    public string? Error { get; set; }
    public bool IsValid => Error == null;
}
