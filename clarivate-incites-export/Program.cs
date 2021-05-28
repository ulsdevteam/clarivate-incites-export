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

            Parser.Default.ParseArguments<Options>(args).WithParsed(RunExport);
        }

        static void RunExport(Options options)
        {
            using var udDataConnection = new OracleConnection(Config["UD_DATA_CONNECTION"]);
            var employeeData = udDataConnection.Query<(
                string EMPLID,
                string EMPLOYEE_NBR,
                string USERNAME,
                string LAST_NAME,
                string FIRST_NAME,
                string EMAIL_ADDRESS,
                string DEPARTMENT_CD,
                string DEPARTMENT_DESCR,
                string RESPONSIBILITY_CENTER_CD,
                string RESPONSIBILITY_CENTER_DESCR
                )>(GetSql("EmployeeDataQuery.sql")).ToList();

            var employees = employeeData.Select(e => new Person
            {
                EmplId = e.EMPLID,
                EmployeeNbr = e.EMPLOYEE_NBR,
                Username = e.USERNAME,
                FirstName = e.FIRST_NAME,
                LastName = e.LAST_NAME,
                OrganizationId = e.DEPARTMENT_CD,
				OrganizationName = e.DEPARTMENT_DESCR,
                EmailAddresses = e.EMAIL_ADDRESS is not null
                    ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { e.EMAIL_ADDRESS }
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            }).ToList();

            var topLevelOrg = new OrgHierarchyRecord { OrganizationID = "100000", OrganizationName = "University of Pittsburgh" };
            var orgHierarchy = new List<OrgHierarchyRecord>() { topLevelOrg };
            orgHierarchy.AddRange(employeeData
                .GroupBy(e => e.RESPONSIBILITY_CENTER_CD)
                .SelectMany(rcGroup =>
                {
                    var depts = rcGroup.GroupBy(e => e.DEPARTMENT_CD)
                        .Select(depGroup => new OrgHierarchyRecord
                        {
                            OrganizationID = depGroup.Key,
                            OrganizationName = depGroup.First().DEPARTMENT_DESCR,
                            ParentOrgaID = rcGroup.Key != "00" ? rcGroup.Key + "000" : topLevelOrg.OrganizationID
                        });
                    return rcGroup.Key == "00" ? depts : depts.Prepend(new OrgHierarchyRecord
                    {
                        OrganizationID = rcGroup.Key + "000",
                        OrganizationName = rcGroup.First().RESPONSIBILITY_CENTER_DESCR,
                        ParentOrgaID = topLevelOrg.OrganizationID
                    });
                }));

            var employeeLookupByEmplId = employees.Where(e => e.EmplId is not null).ToLookup(e => e.EmplId);
            var employeeLookupByUsername = employees.Where(e => e.Username is not null).ToLookup(e => e.Username);

            var researchIdentifiers = udDataConnection.Query<(
                string EMPLID,
                string USERNAME,
                string EMAIL,
                string DISPLAY_NAME,
                string IDENTIFIER_VALUE
                )>(GetSql("ResearcherIdsQuery.sql"));

            foreach (var id in researchIdentifiers)
            {
                foreach (var employee in employeeLookupByEmplId[id.EMPLID].Concat(employeeLookupByUsername[id.USERNAME]))
                {
                    if (id.EMAIL is not null) employee.EmailAddresses.Add(id.EMAIL);

                    if (id.DISPLAY_NAME == "Email address")
                        employee.EmailAddresses.Add(id.IDENTIFIER_VALUE);
                    else
                        employee.Identifiers.Add(new Identifier(id.DISPLAY_NAME, id.IDENTIFIER_VALUE));
                }
            }

            using var orcidConnection = new OracleConnection(Config["ORCID_CONNECTION"]);
            var orcids = orcidConnection.Query<(string USERNAME, string ORCID)>(GetSql("OrcidQuery.sql"));
            foreach (var orcid in orcids)
            {
                foreach (var employee in employeeLookupByUsername[orcid.USERNAME])
                {
                    employee.Identifiers.Add(new Identifier("ORCID", orcid.ORCID));
                }
            }

            WriteToCsv(options.OrgHierarchyCsvOutputPath, orgHierarchy);
            WriteToCsv(options.ResearchersCsvOutputPath, employees.Select(e => new PersonRecord(e)));
        }

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