using System;
using System.Collections.Generic;
using System.Linq;

namespace clarivate_incites_export;

readonly record struct Identifier(string Name, string Value)
{
    public override string ToString() => $"{Name}:{Value}";

    public static string TranslateId(string id) => id switch
    {
        "8" => "WOS",
        "9" => "ORCID",
        "10" => "SCOPUS",
        "11" => "ARXIV",
        "17" => "EMAIL",
        "23" => "SSRN",
        _ => throw new ArgumentException($"Unrecognized research identifier id: '{id}'.", nameof(id))
    };
}

class Identifiers
{
    public HashSet<Identifier> Ids { get; private init; } = new();
    public HashSet<string> Emails { get; private init; } = new(StringComparer.OrdinalIgnoreCase);

    public static Identifiers operator +(Identifiers lhs, Identifiers rhs) => new Identifiers
    {
        Ids = new HashSet<Identifier>(lhs.Ids.Concat(rhs.Ids)),
        Emails = new HashSet<string>(lhs.Emails.Concat(rhs.Emails), StringComparer.OrdinalIgnoreCase)
    };
}

class IdentifierLookup
{
    Dictionary<string, Identifiers> ByEmplid { get; } = new();
    Dictionary<string, Identifiers> ByUsername { get; } = new();

    public void AddId(string emplid, string username, Identifier identifier)
    {
        if (emplid is not null) { ByEmplid.GetOrInsertNew(emplid).Ids.Add(identifier); }
        if (username is not null) { ByUsername.GetOrInsertNew(username).Ids.Add(identifier); }
    }

    public void AddEmail(string emplid, string username, string email)
    {
        if (emplid is not null) { ByEmplid.GetOrInsertNew(emplid).Emails.Add(email); }
        if (username is not null) { ByUsername.GetOrInsertNew(username).Emails.Add(email); }
    }

    public Identifiers GetIdentifiers(string emplid, string username)
    {
        var identifiers = new Identifiers();
        if (emplid is not null && ByEmplid.TryGetValue(emplid, out var ids)) { identifiers += ids; }
        if (username is not null && ByUsername.TryGetValue(username, out ids)) { identifiers += ids; }
        return identifiers;
    }
}