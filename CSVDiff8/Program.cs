using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Utf8StringInterpolation;
using ZLogger;
using ZLogger.Providers;

//==
var builder = ConsoleApp.CreateBuilder(args);
builder.ConfigureServices((ctx,services) =>
{
    // Register appconfig.json to IOption<MyConfig>
    services.Configure<MyConfig>(ctx.Configuration);

    // Using Cysharp/ZLogger for logging to file
    services.AddLogging(logging =>
    {
        logging.ClearProviders();
        logging.SetMinimumLevel(LogLevel.Trace);
        var jstTimeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");
        var utcTimeZoneInfo = TimeZoneInfo.Utc;
        logging.AddZLoggerConsole(options =>
        {
//            options.UseJsonFormatter();
            options.UsePlainTextFormatter(formatter => 
            {
                formatter.SetPrefixFormatter($"{0:yyyy-MM-dd'T'HH:mm:sszzz}|{1:short}|", (in MessageTemplate template, in LogInfo info) => template.Format(TimeZoneInfo.ConvertTime(info.Timestamp.Utc, jstTimeZoneInfo), info.LogLevel));
//                formatter.SetSuffixFormatter($" ({0})", (in MessageTemplate template, in LogInfo info) => template.Format(info.Category));
                formatter.SetExceptionFormatter((writer, ex) => Utf8String.Format(writer, $"{ex.Message}"));
            });
        });
        logging.AddZLoggerRollingFile(options =>
        {
//            options.UseJsonFormatter();
            options.UsePlainTextFormatter(formatter => 
            {
                formatter.SetPrefixFormatter($"{0:yyyy-MM-dd'T'HH:mm:sszzz}|{1:short}|", (in MessageTemplate template, in LogInfo info) => template.Format(TimeZoneInfo.ConvertTime(info.Timestamp.Utc, jstTimeZoneInfo), info.LogLevel));
//                formatter.SetSuffixFormatter($" ({0})", (in MessageTemplate template, in LogInfo info) => template.Format(info.Category));
                formatter.SetExceptionFormatter((writer, ex) => Utf8String.Format(writer, $"{ex.Message}"));
            });

            // File name determined by parameters to be rotated
            options.FilePathSelector = (timestamp, sequenceNumber) => $"logs/{timestamp.ToLocalTime():yyyy-MM-dd}_{sequenceNumber:00}.log";
            
            // The period of time for which you want to rotate files at time intervals.
            options.RollingInterval = RollingInterval.Day;
            
            // Limit of size if you want to rotate by file size. (KB)
            options.RollingSizeKB = 1024;        
        });
    });
});

var app = builder.Build();
app.AddCommands<CsvApp>();
app.Run();

public class CsvApp : ConsoleAppBase
{
    readonly ILogger<CsvApp> logger;
    readonly IOptions<MyConfig> config;

    public CsvApp(ILogger<CsvApp> logger,IOptions<MyConfig> config)
    {
        this.logger = logger;
        this.config = config;
    }

    //ReadCsv
    [Command("readcsvfile")]
    public int ReadCsvFile(string incsv1, string incsv2)
    {
        logger.ZLogDebug($"ReadCsvFile|start!");
        if (string.IsNullOrEmpty(incsv1) || string.IsNullOrEmpty(incsv2))
        {
            logger.ZLogError($"Error: arg is NullOrEmpty.");
            return 1;
        }

        //INPUT
        
        //OUTPUT
        List<string> inHeader = new List<string>();
        Dictionary<string, Dictionary<string, string>> inDict1 = new Dictionary<string, Dictionary<string, string>>();
        Dictionary<string, Dictionary<string, string>> inDict2 = new Dictionary<string, Dictionary<string, string>>();


        putCsvToListHeader(incsv2, inHeader);
        for (int i = 0; i < inHeader.Count; i++)
        {
            logger.ZLogTrace($"ReadCsvFile|csv:{incsv2} header:{i+1} = {inHeader[i]}");
        }

        putCsvToDictinayData(incsv1, "拠点番号", inDict1);
        logger.ZLogDebug($"ReadCsvFile|csv:{incsv1} data count = {inDict1.Keys.Count}");
        foreach (var datakey in inDict1.Keys)
        {
            foreach (var key in inDict1[datakey].Keys)
            {
                logger.ZLogTrace($"ReadCsvFile|csv:{incsv1} data:{datakey} {key} = {(inDict1[datakey])[key]}");
            }
        }
        putCsvToDictinayData(incsv2, "拠点番号", inDict2);
        logger.ZLogDebug($"ReadCsvFile|csv:{incsv2} data count = {inDict2.Keys.Count}");
        foreach (var datakey in inDict2.Keys)
        {
            foreach (var key in inDict2[datakey].Keys)
            {
                logger.ZLogTrace($"ReadCsvFile|csv:{incsv2} data:{datakey} {key} = {(inDict2[datakey])[key]}");
            }
        }

        //exec
        ImmutableList<string> listDatakeyPrevious;
        ImmutableList<string> listDatakeyCurrent;
        listDatakeyPrevious = inDict1.Keys.ToImmutableList<string>();
        listDatakeyCurrent = inDict2.Keys.ToImmutableList<string>();
        //OUTPUT
        Dictionary<string, ImmutableList<string>> updateAll = new Dictionary<string, ImmutableList<string>>();

        var addDatakey = listDatakeyCurrent.Except(listDatakeyPrevious);
        var delDatakey = listDatakeyPrevious.Except(listDatakeyCurrent);
        var matchDatakey = listDatakeyPrevious.Intersect(listDatakeyCurrent);
        logger.ZLogDebug($"ReadCsvFile|matchDatakey:{matchDatakey.Count().ToString()} addDatakey:{addDatakey.Count().ToString()} delDatakey:{delDatakey.Count().ToString()}");

        foreach (var datakey in matchDatakey)
        {
            var dictDataPrevious = inDict1[datakey];
            var dictDataCurrent = inDict2[datakey];

            var ret = CSVUtility.CompareDictionaryUpdate(inHeader, dictDataPrevious, dictDataCurrent);
            if (ret.IsMatch == true)
            {
                //NO CHANGE
//                logger.ZLogTrace($"ReadCsvFile|datakey:{datakey} NO CHANGE.");
            }
            else
            {
                //CHANGE!!
//                logger.ZLogDebug($"ReadCsvFile|datakey:{datakey} Change!!");
                ImmutableList<string> updateKeys = ret.updateKeys.ToImmutableList();
                updateAll.Add(datakey, updateKeys);
            }
        }

        //result
        string[] targetColmuns = ["開始日予定","終了日予定","開始日実績","終了日実績"];
        Dictionary<UInt32, Dictionary<string, string>> resultDict = new Dictionary<UInt32, Dictionary<string, string>>();
        UInt32 countKeys = 1;
        foreach (var targetColumn in targetColmuns)
        {
            foreach (var datakey in updateAll.Keys)
            {
                Dictionary<string, string> tmpDict = new Dictionary<string, string>();
                var dictDataPrevious = inDict1[datakey];
                var dictDataCurrent = inDict2[datakey];

                if (updateAll[datakey].Contains(targetColumn))
                {
                    tmpDict.Add("targetColumn", targetColumn);
                    tmpDict.Add("datakey", datakey);
                    tmpDict.Add("previous", dictDataPrevious[targetColumn]);
                    tmpDict.Add("current", dictDataCurrent[targetColumn]);
                    resultDict.Add(countKeys, tmpDict);
                    countKeys++;
                }
            }
        }

        //result[column]
        logger.ZLogInformation($"==result[column]==");
        logger.ZLogInformation($"csv1:{incsv1}");
        logger.ZLogInformation($"csv2:{incsv2}");
        foreach (var targetColumn in targetColmuns)
        {
            logger.ZLogInformation($"column:{targetColumn}");
            Boolean isNotContains = true;
            foreach (var key in resultDict.Keys)
            {
                if (resultDict[key].ContainsValue(targetColumn))
                {
                    isNotContains = false;
                    logger.ZLogInformation($"  datakey:{(resultDict[key])["datakey"]} {(resultDict[key])["previous"]} => {(resultDict[key])["current"]}");
                }
                else
                {
                }
            }

            if (isNotContains)
            {
                logger.ZLogInformation($"  NO CHANGE.");
            }
        }

        //result[datakey]
        logger.ZLogInformation($"==result[datakey]==");
        logger.ZLogInformation($"csv1:{incsv1}");
        logger.ZLogInformation($"csv2:{incsv2}");
        foreach (var datakey in updateAll.Keys)
        {
            Boolean isContains = false;
            foreach (var targetColumn in targetColmuns)
            {
                if (updateAll[datakey].Contains(targetColumn))
                {
                    isContains = true;
                }
            }

            if (isContains)
            {
                logger.ZLogInformation($"datakey:{datakey}");
                foreach (var targetColumn in targetColmuns)
                {
                    if (updateAll[datakey].Contains(targetColumn))
                    {
                        var dictDataPrevious = inDict1[datakey];
                        var dictDataCurrent = inDict2[datakey];

                        if (updateAll[datakey].Contains(targetColumn))
                        {
                            logger.ZLogInformation($"  column:{targetColumn} {dictDataPrevious[targetColumn]} => {dictDataCurrent[targetColumn]}");
                        }
                    }
                }
            }
        }


        logger.ZLogDebug($"ReadCsvFile|end!");
        return 0;
    }

/*
    //ReadCsv backup
    [Command("readcsvfile")]
    public int ReadCsvFile(string incsv)
    {
        logger.ZLogDebug($"ReadCsvFile|start!");
        if (string.IsNullOrEmpty(incsv))
        {
            logger.ZLogError($"Error: arg is NullOrEmpty.");
            return 1;
        }

        var readCsvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            Delimiter = ",",
            NewLine = "\r\n",
            Encoding = Encoding.UTF8,
            DetectColumnCountChanges = true,
        };

        string[] headers = {};
        UInt32 currentRowPosition = 0;
        using (var csvStreamReader = new StreamReader(incsv, Encoding.UTF8))
        using (var csvReader = new CsvReader(csvStreamReader, readCsvConfig))
        {
            // header : true/false
            if (true)
            {
                csvReader.Read();
                csvReader.ReadHeader();
                headers = csvReader.HeaderRecord;
                for (int i = 0; i < headers.Length; i++)
                {
                    logger.ZLogDebug($"ReadCsvFile|header colmun{i+1} = {headers[i]}");
                }
            }

            while (csvReader.Read())
            {
                currentRowPosition++;
                string[] record = csvReader.Parser.Record;
                for (int i = 0; i < record.Length; i++)
                {
                    logger.ZLogDebug($"ReadCsvFile|data row{currentRowPosition} colmun{i+1} = {record[i]}");
                }
            }


        }

        logger.ZLogDebug($"ReadCsvFile|end!");
        return 0;
    }
*/


    private int putCsvToListHeader(string incsv, List<string> outlist)
    {
        var readCsvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            Delimiter = ",",
            NewLine = "\r\n",
            Encoding = Encoding.UTF8,
            DetectColumnCountChanges = true,
        };

        string[] headers = {};
        using (var csvStreamReader = new StreamReader(incsv, Encoding.UTF8))
        using (var csvReader = new CsvReader(csvStreamReader, readCsvConfig))
        {
            // header : true/false
            if (true)
            {
                csvReader.Read();
                csvReader.ReadHeader();
                headers = csvReader.HeaderRecord;
                for (int i = 0; i < headers.Length; i++)
                {
                    outlist.Add(headers[i]);
                }
            }
        }

        return 0;
    }


    private int putCsvToDictinayData(string incsv, string incolumn, Dictionary<string, Dictionary<string, string>> outdict)
    {
        var readCsvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            Delimiter = ",",
            NewLine = "\r\n",
            Encoding = Encoding.UTF8,
            DetectColumnCountChanges = true,
        };

        string[] headers = {};
        string key_value = "DEFAULT";
        UInt32 currentRowPosition = 0;
        using (var csvStreamReader = new StreamReader(incsv, Encoding.UTF8))
        using (var csvReader = new CsvReader(csvStreamReader, readCsvConfig))
        {
            // header : true/false
            if (true)
            {
                csvReader.Read();
                csvReader.ReadHeader();
                headers = csvReader.HeaderRecord;
//                for (int i = 0; i < headers.Length; i++)
//                {
//                    logger.ZLogDebug($"putCsvToDictionay|csv{incsv} header colmun{i+1} = {headers[i]}");
//                }
            }

            while (csvReader.Read())
            {
                currentRowPosition++;
                Dictionary<string, string> tmpDict = new Dictionary<string, string>();
                string[] record = csvReader.Parser.Record;
                for (int i = 0; i < record.Length; i++)
                {
//                    logger.ZLogDebug($"putCsvToDictionay|csv{incsv} data row{currentRowPosition} colmun{i+1} = {record[i]}");
                    string header = headers[i].ToString();
                    string value = record[i].ToString();
                    tmpDict.Add(header, value);
                    if (incolumn.Equals(header))
                    {
                        key_value = value;
                    }
                }
                outdict.Add(key_value ,tmpDict);
            }


        }
        if (key_value.Equals("DEFAULT"))
        {
            return -1;
        }
        return 0;
    }

/*
//== Sample
    public void Echo(string msg, int repeat = 3)
    {
        for (var i = 0; i < repeat; i++)
        {
            logger.ZLogDebug($"Echo|test");
            logger.ZLogTrace($"msg:{msg}");
        }
    }

    public void Sum([Option(0)]int x, [Option(1)]int y)
    {
        Console.WriteLine((x + y).ToString());
    }
*/
}

//==
public class MyConfig
{
    public string Mode {get; set;} = "DEFAULT";
}
