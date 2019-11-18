using System;
using System.Collections.Generic;
using System.Data.Common;

namespace VendorTaskTLG
{

    public class Vendor
    {
        //public string BankAccount { get; set; }
        public string CompanyId { get; set; }
        //public string CompanyRegistrationNumber { get; set; }
        public string CountryCode { get; set; }
        //public DateTime ExpiryDate { get; set; }
        public string ExternalReference { get; set; }  
        public string LanguageCode { get; set; }
        //public int Priority { get; set; }
        public string AliasName { get; set; }
        public string Status { get; set; }
        public string SupplierGroupId { get; set; }
        public string SupplierId { get; set; }
        public string SupplierName { get; set; }
        public Invoice Invoice { get; set; }
        public List<ContactPoints> ContactPoints { get; set; }
        public List<RelatedValue> RelatedValues { get; set; }

        public Vendor()
        {}

        public Vendor(DbDataReader reader, Settings settings)
        {
            string supplierId = (string)reader["apar_id"];
            if (!string.IsNullOrEmpty(supplierId))
            {
                SupplierId = supplierId;
                //Add invoice data to vendor
                Invoice invoice = new Invoice();
                //invoice.CalculatePayDiscountOnTax = true;
                invoice.PaymentMethod = (string)reader["pay_method"];
                invoice.CurrencyCode = (string)settings.CurrencyCode;
                //invoice.TaxFilingRequirement = (string)settings.TaxFilingRequirement;
                invoice.PaymentTermsId = (string)settings.PaymentTermsId;
                Invoice = invoice;

                string supplierName = (string)reader["apar_name"];
                if (!string.IsNullOrEmpty(supplierName)) SupplierName = supplierName;
                else Console.WriteLine("Error: Vendor name not declared");

                string aliasName = (string)reader["short_name"];
                if (!string.IsNullOrEmpty(aliasName)) AliasName = aliasName;
                else Console.WriteLine("Error: Alias name not declared");

                string companyId = (string)reader["client"];
                if (!string.IsNullOrEmpty(companyId)) CompanyId = companyId;
                else Console.WriteLine("Error: Client not declared");

                string externalReference = (string)reader["ext_apar_ref"];
                if (!string.IsNullOrEmpty(externalReference)) ExternalReference = externalReference;
                else Console.WriteLine("Error: External reference not declared");

                string countryCode = (string)reader["country_code"];
                if (!string.IsNullOrEmpty(countryCode)) CountryCode = countryCode;
                else Console.WriteLine("Error: Country code not declared");

                LanguageCode = (string)settings.LanguageCode;

                string status = (string)reader["status"];
                if (!string.IsNullOrEmpty(status)) Status = status;
                else Console.WriteLine("Error: Statusde not declared");

                //SupplierGroupID it's a static value, so we use ATE task Settings to recover it
                string supplierGroupId = (string)reader["apar_gr_id"];
                if (!string.IsNullOrEmpty(supplierGroupId)) SupplierGroupId = supplierGroupId;
                else Console.WriteLine("Error: SupplierGroupId not declared");

                //Additional vendor information
                AdditionalContactInfo addContactInfo = new AdditionalContactInfo();
                string email = (string)reader["e_mail"];
                if (!string.IsNullOrEmpty(email)) addContactInfo.EMail = (string)reader["e_mail"];
                else Console.WriteLine("Error: Email not declared");

                //Adding address information
                Address address = new Address();
                if (!string.IsNullOrEmpty(countryCode)) address.CountryCode = countryCode;
                else Console.WriteLine("Error: Country code not declared");

                string streetAddress = (string)reader["address"];
                if (!string.IsNullOrEmpty(streetAddress)) address.StreetAddress = streetAddress;
                else Console.WriteLine("Error: Street Address not declared");

                string province = (string)reader["province"];
                if (!string.IsNullOrEmpty(province)) address.Province = province;
                else Console.WriteLine("Error: Province not declared");

                string place = (string)reader["place"];
                if (!string.IsNullOrEmpty(place)) address.Place = place;
                else Console.WriteLine("Error: Place not declared");

                string postcode = (string)reader["zip_code"];
                if (!string.IsNullOrEmpty(postcode)) address.Postcode = postcode;
                else Console.WriteLine("Error: Postcode not declared");

                //Setting additional vendor information and address information to contact point
                ContactPoints contactPoint = new ContactPoints();
                contactPoint.AdditionalContactInfo = addContactInfo;
                contactPoint.Address = address;

                //Setting phone numbers
                PhoneNumbers phoneNumbers = new PhoneNumbers();
                string telephone = (string)reader["telephone_1"];
                if (!string.IsNullOrEmpty(telephone)) phoneNumbers.Telephone1 = telephone;
                else Console.WriteLine("Telephone not declared");

                contactPoint.PhoneNumbers = phoneNumbers;
                List<ContactPoints> contactLists = new List<ContactPoints>();
                contactLists.Add(contactPoint);
                ContactPoints = contactLists;

                RelatedValue relatedValue = new RelatedValue();
                string relationID = (string)reader["rel_attr_id"];
                if (!string.IsNullOrEmpty(relationID)) relatedValue.RelationId = relationID;
                else Console.WriteLine("Error: Relation Id not declared");

                string relValue = (string)reader["rel_value"];
                if (!string.IsNullOrEmpty(relValue)) relatedValue.relatedValue = relValue;
                else Console.WriteLine("Error: RelValue not declared");

                List<RelatedValue> relatedValueList = new List<RelatedValue>();
                relatedValueList.Add(relatedValue);
                RelatedValues = relatedValueList;
            }
            else Console.WriteLine("Error: Vendor ID not declared");
        }
    }

    public class RelatedValue
    {
        //public double UnitValue { get; set; }
        //public string RelationGroup { get; set; }
        public string RelationId { get; set; }
        public string relatedValue { get; set; }
    }

    public class Invoice
    {
        //public bool CalculatePayDiscountOnTax { get; set; }
        //public string CreditLimit { get; set; }
        public string CurrencyCode { get; set; }
        public string PaymentTermsId { get; set; }
        public string PaymentMethod { get; set; }
        //public string TaxCode { get; set; }
        //public string TaxFilingRequirement { get; set; }
        //public string TaxSystem { get; set; }
    }

    public class Payment
    {
        //public string BankClearingCode { get; set; }
        //public string Iban { get; set; }
        //public string PayRecipient { get; set; }
        //public string PostalAccount { get; set; }
    }

    public class AdditionalContactInfo
    {
        //public string ContactPerson { get; set; }
        //public string ContactPosition { get; set; }
        public string EMail { get; set; }
        public string Url { get; set; }
    }

    public class Address
    {
        public string CountryCode { get; set; }
        public string Place { get; set; }
        public string Postcode { get; set; }
        public string Province { get; set; }
        public string StreetAddress { get; set; }
    }

    public class PhoneNumbers
    {
        public string Telephone1 { get; set; }
    }

    public class ContactPoints
    {
        public AdditionalContactInfo AdditionalContactInfo { get; set; }
        public Address Address { get; set; }
        //public string ContactPointType { get; set; }
        public PhoneNumbers PhoneNumbers { get; set; }
        public int SortOrder { get; set; }
    }
   
    public class CustomFieldGroups
    {
       
    }

}
