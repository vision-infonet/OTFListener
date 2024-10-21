using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace OTFService
{
    public partial class OTFService : ServiceBase
    {
        private static string OTFServiceStartCloseLog = "OTFServiceStartClose.log";
        public OTFService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            OTFListener.Program.START_MAIN_PROGRAM.BeginInvoke(new string[] { "winservice" },  new AsyncCallback(MainProgramLoaded), null);
        }
        private void MainProgramLoaded(object obj)
        {
            OTFListener.Log.LogEnter("OTFService started", " ", null, OTFServiceStartCloseLog);
        }
        protected override void OnStop()
        {
            OTFListener.Log.LogEnter("OTFService closed OnStop", " ", null, OTFServiceStartCloseLog);
        }
        protected override void OnShutdown()//2024-Oct-18 Vision
        {
            OTFListener.Log.LogEnter("OTFService closed OnShutdown", " ", null, OTFServiceStartCloseLog);
        }
    }
}
