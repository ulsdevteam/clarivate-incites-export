using System;
using System.Collections.Generic;
using System.Linq;

namespace clarivate_incites_export
{
    class Identifiers
    {
        public HashSet<Identifier> Ids { get; private init; } = new HashSet<Identifier>();
        public HashSet<string> Emails { get; private init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static Identifiers operator +(Identifiers lhs, Identifiers rhs)
        {
            return new Identifiers
            {
                Ids = new HashSet<Identifier>(lhs.Ids.Concat(rhs.Ids)),
                Emails = new HashSet<string>(lhs.Emails.Concat(rhs.Emails))
            };
        }
    }

    class IdentifierLookup
    {
        readonly Dictionary<string, Identifiers> byEmplid = new Dictionary<string, Identifiers>();
        readonly Dictionary<string, Identifiers> byUsername = new Dictionary<string, Identifiers>();

        public void AddId(string emplid, string username, Identifier identifier)
        {
            if (emplid is not null)
            {
                if (!byEmplid.ContainsKey(emplid)) { byEmplid[emplid] = new Identifiers(); }

                byEmplid[emplid].Ids.Add(identifier);
            }

            if (username is not null)
            {
                if (!byUsername.ContainsKey(username)) { byUsername[username] = new Identifiers(); }

                byUsername[username].Ids.Add(identifier);
            }
        }

        public void AddEmail(string emplid, string username, string email)
        {
            if (emplid is not null)
            {
                if (!byEmplid.ContainsKey(emplid)) { byEmplid[emplid] = new Identifiers(); }

                byEmplid[emplid].Emails.Add(email);
            }

            if (username is not null)
            {
                if (!byUsername.ContainsKey(username)) { byUsername[username] = new Identifiers(); }

                byUsername[username].Emails.Add(email);
            }
        }

        public Identifiers GetIdentifiers(string emplid, string username)
        {
            var identifiers = new Identifiers();
            if (emplid is not null && byEmplid.ContainsKey(emplid)) { identifiers += byEmplid[emplid]; }
            if (username is not null && byUsername.ContainsKey(username)) { identifiers += byUsername[username]; }
            return identifiers;
        }
    }

    readonly struct Identifier
    {
        public string Name { get; }
        public string Value { get; }

        public Identifier(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public bool Equals(Identifier other)
        {
            return Name == other.Name && Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is Identifier other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Value);
        }

        public static bool operator ==(Identifier left, Identifier right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Identifier left, Identifier right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return $"{Name}:{Value}";
        }

        public static string TranslateId(string id)
        {
            return id switch
            {
                "8" => "WOS",
                "9" => "ORCID",
                "10" => "SCOPUS",
                "11" => "ARXIV",
                "17" => "EMAIL",
                "23" => "SSRN",
                _ => throw new ArgumentException("Unrecognized research identifier id.", nameof(id))
            };
        }
    }
}