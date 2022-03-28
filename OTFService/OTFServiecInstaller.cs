using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace OTFService
{
    [RunInstaller(true)]
    public partial class OTFServiecInstaller : System.Configuration.Install.Installer
    {
        ServiceInstaller serviceInstaller;
        public OTFServiecInstaller()
        {
            InitializeComponent();
            ServiceProcessInstaller serviceProcessInstaller = new ServiceProcessInstaller();
            serviceInstaller = new ServiceInstaller();

            //# Service Account Information
            serviceProcessInstaller.Account = ServiceAccount.LocalSystem;
            serviceProcessInstaller.Username = null;
            serviceProcessInstaller.Password = null;

            //# Service Information

            serviceInstaller.DisplayName = "OTF Listener Service";
            serviceInstaller.StartType = ServiceStartMode.Manual;

            //# This must be identical to the WindowsService.ServiceBase name

            //# set in the constructor of WindowsService.cs

            serviceInstaller.ServiceName = "OTFListenerService";

            this.Installers.Add(serviceProcessInstaller);
            this.Installers.Add(serviceInstaller);
        }

        public override void Install(IDictionary stateSaver)
        {
            base.Install(stateSaver);
            string targetDirectory = Context.Parameters["assemblypath"];
            System.Configuration.Configuration config = System.Configuration.ConfigurationManager.OpenExeConfiguration(targetDirectory);
            config.AppSettings.Settings["Path"].Value = targetDirectory.Replace(@"\OTFService.exe", string.Empty) + @"\";
            config.Save();
            config.SaveAs(targetDirectory.Replace(@"\OTFService.exe", string.Empty) + @"\OTFListener.exe.config");
        }
    }
}
