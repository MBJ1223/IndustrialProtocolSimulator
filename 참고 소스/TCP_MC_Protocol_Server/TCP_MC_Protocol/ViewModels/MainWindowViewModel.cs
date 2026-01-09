using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Prism.Commands;
using Prism.Mvvm;

namespace TCP_MC_Protocol.ViewModels
{
    class MainWindowViewModel : BindableBase
    {

        private ICommand _loaded_Command;
        public ICommand Loaded_Command => (this._loaded_Command) ??
                   (_loaded_Command = new DelegateCommand(SocketOpen));


        private void SocketOpen()
        {
            //CGlobal._MelsecProtocol.ServerOpen(502);
        }

    }
}
