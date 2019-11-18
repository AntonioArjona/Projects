using A1AR.SVC.Worker.Lib.Attributes;
using A1AR.SVC.Worker.Lib.Common;

namespace CustomerTaskTLG
{
    public class Settings : IWorkerSetting
    {
        [WorkerSettingConnection("dbConnection", "Data Source=localhost; Initial Catalog=A1AR.SVC.Task.Engine; User=agresso; Password=agresso")]
        public IDBConnectionFactory DbConnection { get; set; }

        [WorkerSetting]
        public string Client_username { get; set; }

        [WorkerSetting]
        public string Client_password { get; set; }

        [WorkerSetting]
        public string AuthenticationType { get; set; }

        [WorkerSetting]
        public string PaymentTermsId { get; set; }

        [WorkerSetting]
        public string CustomerGroupId { get; set; }

        [WorkerSetting]
        public string DebtCollectionCode { get; set; }

        [WorkerSetting]
        public string APIAddress { get; set; }

        [WorkerSetting]
        public string RelAttrId { get; set; }
    }
}
