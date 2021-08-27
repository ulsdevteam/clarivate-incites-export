using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using CommandLine;
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

            Parser.Default.ParseArguments<Options>(args).WithParsed(options => {
				try
				{
					RunExport(options);
				}
				catch (ConnectionException e)
				{
					Console.Error.WriteLine(e);
				}
				catch (OracleException e) {
					Console.Error.WriteLine("Oracle Error: " + e);
				}
			});
        }

        static void RunExport(Options options)
        {
            using var udDataConnection = Connect("UD_DATA_CONNECTION");
            var employeeData = udDataConnection.Query<EmployeeData>(GetSql("EmployeeDataQuery.sql")).ToList();
            
            var maxJobKeyLen = employeeData.Select(e => e.JOB_KEY.Length).Max();

            var orgHierarchy = new HierarchyBuilder()
                .TopLevel("1" + new string('0', maxJobKeyLen + 6), "Selections from University of Pittsburgh")
                .Then(e => e.RESPONSIBILITY_CENTER_CD, (e, _) => e.RESPONSIBILITY_CENTER_CD + new string('0', maxJobKeyLen + 4), (e, _) => "Selections from " + e.RESPONSIBILITY_CENTER_DESCR)
                .Then(e => e.DEPARTMENT_CD, (e, _) => e.DEPARTMENT_CD + new string('0', maxJobKeyLen + 1), (e, _) => e.DEPARTMENT_DESCR)
                .Then(e => e.JOB_KEY, (e, _) => e.DEPARTMENT_CD + e.JOB_KEY.PadLeft(maxJobKeyLen, '0') + "0", (e, parent) => parent.OrganizationName + " - " + e.JOB_FAMILY + " - " + e.JOB_CLASS)
                .Then(e => TenureCode(e.FACULTY_TENURE_STATUS_DESCR), (e, _) => e.DEPARTMENT_CD + e.JOB_KEY.PadLeft(maxJobKeyLen, '0') + TenureCode(e.FACULTY_TENURE_STATUS_DESCR), (e, parent) => parent.OrganizationName + " - " + TenureDesc(e.FACULTY_TENURE_STATUS_DESCR))
                .Build(employeeData);

            var employees = employeeData.Select(e => new Person
            {
                EmplId = e.EMPLID,
                EmployeeNbr = e.EMPLOYEE_NBR,
                Username = e.USERNAME,
                FirstName = e.FIRST_NAME,
                LastName = e.LAST_NAME,
                OrganizationId = e.LeafOrganizationID,
                OrganizationName = e.LeafOrganizationName,
                TenureStatus = e.FACULTY_TENURE_STATUS_DESCR,
                EmailAddresses = e.EMAIL_ADDRESS is not null
                    ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { e.EMAIL_ADDRESS }
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                JobKey = e.JOB_KEY,
                JobName = $"{e.JOB_TYPE} - {e.JOB_FAMILY} - {e.JOB_CLASS}"
            }).ToList();

            var employeeLookupByEmplId = employees.Where(e => e.EmplId is not null).ToLookup(e => e.EmplId);
            var employeeLookupByUsername = employees.Where(e => e.Username is not null).ToLookup(e => e.Username);

            var researchIdentifiers = udDataConnection.Query<(
                string EMPLID,
                string USERNAME,
                string EMAIL,
                string IDENTIFIER_ID,
                string IDENTIFIER_VALUE
                )>(GetSql("ResearcherIdsQuery.sql"));

            foreach (var id in researchIdentifiers)
            {
                foreach (var employee in employeeLookupByEmplId[id.EMPLID].Concat(employeeLookupByUsername[id.USERNAME]))
                {
                    if (id.EMAIL is not null) employee.EmailAddresses.Add(id.EMAIL);

					// ID of 17 is an email address
                    if (id.IDENTIFIER_ID == "17")
                        employee.EmailAddresses.Add(id.IDENTIFIER_VALUE);
                    else
                        employee.Identifiers.Add(new Identifier(Identifier.TranslateId(id.IDENTIFIER_ID), id.IDENTIFIER_VALUE));
                }
            }

            using var orcidConnection = Connect("ORCID_CONNECTION");
            var orcids = orcidConnection.Query<(string USERNAME, string ORCID)>(GetSql("OrcidQuery.sql"));
            foreach (var orcid in orcids)
            {
                foreach (var employee in employeeLookupByUsername[orcid.USERNAME])
                {
                    employee.Identifiers.Add(new Identifier("ORCID", orcid.ORCID));
                }
            }

            var employeeRecords = new List<PersonRecord>();
            foreach (var e in employees)
            {
                var record = new PersonRecord(e);
                employeeRecords.Add(record);
            }

            var dupes = orgHierarchy.GroupBy(o => long.Parse(o.OrganizationID.TrimStart('0'))).Where(g => g.Count() > 1);
            foreach (var dupe in dupes)
            {
                Console.WriteLine($"Duplicate organization id: {dupe.Key}");
                foreach (var org in dupe)
                {
                    Console.WriteLine(org.OrganizationName);
                }
            }

            WriteToCsv(options.OrgHierarchyCsvOutputPath, orgHierarchy);
            WriteToCsv(options.ResearchersCsvOutputPath, employeeRecords);
        }

		static OracleConnection Connect(string connectionStringName) {
			try
			{				
				var connection = new OracleConnection(Config[connectionStringName]);
				connection.Open();
				return connection;
			}
			catch (System.InvalidOperationException e) when (e.Message == "OracleConnection.ConnectionString is invalid")
			{				
				throw new ConnectionException($"Connection string '{connectionStringName}' is invalid and a connection to oracle could not be made.", e);
			}
		}

        static string TenureCode(string tenureStatus) => tenureStatus switch
        {
            null or "" or "Non-Tenured" => "1",
            "Tenure Stream" => "2",
            "Tenured" => "3",
			_ => throw new ArgumentException("Unrecognized Tenure Status value.", nameof(tenureStatus))
        };

        static string TenureDesc(string tenureStatus) => tenureStatus switch
        {
            null or "" or "Non-Tenured" => "Non tenure track",
            "Tenure Stream" => "Tenure track, pre-tenure",
            "Tenured" => "Tenure track, tenured",
			_ => throw new ArgumentException("Unrecognized Tenure Status value.", nameof(tenureStatus))
        };

        static string GetSql(string filename)
        {
            using var stream = Assembly.GetExecutingAssembly()
                                   .GetManifestResourceStream($"clarivate_incites_export.sql.{filename}") ??
                               throw new InvalidOperationException("Resource not found.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
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