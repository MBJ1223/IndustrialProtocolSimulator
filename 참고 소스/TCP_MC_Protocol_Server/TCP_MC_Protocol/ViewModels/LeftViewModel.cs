using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Prism.Commands;
using Prism.Mvvm;

namespace TCP_MC_Protocol.ViewModels
{
    class LeftViewModel : BindableBase
    {
        private List<string> _listBoxProcessInfo = new List<string>();
        public List<string> ListBoxProcessInfo
        {
            get { return _listBoxProcessInfo; }
            set { SetProperty(ref _listBoxProcessInfo, value); }
        }

        private List<string> _listBoxOriginlInfo = new List<string>();
        public List<string> ListBoxOriginlInfo
        {
            get { return _listBoxOriginlInfo; }
            set { SetProperty(ref _listBoxOriginlInfo, value); }
        }

        private string _textBlockOpenPort = "Not Server Open ";
        public string TextBlockOpenPort
        {
            get { return _textBlockOpenPort; }
            set { SetProperty(ref _textBlockOpenPort, value); }
        }

        private string _textBoxServerPort = "19000";
        public string TextBoxServerPort
        {
            get { return _textBoxServerPort; }
            set { SetProperty(ref _textBoxServerPort, value); }
        }

        private ICommand _openServerCommand;
        public ICommand OpenServerCommand => (this._openServerCommand) ??
                   (_openServerCommand = new DelegateCommand(ServerOpen));

        private ICommand _closeServerCommand;
        public ICommand CloseServerCommand => (this._closeServerCommand) ??
                   (_closeServerCommand = new DelegateCommand(ServerClose));


        private ICommand _loaded_Command;
        public ICommand Loaded_Command => (this._loaded_Command) ??
                   (_loaded_Command = new DelegateCommand(ProgramInit_Start));



        private void ProgramInit_Start()
        {
            //CGlobal._MelsecProtocol.ServerOpen(CGlobal._severOpenPort);
            TextBoxServerPort = CGlobal.SeverOpenPort.ToString();

            AutoView();
        }

        private void ServerOpen()
        {
            if (Regex.IsMatch(TextBoxServerPort, "[0-9]"))
            {
                CGlobal.SeverOpenPort = Convert.ToInt32(TextBoxServerPort);
                CGlobal.MelsecProtocol.ServerOpen(CGlobal.SeverOpenPort);
                //TextBlockOpenPort = "Sever Open Port : " + CGlobal.SeverOpenPort.ToString();
                CGlobal.ServerStatus = "Sever Open Port : " + CGlobal.SeverOpenPort.ToString();
            }
        }

        private void ServerClose()
        {
            CGlobal.MelsecProtocol.ServerClose();
        }

        private void AutoView()
        {
            Thread MessageCheck = new Thread(new ThreadStart(ViewMessage));

            MessageCheck.Start();
        }


        private void ViewMessage()
        {
            while (CGlobal.ProgramStart)
            {
                if (CGlobal.RecivePacketProcessLog.Count != ListBoxProcessInfo.Count)
                {
                    ListBoxProcessInfo = CGlobal.RecivePacketProcessLog.ToList();
                }
                
                if (CGlobal.RecivePacketOriginLog.Count != ListBoxOriginlInfo.Count)
                {
                    ListBoxOriginlInfo = CGlobal.RecivePacketOriginLog.ToList();
                }
                Thread.Sleep(100);
            }
        }


    }
}
