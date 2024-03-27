using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Security.Cryptography.X509Certificates;
using System.Reflection;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Xml.Linq;
using System.Net.Sockets;

namespace OTFListener
{
    public class MobilePaymentProcessor
    {
        public HttpListener _https_listener;
        public static string LOCAL_IP = string.Empty, LOCAL_URLACL;
        public int OTF_Listener_Port=9661, OPT_Listener_Port=9660;
        public static MobilePaymentProcessor _mobilePaymentProcessor = null;
        public static string MY_GUID = string.Empty;
        public static string _log = "OTFMobilePaymentProcessor.log";
        public static X509Certificate2 cert2 = null;
        public static string certificate = System.Configuration.ConfigurationManager.AppSettings["Path"]
                                                 + System.Configuration.ConfigurationManager.AppSettings["CertFileName"];
        public static string certificate_password = System.Configuration.ConfigurationManager.AppSettings["CertFilePassword"];
        private TcpClient tcpClient;
        private ManualResetEvent OPTResponseManualResetEvent = new ManualResetEvent(false);
        private byte[] receive_buffer = new byte[2048];
        private int received_data_length = 0;
        internal static bool runningasservice = false;
        Thread receiving_thread = null;//2023-Feb-01 Vision

        public MobilePaymentProcessor()
        {
            OTF_Listener_Port = int.Parse(System.Configuration.ConfigurationManager.AppSettings["OTF_Listener_Port"]);
            OPT_Listener_Port = int.Parse(System.Configuration.ConfigurationManager.AppSettings["OPT_Listener_Port"]);
            //LOCAL_URLACL = System.Configuration.ConfigurationManager.AppSettings["LocalUrlAcl"];
            LOCAL_URLACL = LOCAL_IP = GetLocalIPAddress();
            Log.LogEnter("*******************MobilePaymentProcessor is started********************\r\n", string.Empty, string.Empty, _log);
            SetupSsl_Infonet(OTF_Listener_Port);
            //SetupSsl_Infonet(OTF_Listener_Port, ref cert2);
            //SetupSsl(LocalPort, ref cert2);
            InitialHttpsListener();
            ThreadPool.QueueUserWorkItem(new WaitCallback(ConnectToOPT));
            
        }

        public void InitialHttpsListener()
        {
            if (!HttpListener.IsSupported)
            {
                Log.LogEnter("Error in MobilePaymentProcessor.cs/InitialListener():HttpListener Is Not Supported.","Debug", string.Empty, _log);
                return;
            }
            try
            {
                ServicePointManager.ServerCertificateValidationCallback += new System.Net.Security.RemoteCertificateValidationCallback(ValidationCallBack);
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11;

                _https_listener = new HttpListener();
                _https_listener.Prefixes.Add(System.Configuration.ConfigurationManager.AppSettings["SecurityLevel"]
                                                            + "://" + LOCAL_IP + ":" + OTF_Listener_Port + "/");
                _https_listener.IgnoreWriteExceptions = true;//2023-Feb-01 Vision
                _https_listener.Start();
                receiving_thread = new Thread(this.ReceiveDataThread_HTTP);
                receiving_thread.Start();
                Log.LogEnter($"Http listener started on : {LOCAL_IP}:{OTF_Listener_Port}", "Debug", string.Empty, _log);
            }
            catch (Exception ex)
            {
                Log.LogEnter( $"Error in MobilePaymentProcessor()/InitialListener() : {ex.ToString()}","Error",string.Empty, _log);
            }
        }
        public static MobilePaymentProcessor GetInstance()
        {
            if (_mobilePaymentProcessor == null)
                _mobilePaymentProcessor = new MobilePaymentProcessor();
            return _mobilePaymentProcessor;
        }

        public void ReceiveDataThread_HTTP(object obj)
        {
            while (true)
            {
                try
                {
                    HttpListenerContext context = _https_listener.GetContext();
                    ThreadPool.QueueUserWorkItem(new WaitCallback(this.HandleReceivedDataThread), context);
                    Thread.Sleep(50);
                }
                catch (Exception ex)
                {
                    Log.LogEnter($"Error in MobilePaymentProcessor.cs/ReceiveDataThread_HTTP() : {ex.ToString()}", "Error", string.Empty, _log);
                    Thread.Sleep(50);
                }
            }
        }
        private void HandleReceivedDataThread(object obj)
        {
            HttpListenerContext context = (HttpListenerContext)obj;
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;
            string _receivedmsg = string.Empty;
            XElement _xelement = null;
            try
            {
                using (System.IO.Stream body = request.InputStream)
                {
                    using (System.IO.StreamReader reader = new System.IO.StreamReader(body, request.ContentEncoding))
                    {
                        _receivedmsg = reader.ReadToEnd();
                        try
                        {
                            _xelement = XElement.Parse(_receivedmsg);
                            if (!runningasservice)
                                Console.WriteLine(DateTime.Now.ToString() + " Received HTTPS request" + "\r\n" + _receivedmsg + "\r\n");
                        }
                        catch (Exception ex)
                        {
                            Log.LogEnter($"Received HTTP * *********invalid xml * *********\r\n{_receivedmsg}\r\n{ex.ToString()}", "Error", string.Empty, _log);
                            return;
                        }
                        Log.LogEnter("Received HTTP Reqest xml \r\n" + _xelement.ToString() + "\r\n", string.Empty, string.Empty, _log);
                        TransferMobileRequest(_xelement, response);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogEnter($"Error in HandleReceivedDataThread: {ex.ToString()}", "", string.Empty, _log);
            }
        }

        private void TransferMobileRequest(XElement element, HttpListenerResponse resp)
        {
            try
            {
                if (tcpClient.Connected)
                {
                    try
                    {
                        NetworkStream _stream = tcpClient.GetStream();
                        byte[] _tempbytes = System.Text.ASCIIEncoding.ASCII.GetBytes(element.ToString());
                        _stream.Write(_tempbytes, 0, _tempbytes.Length);
                        received_data_length = _stream.Read(receive_buffer, 0, receive_buffer.Length);
                        if (!runningasservice)
                            Console.WriteLine( $"{DateTime.Now.ToString()} Receive response from OPT:\r\n" +
                            $"{System.Text.ASCIIEncoding.ASCII.GetString(receive_buffer, 0, received_data_length)}\r\n");
                        Log.LogEnter($"Receive response from OPT:\r\n" +
                            $"{System.Text.ASCIIEncoding.ASCII.GetString(receive_buffer, 0, received_data_length)}", 
                            "", string.Empty, _log);
                        resp.OutputStream.Write(receive_buffer, 0, received_data_length);
                        resp.OutputStream.Close();
                        resp.Close();
                    }
                    catch (Exception ex)
                    {
                        Log.LogEnter($"Error in TransferMobileRequest: {ex.ToString()}", "TcpClient will be closed and reconnect to OPT", string.Empty, _log);
                        tcpClient.Close();
                        //2023-Feb-01 Vision added begin
                        _https_listener.Stop();
                        _https_listener.Abort();
                        if (this.receiving_thread != null)
                            this.receiving_thread.Abort();
                        this.InitialHttpsListener();
                        //2023-Feb-01 Vision added end
                        Thread.Sleep(5000);
                        //TransferMobileRequest(element, resp);//resend the same request in case that OPT reloaded.//2024-Mar-27 Vision commented out: resending /re-response confused OTF server.
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogEnter($"Error in HandleMobileRequest: {ex.ToString()}", "Error", string.Empty, _log);
            }
        }

        private void ConnectToOPT(object obj)
        {
            while (true)
            {
                if (tcpClient == null || !tcpClient.Connected)
                {
                    tcpClient = new TcpClient(AddressFamily.InterNetwork);
                    tcpClient.BeginConnect(IPAddress.Parse(LOCAL_IP), OPT_Listener_Port, null, null);
                }
                Thread.Sleep(5000);
            }
        }

        
        private bool ValidationCallBack(object sender, X509Certificate cert, X509Chain chain, System.Net.Security.SslPolicyErrors error)
        {
            if (error == System.Net.Security.SslPolicyErrors.None)
                return true;
            else
            {
                return false;
            }
            //return true;
        }

        public static string GetLocalIPAddress()
        {
            string _result = string.Empty;
            try
            {
                IPAddress[] _ipaddresses = Dns.GetHostAddresses(Dns.GetHostName());
                if (_ipaddresses.Length > 0)
                {
                    foreach (IPAddress ipaddress in _ipaddresses)
                    {
                        if (ipaddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                            && ipaddress.ToString() != "127.0.0.1")
                        {
                            _result = System.Text.Encoding.ASCII.GetString(ConvertToByte(ipaddress.ToString()));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogEnter($"Error in MobilePaymentProcessor.cs/GetLocalIPAddress(): {ex.ToString()}", "Error", string.Empty, _log);
                _result = string.Empty;
            }
            return _result;
        }

        //public void SetupSsl_Infonet(int port, ref X509Certificate2 cert2)
        public void SetupSsl_Infonet(int port)
        {
            try
            {
                if (!string.IsNullOrEmpty(certificate_password))
                    cert2 = new X509Certificate2(certificate, certificate_password);
                else
                    cert2 = new X509Certificate2(certificate);
                ExecuteNetShCmd(" netsh http delete urlacl url="+ System.Configuration.ConfigurationManager.AppSettings["SecurityLevel"] + "://" + LOCAL_URLACL.Replace("_", Dns.GetHostName()) + ":"+ port +"/");
                Log.LogEnter(" netsh http delete urlacl url=" + System.Configuration.ConfigurationManager.AppSettings["SecurityLevel"] + "://" + LOCAL_URLACL.Replace("_", Dns.GetHostName()) + ":" + port + "/", "Netsh Command", string.Empty, _log);
                Thread.Sleep(500);
                ExecuteNetShCmd(" netsh http add urlacl url=" + System.Configuration.ConfigurationManager.AppSettings["SecurityLevel"] + "://" + LOCAL_URLACL.Replace("_", Dns.GetHostName()) + ":" + port + "/ user=users");
                Log.LogEnter(" netsh http add urlacl url=" + System.Configuration.ConfigurationManager.AppSettings["SecurityLevel"] + "://" + LOCAL_URLACL.Replace("_", Dns.GetHostName()) + ":" + port + "/ user=users", "Netsh Command", string.Empty, _log);
                Thread.Sleep(500);
                ExecuteNetShCmd(" http delete sslcert ipport=" + LOCAL_URLACL + ":" + port);
                Log.LogEnter(" http delete sslcert ipport=" + LOCAL_URLACL + ":" + port, "Netsh Command", string.Empty, _log);
                Thread.Sleep(500);
                ExecuteNetShCmd(" http add sslcert ipport=" + LOCAL_URLACL + ":" + port + " certhash=" + cert2.GetCertHashString() + " appid={" + GetGUID() + "} certstorename=Root");
                Log.LogEnter(" http add sslcert ipport=" + LOCAL_URLACL + ":" + port + " certhash=" + cert2.GetCertHashString() + " appid={" + GetGUID() + "} certstorename=Root", "Netsh Command", string.Empty, _log);
                //ExecuteNetShCmd(" http delete sslcert hostnameport=" + LOCAL_URLACL.Replace("_", Dns.GetHostName()) + ":" + port);
                //Log.LogEnter(   " http delete sslcert hostnameport=" + LOCAL_URLACL.Replace("_", Dns.GetHostName()) + ":" + port, "Netsh Command", string.Empty, _log);
                //ExecuteNetShCmd(" http add sslcert hostnameport=" + LOCAL_URLACL.Replace("_", Dns.GetHostName()) + ":" + port + " certhash=" + cert2.GetCertHashString() + " appid={" + GetGUID() + "}" + " certstorename=Root");
                //Log.LogEnter(   " http add sslcert hostnameport=" + LOCAL_URLACL.Replace("_", Dns.GetHostName()) + ":" + port + " certhash=" + cert2.GetCertHashString() + " appid={" + GetGUID() + "}" + " certstorename=Root", "Netsh Command", string.Empty, _log);
            }
            catch (Exception e)
            {
                Log.LogEnter($"Error in SetupSsl_Infonet():\r\n{e.ToString()}", "Error", string.Empty, _log);
            }
        }

        public bool SetupSsl(int port, ref X509Certificate2 cert2)
        {
            Assembly myAssembly = Assembly.GetExecutingAssembly();
            MY_GUID = myAssembly.GetType().GUID.ToString();
            X509Store store = new X509Store(StoreLocation.LocalMachine);
            //Use the first cert to configure Ssl
            store.Open(OpenFlags.ReadOnly);
            //Assumption is we have certs. If not then this call will fail :(
            try
            {
                bool found = false;
                int temp = store.Certificates.Count;
                foreach (X509Certificate2 cert in store.Certificates)
                {
                    String certHash = cert.GetCertHashString();
                    //Only install certs issued for the machine and has the name as the machine name
                    //if (cert.Subject.ToUpper().IndexOf(Environment.MachineName.ToUpper()) >= 0)
                    if (cert.Subject.ToUpper().IndexOf("LOCALHOST") >= 0)
                    {
                        try
                        {
                            found = true;
                            //ExecuteNetShCmd(" http delete sslcert ipport=0.0.0.0:" + port);
                            ExecuteNetShCmd(" http add sslcert ipport=0.0.0.0:" + port + " certhash=" + certHash + " appid={" + MY_GUID + "}");
                            cert2 = cert;
                        }
                        catch (Exception e)
                        {
                            return false;
                        }
                    }
                }
                if (!found)
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                return false;
            }
            finally
            {
                if (store != null)
                {
                    store.Close();
                }
            }

            return true;
        }
        public string GetGUID()
        {
            Assembly myAssembly = Assembly.GetExecutingAssembly();
            return myAssembly.GetType().GUID.ToString();
        }

        /// <summary>
        /// execute command: netsh http
        /// </summary>
        /// <param name="arguments"></param>
        public static void ExecuteNetShCmd(string arguments)
        {
            try
            {
                ProcessStartInfo procStartInfo = new ProcessStartInfo("netsh", arguments);
                procStartInfo.RedirectStandardOutput = true;
                procStartInfo.UseShellExecute = false;
                procStartInfo.CreateNoWindow = true;
                Process.Start(procStartInfo);
            }
            catch(Exception ex)
            {

            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private static byte[] ConvertToByte(string str)
        {
            byte[] bb = new byte[str.Length];
            int i = 0;
            foreach (char c in str)
                bb[i++] = (byte)c;
            return bb;
        }
    }
}
