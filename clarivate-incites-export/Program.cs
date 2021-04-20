using System;
using System.Collections.Generic;
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

            var employees = GetEmployeeInfo().ToList();
            Console.WriteLine($"{employees.Count(person => person.Username is null)} out of {employees.Count} employees with no username.");
            Console.WriteLine($"{employees.Select(p => p.FirstName + p.LastName).Distinct().Count()} distinct names out of {employees.Count} employees.");

            var people = People.From(employees, GetOrcidUsers(), ReadResearcherIdentifierValuesCsv());
            Console.WriteLine($"{people.Count(person => person.Identifiers.Any())} employees with identifiers out of {people.Count} total.");
            // foreach (var person in people)
            // {
            //     Console.WriteLine(person.Username);
            //     foreach (var identifier in person.Identifiers)
            //     {
            //         Console.WriteLine($"\t{identifier.Name}:\t{identifier.Value}");
            //     }
            // }
        }

        static IEnumerable<Person> GetEmployeeInfo()
        {
            using var udDataConnection = new OracleConnection(Config["UD_DATA_CONNECTION"]);
            return udDataConnection.Query(@"
            select
            username,
            first_name,
            last_name,
			responsibility_center_cd

			from
			  (select 
				employee_key,
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