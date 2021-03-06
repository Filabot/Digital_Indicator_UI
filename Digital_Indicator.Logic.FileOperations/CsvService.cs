﻿using Digital_Indicator.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Digital_Indicator.Logic.FileOperations
{
    public class CsvService : ICsvService
    {
        IFileService _fileService;
        List<DataListXY> DataList { get; }


        public CsvService(IFileService fileService)
        {
            _fileService = fileService;
        }

        public void SaveSettings(HashSet<DataListXY> dataList, string spoolNumber, string batchNumber, string description)
        {
            StringBuilder stringBuilderCsv = new StringBuilder();
            stringBuilderCsv.Append("Timestamp, Diameter,\r\n");

            foreach (DataListXY list in dataList)
            {
                stringBuilderCsv.Append(list.X.ToString() + "," + list.Y.ToString() + ",\r\n");
            }

            string csvString = stringBuilderCsv.ToString();
            csvString = csvString.TrimEnd(','); //remove trailing comma

            if (batchNumber != "0")
            {
                batchNumber = "_Batch" + batchNumber;
            }
            else
            {
                batchNumber = "";
            }

            string fileName = DateTime.Now.Month.ToString("00") + "-" + DateTime.Now.Day.ToString("00") + "-" + DateTime.Now.Year.ToString("0000") +
                "_" + description + "_" + "Spool" + spoolNumber + batchNumber + ".csv";

            _fileService.WriteFile(_fileService.EnvironmentDirectory + @"\" + fileName, csvString.ToString());
        }
    }
}
