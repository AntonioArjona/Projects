using System.Collections.Generic;
using System.Data.Common;

namespace VendorTaskTLG
{

    public class Vendor
    {
        public string CompanyId { get; set; }
        public string CountryCode { get; set; }
        public string ExternalReference { get; set; }  
        public string LanguageCode { get; set; }
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
                Invoice = new Invoice();
                //invoice.CalculatePayDiscountOnTax = true;
                Invoice.PaymentMethod = (string)reader["pay_method"];
                Invoice.CurrencyCode = (string)settings.CurrencyCode;
                //invoice.TaxFilingRequirement = (string)settings.TaxFilingRequirement;
                Invoice.PaymentTermsId = (string)settings.PaymentTermsId;

                if (!string.IsNullOrEmpty((string)reader["apar_name"])) SupplierName = (string)reader["apar_name"];

                if (!string.IsNullOrEmpty((string)reader["short_name"])) AliasName = (string)reader["short_name"];

                if (!string.IsNullOrEmpty((string)reader["client"])) CompanyId = (string)reader["client"];

                if (!string.IsNullOrEmpty((string)reader["ext_apar_ref"])) ExternalReference = (string)reader["ext_apar_ref"];

                LanguageCode = (string)settings.LanguageCode;

                if (!string.IsNullOrEmpty((string)reader["status"])) Status = (string)reader["status"];

                if (!string.IsNullOrEmpty((string)reader["apar_gr_id"])) SupplierGroupId = (string)reader["apar_gr_id"];

                //Additional vendor information
                AdditionalContactInfo addContactInfo = new AdditionalContactInfo();
                if (!string.IsNullOrEmpty((string)reader["e_mail"])) addContactInfo.EMail = (string)reader["e_mail"];

                //Adding address information
                string countryCode = (string)settings.LanguageCode;
                Address address = new Address();
                if (!string.IsNullOrEmpty(countryCode)) address.CountryCode = countryCode;

                if (!string.IsNullOrEmpty((string)reader["address"])) address.StreetAddress = (string)reader["address"];

                if (!string.IsNullOrEmpty((string)reader["province"])) address.Province = (string)reader["province"];

                if (!string.IsNullOrEmpty((string)reader["place"])) address.Place = (string)reader["place"];

                if (!string.IsNullOrEmpty((string)reader["zip_code"])) address.Postcode = (string)reader["zip_code"];

                //Setting additional vendor information and address information to contact point
                ContactPoints contactPoint = new ContactPoints();
                contactPoint.AdditionalContactInfo = addContactInfo;
                contactPoint.Address = address;

                //Setting phone numbers
                PhoneNumbers phoneNumbers = new PhoneNumbers();
                if (!string.IsNullOrEmpty((string)reader["telephone_1"])) phoneNumbers.Telephone1 = (string)reader["telephone_1"];

                contactPoint.PhoneNumbers = phoneNumbers;
                List<ContactPoints> contactLists = new List<ContactPoints>();
                contactLists.Add(contactPoint);
                ContactPoints = contactLists;

                RelatedValue relatedValue = new RelatedValue();
                if (!string.IsNullOrEmpty((string)settings.RelAttrId)) relatedValue.RelationId = (string)settings.RelAttrId;

                if (!string.IsNullOrEmpty((string)reader["rel_value"])) relatedValue.relatedValue = (string)reader["rel_value"];

                List<RelatedValue> relatedValueList = new List<RelatedValue>();
                relatedValueList.Add(relatedValue);
                RelatedValues = relatedValueList;
            }
        }
    }

    public class RelatedValue
    {
        public string RelationId { get; set; }
        public string relatedValue { get; set; }
    }

    public class Invoice
    {
        public string CurrencyCode { get; set; }
        public string PaymentTermsId { get; set; }
        public string PaymentMethod { get; set; }
    }

    public class Payment
    {
    }

    public class AdditionalContactInfo
    {
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
        public PhoneNumbers PhoneNumbers { get; set; }
        public int SortOrder { get; set; }
    }
   
    public class CustomFieldGroups
    {
    }

}
