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
        public static IConfiguration Config { get; private set; }
        
        static void Main(string[] args)
        {
            DotEnv.Load();
            Config = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            // var employees = GetEmployeeInfo().ToList();
            // Console.WriteLine($"{employees.Count(person => person.PeoplesoftId is null)} out of {employees.Count} employees with no emplid.");
            // Console.WriteLine($"{employees.Select(p => p.FirstName + p.LastName).Distinct().Count()} distinct names out of {employees.Count} employees.");
            // var orgHierarchy = GetOrgHierarchyRecords();
            // WriteToCsv("OrgHierarchySample.csv", orgHierarchy);
            // var people = People.From(employees, GetOrcidUsers(), ReadResearcherIdentifierValuesCsv());
            // Console.WriteLine($"{people.Count(person => person.Identifiers.Any())} employees with identifiers out of {people.Count} total.");
            // foreach (var person in people)
            // {
            //     Console.WriteLine(person.Username);
            //     foreach (var identifier in person.Identifiers)
            //     {
            //         Console.WriteLine($"\t{identifier.Name}:\t{identifier.Value}");
            //     }
            // }
            LoadEmployeeData();
            
        }

        static void LoadEmployeeData()
        {
	        using var udDataConnection = new OracleConnection(Config["UD_DATA_CONNECTION"]);
	        var employeeData = udDataConnection.Query(@"
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
				and udj.JOB_TYPE in ('Academic', 'Faculty', 'Post Doctoral')").ToList();

	        var employees = employeeData.Select(e => new Person
	        {
		        EmplId = e.EMPLID,
		        EmployeeNbr = e.EMPLOYEE_NBR,
		        Username = e.USERNAME,
		        FirstName = e.FIRST_NAME,
		        LastName = e.LAST_NAME,
		        OrganizationId = "DEPT-" + e.DEPARTMENT_CD,
		        EmailAddresses = e.EMAIL_ADDRESS is not null
			        ? new HashSet<string> {e.EMAIL_ADDRESS}
			        : new HashSet<string>()
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
	        WriteToCsv("OrgHierarchySample.csv", orgHierarchy);
	        
	        var employeeLookupByEmplId = employees.Where(e => e.EmplId is not null).ToDictionary(e => e.EmplId);
	        var employeeLookupByUsername = employees.Where(e => e.Username is not null).ToDictionary(e => e.Username);

	        var researchIdentifiers =
		        udDataConnection.Query(@"
		        	select 
		        	EMPLID,
		        	USERNAME,
		        	EMAIL,
		        	DISPLAY_NAME,
		        	IDENTIFIER_VALUE
		        	from UD_DATA.UD_RESEARCHER_IDS");

	        foreach (var id in researchIdentifiers)
	        {
		        Person employee = null;
		        if (id.EMPLID is not null && employeeLookupByEmplId.TryGetValue(id.EMPLID, out employee) ||
		            id.USERNAME is not null && employeeLookupByUsername.TryGetValue(id.USERNAME, out employee))
		        {
			        // employee is guaranteed not to be null if the above if condition returned true
			        Debug.Assert(employee != null, nameof(employee) + " != null");
			        
			        if (id.EMAIL is not null) employee.EmailAddresses.Add(id.EMAIL);

			        if (id.DISPLAY_NAME == "Email address")
				        employee.EmailAddresses.Add(id.IDENTIFIER_VALUE);
			        else
				        employee.Identifiers.Add(new Identifier(id.DISPLAY_NAME, id.IDENTIFIER_VALUE));
		        }
	        }
	        
	        using var orcidConnection = new OracleConnection(Config["ORCID_CONNECTION"]);
	        var orcids = orcidConnection.Query("select USERNAME, ORCID from ORCID_USERS where ORCID is not null");
	        foreach (var orcid in orcids)
	        {
		        if (employeeLookupByUsername.TryGetValue((string) orcid.USERNAME, out var employee))
		        {
			        employee.Identifiers.Add(new Identifier("ORCID", orcid.ORCID));
		        }
	        }
	        
	        WriteToCsv("ResearchersSample.csv", employees.Select(e => new PersonRecord(e)));
        }

        static IEnumerable<Person> GetEmployeeInfo()
        {
            using var udDataConnection = new OracleConnection(Config["UD_DATA_CONNECTION"]);
            return udDataConnection.Query(@"
            select
            emplid,
            username,
            first_name,
            last_name,
			responsibility_center_cd

			from
			  (select 
				employee_key,
			    emplid,
			    username,
            	first_name,
            	last_name,
			    responsibility_center_cd,
				rank() OVER (PARTITION BY emplid
				   ORDER BY full_dt DESC) ranker
				from   
					(select
					ude.emplid,
					cal.full_dt,
					pye.employee_key,
					ude.username,
					ude.first_name,
					ude.last_name,
			        udd.responsibility_center_cd

					from UD_DATA.PY_EMPLOYMENT pye
					join UD_DATA.UD_EMPLOYEE ude on pye.EMPLOYEE_KEY = ude.employee_key
					join UD_DATA.UD_EMPLOYEE_FULL_PART_TIME efpt on pye.employee_full_part_time_key = efpt.employee_full_part_time_key
					join UD_DATA.UD_JOB udj on pye.job_key = udj.job_key
					join UD_DATA.UD_DEPARTMENT udd on pye.department_key = udd.department_key
					join UD_DATA.UD_CALENDAR cal on pye.calendar_key = cal.calendar_key
					join UD_DATA.UD_ASSIGNMENT_STATUS uas on pye.assignment_status_key = uas.assignment_status_key
					where udj.current_flg = 1
					and udd.current_flg = 1
					and udj.job_type in ('Academic', 'Faculty', 'Post Doctoral')
					and cal.calendar_key=SYS_CONTEXT ('G$CONTEXT', 'PYM_CU_CAL_K_0000')

					UNION

					select 
			    	ude.emplid,
					m.last_dt as full_dt,
					pye.employee_key,
			        ude.username,
					ude.first_name,
					ude.last_name,
			        udd.responsibility_center_cd

					from UD_DATA.PY_EMPLOYMENT pye
					join UD_DATA.UD_EMPLOYEE ude on pye.EMPLOYEE_KEY = ude.employee_key
					join UD_DATA.UD_EMPLOYEE_FULL_PART_TIME efpt on pye.employee_full_part_time_key = efpt.employee_full_part_time_key
					join UD_DATA.UD_JOB udj on pye.job_key = udj.job_key
					join UD_DATA.UD_DEPARTMENT udd on pye.department_key = udd.department_key
					join UD_DATA.UD_CALENDAR cal on pye.calendar_key = cal.calendar_key
					join UD_DATA.UD_ASSIGNMENT_STATUS uas on pye.assignment_status_key = uas.assignment_status_key
					inner join (select ude.emplid, max(cal.full_dt) as last_dt
						from UD_DATA.PY_EMPLOYMENT pye
						join UD_DATA.UD_EMPLOYEE ude on pye.EMPLOYEE_KEY = ude.employee_key
						join UD_DATA.UD_CALENDAR cal on pye.calendar_key = cal.calendar_key
						where cal.full_dt = '31-DEC-19'
						group by ude.emplid)m 
						on m.emplid = ude.emplid and m.last_dt = cal.full_dt
				   
				   where udj.current_flg = 1
				   and udd.current_flg = 1
				   and udj.job_type in ('Academic', 'Faculty', 'Post Doctoral'))u
			  )x
			where ranker = 1").Select(employee => 
	            new Person
            {
	            EmplId = employee.EMPLID,
	            Username = employee.USERNAME,
	            FirstName = employee.FIRST_NAME,
	            LastName = employee.LAST_NAME,
	            OrganizationId = employee.RESPONSIBILITY_CENTER_CD
            });
        }

        static IEnumerable<Person> GetOrcidUsers()
        {
            using var orcidConnection = new OracleConnection(Config["ORCID_CONNECTION"]);
            return orcidConnection.Query("select USERNAME, ORCID from ORCID_USERS where ORCID is not null").Select(
                user => new Person
                {
                    Username = user.USERNAME, Identifiers = {new Identifier("orcid", user.ORCID)}
                });
        }

        static IEnumerable<Person> ReadResearcherIdentifierValuesCsv()
        {
            using var reader = new StreamReader("Researcher Identifier Values.csv");
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            return csv.GetRecords<ResearcherIdValuesRecord>().GroupBy(record => record.Username).Select(grp =>
                new Person
                {
                    Username = grp.Key,
                    Identifiers = grp.Select(record => new Identifier(record.Name, record.IdentifierValue)).ToHashSet()
                })
                .ToList();
        }

        static IEnumerable<OrgHierarchyRecord> GetOrgHierarchyRecords()
        {
	        using var udDataConnection = new OracleConnection(Config["UD_DATA_CONNECTION"]);
	        var responsibilityCenters = udDataConnection.Query(@"
				select RESPONSIBILITY_CENTER_CD, RESPONSIBILITY_CENTER_DESCR from UD_DATA.UD_RESPONSIBILITY_CENTER
		        where CURRENT_FLG = 1 and ENABLED_FLG = 'Y'")
		        .Select(row => new OrgHierarchyRecord
		        {
			        OrganizationId = row.RESPONSIBILITY_CENTER_CD, 
			        OrganizationName = row.RESPONSIBILITY_CENTER_DESCR
		        }).ToList();
	        var departments = udDataConnection.Query(@"
				select d.DEPARTMENT_CD, d.DEPARTMENT_DESCR, d.RESPONSIBILITY_CENTER_CD
				from UD_DATA.UD_DEPARTMENT d
				join UD_DATA.UD_RESPONSIBILITY_CENTER rc on d.RESPONSIBILITY_CENTER_CD = rc.RESPONSIBILITY_CENTER_CD
				where d.CURRENT_FLG = 1 and d.ENABLED_FLG = 'Y' and rc.CURRENT_FLG = 1 and rc.ENABLED_FLG = 'Y'")
		        .Select(row => new OrgHierarchyRecord
		        {
					OrganizationId = row.DEPARTMENT_CD,
					OrganizationName = row.DEPARTMENT_DESCR,
					ParentOrgId = row.RESPONSIBILITY_CENTER_CD
		        }).ToList();

	        return responsibilityCenters.Concat(departments);
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