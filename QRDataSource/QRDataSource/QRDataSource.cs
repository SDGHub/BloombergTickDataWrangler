using System;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;

namespace QRDataSource
{

    public class QRDataSource
    {
        public string Name { get { return name; } }
        public string DSPath
        {
            get { return dsPath; }
            set
            {
                dsPath = value;
                name = Path.GetFileNameWithoutExtension(dsPath);
                sqlDbName = Name.Trim().Replace(' ', '_');
                sqlDbLogName = sqlDbName + "_Log";
                string basePath = Path.GetDirectoryName(dsPath) + "\\";
                sqlDbPath = basePath + sqlDbName + ".mdf";
                sqlDbLogPath = basePath + sqlDbName + "_log.ldf";
            }
        }
        public DateTime StartDate { get; set; }

        public string sqlConnectionString { get; set; }
        SqlConnection cn = null;

        public bool initialized = false;

        private string name;
        private string dsPath;
        private string sqlDbName;
        private string sqlDbLogName;
        private string sqlDbPath;
        private string sqlDbLogPath;

        public enum TickDataType { TRADE, ASK, BID }

        public QRDataSource()
        {
        }

        #region SQL connection

        public bool checkSQLConnString()
        {
            if (sqlConnectionString == string.Empty)
                return false;

            return checkSQLConnString(sqlConnectionString);
        }

        public bool checkSQLConnString(string sqlConnString)
        {
            using (SqlConnection testConn = new SqlConnection(sqlConnString))
            {
                try
                {
                    testConn.Open();
                    testConn.Close();
                }
                catch (SqlException e)
                {
                    Debug.Print("SQL Error: {0}", e.Message);
                    return false;
                }
            }
            return true;
        }

        public bool getSQLconnection()
        {
            if (string.IsNullOrEmpty(sqlConnectionString))
            {
                return false;
            }
            else
            {
                try
                {
                    cn = new SqlConnection(sqlConnectionString);
                    cn.Open();
                    cn.Close();
                }
                catch (SqlException e)
                {
                    Debug.Print("SQL Error: {0}", e.Message);
                    return false;
                }
            }
            return true;
        }

        private string getSQLConnStringFromFile()
        {
            // returns the SQL connection string if it can be found.
            // otherwise it returns an empty string
            string connString = string.Empty;
            try
            {
                using (StreamReader sr = new StreamReader("SQLConnectionString.txt"))
                {
                    connString = sr.ReadToEnd();
                }
            }
            catch (Exception)
            {
                Debug.Print("Unable to read/find SQL connection file.");
            }

            return connString;
        }

        #endregion

        public bool createDataBase()
        {
            bool successful = false;
            string script = getSQLQuantixDbCreateScriptfromFile();

            if (String.IsNullOrEmpty(script))
            {
                Debug.Print("Empty database creation script");
                return successful;
            }
            else
            {
                using (cn = new SqlConnection(sqlConnectionString))
                {
                    try
                    {
                        cn.Open();
                        Server server = new Server(new ServerConnection(cn));
                        server.ConnectionContext.ExecuteNonQuery(script);
                        cn.Close();
                    }
                    catch (Exception e)
                    {
                        Debug.Print("SQL Error during SQL DB create: {0}", e.Message);
                        return successful;
                    }

                    if (File.Exists(dsPath))
                        File.Delete(dsPath);
                    using (StreamWriter sw = new StreamWriter(dsPath))
                    {
                        sw.Write(getDSInitString());
                    }

                    sqlConnectionString = sqlConnectionString.Replace("master", sqlDbName);
                    successful = true;
                    initialized = true;
                }
            }
            return successful;
        }

        public void SaveDS(string dsPath)
        {
            if (File.Exists(dsPath))
                File.Delete(dsPath);
            using (StreamWriter sw = new StreamWriter(dsPath))
            {
                sw.Write(getDSInitString());
            };
        }

        private string getDSInitString()
        {
            string s = "dsPtyName|" + Name + "\r\n";
            s += "dsPtyPath|" + dsPath + "\r\n";
            s += "dsPtySqlDbName|" + sqlDbName + "\r\n";
            s += "dsPtySqlDbLogName|" + sqlDbLogName + "\r\n";
            s += "dsPtySqlDbPath|" + sqlDbPath + "\r\n";
            s += "dsPtySqlDbLogPath|" + sqlDbLogPath + "\r\n";
            s += "dsPtySqlConnectionString|" + sqlConnectionString.Replace("master", sqlDbName) + "\r\n";
            s += "dsPtyStartDate|" + StartDate + "\r\n";

            return s;
        }

        private string getSQLQuantixDbCreateScriptfromFile()
        {
            // returns the SQL database creation script if it can be found.
            // otherwise, it returns an empty string

            string dbReplaceNameTarget = "dbFileName_PlaceHolder";
            string dbReplacePathTarget = "dbFilePath_PlaceHolder";
            string dbLogReplaceNameTarget = "dbLogFileName_PlaceHolder";
            string dbLogReplacePathTarget = "dbLogFilePath_PlaceHolder";

            string dbCreatScript = string.Empty;

            string filePath = @"SQLAdapterTemplate.txt";
            //string filePath = "QuantixDbCreateScript.txt";
            try
            {
                using (StreamReader sr = new StreamReader(filePath))
                {
                    dbCreatScript = sr.ReadToEnd();
                }
            }
            catch (Exception)
            {
                Debug.Print("Unable to read/find SQL connection file.");
                return dbCreatScript;
            }

            string temp1 = dbCreatScript.Replace(dbReplaceNameTarget, sqlDbName);
            string temp2 = temp1.Replace(dbReplacePathTarget, sqlDbPath);
            string temp3 = temp2.Replace(dbLogReplaceNameTarget, sqlDbLogName);
            dbCreatScript = temp3.Replace(dbLogReplacePathTarget, sqlDbLogPath);

            using (StreamWriter sw = new StreamWriter(@"FinalDbCreateScript.txt"))
            {
                sw.Write(dbCreatScript);
            }

            return dbCreatScript;
        }

        public void loadDataSource(string dsPath)
        {
            using (StreamReader sr = new StreamReader(dsPath))
            {
                int cnt = 0;
                while (!sr.EndOfStream)
                {
                    string[] line = sr.ReadLine().Split('|');
                    switch (line[0])
                    {
                        case "dsPtyName":
                            name = @line[1];
                            cnt++;
                            break;
                        case "dsPtyPath":
                            dsPath = @line[1];
                            cnt++;
                            break;
                        case "dsPtySqlDbName":
                            sqlDbName = line[1];
                            cnt++;
                            break;
                        case "dsPtySqlDbLogName":
                            sqlDbLogName = line[1];
                            cnt++;
                            break;
                        case "dsPtySqlDbPath":
                            sqlDbPath = @line[1];
                            cnt++;
                            break;
                        case "dsPtySqlDbLogPath":
                            sqlDbLogPath = @line[1];
                            cnt++;
                            break;
                        case "dsPtySqlConnectionString":
                            sqlConnectionString = line[1];
                            cnt++;
                            break;
                        case "dsPtyStartDate":
                            StartDate = Convert.ToDateTime(line[1].ToString());
                            cnt++;
                            break;
                        default:
                            break;
                    }
                }

                if (cnt == 8)
                {
                    initialized = true;
                }
                else
                {
                    initialized = false;
                }
            }
        }

        #region SQL Interface

        public DataTable getDailyBarsToUpdate(DateTime updateToDt)
        {
            DataTable dt = new DataTable();
            if (getSQLconnection())
            {
                SqlCommand cmd = cn.CreateCommand();
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = "GetSymbolsToUpdate";

                SqlParameter DSStartDate = new SqlParameter("@DSStartDate", SqlDbType.SmallDateTime);
                DSStartDate.Value = StartDate;
                cmd.Parameters.Add(DSStartDate);

                SqlParameter UpdateToDate = new SqlParameter("@UpdateToDate", SqlDbType.SmallDateTime);
                UpdateToDate.Value = updateToDt;
                cmd.Parameters.Add(UpdateToDate);

                cn.Open();
                SqlDataAdapter sa = new SqlDataAdapter(cmd);
                sa.Fill(dt);
                cn.Close();
            }

            return dt;
        }

        public DataTable getDateTimesOfSymbolSeries(string seriesName, string symbol)
        {
            short SeriesID = GetSeriesID(seriesName);
            int SymbolID = GetSymbolID(symbol);
            DateTime endDate = DateTime.Now.Date.AddDays(1);
            return getDateTimesOfSymbolSeries(SeriesID, SymbolID, StartDate, endDate);
        }

        public DataTable getDateTimesOfSymbolSeries(short seriesID, int symbolID)
        {
            DateTime endDate = DateTime.Now.Date.AddDays(1);
            return getDateTimesOfSymbolSeries(seriesID, symbolID, StartDate, endDate);
        }

        public DataTable getDateTimesOfSymbolSeries(string seriesName, string symbol, DateTime start, DateTime end)
        {
            short SeriesID = GetSeriesID(seriesName);
            int SymbolID = GetSymbolID(symbol);
            return getDateTimesOfSymbolSeries(SeriesID, SymbolID, start, end);

        }

        public DataTable getDateTimesOfSymbolSeries(short seriesID, int symbolID, DateTime start, DateTime end)
        {
            DataTable dt = new DataTable();
            if (getSQLconnection())
            {
                SqlCommand cmd = cn.CreateCommand();
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = "GetDateTimesOfSeriesBetweenDates";

                SqlParameter DSStartDate = new SqlParameter("@StartDate", SqlDbType.SmallDateTime);
                DSStartDate.Value = start;
                cmd.Parameters.Add(DSStartDate);

                SqlParameter DSEndDate = new SqlParameter("@EndDate", SqlDbType.SmallDateTime);
                DSEndDate.Value = end;
                cmd.Parameters.Add(DSEndDate);

                SqlParameter IDPrm = new SqlParameter("@SeriesID", SqlDbType.SmallInt);
                IDPrm.Value = seriesID;
                cmd.Parameters.Add(IDPrm);

                SqlParameter SymbolPrm = new SqlParameter("@SymbolID", SqlDbType.SmallInt);
                SymbolPrm.Value = symbolID;
                cmd.Parameters.Add(SymbolPrm);

                cn.Open();
                SqlDataAdapter sa = new SqlDataAdapter(cmd);
                sa.Fill(dt);
                cn.Close();
            }

            return dt;
        }

        public DataTable getSymbolsOfSeries(string seriesName)
        {
            short SeriesID = GetSeriesID(seriesName);
            return getSymbolsOfSeries(SeriesID);
        }

        public DataTable getSymbolsOfSeries(short seriesID)
        {
            DataTable dt = new DataTable();
            if (getSQLconnection())
            {
                SqlCommand cmd = cn.CreateCommand();
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = "GetSymbolsOfSeries";

                SqlParameter IDPrm = new SqlParameter("@SeriesID", SqlDbType.SmallInt);
                IDPrm.Value = seriesID;
                cmd.Parameters.Add(IDPrm);

                cn.Open();
                SqlDataAdapter sa = new SqlDataAdapter(cmd);
                sa.Fill(dt);
                cn.Close();
            }

            return dt;
        }

        public bool DeleteSeries(string SeriesName, string Symbol)
        {
            bool Result = false;

            short SeriesID = GetSeriesID(SeriesName);
            int SymbolID = GetSymbolID(Symbol);

            if ((SymbolID > 0) && (SeriesID > 0))
            {
                SqlCommand cmd3 = cn.CreateCommand();
                cmd3.CommandType = CommandType.StoredProcedure;
                cmd3.CommandText = "DeleteSeriesSymbol";

                SqlParameter ID3Prm = new SqlParameter("@SymbolID", SqlDbType.SmallInt);
                ID3Prm.Value = SymbolID;
                cmd3.Parameters.Add(ID3Prm);

                SqlParameter ID4Prm = new SqlParameter("@SeriesID", SqlDbType.SmallInt);
                ID4Prm.Value = SeriesID;
                cmd3.Parameters.Add(ID4Prm);

                cn.Open();
                cmd3.ExecuteNonQuery();
                cn.Close();

                Result = true;
            }
            return Result;
        }

        public bool DeleteSeriesAllSymbols(string SeriesName)
        {
            bool Result = false;

            SqlCommand cmd1 = cn.CreateCommand();
            cmd1.CommandType = CommandType.StoredProcedure;
            cmd1.CommandText = "GetSeriesID";

            SqlParameter NamePrm = new SqlParameter("@Name", SqlDbType.VarChar);
            NamePrm.Value = SeriesName;
            cmd1.Parameters.Add(NamePrm);

            SqlParameter ID1Prm = new SqlParameter("@ID", SqlDbType.SmallInt);
            ID1Prm.Direction = ParameterDirection.Output;
            cmd1.Parameters.Add(ID1Prm);

            cn.Open();
            cmd1.ExecuteNonQuery();
            cn.Close();

            if (ID1Prm.Value != DBNull.Value)
            {
                SqlCommand cmd2 = cn.CreateCommand();
                cmd2.CommandType = CommandType.StoredProcedure;
                cmd2.CommandText = "DeleteSeries";

                SqlParameter ID2Prm = new SqlParameter("@ID", SqlDbType.SmallInt);
                ID2Prm.Value = ID1Prm.Value;
                cmd2.Parameters.Add(ID2Prm);

                cn.Open();
                cmd2.ExecuteNonQuery();
                cn.Close();

                Result = true;
            }
            return Result;
        }

        public byte TimeSeriesExists(short SeriesID, int SymbolID)
        {
            byte Result = 0;

            SqlCommand cmd1 = cn.CreateCommand();
            cmd1.CommandType = CommandType.StoredProcedure;
            cmd1.CommandText = "TimeSeriesExists";

            SqlParameter SeriesIDPrm = new SqlParameter("@SeriesID", SqlDbType.SmallInt);
            SeriesIDPrm.Value = SeriesID;
            cmd1.Parameters.Add(SeriesIDPrm);

            SqlParameter SymbolIDPrm = new SqlParameter("@SymbolID", SqlDbType.SmallInt);
            SymbolIDPrm.Value = SymbolID;
            cmd1.Parameters.Add(SymbolIDPrm);

            SqlParameter ExistsPrm = new SqlParameter("@Exists", SqlDbType.TinyInt);
            ExistsPrm.Direction = ParameterDirection.Output;
            cmd1.Parameters.Add(ExistsPrm);

            cn.Open();
            cmd1.ExecuteNonQuery();
            cn.Close();

            if (ExistsPrm.Value != DBNull.Value)
            {
                Result = (byte)ExistsPrm.Value;
            }

            return Result;
        }

        public DateTime GetLastDateTime(string SeriesName, string Symbol)
        {
            short SeriesID = GetSeriesID(SeriesName);
            int SymbolID = GetSymbolID(Symbol);
            return GetLastDateTime(SeriesID, SymbolID);
        }

        public DateTime GetLastDateTime(int SeriesID, int SymbolID)
        {
            DateTime Result;

            SqlCommand cmd1 = cn.CreateCommand();
            cmd1.CommandType = CommandType.StoredProcedure;
            cmd1.CommandText = "GetLastDateTime";

            SqlParameter SeriesIDPrm = new SqlParameter("@SeriesID", SqlDbType.SmallInt);
            SeriesIDPrm.Value = SeriesID;
            cmd1.Parameters.Add(SeriesIDPrm);

            SqlParameter SymbolIDPrm = new SqlParameter("@SymbolID", SqlDbType.SmallInt);
            SymbolIDPrm.Value = SymbolID;
            cmd1.Parameters.Add(SymbolIDPrm);

            SqlParameter ExistsPrm = new SqlParameter("@MaxDate", SqlDbType.DateTime);
            ExistsPrm.Direction = ParameterDirection.Output;
            cmd1.Parameters.Add(ExistsPrm);

            cn.Open();
            cmd1.ExecuteNonQuery();
            cn.Close();

            if (ExistsPrm.Value != DBNull.Value)
            {
                //debug(ExistsPrm.Value.ToString());
                Result = (DateTime)ExistsPrm.Value;
                //debug("GetLastDateTime: return");
            }
            else
                Result = DateTime.MinValue;

            return Result;
        }

        public DateTime GetFirstDateTimeTickData(string Symbol)
        {
            int symbolId = GetSymbolID(Symbol.Trim());
            if (symbolId == 0)
            {
                Console.WriteLine("No such symbol! GetSymbol() returned " + symbolId + ", " + Symbol);
                return DateTime.MaxValue;
            }
            else
            {
                return GetFirstDateTimeTickData(symbolId);
            }
        }

        public DateTime GetFirstDateTimeTickData(int SymbolID)
        {
            DateTime Result;

            SqlCommand cmd1 = cn.CreateCommand();
            cmd1.CommandType = CommandType.StoredProcedure;
            cmd1.CommandText = "GetFirstDateTimeTickData";

            SqlParameter SymbolIDPrm = new SqlParameter("@SymbolID", SqlDbType.SmallInt);
            SymbolIDPrm.Value = SymbolID;
            cmd1.Parameters.Add(SymbolIDPrm);

            SqlParameter ExistsPrm = new SqlParameter("@MaxDate", SqlDbType.DateTime);
            ExistsPrm.Direction = ParameterDirection.Output;
            cmd1.Parameters.Add(ExistsPrm);

            cn.Open();
            cmd1.ExecuteNonQuery();
            cn.Close();

            if (ExistsPrm.Value != DBNull.Value)
            {
                //debug(ExistsPrm.Value.ToString());
                Result = (DateTime)ExistsPrm.Value;
                //debug("GetLastDateTime: return");
            }
            else
                Result = DateTime.MaxValue;

            return Result;
        }

        public DateTime GetLastDateTimeTickData(string Symbol)
        {
            int symbolId = GetSymbolID(Symbol.Trim());
            if (symbolId == 0)
            {
                Console.WriteLine("No such symbol! GetSymbol() returned " + symbolId + ", " + Symbol);
                return DateTime.MinValue;
            }
            else
            {
                return GetLastDateTimeTickData(symbolId);
            }
        }

        public DateTime GetLastDateTimeTickData(int SymbolID)
        {
            DateTime Result;

            SqlCommand cmd1 = cn.CreateCommand();
            cmd1.CommandType = CommandType.StoredProcedure;
            cmd1.CommandText = "GetLastDateTimeTickData";

            SqlParameter SymbolIDPrm = new SqlParameter("@SymbolID", SqlDbType.SmallInt);
            SymbolIDPrm.Value = SymbolID;
            cmd1.Parameters.Add(SymbolIDPrm);

            SqlParameter ExistsPrm = new SqlParameter("@MaxDate", SqlDbType.DateTime);
            ExistsPrm.Direction = ParameterDirection.Output;
            cmd1.Parameters.Add(ExistsPrm);

            cn.Open();
            cmd1.ExecuteNonQuery();
            cn.Close();

            if (ExistsPrm.Value != DBNull.Value)
            {
                //debug(ExistsPrm.Value.ToString());
                Result = (DateTime)ExistsPrm.Value;
                //debug("GetLastDateTime: return");
            }
            else
                Result = DateTime.MinValue;

            return Result;
        }

        public DateTime GetFAAnnounceDate(string Symbol, byte fPerType, byte fPer, int fYear, bool Consolidated)
        {
            if (!getSQLconnection())
            {
                return DateTime.MinValue;
            }
            else
            {
                int symbolId = GetSymbolID(Symbol.Trim());
                if (symbolId == 0)
                {
                    Debug.Print("No such symbol! GetSymbol returned " + symbolId + ", " + Symbol);
                    return DateTime.MinValue;
                }

                return GetFAAnnounceDate(symbolId, fPerType, fPer, fYear, Consolidated);
            }
        }

        public DateTime GetFADate(string Symbol, byte fPerType, byte fPer, int fYear, bool Consolidated)
        {
            if (!getSQLconnection())
            {
                return DateTime.MinValue;
            }
            else
            {
                int symbolId = GetSymbolID(Symbol.Trim());
                if (symbolId == 0)
                {
                    Debug.Print("No such symbol! GetSymbol returned " + symbolId + ", " + Symbol);
                    return DateTime.MinValue;
                }

                return GetFAAnnounceDate(symbolId, fPerType, fPer, fYear, Consolidated);
            }
        }

        public bool DeleteSeriesIfExists(string SeriesName, string Symbol)
        {
            bool Result = false;

            short SeriesID = GetSeriesID(SeriesName);
            int SymbolID = GetSymbolID(Symbol);
            byte Exists = TimeSeriesExists(SeriesID, SymbolID);

            if (Exists <= 0)
            {
                DeleteSeries(SeriesName, Symbol);
                Result = true;
            }
            return Result;
        }

        public bool InsertSeriesRow(string SeriesName, string Symbol, DateTime date, double value)
        {
            bool Result = false;

            int SymbolID = GetSymbolID(Symbol);
            if (SymbolID <= 0)
            {
                SymbolID = InsertSymbol(Symbol);
            }

            short SeriesID = GetSeriesID(SeriesName);
            if (SeriesID <= 0)
            {
                SeriesID = InsertSeriesName(SeriesName);
            }

            SqlCommand cmd = cn.CreateCommand();
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = "InsertDateFloat";
            SqlParameter IDPrm = new SqlParameter("@SeriesID", SqlDbType.SmallInt);
            IDPrm.Value = SeriesID;
            cmd.Parameters.Add(IDPrm);

            SqlParameter SymbolPrm = new SqlParameter("@SymbolID", SqlDbType.SmallInt);
            SymbolPrm.Value = SymbolID;
            cmd.Parameters.Add(SymbolPrm);

            SqlParameter DatePrm = new SqlParameter("@Date", SqlDbType.SmallDateTime);
            DatePrm.Value = date;
            cmd.Parameters.Add(DatePrm);

            SqlParameter ValuePrm = new SqlParameter("@Value", SqlDbType.Real);
            ValuePrm.Value = value;
            cmd.Parameters.Add(ValuePrm);

            cn.Open();
            cmd.ExecuteNonQuery();
            cn.Close();

            return Result;
        }

        public bool InsertSymbolWithFullInfo(string Symbol, string SymbolName, string crncy, string country, string Sector1Name,
            string Sector2Name, string Sector3Name, string Sector4Name, int[] sectorCode, int SecType, string MktStatus, DateTime LastUpdtDt,
            DateTime LastAdjDT, DateTime LastCalcDT, Int16 PX_TRADE_LOT_SIZE, string FUT_CUR_GEN_TICKER, double FUT_CONT_SIZE, double FUT_VAL_PT, double FUT_TICK_SIZE,
            DateTime FUT_DLV_DT_FIRST, double OPT_TICK_SIZE, double OPT_CONT_SIZE, double OPT_STRIKE_PX, string OPT_UNDL_TICKER, string OPT_PUT_CALL, DateTime OPT_EXPIRE_DT)
        {
            if (!getSQLconnection())
            {
                return false;
            }
            else
            {
                SqlCommand cmd1 = cn.CreateCommand();
                cmd1.CommandType = CommandType.StoredProcedure;
                cmd1.CommandText = "InsertSymbolFull";

                SqlParameter pSymbol = new SqlParameter("@Symbol", SqlDbType.VarChar);
                pSymbol.Value = Symbol;
                cmd1.Parameters.Add(pSymbol);

                SqlParameter pSymbolName = new SqlParameter("@SymbolName", SqlDbType.VarChar);
                pSymbolName.Value = SymbolName;
                cmd1.Parameters.Add(pSymbolName);

                SqlParameter pCrncy = new SqlParameter("@crncy", SqlDbType.VarChar);
                pCrncy.Value = crncy;
                cmd1.Parameters.Add(pCrncy);

                SqlParameter pCountry = new SqlParameter("@country", SqlDbType.VarChar);
                pCountry.Value = country;
                cmd1.Parameters.Add(pCountry);

                SqlParameter pSector1Name = new SqlParameter("@Sector1Name", SqlDbType.VarChar);
                if (SecType != 1)
                    pSector1Name.Value = DBNull.Value;
                else
                    pSector1Name.Value = Sector1Name;
                cmd1.Parameters.Add(pSector1Name);

                SqlParameter pSector2Name = new SqlParameter("@Sector2Name", SqlDbType.VarChar);
                if (SecType != 1)
                    pSector2Name.Value = DBNull.Value;
                else
                    pSector2Name.Value = Sector2Name;
                cmd1.Parameters.Add(pSector2Name);

                SqlParameter pSector3Name = new SqlParameter("@Sector3Name", SqlDbType.VarChar);
                if (SecType != 1)
                    pSector3Name.Value = DBNull.Value;
                else
                    pSector3Name.Value = Sector3Name;
                cmd1.Parameters.Add(pSector3Name);

                SqlParameter pSector4Name = new SqlParameter("@Sector4Name", SqlDbType.VarChar);
                if (SecType != 1)
                    pSector4Name.Value = DBNull.Value;
                else
                    pSector4Name.Value = Sector4Name;
                cmd1.Parameters.Add(pSector4Name);

                SqlParameter pSector1 = new SqlParameter("@Sector1", SqlDbType.SmallInt);
                if (SecType != 1)
                    pSector1.Value = DBNull.Value;
                else
                    pSector1.Value = sectorCode[0];
                cmd1.Parameters.Add(pSector1);

                SqlParameter pSector2 = new SqlParameter("@Sector2", SqlDbType.SmallInt);
                if (SecType != 1)
                    pSector2.Value = DBNull.Value;
                else
                    pSector2.Value = sectorCode[1];
                cmd1.Parameters.Add(pSector2);

                SqlParameter pSector3 = new SqlParameter("@Sector3", SqlDbType.SmallInt);
                if (SecType != 1)
                    pSector3.Value = DBNull.Value;
                else
                    pSector3.Value = sectorCode[2];
                cmd1.Parameters.Add(pSector3);

                SqlParameter pSector4 = new SqlParameter("@Sector4", SqlDbType.SmallInt);
                if (SecType != 1)
                    pSector4.Value = DBNull.Value;
                else
                    pSector4.Value = sectorCode[3];
                cmd1.Parameters.Add(pSector4);

                SqlParameter pMktStatus = new SqlParameter("@MktStatus", SqlDbType.VarChar);
                if (SecType != 1)
                    pMktStatus.Value = DBNull.Value;
                else
                    pMktStatus.Value = MktStatus;
                cmd1.Parameters.Add(pMktStatus);

                SqlParameter pSecType = new SqlParameter("@SecType", SqlDbType.SmallInt);
                pSecType.Value = SecType;
                cmd1.Parameters.Add(pSecType);

                SqlParameter pLastUpdtDt = new SqlParameter("@LastUpdtDT", SqlDbType.SmallDateTime);
                pLastUpdtDt.Value = LastUpdtDt;
                cmd1.Parameters.Add(pLastUpdtDt);

                SqlParameter pLastFdmtUpdtDT = new SqlParameter("@LastFdmtUpdtDT", SqlDbType.SmallDateTime);
                pLastFdmtUpdtDT.Value = LastUpdtDt; // <-- use same date as LastUpdtDt
                cmd1.Parameters.Add(pLastFdmtUpdtDT);

                SqlParameter pLastAdjDT = new SqlParameter("@LastAdjDT", SqlDbType.SmallDateTime);
                pLastAdjDT.Value = LastAdjDT;
                cmd1.Parameters.Add(pLastAdjDT);

                SqlParameter pLastCalcDT = new SqlParameter("@LastCalcDT", SqlDbType.SmallDateTime);
                pLastCalcDT.Value = LastCalcDT;
                cmd1.Parameters.Add(pLastCalcDT);

                SqlParameter pPX_TRADE_LOT_SIZE = new SqlParameter("@PX_TRADE_LOT_SIZE", SqlDbType.SmallInt);
                if ((SecType != 1))
                    pPX_TRADE_LOT_SIZE.Value = DBNull.Value;
                else
                    pPX_TRADE_LOT_SIZE.Value = PX_TRADE_LOT_SIZE;
                cmd1.Parameters.Add(pPX_TRADE_LOT_SIZE);

                // futures only data
                SqlParameter pFUT_CUR_GEN_TICKER = new SqlParameter("@FUT_CUR_GEN_TICKER", SqlDbType.VarChar);
                if ((FUT_CUR_GEN_TICKER == String.Empty) || ((SecType != 2) && (SecType != 4)))
                    pFUT_CUR_GEN_TICKER.Value = DBNull.Value;
                else
                    pFUT_CUR_GEN_TICKER.Value = FUT_CUR_GEN_TICKER;
                cmd1.Parameters.Add(pFUT_CUR_GEN_TICKER);

                SqlParameter pFUT_CONT_SIZE = new SqlParameter("@FUT_CONT_SIZE", SqlDbType.Float);
                if ((SecType != 2) && (SecType != 4))
                    pFUT_CONT_SIZE.Value = DBNull.Value;
                else
                    pFUT_CONT_SIZE.Value = FUT_CONT_SIZE;
                cmd1.Parameters.Add(pFUT_CONT_SIZE);

                SqlParameter pFUT_VAL_PT = new SqlParameter("@FUT_VAL_PT", SqlDbType.Float);
                if ((SecType != 2) && (SecType != 4))
                    pFUT_VAL_PT.Value = DBNull.Value;
                else
                    pFUT_VAL_PT.Value = FUT_VAL_PT;
                cmd1.Parameters.Add(pFUT_VAL_PT);

                SqlParameter pFUT_TICK_SIZE = new SqlParameter("@FUT_TICK_SIZE", SqlDbType.Float);
                if ((SecType != 2) && (SecType != 4))
                    pFUT_TICK_SIZE.Value = DBNull.Value;
                else
                    pFUT_TICK_SIZE.Value = FUT_TICK_SIZE;
                cmd1.Parameters.Add(pFUT_TICK_SIZE);

                SqlParameter pFUT_DLV_DT_FIRST = new SqlParameter("@FUT_DLV_DT_FIRST", SqlDbType.SmallDateTime);
                if ((SecType != 2) && (SecType != 4))
                    pFUT_DLV_DT_FIRST.Value = DBNull.Value;
                else
                    pFUT_DLV_DT_FIRST.Value = FUT_DLV_DT_FIRST;
                cmd1.Parameters.Add(pFUT_DLV_DT_FIRST);

                // options only data
                SqlParameter pOPT_TICK_SIZE = new SqlParameter("@OPT_TICK_SIZE", SqlDbType.Float);
                if (SecType != 6)
                    pOPT_TICK_SIZE.Value = DBNull.Value;
                else
                    pOPT_TICK_SIZE.Value = OPT_TICK_SIZE;
                cmd1.Parameters.Add(pOPT_TICK_SIZE);

                SqlParameter pOPT_CONT_SIZE = new SqlParameter("@OPT_CONT_SIZE", SqlDbType.Float);
                if (SecType != 6)
                    pOPT_CONT_SIZE.Value = DBNull.Value;
                else
                    pOPT_CONT_SIZE.Value = OPT_CONT_SIZE;
                cmd1.Parameters.Add(pOPT_CONT_SIZE);

                SqlParameter pOPT_STRIKE_PX = new SqlParameter("@OPT_STRIKE_PX", SqlDbType.Float);
                if (SecType != 6)
                    pOPT_STRIKE_PX.Value = DBNull.Value;
                else
                    pOPT_STRIKE_PX.Value = OPT_STRIKE_PX;
                cmd1.Parameters.Add(pOPT_STRIKE_PX);

                SqlParameter pOPT_UNDL_TICKER = new SqlParameter("@OPT_UNDL_TICKER", SqlDbType.VarChar);
                if (SecType != 6)
                    pOPT_UNDL_TICKER.Value = DBNull.Value;
                else
                    pOPT_UNDL_TICKER.Value = OPT_UNDL_TICKER;
                cmd1.Parameters.Add(pOPT_UNDL_TICKER);

                SqlParameter pOPT_PUT_CALL = new SqlParameter("@OPT_PUT_CALL", SqlDbType.VarChar);
                if (SecType != 6)
                    pOPT_PUT_CALL.Value = DBNull.Value;
                else
                    pOPT_PUT_CALL.Value = OPT_PUT_CALL;
                cmd1.Parameters.Add(pOPT_PUT_CALL);

                SqlParameter pOPT_EXPIRE_DT = new SqlParameter("@OPT_EXPIRE_DT", SqlDbType.SmallDateTime);
                if (SecType != 6)
                    pOPT_EXPIRE_DT.Value = DBNull.Value;
                else
                    pOPT_EXPIRE_DT.Value = OPT_EXPIRE_DT;
                cmd1.Parameters.Add(pOPT_EXPIRE_DT);

                SqlParameter pHasTickData = new SqlParameter("@HasTickData", SqlDbType.Bit);
                pHasTickData.Value = 0;
                cmd1.Parameters.Add(pHasTickData);

                SqlParameter ID1Prm = new SqlParameter("@ID", SqlDbType.SmallInt);
                ID1Prm.Direction = ParameterDirection.Output;
                cmd1.Parameters.Add(ID1Prm);

                cn.Open();
                cmd1.ExecuteNonQuery();
                cn.Close();

                if (ID1Prm.Value == DBNull.Value)
                {
                    return false;
                }
            }

            return true;
        }

        public bool InsertMembershipData(string Symbol, string indexStr, DateTime date, bool isAdd)
        {
            if (!getSQLconnection())
            {
                return false;
            }
            else
            {
                SqlCommand cmd1 = cn.CreateCommand();
                cmd1.CommandType = CommandType.StoredProcedure;
                cmd1.CommandText = "InsertMemberData";


                int symbolId = GetSymbolID(Symbol.Trim());
                if (symbolId == 0)
                {
                    Debug.Print("No such symbol! GetSymbol returned " + symbolId + ", " + Symbol);
                    return false;
                }
                SqlParameter pSymbol = new SqlParameter("@SymbolID", SqlDbType.SmallInt);
                pSymbol.Value = symbolId;
                cmd1.Parameters.Add(pSymbol);

                SqlParameter pIndexName = new SqlParameter("@MembName", SqlDbType.VarChar);
                pIndexName.Value = indexStr;
                cmd1.Parameters.Add(pIndexName);

                SqlParameter pIsAdd = new SqlParameter("@isAdd", SqlDbType.Bit);
                pIsAdd.Value = isAdd;
                cmd1.Parameters.Add(pIsAdd);


                SqlParameter pDate = new SqlParameter("@Date", SqlDbType.SmallDateTime);
                pDate.Value = date;
                cmd1.Parameters.Add(pDate);

                cn.Open();
                cmd1.ExecuteNonQuery();
                cn.Close();
            }

            return true;
        }

        public bool InsertFADateFloat(string Symbol, string SeriesName, byte fPerType, DateTime date, DateTime announceDate, double value, byte fPer, int fYear, bool Consolidated, bool Adjusted)
        {
            if (!getSQLconnection())
            {
                return false;
            }
            else
            {
                int symbolId = GetSymbolID(Symbol.Trim());
                if (symbolId == 0)
                {
                    Debug.Print("No such symbol! GetSymbol returned " + symbolId + ", " + Symbol);
                    return false;
                }

                short SeriesID = GetSeriesID(SeriesName.Trim());
                if (SeriesID <= 0)
                {
                    SeriesID = InsertSeriesName(SeriesName);
                    SetSeriesIsFundamental(SeriesID, true);
                }

                SqlCommand cmd1 = cn.CreateCommand();
                cmd1.CommandType = CommandType.StoredProcedure;
                cmd1.CommandText = "InsertFADateFloat";

                SqlParameter IDPrm = new SqlParameter("@SeriesID", SqlDbType.SmallInt);
                IDPrm.Value = SeriesID;
                cmd1.Parameters.Add(IDPrm);

                SqlParameter pSymbol = new SqlParameter("@SymbolID", SqlDbType.SmallInt);
                pSymbol.Value = symbolId;
                cmd1.Parameters.Add(pSymbol);

                SqlParameter pfPerType = new SqlParameter("@fPerType", SqlDbType.TinyInt);
                pfPerType.Value = fPerType;
                cmd1.Parameters.Add(pfPerType);

                SqlParameter pfYear = new SqlParameter("@fYear", SqlDbType.SmallInt);
                pfYear.Value = fYear;
                cmd1.Parameters.Add(pfYear);

                SqlParameter pfPer = new SqlParameter("@fPer", SqlDbType.TinyInt);
                pfPer.Value = fPer;
                cmd1.Parameters.Add(pfPer);

                SqlParameter pAdjusted = new SqlParameter("@Adjusted", SqlDbType.Bit);
                pAdjusted.Value = Adjusted;
                cmd1.Parameters.Add(pAdjusted);

                SqlParameter pConsolidated = new SqlParameter("@Consolidated", SqlDbType.Bit);
                pConsolidated.Value = Consolidated;
                cmd1.Parameters.Add(pConsolidated);

                SqlParameter pDate = new SqlParameter("@Date", SqlDbType.SmallDateTime);
                pDate.Value = date;
                cmd1.Parameters.Add(pDate);

                SqlParameter pAnnounceDate = new SqlParameter("@AnnounceDate", SqlDbType.SmallDateTime);
                pAnnounceDate.Value = announceDate;
                cmd1.Parameters.Add(pAnnounceDate);

                SqlParameter ValuePrm = new SqlParameter("@Val", SqlDbType.Real);
                ValuePrm.Value = value;
                cmd1.Parameters.Add(ValuePrm);

                cn.Open();
                cmd1.ExecuteNonQuery();
                cn.Close();
            }
            return true;
        }

        public DataTable GetEmptyTickDataTable()
        {
            DataTable dt = new DataTable();

            if (getSQLconnection())
            {
                SqlCommand cmd = cn.CreateCommand();
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = "GetEmptyTickDataTable";

                cn.Open();
                SqlDataAdapter sa = new SqlDataAdapter(cmd);
                sa.Fill(dt);
                cn.Close();
            }
            return dt;
        }

        public DataTable GetEmptyTickDataStateTable()
        {
            DataTable dt = new DataTable();

            if (getSQLconnection())
            {
                SqlCommand cmd = cn.CreateCommand();
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = "GetEmptyTickDataStateTable";

                cn.Open();
                SqlDataAdapter sa = new SqlDataAdapter(cmd);
                sa.Fill(dt);
                cn.Close();
            }
            return dt;
        }

        public bool InsertTickDataBulk(DataTable tickDataTbl)
        {
            if (!getSQLconnection())
            {
                return false;
            }
            else
            {

                using (SqlBulkCopy bulkCopy = new SqlBulkCopy(cn))
                {
                    bulkCopy.DestinationTableName = "dbo.SeriesTick";

                    try
                    {

                        cn.Open();
                        DateTime timer = DateTime.Now;
                        bulkCopy.WriteToServer(tickDataTbl);
                        TimeSpan time = DateTime.Now.Subtract(timer);
                        cn.Close();
                        Console.WriteLine("total time was " + time.ToString());
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }


            }

            return true;
        }

        public bool InsertTickData(string symbol, TickDataType type, DateTime date, double price, double size, string conditionCodes, string exchangeCodes)
        {
            if (!getSQLconnection())
            {
                return false;
            }
            else
            {
                int SymbolID = GetSymbolID(symbol);
                if (SymbolID <= 0)
                {
                    SymbolID = InsertSymbol(symbol);
                }

                byte fieldType;
                switch (type)
                {
                    case TickDataType.TRADE:
                        fieldType = 0;
                        break;
                    case TickDataType.ASK:
                        fieldType = 1;
                        break;
                    case TickDataType.BID:
                        fieldType = 2;
                        break;
                    default:
                        return false;
                }

                SqlCommand cmd = cn.CreateCommand();
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = "InsertTickData";

                SqlParameter SymbolPrm = new SqlParameter("@SymbolID", SqlDbType.SmallInt);
                SymbolPrm.Value = SymbolID;
                cmd.Parameters.Add(SymbolPrm);

                SqlParameter pFldType = new SqlParameter("@FldType", SqlDbType.TinyInt);
                pFldType.Value = fieldType;
                cmd.Parameters.Add(pFldType);

                SqlParameter DatePrm = new SqlParameter("@Date", SqlDbType.DateTime2);
                DatePrm.Value = date;
                cmd.Parameters.Add(DatePrm);

                SqlParameter pPrice = new SqlParameter("@Price", SqlDbType.Real);
                pPrice.Value = price;
                cmd.Parameters.Add(pPrice);

                SqlParameter pSize = new SqlParameter("@Size", SqlDbType.Real);
                pSize.Value = size;
                cmd.Parameters.Add(pSize);

                SqlParameter pCondCode = new SqlParameter("@CondCode", SqlDbType.VarChar);
                if (conditionCodes == string.Empty)
                    pCondCode.Value = DBNull.Value;
                else
                    pCondCode.Value = conditionCodes;

                cmd.Parameters.Add(pCondCode);

                SqlParameter pExchgCode = new SqlParameter("@ExchgCode", SqlDbType.VarChar);
                if (exchangeCodes == string.Empty)
                    pExchgCode.Value = DBNull.Value;
                else
                    pExchgCode.Value = exchangeCodes;
                cmd.Parameters.Add(pExchgCode);

                cn.Open();
                cmd.ExecuteNonQuery();
                cn.Close();

            }

            return true;
        }

        public bool ModifyLastUpdtDT(string symbol, DateTime LastUpdtDT)
        {
            if (!getSQLconnection())
            {
                return false;
            }
            else
            {
                SqlCommand cmd1 = cn.CreateCommand();
                cmd1.CommandType = CommandType.StoredProcedure;
                cmd1.CommandText = "ModifyLastUpdtDT";

                SqlParameter pSymbol = new SqlParameter("@Symbol", SqlDbType.VarChar);
                pSymbol.Value = symbol;
                cmd1.Parameters.Add(pSymbol);

                SqlParameter pLastUpdtDT = new SqlParameter("@LastUpdtDT", SqlDbType.VarChar);
                pLastUpdtDT.Value = LastUpdtDT;
                cmd1.Parameters.Add(pLastUpdtDT);

                cn.Open();
                cmd1.ExecuteNonQuery();
                cn.Close();
            }
            return true;
        }

        public bool ModifyLastFdmtUpdtDT(string symbol, DateTime LastFdmtUpdtDT)
        {
            if (!getSQLconnection())
            {
                return false;
            }
            else
            {
                SqlCommand cmd1 = cn.CreateCommand();
                cmd1.CommandType = CommandType.StoredProcedure;
                cmd1.CommandText = "ModifyLastFdmtUpdtDT";

                SqlParameter pSymbol = new SqlParameter("@Symbol", SqlDbType.VarChar);
                pSymbol.Value = symbol;
                cmd1.Parameters.Add(pSymbol);

                SqlParameter pLastFdmtUpdtDT = new SqlParameter("@LastFdmtUpdtDT", SqlDbType.VarChar);
                pLastFdmtUpdtDT.Value = LastFdmtUpdtDT;
                cmd1.Parameters.Add(pLastFdmtUpdtDT);

                cn.Open();
                cmd1.ExecuteNonQuery();
                cn.Close();
            }
            return true;
        }

        public bool ModifyLastCalcDT(string symbol, DateTime LastCalcDT)
        {
            if (!getSQLconnection())
            {
                return false;
            }
            else
            {
                SqlCommand cmd1 = cn.CreateCommand();
                cmd1.CommandType = CommandType.StoredProcedure;
                cmd1.CommandText = "ModifyLastCalcDT";

                SqlParameter pSymbol = new SqlParameter("@Symbol", SqlDbType.VarChar);
                pSymbol.Value = symbol;
                cmd1.Parameters.Add(pSymbol);

                SqlParameter pLastCalcDT = new SqlParameter("@LastCalcDT", SqlDbType.VarChar);
                pLastCalcDT.Value = LastCalcDT;
                cmd1.Parameters.Add(pLastCalcDT);

                cn.Open();
                cmd1.ExecuteNonQuery();
                cn.Close();
            }
            return true;
        }

        public bool ModifyLastAdjDT(string symbol, DateTime LastAdjDT)
        {
            if (!getSQLconnection())
            {
                return false;
            }
            else
            {
                SqlCommand cmd1 = cn.CreateCommand();
                cmd1.CommandType = CommandType.StoredProcedure;
                cmd1.CommandText = "ModifyLastAdjDT";

                SqlParameter pSymbol = new SqlParameter("@Symbol", SqlDbType.VarChar);
                pSymbol.Value = symbol;
                cmd1.Parameters.Add(pSymbol);

                SqlParameter pLastAdjDT = new SqlParameter("@LastAdjDT", SqlDbType.VarChar);
                pLastAdjDT.Value = LastAdjDT;
                cmd1.Parameters.Add(pLastAdjDT);

                cn.Open();
                cmd1.ExecuteNonQuery();
                cn.Close();
            }
            return true;
        }

        public DataTable getTickDataSeries(string Symbol)
        {
            //[GetEmptyTickDataTable]
            DataTable dt = new DataTable();
            if (getSQLconnection())
            {
                int SymbolID = GetSymbolID(Symbol);
                if (SymbolID > 0)
                {
                    SqlCommand cmd = cn.CreateCommand();
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandText = "GetSymbolsOfSeries";

                    SqlParameter IDPrm = new SqlParameter("@SeriesID", SqlDbType.SmallInt);
                    IDPrm.Value = SymbolID;
                    cmd.Parameters.Add(IDPrm);

                    cn.Open();
                    SqlDataAdapter sa = new SqlDataAdapter(cmd);
                    sa.Fill(dt);
                    cn.Close();
                }
            }

            return dt;
        }

        public DataTable getTickDataSeries(string Symbol, DateTime start, DateTime end)
        {

            DataTable dt = new DataTable();
            if (getSQLconnection())
            {
                int SymbolID = GetSymbolID(Symbol);
                if (SymbolID > 0)
                {
                    SqlCommand cmd = cn.CreateCommand();
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandText = "GetTickDataSeriesBetweenDates";

                    SqlParameter IDPrm = new SqlParameter("@SymbolID", SqlDbType.SmallInt);
                    IDPrm.Value = SymbolID;
                    cmd.Parameters.Add(IDPrm);
                    
                    SqlParameter pStartDate = new SqlParameter("@StartDate", SqlDbType.DateTime2);
                    pStartDate.Value = start;
                    cmd.Parameters.Add(pStartDate);
                    
                    SqlParameter pEndDate = new SqlParameter("@EndDate", SqlDbType.DateTime2);
                    pEndDate.Value = end;
                    cmd.Parameters.Add(pEndDate);

                    cn.Open();
                    SqlDataAdapter sa = new SqlDataAdapter(cmd);
                    sa.Fill(dt);
                    cn.Close();
                }
            }

            return dt;
        }

        public bool ModifyBasedOnRefresh(Dictionary<string, string> MktStatusChg, Dictionary<string, string> CurrFutChg)
        {
            if (!getSQLconnection())
            {
                return false;
            }
            else
            {
                if (MktStatusChg.Count > 0)
                {
                    foreach (var item in MktStatusChg)
                    {
                        SqlCommand cmd1 = cn.CreateCommand();
                        cmd1.CommandType = CommandType.StoredProcedure;
                        cmd1.CommandText = "ModifyMktStatus";

                        SqlParameter pSymbol = new SqlParameter("@Symbol", SqlDbType.VarChar);
                        pSymbol.Value = item.Key;
                        cmd1.Parameters.Add(pSymbol);

                        SqlParameter pMktStatus = new SqlParameter("@MktStatus", SqlDbType.VarChar);
                        pMktStatus.Value = item.Value;
                        cmd1.Parameters.Add(pMktStatus);

                        cn.Open();
                        cmd1.ExecuteNonQuery();
                        cn.Close();
                    }
                }

                if (CurrFutChg.Count > 0)
                {
                    foreach (var item in CurrFutChg)
                    {
                        SqlCommand cmd1 = cn.CreateCommand();
                        cmd1.CommandType = CommandType.StoredProcedure;
                        cmd1.CommandText = "ModifyGenTicker";

                        SqlParameter pSymbol = new SqlParameter("@Symbol", SqlDbType.VarChar);
                        pSymbol.Value = item.Key;
                        cmd1.Parameters.Add(pSymbol);

                        SqlParameter pFUT_CUR_GEN_TICKER = new SqlParameter("@FUT_CUR_GEN_TICKER", SqlDbType.VarChar);
                        pFUT_CUR_GEN_TICKER.Value = item.Value;
                        cmd1.Parameters.Add(pFUT_CUR_GEN_TICKER);

                        cn.Open();
                        cmd1.ExecuteNonQuery();
                        cn.Close();
                    }
                }
            }
            return true;
        }

        public bool SetSeriesIsFundamental(string SeriesName, bool isFundamental)
        {
            if (!getSQLconnection())
            {
                Debug.Print("Connection to SQL DB failed");
                return false;
            }
            else
            {
                short SeriesID = GetSeriesID(SeriesName);
                if (SeriesID > 0)
                {
                    SqlCommand cmd1 = cn.CreateCommand();
                    cmd1.CommandType = CommandType.StoredProcedure;
                    cmd1.CommandText = "SetIsFundamental";

                    SqlParameter pSeriesID = new SqlParameter("@ID", SqlDbType.SmallInt);
                    pSeriesID.Value = SeriesID;
                    cmd1.Parameters.Add(pSeriesID);

                    SqlParameter pIsFundamental = new SqlParameter("@IsFundamental", SqlDbType.Bit);
                    pIsFundamental.Value = isFundamental;
                    cmd1.Parameters.Add(pIsFundamental);

                    cn.Open();
                    cmd1.ExecuteNonQuery();
                    cn.Close();

                }
                else
                {
                    Debug.Print("No series found for : {0}", SeriesName);
                    return false;
                }
            }
            return true;
        }

        public int GetSymbolID(string Symbol)
        {
            int Result = 0;

            SqlCommand cmd2 = cn.CreateCommand();
            cmd2.CommandType = CommandType.StoredProcedure;
            cmd2.CommandText = "GetSymbolID";

            SqlParameter SymbolPrm = new SqlParameter("@Symbol", SqlDbType.VarChar);
            SymbolPrm.Value = Symbol;
            cmd2.Parameters.Add(SymbolPrm);

            SqlParameter ID2Prm = new SqlParameter("@ID", SqlDbType.SmallInt);
            ID2Prm.Direction = ParameterDirection.Output;
            cmd2.Parameters.Add(ID2Prm);

            cn.Open();
            cmd2.ExecuteNonQuery();
            cn.Close();

            if (ID2Prm.Value != DBNull.Value)
            {
                Result = (Int16)ID2Prm.Value;
                //Debug.Print("GetSymbolID return value is " + ID2Prm.SqlValue.ToString());
            }
            else
            {
                Console.WriteLine("GetSymbolID for {0} failed. Return value is {1} ", Symbol, ID2Prm.SqlValue.ToString());
            }

            return Result;
        }

        ///* private function */


        private short InsertSymbol(string Symbol)
        {
            short Result = 0;

            SqlCommand cmd1 = cn.CreateCommand();
            cmd1.CommandType = CommandType.StoredProcedure;
            cmd1.CommandText = "InsertSymbol";

            SqlParameter SymbolPrm = new SqlParameter("@Symbol", SqlDbType.VarChar);
            SymbolPrm.Value = Symbol;
            cmd1.Parameters.Add(SymbolPrm);

            SqlParameter ID1Prm = new SqlParameter("@ID", SqlDbType.SmallInt);
            ID1Prm.Direction = ParameterDirection.Output;
            cmd1.Parameters.Add(ID1Prm);

            cn.Open();
            cmd1.ExecuteNonQuery();
            cn.Close();

            if (ID1Prm.Value != DBNull.Value)
            {
                Result = (short)ID1Prm.Value;
            }

            return Result;
        }

        private short InsertSeriesName(string SeriesName)
        {
            short Result = 0;

            SqlCommand cmd = cn.CreateCommand();
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = "InsertSeriesName";

            SqlParameter SeriesNamePrm = new SqlParameter("@Name", SqlDbType.VarChar);
            SeriesNamePrm.Value = SeriesName;
            cmd.Parameters.Add(SeriesNamePrm);

            SqlParameter ID2Prm = new SqlParameter("@ID", SqlDbType.SmallInt);
            ID2Prm.Direction = ParameterDirection.Output;
            cmd.Parameters.Add(ID2Prm);

            cn.Open();
            cmd.ExecuteNonQuery();
            cn.Close();

            if (ID2Prm.Value != DBNull.Value)
            {
                Result = (short)ID2Prm.Value;
            }

            return Result;
        }

        private void InsertRow(short SeriesID, short SymbolID, DateTime date, Single value)
        {

            SqlCommand cmd = cn.CreateCommand();
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = "InsertDateFloat";
            SqlParameter IDPrm = new SqlParameter("@SeriesID", SqlDbType.SmallInt);
            IDPrm.Value = SeriesID;
            cmd.Parameters.Add(IDPrm);

            SqlParameter SymbolPrm = new SqlParameter("@SymbolID", SqlDbType.SmallInt);
            SymbolPrm.Value = SymbolID;
            cmd.Parameters.Add(SymbolPrm);

            SqlParameter DatePrm = new SqlParameter("@Date", SqlDbType.SmallDateTime);
            DatePrm.Value = date;
            cmd.Parameters.Add(DatePrm);

            SqlParameter ValuePrm = new SqlParameter("@Value", SqlDbType.Real);
            ValuePrm.Value = value;
            cmd.Parameters.Add(ValuePrm);

            cn.Open();
            cmd.ExecuteNonQuery();
            cn.Close();

            SortedList<DateTime, bool> MembData = new SortedList<DateTime, bool>();
        }

        private short GetSeriesID(string SeriesName)
        {
            short Result = 0;

            SqlCommand cmd1 = cn.CreateCommand();
            cmd1.CommandType = CommandType.StoredProcedure;
            cmd1.CommandText = "GetSeriesID";

            SqlParameter NamePrm = new SqlParameter("@Name", SqlDbType.VarChar);
            NamePrm.Value = SeriesName;
            cmd1.Parameters.Add(NamePrm);

            SqlParameter ID1Prm = new SqlParameter("@ID", SqlDbType.SmallInt);
            ID1Prm.Direction = ParameterDirection.Output;
            cmd1.Parameters.Add(ID1Prm);

            cn.Open();
            cmd1.ExecuteNonQuery();
            cn.Close();

            if (ID1Prm.Value != DBNull.Value)
            {
                Result = (short)ID1Prm.Value;
            }

            return Result;
        }

        private bool SetSeriesIsFundamental(short SeriesID, bool isFundamental)
        {
            if (SeriesID > 0)
            {
                SqlCommand cmd1 = cn.CreateCommand();
                cmd1.CommandType = CommandType.StoredProcedure;
                cmd1.CommandText = "SetIsFundamental";

                SqlParameter pSeriesID = new SqlParameter("@ID", SqlDbType.SmallInt);
                pSeriesID.Value = SeriesID;
                cmd1.Parameters.Add(pSeriesID);

                SqlParameter pIsFundamental = new SqlParameter("@IsFundamental", SqlDbType.Bit);
                pIsFundamental.Value = isFundamental;
                cmd1.Parameters.Add(pIsFundamental);

                cn.Open();
                cmd1.ExecuteNonQuery();
                cn.Close();

            }
            else
            {
                Debug.Print("Series IsFundamental setting NOT updated. No series found for ID {0}", SeriesID);
                return false;
            }
            return true;
        }

        private DateTime GetFAAnnounceDate(int SymbolID, byte fPerType, byte fPer, int fYear, bool Consolidated)
        {
            DateTime Result;

            SqlCommand cmd1 = cn.CreateCommand();
            cmd1.CommandType = CommandType.StoredProcedure;
            cmd1.CommandText = "GetFAAnnounceDate";

            SqlParameter SymbolIDPrm = new SqlParameter("@SymbolID", SqlDbType.SmallInt);
            SymbolIDPrm.Value = SymbolID;
            cmd1.Parameters.Add(SymbolIDPrm);

            SqlParameter pConsolidated = new SqlParameter("@Consolidated", SqlDbType.Bit);
            pConsolidated.Value = Consolidated;
            cmd1.Parameters.Add(pConsolidated);

            SqlParameter pfPerType = new SqlParameter("@fPerType", SqlDbType.TinyInt);
            pfPerType.Value = fPerType;
            cmd1.Parameters.Add(pfPerType);

            SqlParameter pfPer = new SqlParameter("@fPer", SqlDbType.TinyInt);
            pfPer.Value = fPer;
            cmd1.Parameters.Add(pfPer);

            SqlParameter pfYear = new SqlParameter("@fYear", SqlDbType.SmallInt);
            pfYear.Value = fYear;
            cmd1.Parameters.Add(pfYear);

            SqlParameter pAnnounceDate = new SqlParameter("@AnnounceDate", SqlDbType.DateTime);
            pAnnounceDate.Direction = ParameterDirection.Output;
            cmd1.Parameters.Add(pAnnounceDate);

            cn.Open();
            cmd1.ExecuteNonQuery();
            cn.Close();

            if (pAnnounceDate.Value != DBNull.Value)
            {
                //debug(ExistsPrm.Value.ToString());
                Result = (DateTime)pAnnounceDate.Value;
                //debug("GetLastDateTime: return");
            }
            else
                Result = DateTime.MinValue;

            return Result;
        }

        private DateTime GetFADate(int SymbolID, byte fPerType, byte fPer, int fYear, bool Consolidated)
        {
            DateTime Result;

            SqlCommand cmd1 = cn.CreateCommand();
            cmd1.CommandType = CommandType.StoredProcedure;
            cmd1.CommandText = "GetFADate";

            SqlParameter SymbolIDPrm = new SqlParameter("@SymbolID", SqlDbType.SmallInt);
            SymbolIDPrm.Value = SymbolID;
            cmd1.Parameters.Add(SymbolIDPrm);

            SqlParameter pConsolidated = new SqlParameter("@Consolidated", SqlDbType.Bit);
            pConsolidated.Value = Consolidated;
            cmd1.Parameters.Add(pConsolidated);

            SqlParameter pfPerType = new SqlParameter("@fPerType", SqlDbType.TinyInt);
            pfPerType.Value = fPerType;
            cmd1.Parameters.Add(pfPerType);

            SqlParameter pfPer = new SqlParameter("@fPer", SqlDbType.TinyInt);
            pfPer.Value = fPer;
            cmd1.Parameters.Add(pfPer);

            SqlParameter pfYear = new SqlParameter("@fYear", SqlDbType.SmallInt);
            pfYear.Value = fYear;
            cmd1.Parameters.Add(pfYear);

            SqlParameter pAnnounceDate = new SqlParameter("@Date", SqlDbType.DateTime);
            pAnnounceDate.Direction = ParameterDirection.Output;
            cmd1.Parameters.Add(pAnnounceDate);

            cn.Open();
            cmd1.ExecuteNonQuery();
            cn.Close();

            if (pAnnounceDate.Value != DBNull.Value)
            {
                Result = (DateTime)pAnnounceDate.Value;
            }
            else
                Result = DateTime.MinValue;

            return Result;
        }


        #endregion

    }
}