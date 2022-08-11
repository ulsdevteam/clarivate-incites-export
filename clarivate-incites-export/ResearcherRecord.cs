using System.Linq;
using CsvHelper.Configuration.Attributes;

namespace clarivate_incites_export
{
    class ResearcherRecord
    {
        public ResearcherRecord(EmployeeData employee, OrgHierarchyRecord organization, Identifiers identifiers)
        {
            PersonID = employee.EMPLOYEE_NBR;
            FirstName = employee.FIRST_NAME;
            LastName = employee.LAST_NAME;
            OrganizationID = organization.OrganizationID;
            OrganizationName = organization.OrganizationName;
            AuthorID = identifiers.Ids.Count > 1
                ? string.Join("", identifiers.Ids.Select(id => $"({id})"))
                : identifiers.Ids.Select(id => id.ToString()).SingleOrDefault();
            EmailAddress = identifiers.Emails.Count > 1
                ? string.Join("", identifiers.Emails.Select(email => $"({email})"))
                : identifiers.Emails.SingleOrDefault() ?? "no_email@pitt.edu";
        }

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
    }
}