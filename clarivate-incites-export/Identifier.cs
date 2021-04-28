using System;

namespace clarivate_incites_export
{
    public readonly struct Identifier
    {
        public string Name { get; }
        public string Value { get; }

        public Identifier(string name, string value)
        {
            Name = TranslateName(name);
            Value = value;
        }

        public bool Equals(Identifier other) => Name == other.Name && Value == other.Value;

        public override bool Equals(object obj) => obj is Identifier other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Name, Value);

        public static bool operator ==(Identifier left, Identifier right) => left.Equals(right);

        public static bool operator !=(Identifier left, Identifier right) => !left.Equals(right);

        public override string ToString() => $"{Name}:{Value}";

        static string TranslateName(string displayName) => displayName switch
        {
            "Scopus ID" => "SCOPUS",
            "arXiv Author Identifier" => "ARXIV",
            "SSRN Author ID" => "SSRN",
            "ResearcherID" => "WOS",
            _ => displayName
        };
    }
}