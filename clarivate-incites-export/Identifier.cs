using System;

namespace clarivate_incites_export
{
    public readonly struct Identifier
    {
        public string Name { get; }
        public string Value { get; }

        public Identifier(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public bool Equals(Identifier other) => Name == other.Name && Value == other.Value;

        public override bool Equals(object obj) => obj is Identifier other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Name, Value);

        public static bool operator ==(Identifier left, Identifier right) => left.Equals(right);

        public static bool operator !=(Identifier left, Identifier right) => !left.Equals(right);
    }
}