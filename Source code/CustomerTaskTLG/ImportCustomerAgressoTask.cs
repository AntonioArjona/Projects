using A1AR.SVC.Worker.Lib.Common;
using KellermanSoftware.CompareNetObjects;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using RestSharp.Authenticators;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;

namespace CustomerTaskTLG
{
    public class ImportCustomerAgressoTask : Worker<Parameters, Settings>
    {
        public ImportCustomerAgressoTask(Settings settings) : base(settings) { }

        public override async Task<JobResult> Execute(Parameters parameters)
        {
            try
            {
                Logger.Information("||||||||||||||||||||||| Starting process |||||||||||||||||||||||");
                //Counters for failed and total records
                int recordsNumber = 0, errorCount = 0;
                Logger.Information("- Connecting to database: ");
                using (var connection = (DbConnection)Settings.DbConnection.CreateConnection())
                {
                    Logger.Information("OK");
                    await connection.OpenAsync();
                    DbCommand command = GetCustomerDataCommand(connection);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (reader.Read())
                        {
                            recordsNumber++;
                            //Setting customer object values
                            string fullCustomerId = (string)reader["full_apar_id"];
                            string applicationclient = (string)reader["applicationclient"];
                            Logger.Information("\n"); Logger.Information("\n-------- Customer ID: " + fullCustomerId + "--------\n");
                            //Condition created to avoid processing a customer if address hasn't been set
                            if (string.IsNullOrEmpty((string)reader["place"]) && string.IsNullOrEmpty((string)reader["zip_code"]) &&
                                string.IsNullOrEmpty((string)reader["province"]) && string.IsNullOrEmpty((string)reader["address"]))
                            {
                                string codeResponse = "ERROR";
                                string System1ErrorMessage = "Address not established";
                                await updateStatusCustomer(codeResponse, fullCustomerId, applicationclient, System1ErrorMessage);
                                errorCount++;
                            }
                            else {
                                Customer customer = new Customer(reader, Settings);

                                Logger.Information("- Customer data received and processed: " + JsonConvert.SerializeObject(customer));
                                IRestResponse response = ExecutePostRestApi(customer);

                                Logger.Information("-------STATUS CODE: " + response.StatusCode.ToString());
                                if (response.StatusCode == System.Net.HttpStatusCode.Created)
                                {
                                    string codeResponse = "SUCCESS";
                                    string System1ErrorMessage = "";
                                    Logger.Information("- Update STG_Customer table");
                                    //Update Customer table
                                    await updateStatusCustomer(codeResponse, fullCustomerId, applicationclient, System1ErrorMessage);
                                }
                                else
                                {
                                    //If "response" returns an already existing customer value error, we have to update the customer
                                    if (response.Content.Contains("\"code\":3010") && response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                                    {
                                        Logger.Information("- Customer ID already exists, starting customer update");
                                        //We have to create a JSON with Patch struture for update every field in API Customer
                                        //First we have to get the customer data with the same CustomerId

                                        IRestResponse responseGet = ExecuteGetRestApi(customer);
                                        Customer customerOld = JsonConvert.DeserializeObject<Customer>(responseGet.Content);

                                        Logger.Information("- Looking for differences between new and old customers");
                                        response = CompareCustomerDifferencesAndUpdate(customer, customerOld);
                                        string codeResponse = "";
                                        string System1ErrorMessage = "";
                                        //If response is null means that we don't have to update
                                        if (response == null) codeResponse = "SUCCESS";
                                        else
                                        {
                                            if (response.StatusCode == System.Net.HttpStatusCode.OK) codeResponse = "SUCCESS";
                                            else
                                            {
                                                codeResponse = response.StatusCode.ToString();
                                                JObject o = JObject.Parse(response.Content);
                                                dynamic obj = o.SelectTokens("$..notificationMessages");
                                                System1ErrorMessage = JsonConvert.SerializeObject(obj);
                                                Logger.Information("- Errors = : " + System1ErrorMessage);
                                                errorCount++;
                                            }
                                        }
                                        await updateStatusCustomer(codeResponse, fullCustomerId, applicationclient, System1ErrorMessage);
                                    }
                                    else
                                    {
                                        Logger.Information($"- Error creating customer: {response.Content}");
                                        string codeResponse = response.StatusCode.ToString();

                                        Logger.Information("- Code response = : " + codeResponse);
                                        string System1ErrorMessage = "";

                                        JObject o = JObject.Parse(response.Content);
                                        dynamic obj = o.SelectTokens("$..notificationMessages");
                                        System1ErrorMessage = JsonConvert.SerializeObject(obj);

                                        Logger.Information("- Errors = : " + System1ErrorMessage);
                                        await updateStatusCustomer(codeResponse, fullCustomerId, applicationclient, System1ErrorMessage);
                                        errorCount++;
                                    }
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
            }
            return JobResult.Success("OK");
        }


        public async Task<bool> updateStatusCustomer(String codeResponse, String fullCustomerId, String applicationclient, String system1ErrorMessage) {

            using (var connection_update = (DbConnection)Settings.DbConnection.CreateConnection())
            {
                await connection_update.OpenAsync();
                Logger.Information("- Inserting operation code and message into STG_Customer: " + codeResponse + " - " + system1ErrorMessage);
                
                //Insert response into STG_Customer
                ResponseExec responseExec = new ResponseExec();
                responseExec.code = codeResponse;
                responseExec.message = system1ErrorMessage;
                DbCommand command2 = SetCustomerResponseCommand(connection_update, responseExec, fullCustomerId, applicationclient);
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
            var insert = $"INSERT INTO STG_MidOfficeBatchControl (CreatedDate,CreatedBy,UpdatedDate,UpdatedBy,TransactionType,Status,RecordCount,ErrorCount,ApplicationClient,CommissionRecordCount,CommissionErrorCount,ReProcessFlag) VALUES ('{datetime}','AGRNS','{datetime}','AGRNS','Agresso Customer','Finished',{recordsNumber},{errorCount},'AGRESSO',0,0,'')";
            command.CommandText = insert;
            return command;
        }

        public DbCommand GetCustomerDataCommand(DbConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText = @"SELECT left(rtrim('PC'),25) apar_gr_id,
            right(rtrim(c.customerid),7) apar_id,
            c.customerid as full_apar_id,
            case when c.name<> '' then left(rtrim(c.name),255) else right(rtrim(c.customerid),7) end apar_name,
            'R' apar_type,
            left(m.IncludedBranches,25) client,
            left(rtrim(case when c.NetSuiteExternalReference <> '' then c.NetSuiteExternalReference else '.' end),100) ext_apar_ref,
            'CH' pay_method,
            right(rtrim(c.customerid),7) short_name,
            case c.activeflag when 1 then 'N' else 'C' end status,
            left(rtrim(isnull(a.addr1,'') ),160) address,
            '1' address_type,
            left(rtrim(isnull(a.country, 'US')),25) country_code,
            left(rtrim(isnull(a.city,'')),40) place,
            left(rtrim(isnull(a.state,'')),40) province,
            left(rtrim(isnull(a.zip,'') ),15) zip_code,
            left(rtrim(c.email),255) e_mail,
            left(rtrim(c.webaddress),255) url_path,
            left(rtrim(isnull(a.phone,'') ), 35) telephone_1,
            left(rtrim(isnull(a.attention,'')),255) description,
            c.agentid agent,
            0 sequence_no,
            isnull(c.applicationclient,'') applicationclient,
            'AGENT' rel_name
            FROM STG_Customer c left JOIN STG_CustomerAddress a on c.customerid = a.customerid and c.applicationclient = a.applicationclient
            JOIN STG_MidOfficeControl m on c.applicationclient = m.applicationclient and m.application = 'AGRESSO'
            WHERE c.category = 'TRAVEL' and (c.System1RefernceID != 'SUCCESS' or c.System1RefernceID is null)";
            Logger.Information("- Executing query: " + command.CommandText);
            return command;
        }

        public DbCommand SetCustomerResponseCommand(DbConnection connection, ResponseExec responseExec, String fullCustomerId, String applicationclient)
        {
            var command = connection.CreateCommand();
            var update = $"UPDATE STG_Customer SET System1RefernceID = '{responseExec.code}', System1ErrorMessage = '{responseExec.message}' WHERE CustomerId = '{fullCustomerId}' AND ApplicationClient = '{applicationclient}'";
            if (responseExec.code == "SUCCESS")
            {
                update = $"UPDATE STG_Customer SET System1RefernceID = '{responseExec.code}', System1ErrorMessage = NULL WHERE CustomerId = '{fullCustomerId}' AND ApplicationClient = '{applicationclient}'";
            }
            //if code response is 3010, we set the message of update and success
            else if(responseExec.code == "3010") 
            {
                update = $"UPDATE STG_Customer SET System1RefernceID = 'SUCCESS', System1ErrorMessage = 'Customer updated' WHERE CustomerId = '{fullCustomerId}' AND ApplicationClient = '{applicationclient}'";
            }
            command.CommandText = update;
            Logger.Information("- Executing query: " + update);
            return command;
        }

        public IRestResponse ExecutePostRestApi(Customer customer)
        {
            //Setting API URL, Authentication and Serialize customer object to JSON
            string requestUrl = $"{Settings.APIAddress}";
            
            Logger.Information("- Trying to insert a new customer with API Customer POST: " + requestUrl);
            IRestClient client = new RestClient(requestUrl);
            AddAuthentication(client);

            IRestRequest request = new RestRequest(RestSharp.Method.POST);
            var jsonBody = JsonConvert.SerializeObject(customer);
            request.AddJsonBody(jsonBody);

            //Consuming API REST with POST method
            IRestResponse response = client.ExecuteAsPost(request, "POST");
            return response;
        }

        public IRestResponse ExecuteGetRestApi(Customer customer)
        {
            //Setting API URL, Authentication and Serialize customer object to JSON
            string requestUrl = $"{Settings.APIAddress}{customer.CustomerId}";
            
            Logger.Information("- Trying to get customer' data with API Customer GET: " + requestUrl);
            IRestClient client = new RestClient(requestUrl);
            AddAuthentication(client);

            IRestRequest request = new RestRequest(RestSharp.Method.GET);
            //Consuming API REST with GET method
            IRestResponse response = client.ExecuteAsGet(request, "GET");
            return response;
        }

        public IRestResponse ExecutePatchRestApi(Customer customer, string jsonBody)
        {
            //Setting API URL, Authentication and Serialize customer object to JSON
            string requestUrl = $"{Settings.APIAddress}{customer.CustomerId}?_action=patch";
            Logger.Information("- Trying to update a field with API Customer PATCH: " + requestUrl);
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
            // Getting authentication data from ATE task Settings
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

        public IRestResponse CompareCustomerDifferencesAndUpdate(Customer newCustomer, Customer oldCustomer)
        {

            CompareLogic compareLogic = new CompareLogic();
            compareLogic.Config.MaxDifferences = 100;
            compareLogic.Config.MembersToIgnore.Add("RelatedValues");
            //Create a couple objects to compare
            ComparisonResult result = compareLogic.Compare(newCustomer, oldCustomer);
            //These will be different, write out the differences
            string fulljsonBody = $"[";
            string jsonBody = "";
            bool firstCycle = true;
            JObject rss;
            foreach (var diff in result.Differences)
            {
                var valueNewCustomer = diff.Object1Value;
                var valueOldCustomer = diff.Object2Value;
                var propertyName = diff.PropertyName;
                //Exception created to avoid useless API calls, for example update a field from "" to (null)
                if ((valueNewCustomer == "(null)" && valueOldCustomer == "") || (valueOldCustomer == "(null)" && valueNewCustomer == "") || (valueNewCustomer == "(null)" && valueOldCustomer == "(null)") || (valueOldCustomer == "" && valueNewCustomer == "")) { }
                else
                {
                    if (propertyName != "Payment") {
                        if (valueNewCustomer == "(null)") valueNewCustomer = "";
                        string path = propertyName.Replace('.', '/');
                        path = path.Replace("[", "/");
                        path = path.Replace("]/", "/");
                        rss = new JObject(
                            new JProperty("path", path),
                            new JProperty("op", "Replace"),
                            new JProperty("value", valueNewCustomer)
                        );
                        if (firstCycle)
                        {
                            jsonBody = rss.ToString();
                            firstCycle = false;
                        }
                        else jsonBody = String.Concat(jsonBody, "," + rss.ToString());
                       
                    }
                }
            }

            var newrelatedValues = newCustomer.RelatedValues;
            var oldrelatedValues = oldCustomer.RelatedValues;
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
                        jsonBody = rss.ToString();
                        firstCycle = false;
                    }
                    else jsonBody = String.Concat(jsonBody, "," + rss.ToString());
                    
                }
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
                        jsonBody = rss.ToString();
                        firstCycle = false;
                    }
                    else jsonBody = String.Concat(jsonBody, "," + rss.ToString());
                    
                }
            }
            fulljsonBody = String.Concat(fulljsonBody, jsonBody,"]");
            IRestResponse resUpdate;
            if (fulljsonBody != "[]")
            {
                Logger.Information("- Updating: " + fulljsonBody);
                resUpdate = ExecutePatchRestApi(newCustomer, fulljsonBody);
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
