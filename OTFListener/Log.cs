using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;

namespace OTFListener
{
    public static class Log
    {
        #region Variables
        private static string _filefolder = System.Configuration.ConfigurationManager.AppSettings["Path"] + "Logs\\";
        private const long _maxlogfilelength = 1000000;
        public delegate void LogEnterDelegate(string data, string dataactivetype, string address, string filename);
        private static LogEnterDelegate _logenterdelegate;
        private static object _sync = new object();
        #endregion

        #region methods
        
        public static void LogEnter(string data, string dataactivetype, string address, string filename)
        {
            _logenterdelegate = WriteLog;
            System.IAsyncResult _asyncresult = _logenterdelegate.BeginInvoke(data, dataactivetype, address, filename, null, null);
        }

        private static void WriteLog(string data, string dataactivetype, string address, string filename)
        {
            try
            {
                lock (_sync)
                {
                    ValidateFile(_filefolder, filename, ""); //Validate file exist, otherwise create new file  

                    using (System.IO.StreamWriter streamwriter = System.IO.File.AppendText(_filefolder + filename))
                    {
                        if (null != address)
                        {
                            streamwriter.Write("\n");
                            streamwriter.WriteLine("{0} {1}", System.DateTime.Now.ToLongTimeString(), System.DateTime.Now.ToLongDateString());
                            streamwriter.WriteLine("{0}", dataactivetype);
                        }
                        else
                            streamwriter.Write("{0}{1}{2}", System.DateTime.Now.ToLongTimeString(), System.DateTime.Now.ToLongDateString(), dataactivetype);

                        streamwriter.WriteLine("{0}", data);

                        if (null != address)
                        {
                            streamwriter.WriteLine(address);
                            streamwriter.WriteLine("-------------------------------------------------------------------");
                        }
                        streamwriter.Flush(); //write down in file
                    }
                }
            }
            catch (System.IO.IOException Exio) { System.Console.WriteLine("Log IO Err : " + Exio.ToString()); }
            catch (System.Exception Ex) { System.Console.WriteLine("Log Err : " + Ex.ToString()); }
        }

        public static void ValidateFile(string filefolder, string filename, string defaultvalue)
        {
            if (!System.IO.Directory.Exists(filefolder))
                System.IO.Directory.CreateDirectory(filefolder);

            System.IO.FileInfo _fileinfo = new System.IO.FileInfo(filefolder + filename);

            if (!System.IO.File.Exists(filefolder + filename) || (_fileinfo.Length == 0 && !string.IsNullOrEmpty(defaultvalue))) 
            {
                System.IO.FileStream _filestream = _fileinfo.Create();

                if (!string.IsNullOrEmpty(defaultvalue))
                {
                    System.IO.StreamWriter _streamwriter = new System.IO.StreamWriter(_filestream);
                    _streamwriter.Write(defaultvalue);
                    _streamwriter.Flush();
                }
                _filestream.Close();
            }
            else  //when file exist in local drive check the file volume
            {
                if (filename.IndexOf(".log") > 0 && _fileinfo.Length > _maxlogfilelength)
                {
                    MoveFile(filefolder, filename, filefolder, true);
                    ValidateFile(filefolder, filename, defaultvalue);
                }
            }

        }

        public static void MoveFile(string sourcefilefolder, string sourcefile, string destinationfolder, bool blrename)
        {
            string _destinationfile = sourcefile;
            System.IO.Directory.CreateDirectory(destinationfolder);

            if (blrename)
                _destinationfile = sourcefile.Insert(sourcefile.IndexOf("."),
                                                    System.DateTime.Now.Year.ToString() + System.DateTime.Now.Month.ToString() +
                                                    System.DateTime.Now.Day.ToString() + System.DateTime.Now.Hour.ToString() +
                                                    System.DateTime.Now.Minute.ToString() + System.DateTime.Now.Second.ToString());

            System.IO.File.Move(sourcefilefolder + sourcefile, destinationfolder + _destinationfile);
            System.IO.File.SetAttributes(destinationfolder + _destinationfile, System.IO.FileAttributes.Normal);
        }
        #endregion
    }
}
