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
        public OTFService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            OTFListener.Program.START_MAIN_PROGRAM.BeginInvoke(new string[] { "winservice" }, null, null);
        }

        protected override void OnStop()
        {
        }
    }
}
