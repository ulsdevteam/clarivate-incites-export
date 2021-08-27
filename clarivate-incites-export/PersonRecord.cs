using System.Linq;
using CsvHelper.Configuration.Attributes;

namespace clarivate_incites_export
{
    class PersonRecord
    {
        // ID from peoplesoft / employee number
        [Index(0)]
        public string PersonID { get; set; }
        
        [Index(1)]
        public string FirstName { get; set; }
        
        [Index(2)]
        public string LastName { get; set; }
        
        [Index(3)]
        public string OrganizationID { get; set; }
        [Index(4)]
        public string OrganizationName { get; set; }
        
        [Index(5)]
        public string DocumentID { get; set; }
        
        // identifiers
        [Index(6)]
        public string AuthorID { get; set; }
        
        [Index(7)]
        public string EmailAddress { get; set; }
        
        [Index(8)]
        public string OtherNames { get; set; }
        
        [Index(9)]
        public string FormerInstitution { get; set; }
        
        public PersonRecord() {}

        public PersonRecord(Person person)
        {
            PersonID = person.EmployeeNbr;
            FirstName = person.FirstName;
            LastName = person.LastName;
            OrganizationID = person.OrganizationId;
            OrganizationName = person.OrganizationName;
            AuthorID = person.Identifiers.Count > 1
                ? string.Join("",person.Identifiers.Select(id => $"({id})"))
                : person.Identifiers.Select(id => id.ToString()).SingleOrDefault();
            EmailAddress = person.EmailAddresses.Count > 1
                ? string.Join("",person.EmailAddresses.Select(email => $"({email})"))
                : person.EmailAddresses.SingleOrDefault() ?? "no_email@pitt.edu";
        }

        public PersonRecord WithOrganization(string organizationId, string organizationName) {
            var newPerson = (PersonRecord) MemberwiseClone();
            newPerson.OrganizationID = organizationId;
            newPerson.OrganizationName = organizationName;
            return newPerson;
        }
    }
}