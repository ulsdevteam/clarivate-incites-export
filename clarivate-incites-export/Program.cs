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
using clarivate_incites_export;

DotEnv.Load();
IConfiguration config = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .Build();
Parser.Default.ParseArguments<Options>(args).WithParsed(options =>
{
    try { RunExport(options); }
    catch (ConnectionException e) { Console.Error.WriteLine(e); }
    catch (OracleException e) { Console.Error.WriteLine("Oracle Error: " + e); }
});

void RunExport(Options options)
{
    using var udDataConnection = Connect("UD_DATA_CONNECTION");
    using var orcidConnection = Connect("ORCID_CONNECTION");

    var idLookup = new IdentifierLookup();
    var identifiers = udDataConnection.Query<IdentifierData>(GetSql("ResearcherIdsQuery.sql"));
    foreach (var (emplid, username, email, identifierId, identifierValue) in identifiers)
    {
        if (!string.IsNullOrWhiteSpace(email)) { idLookup.AddEmail(emplid, username, email); }
        var identifierName = Identifier.TranslateId(identifierId);
        if (identifierName == "EMAIL") { idLookup.AddEmail(emplid, username, identifierValue); }
        else { idLookup.AddId(emplid, username, new Identifier(identifierName, identifierValue)); }
    }

    var orcids = orcidConnection.Query<OrcidData>(GetSql("OrcidQuery.sql"));
    foreach (var (username, orcid) in orcids) { idLookup.AddId(null, username, new Identifier("ORCID", orcid)); }

    var locationDifferentiator = new Dictionary<string, int>();

    // This will be called once on each location, and assigns them an increasing id within the parent org
    string GetLocationOrgId(string parentOrgId)
    {
        if (locationDifferentiator.TryGetValue(parentOrgId, out var num))
        {
            locationDifferentiator[parentOrgId] = num + 1;
            return parentOrgId + num;
        }
        locationDifferentiator[parentOrgId] = 2;
        return parentOrgId + "1";
    }

    var employeeData = udDataConnection.Query<EmployeeData>(GetSql("EmployeeDataQuery.sql")).ToList();
    var maxJobKeyLen = employeeData.Select(e => e.JOB_KEY.Length).Max();
    var (orgHierarchy, researcherRecords) = new HierarchyBuilder()
        .TopLevel("0", "Selections from University of Pittsburgh")
        .Then(
            e => e.RESPONSIBILITY_CENTER_CD,
            (e, _) => e.RESPONSIBILITY_CENTER_CD,
            (e, _) => "Selections from " + e.RESPONSIBILITY_CENTER_DESCR)
        .Then(
            e => e.DEPARTMENT_CD,
            (e, _) => e.DEPARTMENT_CD,
            (e, _) => e.DEPARTMENT_DESCR)
        .ThenCond(
            e => e.RESPONSIBILITY_CENTER_CD == "35", // School of Medicine
            e => e.BUILDING_NAME,
            (_, parent) => GetLocationOrgId(parent.OrganizationID),
            (e, parent) => parent.OrganizationName + " - " + (e.BUILDING_NAME ?? "Unknown Office Location"))
        .Then(
            e => e.JOB_KEY,
            (e, parent) => parent.OrganizationID + e.JOB_KEY.PadLeft(maxJobKeyLen, '0'),
            (e, parent) => parent.OrganizationName + " - " + e.JOB_FAMILY + " - " + e.JOB_CLASS)
        .Then(
            e => TenureCode(e.FACULTY_TENURE_STATUS_DESCR),
            (e, parent) => parent.OrganizationID + TenureCode(e.FACULTY_TENURE_STATUS_DESCR),
            (e, parent) => parent.OrganizationName + " - " + TenureDesc(e.FACULTY_TENURE_STATUS_DESCR))
        .BuildRecords(employeeData, idLookup);

    var dupes = orgHierarchy.GroupBy(o => long.Parse(o.OrganizationID)).Where(g => g.Count() > 1);
    foreach (var dupe in dupes)
    {
        Console.Error.WriteLine($"WARNING: Duplicate organization id: {dupe.Key}");
        foreach (var org in dupe) { Console.Error.WriteLine(org.OrganizationName); }
    }

    WriteToCsv(options.OrgHierarchyCsvOutputPath ?? $"org_{DateTime.Today:yyyy-MM-dd}.csv", orgHierarchy);
    WriteToCsv(options.ResearchersCsvOutputPath ?? $"res_{DateTime.Today:yyyy-MM-dd}.csv", researcherRecords);
}

OracleConnection Connect(string connectionStringName)
{
    try
    {
        var connection = new OracleConnection(config[connectionStringName]);
        connection.Open();
        return connection;
    }
    catch (InvalidOperationException e) when (e.Message == "OracleConnection.ConnectionString is invalid")
    {
        throw new ConnectionException(
            $"Connection string '{connectionStringName}' is invalid and a connection to oracle could not be made.",
            e);
    }
}

static string TenureCode(string tenureStatus)
{
    return tenureStatus switch
    {
        null or "" or "Non-Tenured" => "1",
        "Tenure Stream" => "2",
        "Tenured" => "3",
        _ => throw new ArgumentException("Unrecognized Tenure Status value.", nameof(tenureStatus))
    };
}

static string TenureDesc(string tenureStatus)
{
    return tenureStatus switch
    {
        null or "" or "Non-Tenured" => "Non tenure track",
        "Tenure Stream" => "Tenure track, pre-tenure",
        "Tenured" => "Tenure track, tenured",
        _ => throw new ArgumentException("Unrecognized Tenure Status value.", nameof(tenureStatus))
    };
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

static class Extensions
{
    public static (List<T>, List<T>) SplitBy<T>(this IEnumerable<T> source, Func<T, bool> predicate)
    {
        var trueList = new List<T>();
        var falseList = new List<T>();
        foreach (var item in source) { (predicate(item) ? trueList : falseList).Add(item); }
        return (trueList, falseList);
    }

    public static TValue GetOrInsertNew<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
        where TValue : class, new()
    {
        if (!dictionary.TryGetValue(key, out var value)) { dictionary.Add(key, value = new TValue()); }
        return value;
    }
}