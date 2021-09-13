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
        public string TenureStatus { get; set; }
        public string JobKey { get; set; }
        public string JobName { get; set; }
        public HashSet<Identifier> Identifiers { get; init; } = new HashSet<Identifier>();
        public HashSet<string> EmailAddresses { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public Person(EmployeeData employeeData) 
        {
            EmplId = employeeData.EMPLID;
            EmployeeNbr = employeeData.EMPLOYEE_NBR;
            Username = employeeData.USERNAME;
            FirstName = employeeData.FIRST_NAME;
            LastName = employeeData.LAST_NAME;
            OrganizationId = employeeData.LeafOrganizationID;
            OrganizationName = employeeData.LeafOrganizationName;
            TenureStatus = employeeData.FACULTY_TENURE_STATUS_DESCR;
            if (employeeData.EMAIL_ADDRESS is not null) 
                EmailAddresses.Add(employeeData.EMAIL_ADDRESS);
            JobKey = employeeData.JOB_KEY;
            JobName = $"{employeeData.JOB_TYPE} - {employeeData.JOB_FAMILY} - {employeeData.JOB_CLASS}";
        }
    }
}