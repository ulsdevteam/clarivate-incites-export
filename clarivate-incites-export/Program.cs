using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
				catch (OracleException e)
                {
					Console.Error.WriteLine("Oracle Error: " + e);
				}
			});
        }

        static void RunExport(Options options)
        {
            using var udDataConnection = Connect("UD_DATA_CONNECTION");
            var employeeData = udDataConnection.Query<EmployeeData>(GetSql("EmployeeDataQuery.sql")).ToList();
            
            var maxJobKeyLen = employeeData.Select(e => e.JOB_KEY.Length).Max();
            // var buildingDifferentiator = new Dictionary<string, int>();
            // string GetBuildingOrgId(string parentOrgId) {
            //     if (buildingDifferentiator.TryGetValue(parentOrgId, out var num)) {
            //         buildingDifferentiator[parentOrgId] = num + 1;
            //         return parentOrgId + num;
            //     } else {
            //         buildingDifferentiator[parentOrgId] = 2;
            //         return parentOrgId + "1";
            //     }
            // }

            var orgHierarchy = new HierarchyBuilder()
                .TopLevel("0", "Selections from University of Pittsburgh")
                .Then(
                    e => e.RESPONSIBILITY_CENTER_CD, 
                    (e, _) => e.RESPONSIBILITY_CENTER_CD, 
                    (e, _) => "Selections from " + e.RESPONSIBILITY_CENTER_DESCR)
                .Then(
                    e => e.DEPARTMENT_CD, 
                    (e, _) => e.DEPARTMENT_CD, 
                    (e, _) => e.DEPARTMENT_DESCR)
                .Then(
                    e => e.JOB_KEY, 
                    (e, parent) => parent.OrganizationID + e.JOB_KEY.PadLeft(maxJobKeyLen, '0'), 
                    (e, parent) => parent.OrganizationName + " - " + e.JOB_FAMILY + " - " + e.JOB_CLASS)
                .Then(
                    e => TenureCode(e.FACULTY_TENURE_STATUS_DESCR), 
                    (e, parent) => parent.OrganizationID + TenureCode(e.FACULTY_TENURE_STATUS_DESCR), 
                    (e, parent) => parent.OrganizationName + " - " + TenureDesc(e.FACULTY_TENURE_STATUS_DESCR))
                // .Then(
                //     e => e.BUILDING_NAME, 
                //     (e, parent) => GetBuildingOrgId(parent.OrganizationID), 
                //     (e, parent) => parent.OrganizationName + " - " + e.BUILDING_NAME)
                .Build(employeeData);

            var employees = employeeData.Select(e => new Person(e)).ToList();

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

            var dupes = orgHierarchy.GroupBy(o => long.Parse(o.OrganizationID)).Where(g => g.Count() > 1);
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
            using var stream = System.Reflection.Assembly.GetExecutingAssembly()
                                   .GetManifestResourceStream($"clarivate_incites_export.sql.{filename}") ??
                               throw new InvalidOperationException("Resource not found.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        static void WriteToCsv<T>(string outputPath, IEnumerable<T> records)
        {
            using var writer = new StreamWriter(outputPath);
            using var csv = new CsvWriter(writer, System.Globalization.CultureInfo.InvariantCulture);
            csv.WriteHeader<T>();
            csv.NextRecord();
            csv.WriteRecords(records);
        }
    }
}