using System;
using System.Collections.Generic;

namespace clarivate_incites_export
{
    public class Person
    {
        public string EmplId { get; set; }
        public string EmployeeNbr { get; set; }
        public string Username { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string OrganizationId { get; set; }
        public string OrganizationName { get; set; }
        public HashSet<Identifier> Identifiers { get; init; } = new HashSet<Identifier>();
        public HashSet<string> EmailAddresses { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }
}