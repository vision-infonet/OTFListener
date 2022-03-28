using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OTFListener
{
    public class Program
    {
        public delegate void StarMainProgram(string[] args);
        public static StarMainProgram START_MAIN_PROGRAM = Main;
        public static void Main(string[] args)
        {
            if (null != args && args.Length > 0 && args[0] == "winservice")
                MobilePaymentProcessor.runningasservice = true;
            MobilePaymentProcessor.GetInstance();
        }
    }
}
