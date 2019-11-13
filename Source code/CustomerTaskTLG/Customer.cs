using System.Collections.Generic;
using System.Data.Common;

namespace CustomerTaskTLG
{
    public class Customer
    {
        public string AliasName { get; set; }
        public string CompanyId { get; set; }
        public string CountryCode { get; set; }
        public string CustomerGroupId { get; set; }
        public string CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string ExternalReference { get; set; }
        public Invoice Invoice { get; set; }
        public Payment Payment { get; set; }
        public List<ContactPoints> ContactPoints { get; set; }
        public List<RelatedValue> RelatedValues { get; set; }

        public Customer()
        {}

        public Customer(DbDataReader reader, Settings settings)
        {
            //Customer main part
            Invoice = new Invoice();
            string customerId = (string)reader["apar_id"];
            if (!string.IsNullOrEmpty(customerId))
            {
                CustomerId = customerId;
                //Add invoice data to customer
                Invoice.HeadOffice = customerId;
                Invoice.PaymentTermsId = (string)settings.PaymentTermsId;
            }
            if (!string.IsNullOrEmpty((string)reader["apar_name"])) CustomerName = (string)reader["apar_name"];

            if (!string.IsNullOrEmpty((string)reader["short_name"])) AliasName = (string)reader["short_name"];

            if (!string.IsNullOrEmpty((string)reader["client"])) CompanyId = (string)reader["client"];

            if (!string.IsNullOrEmpty((string)reader["ext_apar_ref"])) ExternalReference = (string)reader["ext_apar_ref"];

            if (!string.IsNullOrEmpty((string)settings.CountryCode)) CountryCode = (string)settings.CountryCode;

            //CustomerGroupId it's a static value, so we use ATE task Settings to recover it
            CustomerGroupId = (string)settings.CustomerGroupId;

            //Additional customer information
            AdditionalContactInfo addContactInfo = new AdditionalContactInfo();
            if (!string.IsNullOrEmpty((string)reader["e_mail"])) addContactInfo.EMail = (string)reader["e_mail"];
            
            //Setting additional customer information and address information to contact point
            ContactPoints contactPoint = new ContactPoints();

            //Adding address information
            contactPoint.Address = new Address();
            if (!string.IsNullOrEmpty((string)settings.CountryCode)) contactPoint.Address.CountryCode = (string)settings.CountryCode;

            if (!string.IsNullOrEmpty((string)reader["place"])) contactPoint.Address.Place = (string)reader["place"];

            if (!string.IsNullOrEmpty((string)reader["zip_code"])) contactPoint.Address.Postcode = (string)reader["zip_code"];

            if (!string.IsNullOrEmpty((string)reader["province"])) contactPoint.Address.Province = (string)reader["province"];

            if (!string.IsNullOrEmpty((string)reader["address"])) contactPoint.Address.StreetAddress = (string)reader["address"];

            contactPoint.AdditionalContactInfo = addContactInfo;

            if (!string.IsNullOrEmpty((string)reader["address_type"])) contactPoint.ContactPointType = (string)reader["address_type"];

            contactPoint.PhoneNumbers = new PhoneNumbers();
            
            if (!string.IsNullOrEmpty((string)reader["telephone_1"])) contactPoint.PhoneNumbers.Telephone1 = (string)reader["telephone_1"];

            List<ContactPoints> contactLists = new List<ContactPoints>();
            contactLists.Add(contactPoint);
            ContactPoints = contactLists;

            RelatedValue relatedValue = new RelatedValue();
            if (!string.IsNullOrEmpty((string)settings.RelAttrId)) relatedValue.RelationId = (string)settings.RelAttrId;

            if (!string.IsNullOrEmpty((string)reader["agent"])) relatedValue.relatedValue = (string)reader["agent"];

            List<RelatedValue> relatedValueList = new List<RelatedValue>();
            relatedValueList.Add(relatedValue);
            RelatedValues = relatedValueList;

            //Setting customer status on payment information
            Payment = new Payment();
            Payment.DebtCollectionCode = (string)settings.DebtCollectionCode;
            Payment.PayRecipient = "";
            if (!string.IsNullOrEmpty((string)reader["pay_method"])) Payment.PayMethod = (string)reader["pay_method"];
            
            if (!string.IsNullOrEmpty((string)reader["status"]))
            {
                if ((string)reader["status"] == "N") Payment.Status = "Active";
                if ((string)reader["status"] == "C") Payment.Status = "Close";
                if ((string)reader["status"] == "P") Payment.Status = "Parked";
            }
        }
    }

    public class Payment
    {
        public string DebtCollectionCode { get; set; }
        public string PayMethod { get; set; }
        public string Status { get; set; }
        public string PayRecipient { get; set; }
    }

    public class Invoice
    {   
        public string HeadOffice { get; set; }
        public string PaymentTermsId { get; set; }
    }

    public class Address
    {
        public string CountryCode { get; set; }
        public string Place { get; set; }
        public string Postcode { get; set; }
        public string Province { get; set; }
        public string StreetAddress { get; set; }
    }

    public class AdditionalContactInfo
    {
        public string EMail { get; set; }
        public string Url { get; set; }
    }

    public class PhoneNumbers
    {
        public string Telephone1 { get; set; }
    }

    public class ContactPoints
    {
        public Address Address { get; set; }
        public AdditionalContactInfo AdditionalContactInfo { get; set; }
        public string ContactPointType { get; set; }
        public PhoneNumbers PhoneNumbers { get; set; }
        public int SortOrder { get; set; }
    }

    public class RelatedValue
    {
        public string RelationId { get; set; }
        public string relatedValue { get; set; }
    }

}