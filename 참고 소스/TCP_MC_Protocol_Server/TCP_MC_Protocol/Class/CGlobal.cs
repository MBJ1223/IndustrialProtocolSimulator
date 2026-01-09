using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TCP_MC_Protocol
{
    public static class CGlobal
    {
        public static int SeverOpenPort = 19000;
        public static string ServerStatus = "Not Server Open";
        public static bool ProgramStart = true;
        public static CMelsecProtocol MelsecProtocol = new CMelsecProtocol();
        public static List<MelsecDataWord> MelsecDataWordInfo = new List<MelsecDataWord>();
        public static ObservableCollection<string> RecivePacketOriginLog = new ObservableCollection<string>();
        public static ObservableCollection<string> RecivePacketProcessLog = new ObservableCollection<string>();
    }

    public class  MelsecDataWord
    {
        public string WordName { get; set; }
        public string WordValue { get; set; }
        public string ASCII { get; set; }
    }

}
