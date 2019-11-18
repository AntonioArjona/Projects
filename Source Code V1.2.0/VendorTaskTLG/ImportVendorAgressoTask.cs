using A1AR.SVC.Worker.Lib.Common;
using KellermanSoftware.CompareNetObjects;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using RestSharp.Authenticators;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Reflection;
using System.Threading.Tasks;

namespace VendorTaskTLG
{
    public class ImportVendorAgressoTask : Worker<Parameters, Settings>
    {
        public ImportVendorAgressoTask(Settings settings) : base(settings) { }

        public override async Task<JobResult> Execute(Parameters parameters)
        {
            try
            {
                Logger.Information("||||||||||||||||||||||| Starting process Version: " + Assembly.GetExecutingAssembly().GetName().Version +"|||||||||||||||||||||||");

                //Check all Settings
                int missingSettings = 0;
                PropertyInfo[] properties = Settings.GetType().GetProperties();
                foreach (PropertyInfo pi in properties)
                {
                    if (pi.PropertyType == typeof(string))
                    {
                        if (String.IsNullOrEmpty((string)pi.GetValue(Settings)))
                        {
                            Logger.Information("Setting " + pi.Name + " not set");
                            missingSettings++;
                        }
                    }
                    else
                    {
                        if (pi.GetValue(Settings) == null)
                        {
                            Logger.Information("Setting " + pi.Name + " not set");
                            missingSettings++;
                        }
                    }
                }
                if (missingSettings > 0) return JobResult.Success("ERROR");

                //Counters for successful and failed records
                int recordsNumber = 0, errorCount = 0;
                Logger.Information("- Connecting to database");
                using (var connection = (DbConnection)Settings.DbConnection.CreateConnection())
                {
                    Logger.Information("OK");
                    await connection.OpenAsync();
                    DbCommand command = GetVendorDataCommand(connection);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (reader.Read())
                        {
                            recordsNumber++;
                            //Setting vendor object values
                            string fullVendorId = (string)reader["full_apar_id"];
                            string applicationclient = (string)reader["applicationclient"];
                            Logger.Information("\n");
                            Logger.Information("-------- Vendor ID: " + (string)reader["apar_id"] + "--------\n");

                            if (string.IsNullOrEmpty((string)reader["client"]))
                            {
                                string codeResponse = "ERROR";
                                string System1ErrorMessage = "Client not established";
                                await updateStatusVendor(codeResponse, fullVendorId, applicationclient, System1ErrorMessage);
                                errorCount++;
                                continue;
                            }

                            Vendor vendor = new Vendor(reader, Settings);

                            Logger.Information("- Vendor data received: " + JsonConvert.SerializeObject(vendor));
                            IRestResponse response = ExecutePostRestApi(vendor);

                            Logger.Information("-------STATUS CODE: " + response.StatusCode.ToString());
                           
                            if (response.StatusCode == System.Net.HttpStatusCode.Created)
                            {
                                string codeResponse = "SUCCESS";
                                string System1ErrorMessage = "";
                                Logger.Information("- Update Vendor table from staging");
                                //Update Vendor table from staging
                                await updateStatusVendor(codeResponse, fullVendorId, applicationclient, System1ErrorMessage);
                            }
                            else
                            {
                                //If "response" returns a duplicate vendorId value, we have to update vendor
                                if (response.Content.Contains("\"code\":3010") && response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                                {
                                    Logger.Information("- Vendor ID already exists, starting vendor update");
                                    //We have to create a JSON with Patch struture for update every field in API Supplier
                                    //First we have to get the vendor data with the same VendorId

                                    IRestResponse responseGet = ExecuteGetRestApi(vendor.SupplierId);
                                    Vendor vendorOld = JsonConvert.DeserializeObject<Vendor>(responseGet.Content);

                                    Logger.Information("- Looking for differences between new and old vendor");
                                    response = CompareVendorDifferencesAndUpdate(vendor, vendorOld);
                                    string codeResponse = "";
                                    string System1ErrorMessage = "";
                                    //If response is null means no patch was required ()
                                    if (response == null) codeResponse = "SUCCESS";
                                    else
                                    {
                                        if (response.StatusCode == System.Net.HttpStatusCode.OK) codeResponse = "SUCCESS";
                                        else
                                        {
                                            System1ErrorMessage = "";
                                            codeResponse = response.StatusCode.ToString();
                                            if (!String.IsNullOrEmpty(response.Content))
                                            {
                                                JObject o = JObject.Parse(response.Content);
                                                dynamic obj = o.SelectTokens("$..notificationMessages");
                                                System1ErrorMessage = JsonConvert.SerializeObject(obj);
                                            }
                                            else
                                            {
                                                System1ErrorMessage = "Unexpected response from API. Response content is empty or null.";
                                                codeResponse = "0";
                                            }
                                            Logger.Information("- Errors: " + System1ErrorMessage);
                                            errorCount++;
                                        }
                                    }
                                    await updateStatusVendor(codeResponse, fullVendorId, applicationclient, System1ErrorMessage);
                                }
                                else
                                {
                                    Logger.Information($"- Error creating vendor: {response.Content}");
                                    string codeResponse = response.StatusCode.ToString();

                                    Logger.Information("- Code response: " + codeResponse);
                                    string System1ErrorMessage = "";

                                    if (!String.IsNullOrEmpty(response.Content))
                                    {
                                        JObject o = JObject.Parse(response.Content);
                                        dynamic obj = o.SelectTokens("$..notificationMessages");
                                        System1ErrorMessage = JsonConvert.SerializeObject(obj);
                                    }
                                    else if (codeResponse == "0")
                                    {
                                        System1ErrorMessage = "ERROR CONNECTING TO AGRESSO API: Please check if API URL is accesible and active.";
                                        codeResponse = "0";
                                    }
                                    else
                                    {
                                        System1ErrorMessage = "Unexpected response from API. Response content is empty or null.";
                                        codeResponse = "0";
                                    }
                                    Logger.Information("- Errors: " + System1ErrorMessage);
                                    await updateStatusVendor(codeResponse, fullVendorId, applicationclient, System1ErrorMessage);
                                    errorCount++;
                                }
                            }
                        }
                        //Insert into MidOfficeBatch
                        using (var connectionMidOfficeBatch = (DbConnection)Settings.DbConnection.CreateConnection())
                        {
                            await connectionMidOfficeBatch.OpenAsync();
                            DbCommand command3 = SetMidOfficeBatchCommand(connectionMidOfficeBatch, errorCount, recordsNumber);
                            Logger.Information("Inserting operation into MidOfficeBatch");
                            var resUpdateMidOffice = await command3.ExecuteNonQueryAsync();
                        }
                    }
                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                Logger.Error(ex.StackTrace);
            }
            return JobResult.Success("OK");
        }

        public async Task<bool> updateStatusVendor(String codeResponse, String fullVendorId, String applicationclient, String system1ErrorMessage) {
            using (var connection_update = (DbConnection)Settings.DbConnection.CreateConnection())
            {
                await connection_update.OpenAsync();
                Logger.Information("- Inserting operation code and message into STG_Vendor: " + codeResponse + " - " + system1ErrorMessage);
                ResponseExec responseExec = new ResponseExec();
                responseExec.code = codeResponse;
                responseExec.message = system1ErrorMessage;
                DbCommand command2 = SetVendorResponseCommand(connection_update, responseExec, fullVendorId, applicationclient);
                var resUpdate = await command2.ExecuteNonQueryAsync();
                return true;
            }

        }

        public DbCommand SetMidOfficeBatchCommand(DbConnection connection, int errorCount, int recordsNumber)
        {
            DateTime time = DateTime.Now;
            string format = "yyyy-MM-dd HH:mm:ss";
            string datetime = time.ToString(format);
            var command = connection.CreateCommand();
            var insert = $"INSERT INTO STG_MidOfficeBatchControl (CreatedDate,CreatedBy,UpdatedDate,UpdatedBy,TransactionType,Status,RecordCount,ErrorCount,ApplicationClient,CommissionRecordCount,CommissionErrorCount,ReProcessFlag) VALUES ('{datetime}','AGRNS','{datetime}','AGRNS','Agresso Vendor','Finished',{recordsNumber},{errorCount},'AGRESSO',0,0,'')";
            command.CommandText = insert;
            return command;
        }

        
        public DbCommand GetVendorDataCommand(DbConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText = $@"SELECT left(rtrim(case v.VendorCategory when 'Agent' then 'P4' when 'SUPPLIER' then 'P2' end ), 25) apar_gr_id,
            right(rtrim(isnull(v.NetSuiteExternalReference,'')),25) apar_id,
            isnull(v.NetSuiteExternalReference,'') full_apar_id,
            left(rtrim(case when v.name<> '' then v.name else v.vendorid end), 255) apar_name,
            'P' apar_type,
            isnull(v.applicationclient,'') applicationclient,
            left(rtrim(isnull(m.IncludedBranches, '')),25) client,
            left(rtrim(case when v.Vendorid <> '' then isnull(v.vendorid,'') else '.' end), 100) ext_apar_ref,
            'CH' pay_method,
            left(rtrim(isnull(v.vendorid,'')),3) short_name,
            case v.ACTIVEFLAG when 1 then 'N' else 'C' end status,
            left(rtrim(isnull(a.addr1,'')),160) address,
            '1' address_type,
            left(rtrim(isnull(a.city,'')),40) place,
            left(rtrim(isnull(a.state,'')),40) province,
            left(rtrim(isnull(a.zip,'')),15) zip_code,
            left(rtrim(isnull(a.email,'')),255) e_mail,
            left(rtrim(isnull(v.weburl,'')),255) url_path,
            left(rtrim(isnull(a.phone,'')),35) telephone_1,
            left(rtrim(isnull(a.attention,'')),255) description,
            0 sequence_no,
            isnull(rg.AgrRevGrp,'') rel_value
            FROM STG_VendorMaster v LEFT OUTER JOIN STG_VendorMasterAddress a on v.vendorid = a.vendorid and v.applicationclient = a.applicationclient
            LEFT OUTER JOIN STG_MidOfficeControl m on v.applicationclient = m.applicationclient and m.application = 'AGRESSO'
            LEFT OUTER JOIN STG_RevGrpReference rg on v.suppliertype = rg.NSSupplierType and
            v.applicationclient = rg.applicationclient
            WHERE v.vendorcategory in ('AGENT', 'SUPPLIER') and (v.System1RefernceID != 'SUCCESS' or v.System1RefernceID is null)";
            Logger.Information("Executing query: " + command.CommandText);
            return command;
        }

        public DbCommand SetVendorResponseCommand(DbConnection connection, ResponseExec responseExec, String fullCustomerId, String applicationclient)
        {
            var command = connection.CreateCommand();
            responseExec.message = responseExec.message.Replace("'", "");
            fullCustomerId = fullCustomerId.Replace("'", "");
            var update = $"UPDATE STG_VendorMaster SET System1RefernceID = '{responseExec.code}', System1ErrorMessage = '{responseExec.message}' WHERE NetSuiteExternalReference = '{fullCustomerId}' AND ApplicationClient = '{applicationclient}'";
            if (responseExec.code == "SUCCESS") update = $"UPDATE STG_VendorMaster SET System1RefernceID = '{responseExec.code}', System1ErrorMessage = NULL WHERE NetSuiteExternalReference = '{fullCustomerId}' AND ApplicationClient = '{applicationclient}'";
            //if code response is 3010, we set the message of update and success
            else if (responseExec.code == "3010") update = $"UPDATE STG_VendorMaster SET System1RefernceID = 'SUCCESS', System1ErrorMessage = 'Vendor updated' WHERE NetSuiteExternalReference = '{fullCustomerId}' AND ApplicationClient = '{applicationclient}'";
            command.CommandText = update;
            Logger.Information("- Executing query: "+update);
            return command;
        }

        public IRestResponse ExecutePostRestApi(Vendor vendor)
        {
            //Setting API URL, Authentication and Serialize customer object to JSON
            string requestUrl = $"{Settings.APIAddress}";

            Logger.Information("- Trying to insert a new vendor with API Suppliers POST: " + requestUrl);
            IRestClient client = new RestClient(requestUrl);
            AddAuthentication(client);

            IRestRequest request = new RestRequest(RestSharp.Method.POST);
            var jsonBody = JsonConvert.SerializeObject(vendor);
            request.AddJsonBody(jsonBody);

            //Consuming API REST with POST method
            IRestResponse response = client.ExecuteAsPost(request, "POST");
            return response;
        }

        public IRestResponse ExecuteGetRestApi(String supplierId)
        {
            //Setting API URL, Authentication and Serialize customer object to JSON
            string requestUrl = $"{Settings.APIAddress}{supplierId}";

            Logger.Information("- Trying to get vendor data with API Suppliers GET: " + requestUrl);
            IRestClient client = new RestClient(requestUrl);
            AddAuthentication(client);

            IRestRequest request = new RestRequest(RestSharp.Method.GET);
            //Consuming API REST with GET method
            IRestResponse response = client.ExecuteAsGet(request, "GET");
            return response;
        }

        public IRestResponse ExecutePatchRestApi(string supplierId, string jsonBody)
        {
            //Setting API URL, Authentication and Serialize customer object to JSON
            string requestUrl = $"{Settings.APIAddress}{supplierId}?_action=patch";
            Logger.Information("- Trying to update a field with API Vendor PATCH: " + requestUrl);
            IRestClient client = new RestClient(requestUrl);
            AddAuthentication(client);
            IRestRequest request = new RestRequest(RestSharp.Method.PATCH);
            request.AddJsonBody(jsonBody);
            request.AddHeader("content-type", "application/json-patch+json");

            //Consuming API REST with PATCH method
            IRestResponse response = client.Execute(request);
            return response;
        }

        public void AddAuthentication(IRestClient client)
        {
            // Recovering authentication data from ATE task Settings
            string authenticationType = Settings.AuthenticationType;
            string username = Settings.Client_username;
            string password = Settings.Client_password;
            IAuthenticator authenticator;
            switch (authenticationType.Trim().ToUpper())
            {
                case "BASIC":
                    authenticator = new HttpBasicAuthenticator(username, password);
                    break;
                case "NTLM":
                    authenticator = new NtlmAuthenticator(username, password);
                    break;
                default:
                    throw new Exception($"Unknown authentication type: {authenticationType}");
            }
            client.Authenticator = authenticator;
        }

        public IRestResponse CompareVendorDifferencesAndUpdate(Vendor newVendor, Vendor oldVendor)
        {

            CompareLogic compareLogic = new CompareLogic();
            compareLogic.Config.MaxDifferences = 100;
            compareLogic.Config.MembersToIgnore.Add("RelatedValues");
            //Create a couple objects to compare
            ComparisonResult result = compareLogic.Compare(newVendor, oldVendor);
            //These will be different, write out the differences
            string fulljsonBody = $"[";
            string jsonBody = "";
            bool firstCycle = true;
            JObject rss;
            foreach (var diff in result.Differences)
            {
                var valueNewVendor = diff.Object1Value;
                var valueOldVendor = diff.Object2Value;
                var propertyName = diff.PropertyName;
                //Exception created to avoid useless API calls, for example update a field from "" to (null)
                if ((valueNewVendor == "(null)" && valueOldVendor == "") || (valueOldVendor == "(null)" && valueNewVendor == "") || (valueNewVendor == "(null)" && valueOldVendor == "(null)") || (valueOldVendor == "" && valueNewVendor == "")) { }
                else
                {
                    if (propertyName != "Payment" && propertyName != "ContactPoints")
                    {
                        if (valueNewVendor == "(null)") valueNewVendor = "";
                        string path = propertyName.Replace('.', '/');
                        path = path.Replace("[", "/");
                        path = path.Replace("]/", "/");
                        rss = new JObject(
                            new JProperty("path", path),
                            new JProperty("op", "Replace"),
                            new JProperty("value", valueNewVendor)
                        );
                        if (firstCycle)
                        {
                            jsonBody = String.Concat(jsonBody, rss.ToString());
                            firstCycle = false;
                        }
                        else jsonBody = String.Concat(jsonBody, "," + rss.ToString());
                    }
                }
            }

            var newrelatedValues = newVendor.RelatedValues;
            var oldrelatedValues = oldVendor.RelatedValues;
            int newindex = newrelatedValues.FindIndex(a => a.RelationId == Settings.RelAttrId);
            int oldindex = oldrelatedValues.FindIndex(a => a.RelationId == Settings.RelAttrId);
            if (newindex >= 0)
            {
                Logger.Information("---------------------- TRYING TO UPDATE RELATED VALUES");
                //Start comparing elements
                //relatedValue
                if (oldindex < 0)
                {
                    RelatedValue relatedVal = new RelatedValue();
                    relatedVal.relatedValue = newrelatedValues[newindex].relatedValue;
                    relatedVal.RelationId = Settings.RelAttrId;
                    List<RelatedValue> listRelVal = new List<RelatedValue>();
                    listRelVal.Add(relatedVal);
                    JArray listRelatedValJson = JArray.FromObject(listRelVal);
                    rss = new JObject(
                                        new JProperty("path", $"RelatedValues"),
                                        new JProperty("op", "Add"),
                                        new JProperty("value", listRelatedValJson)
                                    );
                    if (firstCycle)
                    {
                        jsonBody = String.Concat(jsonBody, rss.ToString());
                        firstCycle = false;
                    }
                    else jsonBody = String.Concat(jsonBody, "," + rss.ToString());
                }
                else {
                    if (newrelatedValues[newindex].relatedValue != oldrelatedValues[oldindex].relatedValue)
                    {
                        rss = new JObject(
                                        new JProperty("path", $"RelatedValues/{oldindex}/relatedValue"),
                                        new JProperty("op", "Replace"),
                                        new JProperty("value", newrelatedValues[newindex].relatedValue)
                                    );
                        if (firstCycle)
                        {
                            jsonBody = String.Concat(jsonBody, rss.ToString());
                            firstCycle = false;
                        }
                        else jsonBody = String.Concat(jsonBody, "," + rss.ToString());
                    }
                }
            }
            fulljsonBody = String.Concat(fulljsonBody, jsonBody, "]");
            IRestResponse resUpdate;
            if (fulljsonBody != "[]")
            {
                Logger.Information("- Updating: " + fulljsonBody);
                resUpdate = ExecutePatchRestApi(newVendor.SupplierId, fulljsonBody);
                Logger.Information("- Result: " + resUpdate.Content);
            }
            else resUpdate = null;
            return resUpdate;
        }
    }

    public class ResponseExec
    {
        public string code { get; set; }
        public string message { get; set; }
    }
}
