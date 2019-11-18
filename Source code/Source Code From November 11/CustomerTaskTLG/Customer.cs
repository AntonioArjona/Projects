using System;
using System.Collections.Generic;
using System.Data.Common;

namespace CustomerTaskTLG
{
    public class Customer
    {
        public string AliasName { get; set; }
        public string CompanyId { get; set; }
        //public string CompanyRegistrationNumber { get; set; }
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
            Invoice invoice = new Invoice();
            string customerId = (string)reader["apar_id"];
            if (!string.IsNullOrEmpty(customerId))
            {
                CustomerId = customerId;
                //Add invoice data to customer
                invoice.HeadOffice = customerId;
                invoice.PaymentTermsId = (string)settings.PaymentTermsId;
                Invoice = invoice;
            }
            else Console.WriteLine("Error: Customer ID not declared");
            string customerName = (string)reader["apar_name"];
            if (!string.IsNullOrEmpty(customerName)) CustomerName = customerName;
            else Console.WriteLine("Error: Customer name not declared");

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

            //CustomerGroupId it's a static value, so we use ATE task Settings to recover it
            CustomerGroupId = (string)settings.CustomerGroupId;

            //Additional customer information
            AdditionalContactInfo addContactInfo = new AdditionalContactInfo();
            string email = (string)reader["e_mail"];
            if (!string.IsNullOrEmpty(email)) addContactInfo.EMail = (string)reader["e_mail"];
            else Console.WriteLine("Error: Email not declared");

            //Adding address information
            Address address = new Address();
            if (!string.IsNullOrEmpty(countryCode)) address.CountryCode = countryCode;
            else Console.WriteLine("Error: Country code not declared");

            string place = (string)reader["place"];
            if (!string.IsNullOrEmpty(place)) address.Place = place;
            else Console.WriteLine("Error: Place not declared");

            string postcode = (string)reader["zip_code"];
            if (!string.IsNullOrEmpty(postcode)) address.Postcode = postcode;
            else Console.WriteLine("Error: Postcode not declared");

            string province = (string)reader["province"];
            if (!string.IsNullOrEmpty(province)) address.Province = province;
            else Console.WriteLine("Error: Province not declared");

            string streetAddress = (string)reader["address"];
            if (!string.IsNullOrEmpty(streetAddress)) address.StreetAddress = streetAddress;
            else Console.WriteLine("Error: Street Address not declared");

            //Setting additional customer information and address information to contact point
            ContactPoints contactPoint = new ContactPoints();
            contactPoint.AdditionalContactInfo = addContactInfo;
            contactPoint.Address = address;

            string contactPointType = (string)reader["address_type"];
            if (!string.IsNullOrEmpty(contactPointType)) contactPoint.ContactPointType = contactPointType;
            else Console.WriteLine("Error:Address type not declared");
            
            PhoneNumbers phoneNumbers = new PhoneNumbers();
            
            string telephone_1 = (string)reader["telephone_1"];
            if (!string.IsNullOrEmpty(telephone_1)) phoneNumbers.Telephone1 = telephone_1;
            else Console.WriteLine("Error: Telephone not declared");

            contactPoint.PhoneNumbers = phoneNumbers;
            List<ContactPoints> contactLists = new List<ContactPoints>();
            contactLists.Add(contactPoint);
            ContactPoints = contactLists;

            RelatedValue relatedValue = new RelatedValue();
            string relationID = (string)reader["rel_value"];
            if (!string.IsNullOrEmpty(relationID)) relatedValue.RelationId = relationID;
            else Console.WriteLine("Error: Relation Id not declared");

            string relationName = (string)reader["rel_name"];
            if (!string.IsNullOrEmpty(relationName)) relatedValue.RelationName = relationName;
            else Console.WriteLine("Error: Relation name not declared");

            string relationValue = (string)reader["agent"];
            if (!string.IsNullOrEmpty(relationValue)) relatedValue.relatedValue = relationValue;
            else Console.WriteLine("Error: Relation value not declared");

            List<RelatedValue> relatedValueList = new List<RelatedValue>();
            relatedValueList.Add(relatedValue);
            RelatedValues = relatedValueList;

            //Setting customer status on payment information
            Payment payment = new Payment();
            payment.DebtCollectionCode = (string)settings.DebtCollectionCode;
            payment.PayRecipient = "";
            string payMethod = (string)reader["pay_method"];
            if (!string.IsNullOrEmpty(payMethod)) payment.PayMethod = payMethod;
            else Console.WriteLine("Error: Postcode not declared");
            
            string status = (string)reader["status"];
            if (!string.IsNullOrEmpty(status))
            {
                if (status == "N") payment.Status = "Active";
                if (status == "C") payment.Status = "Close";
                if (status == "P") payment.Status = "Parked";
            }
            else
            {
                Console.WriteLine("Error: Status not declared");
            }
            Payment = payment;
            //Adding all extra data to customer object
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
        public string RelationName { get; set; }
        public string relatedValue { get; set; }
    }

}