using A1AR.SVC.Worker.Lib.Common;

namespace CustomerTaskTLG
{
    public class Parameters : IFileWatcherParameters
    {
        public string FullPath { get; set; }
    }
}
