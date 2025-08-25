using System.Reactive.Linq;
using ReactiveUI;
namespace PdfCounter.Models
{

    public class SaveJobProperty : ReactiveObject
    {
        string _jobName = "";
        public string JobName
        {
            get => _jobName;
            set => this.RaiseAndSetIfChanged(ref _jobName, value);
        }
    }
}