﻿using System;
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
                string RESPONSIBILITY_CENTER_DESCR,
                string FACULTY_TENURE_STATUS_DESCR,
                string JOB_KEY,
                string JOB_TYPE,
                string JOB_FAMILY,
                string JOB_CLASS
                )>(GetSql("EmployeeDataQuery.sql")).ToList();

            var employees = employeeData.Select(e => new Person
            {
                EmplId = e.EMPLID,
                EmployeeNbr = e.EMPLOYEE_NBR,
                Username = e.USERNAME,
                FirstName = e.FIRST_NAME,
                LastName = e.LAST_NAME,
                OrganizationId = e.DEPARTMENT_CD + TenureCode(e.FACULTY_TENURE_STATUS_DESCR),
                OrganizationName = e.DEPARTMENT_DESCR + " - " + TenureDesc(e.FACULTY_TENURE_STATUS_DESCR),
                TenureStatus = e.FACULTY_TENURE_STATUS_DESCR,
                EmailAddresses = e.EMAIL_ADDRESS is not null
                    ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { e.EMAIL_ADDRESS }
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                JobKey = e.JOB_KEY,
                JobName = $"{e.JOB_TYPE} - {e.JOB_FAMILY} - {e.JOB_CLASS}"
            }).ToList();

            var topLevelOrg = new OrgHierarchyRecord { OrganizationID = "1000000", OrganizationName = "University of Pittsburgh" };
            var orgHierarchy = new List<OrgHierarchyRecord>() { topLevelOrg };
            orgHierarchy.AddRange(employeeData
                .GroupBy(e => e.RESPONSIBILITY_CENTER_CD)
                .SelectMany(rcGroup =>
                {
                    var deptGroups = rcGroup.GroupBy(e => e.DEPARTMENT_CD).ToList();
                    var depts = deptGroups
                        .SelectMany(depGroup => depGroup.GroupBy(d => (TenureCode(d.FACULTY_TENURE_STATUS_DESCR), TenureDesc(d.FACULTY_TENURE_STATUS_DESCR))).Select(t => new OrgHierarchyRecord
                        {
                            OrganizationID = depGroup.Key + t.Key.Item1,
                            OrganizationName = depGroup.First().DEPARTMENT_DESCR + " - " + t.Key.Item2,
                            ParentOrgaID = depGroup.Key + "0"
                        }).Prepend(new OrgHierarchyRecord
                        {
                            OrganizationID = depGroup.Key + "0",
                            OrganizationName = depGroup.First().DEPARTMENT_DESCR,
                            ParentOrgaID = rcGroup.Key != "00" ? rcGroup.Key + "0000" : topLevelOrg.OrganizationID
                        }));
                    return rcGroup.Key == "00" ? depts : depts.Prepend(new OrgHierarchyRecord
                    {
                        OrganizationID = rcGroup.Key + "0000",
                        OrganizationName = rcGroup.First().RESPONSIBILITY_CENTER_DESCR,
                        ParentOrgaID = topLevelOrg.OrganizationID
                    });
                }));
            orgHierarchy.AddRange(employeeData.GroupBy(e => e.JOB_KEY).Select(grp => {
                var e = grp.First();
                return new OrgHierarchyRecord {
                    OrganizationID = grp.Key,
                    OrganizationName = $"{e.JOB_TYPE} - {e.JOB_FAMILY} - {e.JOB_CLASS}",
                    ParentOrgaID = topLevelOrg.OrganizationID
                };
            }));

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
                record = record.WithOrganization(e.JobKey, e.JobName);
                employeeRecords.Add(record);
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