using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using A1AR.SVC.Worker.Lib.Common;

namespace VendorTaskTLG
{
    class DatabaseTools
    {
        public DbDataReader GetCustomerData(DbConnection connection, Vendor vendor)
        {
            var command = connection.CreateCommand();
            command.CommandText = @"SELECT 
                left(rtrim('PC'), 25) apar_gr_id, 
                right(rtrim(c.customerid), 7) apar_id, 
                case when c.name <> '' then left(rtrim(c.name),255) else right(rtrim(c.customerid), 7) end apar_name,
                'R' apar_type, 
                left(m.IncludedBranches, 25) client, 
                left(rtrim(case when c.NetSuiteExternalReference <> '' then c.NetSuiteExternalReference else '.' end),100) ext_apar_ref, 
                'CH' pay_method, 
                right(rtrim(c.customerid), 7) short_name, 
                case c.activeflag when 1 then 'N' else 'C' end status,
                left(rtrim(isnull(a.addr1, '')), 160) address, 
                1 address_type, 
                left(rtrim(isnull(a.country, 'US')), 25)  country_code, 
                left(rtrim(isnull(a.city, '')), 40) place, 
                left(rtrim(isnull(a.state, '')), 40) province, 
                left(rtrim(isnull(a.zip, '')), 15) zip_code,
                left(rtrim(c.email), 255) e_mail, 
                left(rtrim(c.webaddress), 255) url_path, 
                left(rtrim(isnull(a.phone, '')), 35) telephone_1, 
                left(rtrim(isnull(a.attention, '')), 255) description, 
                c.agentid agent,
                0 sequence_no
                FROM STG_Customer c left JOIN STG_CustomerAddress a on c.customerid = a.customerid and c.primarysubsidiary = a.applicationclient
                JOIN STG_MidOfficeControl m on c.applicationclient = m.applicationclient and m.application = 'AGRESSO'
                WHERE c.category = 'TRAVEL'";
            DbDataReader reader = command.ExecuteReader();
            return reader;
        }
    }
}
