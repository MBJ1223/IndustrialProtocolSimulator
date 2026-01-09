using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Prism.Commands;
using Prism.Mvvm;

namespace TCP_MC_Protocol.ViewModels
{
    class RightViewModel : BindableBase
    {

        private bool ViewStart = false;
        private int ViewStartAddress = 1;
        private DispatcherTimer _UiRefreshTimer = new DispatcherTimer();

        private string _textBoxViewAddr = "32010";
        public string TextBoxViewAddr
        {
            get { return _textBoxViewAddr; }
            set { SetProperty(ref _textBoxViewAddr, value); }
        }

        private List<MelsecDataWord> _wordDataInfo;
        public List<MelsecDataWord> WordDataInfo
        {
            get { return _wordDataInfo; }
            set { SetProperty(ref _wordDataInfo, value); }
        }

        //private List<string> _wordAddress = new List<string>();
        //public List<string> WordAddress
        //{
        //    get { return _wordAddress; }
        //    set { SetProperty(ref _wordAddress, value); }
        //}

        //private List<string> _wordValue = new List<string>();
        //public List<string> WordValue
        //{
        //    get { return _wordValue; }
        //    set { SetProperty(ref _wordValue, value); }
        //}

        private int _selectedIndex;
        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set { SetProperty(ref _selectedIndex, value); }
        }

        private string _selectedAddress;
        public string SelectedAddress
        {
            get { return _selectedAddress; }
            set { SetProperty(ref _selectedAddress, value); }
        }

        private string _selectedAddrValue;
        public string SelectedAddrValue
        {
            get { return _selectedAddrValue; }
            set { SetProperty(ref _selectedAddrValue, value); }
        }

        private string _textBlockOpenPort = "Not Server Open ";
        public string TextBlockOpenPort
        {
            get { return _textBlockOpenPort; }
            set { SetProperty(ref _textBlockOpenPort, value); }
        }

        private ICommand _viewStopCommand;
        public ICommand ViewStopCommand => (this._viewStopCommand) ??
                   (_viewStopCommand = new DelegateCommand(AutoViewThreadStop));


        private ICommand _viewStartCommand;
        public ICommand ViewStartCommand => (this._viewStartCommand) ??
                   (_viewStartCommand = new DelegateCommand(AutoViewThreadStart));

        private ICommand _doubleClickCommand;
        public ICommand DoubleClickCommand => (this._doubleClickCommand) ??
                   (_doubleClickCommand = new DelegateCommand(AutoViewCheck));

        //private ICommand _valueEditApply;
        //public ICommand ValueEditApply => (this._valueEditApply) ??
        //           (_valueEditApply = new DelegateCommand(UserEditValueApply));

        private ICommand _enterViewStart;
        public ICommand EnterViewStart => (this._enterViewStart) ??
                   (_enterViewStart = new DelegateCommand(StartAddrView));

        private ICommand _loaded_Command;
        public ICommand Loaded_Command => (this._loaded_Command) ??
                   (_loaded_Command = new DelegateCommand(PageInit));

        private void PageInit()
        {
            _UiRefreshTimer.Interval = TimeSpan.FromMilliseconds(200);
            _UiRefreshTimer.Tick += new EventHandler(EqpPortStatusUiRefresh_Timer_Tick);
            _UiRefreshTimer.Start();
        }

        private void EqpPortStatusUiRefresh_Timer_Tick(object sender, EventArgs e)
        {
            TextBlockOpenPort = (String)CGlobal.ServerStatus.Clone();
        }


        private void AutoViewCheck()
        {
            if(ViewStart)
            {
                ViewStart = false;
            }
        }

        private void AutoViewThreadStart()
        {     
            if (!ViewStart)
            {
                ViewStart = true;
                StartAddrView();
                Thread AutoViewMelsecData = new Thread(AutoViewRun);
                AutoViewMelsecData.Start();
            }
            else
            {
                StartAddrView();
            }
            Thread.Sleep(100);
        }

        private void StartAddrView()
        {
            if(TextBoxViewAddr!="")
            {
                ViewStartAddress = Convert.ToInt32(TextBoxViewAddr);
                if(ViewStartAddress < 0)
                {
                    ViewStartAddress = 0;
                }
            }
            else
            {
                ViewStartAddress = 0;
            }

        }

        private void AutoViewThreadStop()
        {
            ViewStart = false;
            Thread.Sleep(500);
        }

        private void AutoViewRun()
        {
            while(ViewStart)
            {
                if(!CGlobal.ProgramStart)
                {
                    break;
                }
                Thread.Sleep(100);
                StartViewList();
            }
        }

        private void StartViewList()
        {
            if (CGlobal.MelsecProtocol._MelsecServer == null) return;

            if(ViewStartAddress > -1 && ViewStartAddress <= 40000)
            {
                List<MelsecDataWord> NewList = new List<MelsecDataWord>();
                ObservableCollection<string> Address = new ObservableCollection<string>();
                ObservableCollection<string> Value = new ObservableCollection<string>();

                for (int i = ViewStartAddress; i < ViewStartAddress + 99; i++)
                {
                    if (i >= 40000)
                    {
                        break;
                    }
                    else
                    {
                        NewList.Add(CGlobal.MelsecDataWordInfo[i]);
                    }
                }
                WordDataInfo = NewList;
                //WordAddress = Address.ToList();
                //WordValue = Value.ToList();
            }
        }

    }
}
