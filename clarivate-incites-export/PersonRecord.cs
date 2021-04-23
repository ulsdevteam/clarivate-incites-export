using CsvHelper.Configuration.Attributes;

namespace clarivate_incites_export
{
    class PersonRecord
    {
        // ID from peoplesoft
        [Index(0)]
        public string PersonId { get; set; }
        
        [Index(1)]
        public string FirstName { get; set; }
        
        [Index(2)]
        public string LastName { get; set; }
        
        [Index(3)]
        public string OrganizationId { get; set; }
        
        [Index(4)]
        public string DocumentId { get; set; }
        
        // orcid/symplectic id/web of science id?
        [Index(5)]
        public string AuthorId { get; set; }
        
        [Index(6)]
        public string EmailAddress { get; set; }
        
        [Index(7)]
        public string OtherNames { get; set; }
        
        [Index(8)]
        public string FormerInstitution { get; set; }
        
        public PersonRecord() {}

        public PersonRecord(Person person)
        {
            PersonId = person.EmployeeNbr;
            FirstName = person.FirstName;
            LastName = person.LastName;
            OrganizationId = person.OrganizationId;
        }
    }
}