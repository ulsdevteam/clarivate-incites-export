using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using Dapper;
using dotenv.net;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;

namespace clarivate_incites_export
{
    static class Program
    {
	    static readonly string EmployeeDataSql = @"
			select 
			ude.EMPLID,
			ude.EMPLOYEE_NBR,
			ude.USERNAME,
			ude.LAST_NAME,
			ude.FIRST_NAME,
			ude.EMAIL_ADDRESS,
			udd.DEPARTMENT_CD,
			udd.DEPARTMENT_DESCR,
			urc.RESPONSIBILITY_CENTER_CD,
			urc.RESPONSIBILITY_CENTER_DESCR
			
			from UD_DATA.PY_EMPLOYMENT pye
			join UD_DATA.UD_EMPLOYEE ude on pye.EMPLOYEE_KEY = ude.EMPLOYEE_KEY
			join UD_DATA.UD_EMPLOYEE_FULL_PART_TIME efpt on pye.EMPLOYEE_FULL_PART_TIME_KEY = efpt.EMPLOYEE_FULL_PART_TIME_KEY
			join UD_DATA.UD_ASSIGNMENT_STATUS uas on pye.ASSIGNMENT_STATUS_KEY = uas.ASSIGNMENT_STATUS_KEY
			join UD_DATA.UD_DEPARTMENT udd on pye.DEPARTMENT_KEY = udd.DEPARTMENT_KEY
			join UD_DATA.UD_RESPONSIBILITY_CENTER urc on udd.RESPONSIBILITY_CENTER_CD = urc.RESPONSIBILITY_CENTER_CD
			join UD_DATA.UD_JOB udj on pye.JOB_KEY = udj.JOB_KEY
			join UD_DATA.UD_CALENDAR cal on pye.CALENDAR_KEY = cal.CALENDAR_KEY
			
			where cal.CALENDAR_KEY = SYS_CONTEXT ('G$CONTEXT', 'PYM_CU_CAL_K_0000')
			and udd.current_flg = 1 and udj.current_flg = 1
			and udj.JOB_TYPE in ('Academic', 'Faculty', 'Post Doctoral')";

	    static readonly string ResearcherIdsSql = @"
	        select 
	        EMPLID,
	        USERNAME,
	        EMAIL,
	        DISPLAY_NAME,
	        IDENTIFIER_VALUE
	        from UD_DATA.UD_RESEARCHER_IDS";

	    static readonly string OrcidSql = "select USERNAME, ORCID from ORCID_USERS where ORCID is not null";

	    public static IConfiguration Config { get; private set; }
        
        static void Main(string[] args)
        {
            DotEnv.Load();
            Config = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            using var udDataConnection = new OracleConnection(Config["UD_DATA_CONNECTION"]);
            var employeeData = udDataConnection.Query(EmployeeDataSql + " fetch next 50 rows only").ToList();

            var employees = employeeData.Select(e => new Person
            {
	            EmplId = e.EMPLID,
	            EmployeeNbr = e.EMPLOYEE_NBR,
	            Username = e.USERNAME,
	            FirstName = e.FIRST_NAME,
	            LastName = e.LAST_NAME,
	            OrganizationId = "DEPT-" + e.DEPARTMENT_CD,
	            EmailAddresses = e.EMAIL_ADDRESS is not null
		            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) {e.EMAIL_ADDRESS}
		            : new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            }).ToList();

            var orgHierarchy = employeeData
	            .GroupBy(e => e.RESPONSIBILITY_CENTER_CD)
	            .SelectMany(rcGroup => rcGroup.GroupBy(e => e.DEPARTMENT_CD)
		            .Select(depGroup => new OrgHierarchyRecord
		            {
			            OrganizationId = "DEPT-" + depGroup.Key,
			            OrganizationName = depGroup.First().DEPARTMENT_DESCR,
			            ParentOrgId = "RC-" + rcGroup.Key
		            }).Prepend(new OrgHierarchyRecord
		            {
			            OrganizationId = "RC-" + rcGroup.Key,
			            OrganizationName = rcGroup.First().RESPONSIBILITY_CENTER_DESCR
		            })).ToList();
	        
            var employeeLookupByEmplId = employees.Where(e => e.EmplId is not null).ToLookup(e => e.EmplId);
            var employeeLookupByUsername = employees.Where(e => e.Username is not null).ToLookup(e => e.Username);

            var researchIdentifiers =
	            udDataConnection.Query(ResearcherIdsSql);

            foreach (var id in researchIdentifiers)
            {
	            foreach (var employee in employeeLookupByEmplId[(string) id.EMPLID].Concat(employeeLookupByUsername[(string) id.USERNAME]))
	            {
		            if (id.EMAIL is not null) employee.EmailAddresses.Add(id.EMAIL);

		            if (id.DISPLAY_NAME == "Email address")
			            employee.EmailAddresses.Add(id.IDENTIFIER_VALUE);
		            else
			            employee.Identifiers.Add(new Identifier(id.DISPLAY_NAME, id.IDENTIFIER_VALUE));
	            }
            }
	        
            using var orcidConnection = new OracleConnection(Config["ORCID_CONNECTION"]);
            var orcids = orcidConnection.Query(OrcidSql);
            foreach (var orcid in orcids)
            {
	            foreach (var employee in employeeLookupByUsername[(string) orcid.USERNAME])
	            {
		            employee.Identifiers.Add(new Identifier("ORCID", orcid.ORCID));
	            }
            }
	        
            WriteToCsv("OrgHierarchySample.csv", orgHierarchy);
            WriteToCsv("ResearchersSample.csv", employees.Select(e => new PersonRecord(e)));
        }

        static void WriteToCsv<T>(string outputPath, IEnumerable<T> records)
        {
	        using var writer = new StreamWriter(outputPath);
	        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
	        csv.WriteHeader<T>();
	        csv.NextRecord();
	        csv.WriteRecords(records);
        }
    }
}